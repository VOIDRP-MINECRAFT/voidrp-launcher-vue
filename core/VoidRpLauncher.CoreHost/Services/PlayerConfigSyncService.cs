using System.IO;
using System.Text.Json;
using VoidRpLauncher.CoreHost.Services.Account;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Keeps the settings a player changes inside the game — keys, graphics, sound — on their
/// account, so a reinstall, a second computer or a repaired install does not cost them.
///
/// Three things here are not obvious, and each of them is a way settings were being lost:
///
/// * <b>A slot per server.</b> The same account plays packs of different Minecraft versions
///   out of separate folders. One shared slot meant the last server played wrote its
///   options.txt over the other's, so a player who plays two of them had their keys and
///   video settings shuffled every launch. What is already stored under the bare path is
///   still read once, so nothing saved before this is lost.
/// * <b>What is on disk wins when it is newer.</b> Restoring used to overwrite the local
///   file every launch. If the last upload never happened — the game was killed, the
///   launcher was closed with it, the machine went down — that silently threw away
///   everything the player had changed since. The hash of what was last exchanged is kept
///   locally: a local file that no longer matches it has been edited since, and is uploaded
///   rather than replaced.
/// * <b>Settings are picked up late as well as early.</b> The upload runs when the game
///   exits, which is no help if nothing is around to see it exit. The next launch notices
///   the local file has moved on and saves it before restoring anything.
/// </summary>
public sealed class PlayerConfigSyncService
{
    /// <summary>
    /// The files a player edits from inside the game.
    ///
    /// options.txt carries the keys — mod keybinds included — along with video, sound and
    /// chat. The rest are the settings screens of whichever renderer a pack ships: Sodium on
    /// Fabric, Embeddium on NeoForge, Iris for shaders. A file a pack does not have costs
    /// nothing; it is simply never there to upload.
    /// </summary>
    private static readonly string[] Tracked =
    {
        "options.txt",
        "config/sodium-options.json",
        "config/sodium-extra-options.json",
        "config/sodium-extra.json",
        "config/embeddium-options.json",
        "config/iris.properties",
    };

    /// The server the content came from, when the launcher has not resolved one yet.
    private const string DefaultSlot = "default";

    /// Base64 grows a file by a third, and the backend refuses more than this.
    private const int MaxBase64Length = 512 * 1024;

