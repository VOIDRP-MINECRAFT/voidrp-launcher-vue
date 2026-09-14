using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoidRpLauncher.CoreHost.Contracts;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Moves the game files (servers/&lt;slug&gt;: mods, assets, Java, per-server state) to a folder
/// the player picks — typically a bigger drive, or a path without Cyrillic letters. The launcher's
/// own state (auth, settings) stays where it is. A failed move leaves everything at the old place.
/// </summary>
public sealed class GameLocationService
{
    private const long MoveSafetyMarginBytes = 1024L * 1024 * 1024;
    private const long MinFreeForFreshInstallBytes = 8L * 1024 * 1024 * 1024;
    private const int MaxWindowsRootLength = 80;

    private readonly LauncherPathsService _paths;
    private readonly DiagnosticsService _diagnostics;

    public GameLocationService(LauncherPathsService paths, DiagnosticsService diagnostics)
    {
        _paths = paths;
        _diagnostics = diagnostics;
    }

    private string ServersDir(string root) => Path.Combine(root, "servers");

    public GameLocationInfoDto GetInfo(bool includeSize)
    {
        var info = new GameLocationInfoDto
        {
            CurrentRoot = _paths.GamesRootDirectory,
            DefaultRoot = _paths.BaseDirectory,
            IsCustom = _paths.IsCustomGamesRoot,
            GameDirectory = _paths.GameDirectory,
        };
        try { info.FreeBytes = FreeBytes(_paths.GamesRootDirectory); } catch { }
        if (includeSize)
        {
            try { info.SizeBytes = DirectorySize(ServersDir(_paths.GamesRootDirectory)); } catch { }
        }
        return info;
    }