    private readonly LauncherPathsService _paths;
    private readonly LauncherAuthSessionService _auth;
    private readonly HashService _hash;
    private readonly DiagnosticsService _diagnostics;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public PlayerConfigSyncService(
        LauncherPathsService paths,
        LauncherAuthSessionService auth,
        HashService hash,
        DiagnosticsService diagnostics)
    {
        _paths = paths;
        _auth = auth;
        _hash = hash;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Brings the account's settings and the ones on disk into agreement, before a launch.
    ///
    /// Per file: one that has been edited since the last exchange is uploaded, one that has
    /// not is replaced by the account's copy. A file the account has never seen is uploaded
    /// if it is there.
    /// </summary>
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!_auth.IsAuthenticated) return;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var marks = ReadMarks();
            foreach (var relative in Tracked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try { await ExchangeAsync(relative, marks, cancellationToken); }
                catch (Exception ex) { _diagnostics.Warn("Config", $"Could not sync '{relative}': {ex.Message}"); }
            }
            WriteMarks(marks);
        }
        finally { _lock.Release(); }
    }

    /// <summary>Saves whatever has changed on disk since the last exchange.</summary>
    public async Task UploadAsync(CancellationToken cancellationToken = default)
    {
        if (!_auth.IsAuthenticated) return;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var marks = ReadMarks();
            foreach (var relative in Tracked)
            {
                try
                {
                    var local = LocalPath(relative);
                    if (!File.Exists(local)) continue;
                    var digest = _hash.ComputeSha256(local);
                    if (marks.TryGetValue(Slot(relative), out var known) && known == digest) continue;
                    if (await UploadAsync(relative, local, cancellationToken)) marks[Slot(relative)] = digest;
                }
                catch (Exception ex) { _diagnostics.Warn("Config", $"Could not save '{relative}': {ex.Message}"); }
            }
            WriteMarks(marks);
        }
        finally { _lock.Release(); }
    }

    private async Task ExchangeAsync(string relative, Dictionary<string, string> marks, CancellationToken cancellationToken)
    {
        var local = LocalPath(relative);
        var slot = Slot(relative);
        var known = marks.TryGetValue(slot, out var mark) ? mark : null;
        var digest = File.Exists(local) ? _hash.ComputeSha256(local) : null;

        // Edited since we last agreed with the account — which includes the game being
        // killed before anything was uploaded. What the player has wins.
        if (known is not null && digest is not null && known != digest)
        {
            if (await UploadAsync(relative, local, cancellationToken)) marks[slot] = digest;
            return;
        }

        // No mark means this install has never exchanged this file: a fresh install, a
        // repaired one, a new computer. The file on disk is then the pack's own default, not
        // anything the player chose, so the account is asked first and only answered with
        // the local copy if it has nothing — otherwise reinstalling would upload the
        // defaults over the settings it was supposed to bring back.
        var stored = await _auth.GetConfigFileAsync(slot, cancellationToken);
        // Nothing under this server's slot: the account may still hold what earlier versions
        // of the launcher saved without one. Read it once — the next upload writes the slot.
        if (!stored.Found || string.IsNullOrWhiteSpace(stored.ContentB64))
            stored = await _auth.GetConfigFileAsync(relative, cancellationToken);
        if (!stored.Found || string.IsNullOrWhiteSpace(stored.ContentB64))
        {
            // The account holds nothing for this file yet, so what is here becomes the
            // starting point — including the pack's defaults, which is the right thing to
            // save for a player who has not changed anything yet.
            if (digest is not null && await UploadAsync(relative, local, cancellationToken)) marks[slot] = digest;
            return;
        }

        byte[] content;
        try { content = Convert.FromBase64String(stored.ContentB64); }
        catch (FormatException) { _diagnostics.Warn("Config", $"Stored '{relative}' is not readable; leaving the local one."); return; }

        var directory = Path.GetDirectoryName(local);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(local, content, cancellationToken);
        var written = _hash.ComputeSha256(local);
        marks[slot] = written;
        if (written != digest) _diagnostics.Info("Config", $"Restored {slot} from the account.");
    }

    private async Task<bool> UploadAsync(string relative, string localPath, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(localPath, cancellationToken);
        var content = Convert.ToBase64String(bytes);
        if (content.Length > MaxBase64Length)
        {
            _diagnostics.Warn("Config", $"'{relative}' is {bytes.Length / 1024} KB, too large to keep on the account.");
            return false;
        }
        await _auth.SaveConfigFileAsync(Slot(relative), content, cancellationToken);
        _diagnostics.Info("Config", $"Saved {Slot(relative)} to the account.");
        return true;
    }

    /// <summary>The key this file is stored under: the active server, then the path.</summary>
    private string Slot(string relative)
    {
        var slug = _paths.ActiveServerSlug;
        return $"{(string.IsNullOrWhiteSpace(slug) ? DefaultSlot : slug)}/{relative}";
    }

    private string LocalPath(string relative) =>
        Path.Combine(_paths.GameDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

    // What was last exchanged with the account, by slot: a file that no longer hashes to
    // this has been edited since, and is the one worth keeping.
    private string MarksPath => Path.Combine(_paths.StateDirectory, "config-sync.json");

    private Dictionary<string, string> ReadMarks()
    {
        try
        {
            if (!File.Exists(MarksPath)) return new Dictionary<string, string>(StringComparer.Ordinal);
            var json = File.ReadAllText(MarksPath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Config", $"Could not read the sync marks: {ex.Message}");
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private void WriteMarks(Dictionary<string, string> marks)
    {
        try
        {
            Directory.CreateDirectory(_paths.StateDirectory);
            var temporary = MarksPath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(marks));
            File.Move(temporary, MarksPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Config", $"Could not write the sync marks: {ex.Message}");
        }
    }
}