    /// <summary>Returns a player-facing reason why <paramref name="newRoot"/> can't be used, or null.</summary>
    public string? Validate(string? newRoot, bool moveFiles)
    {
        var target = string.IsNullOrWhiteSpace(newRoot) ? _paths.BaseDirectory : newRoot.Trim();
        if (!Path.IsPathFullyQualified(target))
            return "Укажите полный путь к папке, например D:\\Games\\VoidRP.";

        target = Path.GetFullPath(target);
        var current = Path.GetFullPath(_paths.GamesRootDirectory);
        if (SamePath(target, current))
            return "Файлы игры уже лежат в этой папке.";

        // Minecraft natives and the embedded Chromium (MCEF) regularly fail to start from such paths.
        // Going back to the default place is always allowed, whatever the Windows user name is.
        var isDefault = SamePath(target, _paths.BaseDirectory);
        if (!isDefault && HasNonAscii(target))
            return "В пути есть русские буквы или другие не-латинские символы — с такими путями игра и встроенный браузер часто не запускаются. Выберите папку с латинским названием, например D:\\Games\\VoidRP.";

        if (OperatingSystem.IsWindows() && !isDefault)
        {
            if (target.Length > MaxWindowsRootLength)
                return $"Путь слишком длинный ({target.Length} символов). Внутри сборки есть очень глубокие папки, и Windows не сможет их создать — выберите путь покороче, например D:\\Games\\VoidRP.";

            foreach (var system in new[] { Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                var systemPath = Environment.GetFolderPath(system);
                if (!string.IsNullOrWhiteSpace(systemPath) && IsInside(target, systemPath))
                    return "Системные папки (Windows, Program Files) не подходят — там у лаунчера нет прав на запись. Выберите папку на другом диске или в своей папке пользователя.";
            }
        }

        var currentServers = ServersDir(current);
        var targetServers = ServersDir(target);
        if (IsInside(targetServers, currentServers) || IsInside(currentServers, targetServers))
            return "Нельзя переносить файлы игры внутрь их текущей папки. Выберите другую папку.";

        try
        {
            Directory.CreateDirectory(target);
            var probe = Path.Combine(target, $".voidrp-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            return $"В эту папку нельзя записывать файлы: {ex.Message}";
        }

        var free = FreeBytes(target);
        if (moveFiles && Directory.Exists(currentServers))
        {
            foreach (var slugDir in Directory.EnumerateDirectories(currentServers))
            {
                if (Directory.Exists(Path.Combine(targetServers, Path.GetFileName(slugDir))))
                    return $"В выбранной папке уже есть файлы игры (servers\\{Path.GetFileName(slugDir)}). Выберите пустую папку или переключитесь на неё без переноса.";
            }

            if (!SameVolume(current, target))
            {
                var needed = DirectorySize(currentServers) + MoveSafetyMarginBytes;
                if (free < needed)
                    return $"На диске не хватает места: нужно {Gb(needed)}, свободно {Gb(free)}.";
            }
        }
        else if (!Directory.Exists(targetServers) && free < MinFreeForFreshInstallBytes)
        {
            return $"На диске свободно {Gb(free)} — сборке нужно не меньше {Gb(MinFreeForFreshInstallBytes)}.";
        }

        return null;
    }

    public async Task MoveAsync(string? newRoot, bool moveFiles, Action<string, double> progress, CancellationToken cancellationToken)
    {
        var target = string.IsNullOrWhiteSpace(newRoot) ? _paths.BaseDirectory : Path.GetFullPath(newRoot.Trim());
        var current = Path.GetFullPath(_paths.GamesRootDirectory);
        var currentServers = ServersDir(current);
        var targetServers = ServersDir(target);

        if (moveFiles && Directory.Exists(currentServers))
        {
            Directory.CreateDirectory(targetServers);
            var slugDirs = Directory.EnumerateDirectories(currentServers).ToList();

            var toCopy = new List<string>();
            foreach (var slugDir in slugDirs)
            {
                if (!SameVolume(current, target))
                {
                    toCopy.Add(slugDir);
                    continue;
                }

                // Same drive: a rename per server folder, instant. A rename can still fail across
                // mount points (Linux) — then fall back to copying that folder.
                try
                {
                    progress($"Переносим {Path.GetFileName(slugDir)}...", 50);
                    Directory.Move(slugDir, Path.Combine(targetServers, Path.GetFileName(slugDir)));
                }
                catch (IOException ex)
                {
                    _diagnostics.Warn("GameLocation", $"Rename of {slugDir} failed ({ex.Message}), copying instead.");
                    toCopy.Add(slugDir);
                }
            }

            if (toCopy.Count > 0)
                await CopyThenDeleteAsync(toCopy, targetServers, progress, cancellationToken);

            TryDeleteIfEmpty(currentServers);
        }

        _paths.SetGamesRoot(SamePath(target, _paths.BaseDirectory) ? null : target);
        _diagnostics.Info("GameLocation", $"Games root is now {_paths.GamesRootDirectory} (moved files: {moveFiles}).");
    }

    private async Task CopyThenDeleteAsync(List<string> slugDirs, string targetServers, Action<string, double> progress, CancellationToken cancellationToken)
    {
        var files = slugDirs
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Select(f => (Root: dir, File: f)))
            .ToList();
        var totalBytes = Math.Max(1, files.Sum(f => new FileInfo(f.File).Length));
        long copiedBytes = 0;
        var createdTargets = new List<string>();

        try
        {
            foreach (var slugDir in slugDirs)
            {
                var dst = Path.Combine(targetServers, Path.GetFileName(slugDir));
                Directory.CreateDirectory(dst);
                createdTargets.Add(dst);
            }

            var lastReport = DateTime.MinValue;
            foreach (var (root, file) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(root, file);
                var dst = Path.Combine(targetServers, Path.GetFileName(root), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

                await using (var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
                await using (var destination = new FileStream(dst, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
                {
                    await source.CopyToAsync(destination, 1 << 20, cancellationToken);
                }
                File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(file));   // keeps the sync hash cache valid

                copiedBytes += new FileInfo(file).Length;
                if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(250))
                {
                    lastReport = DateTime.UtcNow;
                    progress($"Копируем файлы игры: {Gb(copiedBytes)} из {Gb(totalBytes)}", copiedBytes * 95.0 / totalBytes);
                }
            }
        }
        catch
        {
            // Roll back the partial copy; the originals were never touched.
            foreach (var dir in createdTargets)
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
            throw;
        }

        progress("Удаляем файлы со старого места...", 97);
        foreach (var slugDir in slugDirs)
        {
            try { Directory.Delete(slugDir, recursive: true); }
            catch (Exception ex) { _diagnostics.Warn("GameLocation", $"Copied, but could not delete old folder {slugDir}: {ex.Message}"); }
        }
    }

    public static bool HasNonAscii(string path) => path.Any(c => c > 127);

    private static long DirectorySize(string dir)
        => Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
            : 0;

    private static long FreeBytes(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(root) ? long.MaxValue : new DriveInfo(root).AvailableFreeSpace;
    }

    private static bool SameVolume(string a, string b)
        => string.Equals(Path.GetPathRoot(Path.GetFullPath(a)), Path.GetPathRoot(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string a, string b)
        => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);

    private static bool IsInside(string path, string parent)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar;
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteIfEmpty(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { }
    }

    private static string Gb(long bytes) => $"{bytes / (1024.0 * 1024 * 1024):0.#} ГБ";
}
