using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VoidRpLauncher.CoreHost.Models;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class LauncherPlatformService
{
    public LauncherPlatformInfo Current { get; } = Detect();

    private static LauncherPlatformInfo Detect()
    {
        if (OperatingSystem.IsWindows())
        {
            return new LauncherPlatformInfo
            {
                Rid = "win-x64",
                DisplayName = "Windows x64",
                IsWindows = true,
                IsMacOs = false,
                LauncherExecutableFileName = "VoidRpLauncher.exe",
                UpdaterExecutableFileName = "VoidRpLauncher.Updater.exe",
                RuntimeManifestFileName = "runtime-manifest.win-x64.json"
            };
        }

        if (OperatingSystem.IsMacOS())
        {
            var rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "osx-arm64",
                Architecture.X64 => "osx-x64",
                _ => throw new PlatformNotSupportedException($"macOS architecture '{RuntimeInformation.ProcessArchitecture}' is not supported.")
            };

            return new LauncherPlatformInfo
            {
                Rid = rid,
                DisplayName = rid == "osx-arm64" ? "macOS Apple Silicon" : "macOS Intel",
                IsWindows = false,
                IsMacOs = true,
                LauncherExecutableFileName = "VoidRpLauncher.App",
                UpdaterExecutableFileName = "VoidRpLauncher.Updater",
                RuntimeManifestFileName = $"runtime-manifest.{rid}.json"
            };
        }

        if (OperatingSystem.IsLinux())
        {
            var rid = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "linux-x64",
                Architecture.Arm64 => "linux-arm64",
                _ => throw new PlatformNotSupportedException($"Linux architecture '{RuntimeInformation.ProcessArchitecture}' is not supported.")
            };

            return new LauncherPlatformInfo
            {
                Rid = rid,
                DisplayName = rid == "linux-arm64" ? "Linux ARM64" : "Linux x64",
                IsWindows = false,
                IsMacOs = false,
                IsLinux = true,
                LauncherExecutableFileName = "VoidRpLauncher",
                UpdaterExecutableFileName = "VoidRpLauncher.Updater",
                RuntimeManifestFileName = $"runtime-manifest.{rid}.json"
            };
        }

        throw new PlatformNotSupportedException("Unsupported platform. Supported: Windows x64, Linux x64/arm64, macOS x64/arm64.");
    }
}

public sealed class LauncherPlatformInfo
{
    public string Rid { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsWindows { get; init; }
    public bool IsMacOs { get; init; }
    public bool IsLinux { get; init; }
    public string LauncherExecutableFileName { get; init; } = string.Empty;
    public string UpdaterExecutableFileName { get; init; } = string.Empty;
    public string RuntimeManifestFileName { get; init; } = string.Empty;
}

public sealed class LauncherPathsService
{
    private readonly LauncherPlatformService _platformService;

    public LauncherPlatformInfo Platform => _platformService.Current;
    public string BaseDirectory { get; }
    public string LauncherInstallDirectory { get; }

    // The slug of the server whose files are currently active. Set dynamically
    // from the backend catalogue (never hardcoded), so every server — including
    // ones added later — gets its own isolated install under servers/<slug>/.
    public string? ActiveServerSlug { get; private set; }

    // Per-server root: servers/<slug>. When no server is active yet (very first
    // run, fully offline) we fall back to the legacy single-server layout so
    // existing installs keep working until the catalogue resolves the slug.
    private string? ServerRootDirectory =>
        string.IsNullOrEmpty(ActiveServerSlug) ? null : Path.Combine(BaseDirectory, "servers", ActiveServerSlug);

    public string GameDirectory => ServerRootDirectory is null
        ? Path.Combine(BaseDirectory, "game")
        : Path.Combine(ServerRootDirectory, "game");
    public string VersionsDirectory => Path.Combine(GameDirectory, "versions");
    public string JavaDirectory => Path.Combine(GameDirectory, "java");
    public string LogsDirectory => Path.Combine(BaseDirectory, "logs");
    // Global launcher state shared across all servers (auth tokens, play-tickets,
    // server selection, launcher settings, hash cache).
    public string StateDirectory => Path.Combine(BaseDirectory, "state");
    // Per-server sync/runtime bookkeeping (sync-state json, runtime.stamp) so
    // switching servers never confuses one install's state for another's.
    public string ServerStateDirectory => ServerRootDirectory is null
        ? StateDirectory
        : Path.Combine(ServerRootDirectory, "state");
    public string TempDirectory => Path.Combine(BaseDirectory, "temp");
    public string SelfUpdateDirectory => Path.Combine(TempDirectory, "self-update");
    public string SettingsFilePath => Path.Combine(BaseDirectory, "launcher_settings.json");

    /// <summary>
    /// Points all per-server directories (game, java, versions, sync state) at
    /// servers/&lt;slug&gt;. Slug comes from the backend catalogue, so any new
    /// server is isolated automatically. When <paramref name="isDefault"/> is
    /// true and a legacy single-server install exists, it is relocated once.
    /// </summary>
    public void SetActiveServer(string? slug, bool isDefault = false)
    {
        ActiveServerSlug = string.IsNullOrWhiteSpace(slug) ? null : SanitizeSlug(slug);
        if (ActiveServerSlug is not null && isDefault)
        {
            TryMigrateLegacyLayout();
        }
    }

    // The pre-multi-server launcher stored everything under BaseDirectory/game.
    // The first time the default server becomes active we relocate that install
    // into servers/<slug>/game via a rename (instant on the same volume) so
    // existing players don't re-download the whole pack. Best-effort: on any
    // failure the normal sync path simply rebuilds a fresh install. Guarded by
    // dir checks, so it is idempotent and safe to call repeatedly.
    private void TryMigrateLegacyLayout()
    {
        try
        {
            var legacyGame = Path.Combine(BaseDirectory, "game");
            var targetGame = GameDirectory;
            if (Directory.Exists(legacyGame)
                && !Directory.Exists(targetGame)
                && !string.Equals(Path.GetFullPath(legacyGame), Path.GetFullPath(targetGame), StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(targetGame);
                if (!string.IsNullOrWhiteSpace(parent)) Directory.CreateDirectory(parent);
                Directory.Move(legacyGame, targetGame);
                CleanupLegacyGlobalState();
            }
        }
        catch
        {
            // ignore — sync will recreate the game directory from the manifest
        }
    }

    // Files that stay in the global state dir and are shared across all servers —
    // never removed by legacy cleanup.
    private static readonly HashSet<string> GlobalStateFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "launcher-auth.json", "play-ticket.json", "selected-server.json", "hash-cache.json",
    };

    // After migrating the legacy install we remove the now-orphaned per-server
    // bookkeeping that used to live in the global state dir: the old
    // runtime.stamp and any sync-state <packName>.json files. Shared global
    // files (auth, play-ticket, selection, hash cache) are never touched — a
    // *.json is deleted only if it is actually a sync-state (top-level "files"
    // array) and not in the shared allowlist.
    private void CleanupLegacyGlobalState()
    {
        try
        {
            var stateDir = StateDirectory;
            if (!Directory.Exists(stateDir)) return;

            var stamp = Path.Combine(stateDir, "runtime.stamp");
            if (File.Exists(stamp)) { try { File.Delete(stamp); } catch { } }

            foreach (var json in Directory.EnumerateFiles(stateDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (GlobalStateFileNames.Contains(Path.GetFileName(json))) continue;
                if (IsSyncStateFile(json))
                {
                    try { File.Delete(json); } catch { }
                }
            }
        }
        catch { }
    }

    private static bool IsSyncStateFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("files", out var files)
                && files.ValueKind == JsonValueKind.Array;
        }
        catch
        {
            return false;
        }
    }

    private static string SanitizeSlug(string slug)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(slug.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }

    public LauncherPathsService(LauncherPlatformService platformService)
    {
        _platformService = platformService;
        BaseDirectory = ResolveBaseDirectory();
        LauncherInstallDirectory = ResolveLauncherInstallDirectory();
    }

    private string ResolveBaseDirectory()
    {
        if (Platform.IsMacOs)
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, "Library", "Application Support", "VoidRpLauncher");
        }

        if (Platform.IsLinux)
        {
            var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdgDataHome))
                return Path.Combine(xdgDataHome, "VoidRpLauncher");
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".local", "share", "VoidRpLauncher");
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoidRpLauncher");
    }

    public void EnsureBaseDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(GameDirectory);
        Directory.CreateDirectory(VersionsDirectory);
        Directory.CreateDirectory(JavaDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(StateDirectory);
        Directory.CreateDirectory(ServerStateDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(SelfUpdateDirectory);
    }

    public string GetCurrentExecutablePath()
    {
        var portableExecutable = Environment.GetEnvironmentVariable("PORTABLE_EXECUTABLE_FILE");
        if (!string.IsNullOrWhiteSpace(portableExecutable) && File.Exists(portableExecutable))
        {
            return portableExecutable;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException("Could not resolve current launcher executable path.");

        return executablePath;
    }

    public string GetCurrentLauncherUpdateTargetPath() => Platform.IsMacOs ? GetCurrentAppBundlePath() : GetCurrentExecutablePath();

    public string GetCurrentAppBundlePath()
    {
        var executablePath = GetCurrentExecutablePath();
        var bundlePath = TryResolveAppBundlePath(executablePath);
        if (string.IsNullOrWhiteSpace(bundlePath))
            throw new InvalidOperationException("Current macOS launcher is not running from a .app bundle.");

        return bundlePath;
    }

    public string ResolveJavaExecutablePath()
    {
        var guiPath = Path.Combine(JavaDirectory, "bin", Platform.IsWindows ? "javaw.exe" : "java");
        var consolePath = Path.Combine(JavaDirectory, "bin", Platform.IsWindows ? "java.exe" : "java");
        if (File.Exists(guiPath)) return guiPath;
        if (File.Exists(consolePath)) return consolePath;

        if (Directory.Exists(JavaDirectory))
        {
            var f1 = Directory.EnumerateFiles(JavaDirectory, Platform.IsWindows ? "javaw.exe" : "java", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(f1)) return f1;

            var f2 = Directory.EnumerateFiles(JavaDirectory, Platform.IsWindows ? "java.exe" : "java", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(f2)) return f2;
        }

        throw new FileNotFoundException($"Java executable not found inside '{JavaDirectory}'.");
    }

    private string ResolveLauncherInstallDirectory()
    {
        var executablePath = GetCurrentExecutablePath();

        if (Platform.IsMacOs)
        {
            var bundlePath = TryResolveAppBundlePath(executablePath);
            if (!string.IsNullOrWhiteSpace(bundlePath))
                return Path.GetDirectoryName(bundlePath) ?? AppContext.BaseDirectory;
        }

        return Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
    }

    private static string? TryResolveAppBundlePath(string executablePath)
    {
        var current = Path.GetDirectoryName(executablePath);

        while (!string.IsNullOrWhiteSpace(current))
        {
            if (current.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }
}

public sealed class HashService
{
    private static readonly JsonSerializerOptions CacheJson = new() { WriteIndented = false };

    private readonly string _cacheFilePath;
    private readonly Dictionary<string, HashCacheEntry> _cache;
    private bool _dirty;

    public HashService(LauncherPathsService pathsService)
    {
        _cacheFilePath = Path.Combine(pathsService.StateDirectory, "hash-cache.json");
        _cache = TryLoadCache();
    }

    public string ComputeSha256(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (_cache.TryGetValue(filePath, out var cached)
                && cached.Size == info.Length
                && cached.Mtime == info.LastWriteTimeUtc.Ticks)
            {
                return cached.Hash;
            }
        }
        catch { /* fall through to compute */ }

        var hash = ComputeSha256Raw(filePath);

        try
        {
            var info = new FileInfo(filePath);
            _cache[filePath] = new HashCacheEntry(info.Length, info.LastWriteTimeUtc.Ticks, hash);
            _dirty = true;
        }
        catch { }

        return hash;
    }

    public void Flush()
    {
        if (!_dirty) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
            File.WriteAllText(_cacheFilePath, JsonSerializer.Serialize(_cache, CacheJson));
            _dirty = false;
        }
        catch { }
    }

    private string ComputeSha256Raw(string filePath)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var sha256 = SHA256.Create();
                return Convert.ToHexString(sha256.ComputeHash(stream));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(100);
            }
        }

        throw new IOException($"Unable to read file for SHA-256: {filePath}", lastError);
    }

    private Dictionary<string, HashCacheEntry> TryLoadCache()
    {
        try
        {
            if (!File.Exists(_cacheFilePath)) return new();
            var json = File.ReadAllText(_cacheFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, HashCacheEntry>>(json, CacheJson) ?? new();
        }
        catch { return new(); }
    }

    private sealed record HashCacheEntry(long Size, long Mtime, string Hash);
}

public sealed class LauncherSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly LauncherPathsService _pathsService;

    public event EventHandler? SettingsChanged;

    public LauncherSettingsService(LauncherPathsService pathsService) => _pathsService = pathsService;

    public LauncherUserSettings Load()
    {
        try
        {
            _pathsService.EnsureBaseDirectories();

            if (!File.Exists(_pathsService.SettingsFilePath))
            {
                var defaults = new LauncherUserSettings { MaxRamMb = DetectDefaultRamMb() };
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(_pathsService.SettingsFilePath);
            var settings = JsonSerializer.Deserialize<LauncherUserSettings>(json) ?? new LauncherUserSettings();
            settings.MaxRamMb = NormalizeMemory(settings.MaxRamMb);
            return settings;
        }
        catch
        {
            return new LauncherUserSettings();
        }
    }

    public void Save(LauncherUserSettings settings)
    {
        _pathsService.EnsureBaseDirectories();
        settings.MaxRamMb = NormalizeMemory(settings.MaxRamMb);
        File.WriteAllText(_pathsService.SettingsFilePath, JsonSerializer.Serialize(settings, JsonOptions));
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public int NormalizeMemory(int value)
    {
        if (value <= 0) value = 4096;
        value = (int)(Math.Round(value / 512.0) * 512);
        if (value < 2048) value = 2048;
        if (value > 16384) value = 16384;

        // Cap at (totalPhysicalRam - 2048 MB) so the OS and JVM overhead always have headroom.
        try
        {
            int maxSafe = (int)(GetTotalPhysicalMemoryBytes() / (1024L * 1024) - 2048);
            maxSafe = (int)(Math.Floor(maxSafe / 512.0) * 512);
            if (maxSafe >= 2048 && value > maxSafe)
                value = maxSafe;
        }
        catch { }

        return value;
    }

    private static int DetectDefaultRamMb()
    {
        try
        {
            long totalGb = GetTotalPhysicalMemoryBytes() / (1024L * 1024 * 1024);
            return totalGb >= 16 ? 6144 : 4096;
        }
        catch
        {
            return 4096;
        }
    }

    private static long GetTotalPhysicalMemoryBytes()
    {
        if (OperatingSystem.IsLinux())
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (!line.StartsWith("MemTotal:", StringComparison.Ordinal)) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    return kb * 1024L;
                break;
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            if (GetPhysicallyInstalledSystemMemory(out ulong totalKb))
                return (long)totalKb * 1024L;
        }
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong TotalMemoryInKilobytes);
}

public sealed class ManifestService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private readonly DiagnosticsService _diagnostics;

    public ManifestService(DiagnosticsService diagnostics) => _diagnostics = diagnostics;

    public async Task<LauncherManifest> LoadAsync(string manifestUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(manifestUrl))
            throw new ArgumentException("Manifest URL is empty.", nameof(manifestUrl));

        _diagnostics.Info("Manifest", $"Loading pack manifest: {manifestUrl}");
        var manifest = await HttpClient.GetFromJsonAsync<LauncherManifest>(AddCacheBuster(manifestUrl), JsonOptions, cancellationToken);

        if (manifest is null)
            throw new InvalidOperationException("Manifest is empty or could not be parsed.");

        _diagnostics.Info("Manifest", $"Manifest loaded: {manifest.PackName} {manifest.PackVersion}");
        return manifest;
    }

    private static string AddCacheBuster(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }
}

public sealed class FileSyncService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly LauncherPathsService _pathsService;
    private readonly HashService _hashService;
    private readonly DiagnosticsService _diagnostics;

    public FileSyncService(LauncherPathsService pathsService, HashService hashService, DiagnosticsService diagnostics)
    {
        _pathsService = pathsService;
        _hashService = hashService;
        _diagnostics = diagnostics;
    }

    public Task SyncAsync(LauncherManifest manifest, IProgress<SyncProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        => SyncAsync(manifest, manifest.PackName, null, progress, cancellationToken);

    public Task SyncAsync(LauncherManifest manifest, IReadOnlySet<string>? disabledOptionalMods, IProgress<SyncProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        => SyncAsync(manifest, manifest.PackName, disabledOptionalMods, progress, cancellationToken);

    public async Task SyncAsync(LauncherManifest manifest, string syncKey, IReadOnlySet<string>? disabledOptionalMods, IProgress<SyncProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        if (string.IsNullOrWhiteSpace(syncKey)) throw new ArgumentException("Sync key is empty.", nameof(syncKey));

        _pathsService.EnsureBaseDirectories();

        var normalizedSyncKey = SanitizeFileName(syncKey);
        var stateFilePath = Path.Combine(_pathsService.ServerStateDirectory, $"{normalizedSyncKey}.json");
        var logFilePath = Path.Combine(_pathsService.LogsDirectory, $"sync-{normalizedSyncKey}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var currentManifestPaths = manifest.Files.Select(f => NormalizeRelativePath(f.Path)).Where(p => !string.IsNullOrWhiteSpace(p)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousState = await LoadStateAsync(stateFilePath, cancellationToken);

        await using var logWriter = new StreamWriter(logFilePath, append: false);
        await logWriter.WriteLineAsync($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync started");
        await logWriter.WriteLineAsync($"Key: {normalizedSyncKey}");
        await logWriter.WriteLineAsync($"Pack: {manifest.PackName}");
        await logWriter.WriteLineAsync($"Version: {manifest.PackVersion}");
        await logWriter.WriteLineAsync($"Files in manifest: {manifest.Files.Count}");
        await logWriter.WriteLineAsync(string.Empty);
        _diagnostics.Info("Sync", $"Sync started. Key={normalizedSyncKey}, files={manifest.Files.Count}");

        var orderedFiles = manifest.Files.OrderBy(f => NormalizeRelativePath(f.Path), StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < orderedFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = orderedFiles[index];
            var relativePath = NormalizeRelativePath(entry.Path);
            if (string.IsNullOrWhiteSpace(relativePath)) continue;

            var localPath = Path.Combine(_pathsService.GameDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var localDirectory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(localDirectory))
                Directory.CreateDirectory(localDirectory);

            progress?.Report(new SyncProgressInfo
            {
                Stage = "Проверяем файл",
                CurrentFile = relativePath,
                ProcessedFiles = index,
                TotalFiles = orderedFiles.Count,
                Percent = CalculateSyncPercent(index, orderedFiles.Count, 0),
                DetailMessage = "Проверяем наличие, размер и SHA-256...",
                LocalPath = localPath,
                SourceUrl = entry.Url
            });

            // Skip disabled optional mods and delete local copy so Minecraft won't load them
            if (entry.Optional && !entry.Required &&
                disabledOptionalMods?.Contains(relativePath) == true)
            {
                await logWriter.WriteLineAsync($"[SKIP-DISABLED] {relativePath}");
                if (File.Exists(localPath))
                {
                    try
                    {
                        File.Delete(localPath);
                        DeleteEmptyParentDirectories(localPath, _pathsService.GameDirectory);
                        await logWriter.WriteLineAsync($"[DELETE-DISABLED] {relativePath}");
                    }
                    catch (Exception ex)
                    {
                        await logWriter.WriteLineAsync($"[DELETE-DISABLED-FAILED] {relativePath} :: {ex.Message}");
                    }
                }
                continue;
            }

            var needsDownload = NeedsDownload(entry, localPath, relativePath);
            if (needsDownload)
            {
                _diagnostics.Info("Sync", $"Downloading: {relativePath}");
                await logWriter.WriteLineAsync($"[DOWNLOAD] {relativePath}");
                await DownloadFileAsync(entry, relativePath, localPath, index, orderedFiles.Count, progress, cancellationToken);
            }
            else if (IsPlayerWritable(relativePath) && File.Exists(localPath))
            {
                await logWriter.WriteLineAsync($"[SKIP-PLAYER] {relativePath}");
            }
            else
            {
                await logWriter.WriteLineAsync($"[OK] {relativePath}");
            }

            progress?.Report(new SyncProgressInfo
            {
                Stage = "Файл готов",
                CurrentFile = relativePath,
                ProcessedFiles = index + 1,
                TotalFiles = orderedFiles.Count,
                Percent = orderedFiles.Count == 0 ? 85 : ((double)(index + 1) / orderedFiles.Count) * 85.0,
                DetailMessage = "Файл актуален и готов к использованию.",
                LocalPath = localPath,
                SourceUrl = entry.Url,
                CurrentFileBytesDownloaded = entry.Size,
                CurrentFileBytesTotal = entry.Size
            });
        }

        var staleFiles = previousState.Files.Except(currentManifestPaths, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        for (var index = 0; index < staleFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = NormalizeRelativePath(staleFiles[index]);
            var fullPath = Path.Combine(_pathsService.GameDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            progress?.Report(new SyncProgressInfo
            {
                Stage = "Чистим устаревшие файлы",
                CurrentFile = relativePath,
                ProcessedFiles = index,
                TotalFiles = staleFiles.Count,
                Percent = staleFiles.Count == 0 ? 100 : 85.0 + (((double)index / staleFiles.Count) * 15.0),
                DetailMessage = "Проверяем и удаляем устаревший managed-файл...",
                LocalPath = fullPath
            });

            if (IsProtectedFromDeletion(relativePath))
            {
                await logWriter.WriteLineAsync($"[SKIP-PROTECTED] {relativePath}");
                continue;
            }

            if (!File.Exists(fullPath))
            {
                await logWriter.WriteLineAsync($"[MISSING-ALREADY] {relativePath}");
                continue;
            }

            try
            {
                File.Delete(fullPath);
                DeleteEmptyParentDirectories(fullPath, _pathsService.GameDirectory);
                await logWriter.WriteLineAsync($"[DELETE] {relativePath}");
            }
            catch (Exception ex)
            {
                await logWriter.WriteLineAsync($"[DELETE-FAILED] {relativePath} :: {ex.Message}");
            }
        }

        await ScanAndRemoveRogueModsAsync(currentManifestPaths, logWriter, progress, cancellationToken);

        await SaveStateAsync(stateFilePath, new SyncState { Files = currentManifestPaths.Where(p => !IsPlayerWritable(p)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList() }, cancellationToken);
        _hashService.Flush();
        progress?.Report(new SyncProgressInfo { Stage = "Готово", Percent = 100, ProcessedFiles = orderedFiles.Count, TotalFiles = orderedFiles.Count, DetailMessage = "Синхронизация завершена." });
        _diagnostics.Info("Sync", $"Sync finished. Log file: {logFilePath}");
    }

    private async Task ScanAndRemoveRogueModsAsync(HashSet<string> currentManifestPaths, StreamWriter logWriter, IProgress<SyncProgressInfo>? progress, CancellationToken cancellationToken)
    {
        var modsDir = Path.Combine(_pathsService.GameDirectory, "mods");
        if (!Directory.Exists(modsDir)) return;

        var rogueFiles = Directory.EnumerateFiles(modsDir, "*", SearchOption.AllDirectories)
            .Select(fullPath =>
            {
                var relativePath = NormalizeRelativePath(Path.GetRelativePath(_pathsService.GameDirectory, fullPath));
                return (fullPath, relativePath);
            })
            .Where(f => !currentManifestPaths.Contains(f.relativePath))
            .OrderBy(f => f.relativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rogueFiles.Count == 0) return;

        await logWriter.WriteLineAsync(string.Empty);
        await logWriter.WriteLineAsync($"[ROGUE-SCAN] Found {rogueFiles.Count} unauthorized file(s) in mods/ — removing.");
        _diagnostics.Warn("Sync", $"Rogue mod scan: {rogueFiles.Count} unauthorized file(s) detected.");

        for (var index = 0; index < rogueFiles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (fullPath, relativePath) = rogueFiles[index];

            progress?.Report(new SyncProgressInfo
            {
                Stage = "Удаляем посторонние моды",
                CurrentFile = relativePath,
                ProcessedFiles = index,
                TotalFiles = rogueFiles.Count,
                Percent = 90.0 + ((double)index / rogueFiles.Count * 8.0),
                DetailMessage = "Обнаружен мод, которого нет в официальном сборке. Удаляем.",
                LocalPath = fullPath
            });

            try
            {
                File.Delete(fullPath);
                DeleteEmptyParentDirectories(fullPath, _pathsService.GameDirectory);
                await logWriter.WriteLineAsync($"[DELETE-ROGUE] {relativePath}");
                _diagnostics.Warn("Sync", $"Deleted rogue mod: {relativePath}");
            }
            catch (Exception ex)
            {
                await logWriter.WriteLineAsync($"[DELETE-ROGUE-FAILED] {relativePath} :: {ex.Message}");
                _diagnostics.Error("Sync", $"Failed to delete rogue mod {relativePath}: {ex.Message}");
            }
        }
    }

    private bool NeedsDownload(LauncherManifestFile entry, string localPath, string relativePath)
    {
        if (!File.Exists(localPath)) return true;
        if (entry.AlwaysOverwrite) return true;
        if (IsPlayerWritable(relativePath)) return false;

        var fileInfo = new FileInfo(localPath);
        if (fileInfo.Length != entry.Size) return true;

        return !_hashService.ComputeSha256(localPath).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlayerWritable(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return string.Equals(normalized, "options.txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "servers.dat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "resourcepacks", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("resourcepacks/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "shaderpacks", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("shaderpacks/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "config", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadFileAsync(LauncherManifestFile entry, string relativePath, string localPath, int fileIndex, int totalFiles, IProgress<SyncProgressInfo>? progress, CancellationToken cancellationToken)
    {
        var tempPath = localPath + ".download";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        try
        {
            using var response = await HttpClient.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength > 0 ? response.Content.Headers.ContentLength.Value : entry.Size;

            await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var targetStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[1024 * 64];
                long downloadedBytes = 0;

                while (true)
                {
                    var read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read <= 0) break;

                    await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedBytes += read;

                    var ratio = totalBytes > 0 ? Math.Min(1.0, (double)downloadedBytes / totalBytes) : 0;
                    progress?.Report(new SyncProgressInfo
                    {
                        Stage = "Скачиваем файл",
                        CurrentFile = relativePath,
                        ProcessedFiles = fileIndex,
                        TotalFiles = totalFiles,
                        Percent = CalculateSyncPercent(fileIndex, totalFiles, ratio),
                        DetailMessage = totalBytes > 0 ? $"Скачано {FormatBytes(downloadedBytes)} из {FormatBytes(totalBytes)}" : $"Скачано {FormatBytes(downloadedBytes)}",
                        LocalPath = localPath,
                        SourceUrl = entry.Url,
                        CurrentFileBytesDownloaded = downloadedBytes,
                        CurrentFileBytesTotal = totalBytes
                    });
                }

                await targetStream.FlushAsync(cancellationToken);
            }

            var downloadedHash = _hashService.ComputeSha256(tempPath);
            if (!downloadedHash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Hash mismatch after download. Expected {entry.Sha256}, got {downloadedHash}.");

            if (File.Exists(localPath))
                File.Delete(localPath);

            File.Move(tempPath, localPath);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            throw;
        }
    }

    private static async Task<SyncState> LoadStateAsync(string stateFilePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(stateFilePath)) return new SyncState();

        try
        {
            return JsonSerializer.Deserialize<SyncState>(await File.ReadAllTextAsync(stateFilePath, cancellationToken)) ?? new SyncState();
        }
        catch
        {
            return new SyncState();
        }
    }

    private static async Task SaveStateAsync(string stateFilePath, SyncState state, CancellationToken cancellationToken)
        => await File.WriteAllTextAsync(stateFilePath, JsonSerializer.Serialize(state, JsonOptions), cancellationToken);

    private static double CalculateSyncPercent(int fileIndex, int totalFiles, double fileRatio)
    {
        if (totalFiles <= 0) return 0;
        fileRatio = Math.Max(0, Math.Min(1, fileRatio));
        return (((double)fileIndex / totalFiles) + (fileRatio / totalFiles)) * 85.0;
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/').Trim('/');
    private static bool IsProtectedFromDeletion(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return normalized.StartsWith("saves/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("crash-reports/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("downloads/", StringComparison.OrdinalIgnoreCase)
            || IsPlayerWritable(normalized);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private static void DeleteEmptyParentDirectories(string filePath, string stopRoot)
    {
        var current = Path.GetDirectoryName(filePath);

        while (!string.IsNullOrWhiteSpace(current) &&
               current.StartsWith(stopRoot, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(current, stopRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
            {
                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
            }
            else
            {
                break;
            }
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
            value = value.Replace(invalidChar, '_');

        return value.Replace(' ', '_');
    }

    private sealed class SyncState
    {
        public List<string> Files { get; set; } = new();
    }
}

public sealed class ClientRepairService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly LauncherPathsService _pathsService;
    private readonly DiagnosticsService _diagnostics;

    public ClientRepairService(LauncherPathsService pathsService, DiagnosticsService diagnostics)
    {
        _pathsService = pathsService;
        _diagnostics = diagnostics;
    }

    public async Task RepairAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        _pathsService.EnsureBaseDirectories();

        progress?.Report("Читаем состояние клиента...");
        _diagnostics.Info("Repair", "Reading launcher state files.");

        var stateFiles = Directory.Exists(_pathsService.ServerStateDirectory)
            ? Directory.GetFiles(_pathsService.ServerStateDirectory, "*.json", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        var managedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var stateFile in stateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(stateFile, cancellationToken);
                var state = JsonSerializer.Deserialize<SyncState>(json, JsonOptions);
                if (state?.Files is null) continue;

                foreach (var relativePath in state.Files)
                {
                    if (!string.IsNullOrWhiteSpace(relativePath))
                        managedPaths.Add(NormalizeRelativePath(relativePath));
                }
            }
            catch (Exception ex)
            {
                _diagnostics.Warn("Repair", $"Broken state file skipped: {stateFile} :: {ex.Message}");
            }
        }

        progress?.Report("Удаляем managed-файлы...");

        foreach (var relativePath in managedPaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsProtected(relativePath))
                continue;

            var fullPath = Path.Combine(_pathsService.GameDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                continue;

            try
            {
                File.Delete(fullPath);
                DeleteEmptyParentDirectories(fullPath, _pathsService.GameDirectory);
            }
            catch (Exception ex)
            {
                _diagnostics.Warn("Repair", $"Failed deleting managed file: {fullPath} :: {ex.Message}");
            }
        }

        progress?.Report("Удаляем state-файлы...");

        foreach (var stateFile in stateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Delete(stateFile);
            }
            catch (Exception ex)
            {
                _diagnostics.Warn("Repair", $"Failed deleting state file: {stateFile} :: {ex.Message}");
            }
        }

        progress?.Report("Чистим временные загрузки...");

        if (Directory.Exists(_pathsService.GameDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(_pathsService.GameDirectory, "*.download", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
        }

        progress?.Report("Ремонт завершён.");
        _diagnostics.Info("Repair", "Repair finished.");
    }

    private static bool IsProtected(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        return normalized.StartsWith("saves/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("logs/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("crash-reports/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("downloads/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "options.txt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "servers.dat", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "resourcepacks", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("resourcepacks/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "shaderpacks", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("shaderpacks/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "config", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/').Trim('/');

    private static void DeleteEmptyParentDirectories(string filePath, string stopRoot)
    {
        var current = Path.GetDirectoryName(filePath);

        while (!string.IsNullOrWhiteSpace(current) &&
               current.StartsWith(stopRoot, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(current, stopRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current) && !Directory.EnumerateFileSystemEntries(current).Any())
            {
                Directory.Delete(current);
                current = Path.GetDirectoryName(current);
            }
            else
            {
                break;
            }
        }
    }

    private sealed class SyncState
    {
        public List<string> Files { get; set; } = new();
    }
}

public sealed class LocalMinecraftLaunchService
{
    private readonly LauncherPathsService _pathsService;
    private readonly DiagnosticsService _diagnostics;
    private readonly ServerCatalogService _serverCatalog;

    public LocalMinecraftLaunchService(LauncherPathsService pathsService, DiagnosticsService diagnostics, ServerCatalogService serverCatalog)
    {
        _pathsService = pathsService;
        _diagnostics = diagnostics;
        _serverCatalog = serverCatalog;
    }

    public async Task<Process> LaunchAsync(string nickname, LauncherManifest manifest, int maximumRamMb)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("Nickname is empty.", nameof(nickname));

        if (manifest is null)
            throw new ArgumentNullException(nameof(manifest));

        if (string.IsNullOrWhiteSpace(manifest.LauncherProfileId))
            throw new InvalidOperationException("LauncherProfileId is missing in manifest.");

        _pathsService.EnsureBaseDirectories();

        var javaPath = _pathsService.ResolveJavaExecutablePath();
        var versionJsonPath = Path.Combine(
            _pathsService.VersionsDirectory,
            manifest.LauncherProfileId,
            manifest.LauncherProfileId + ".json");

        _diagnostics.Info("Launch", $"Java path: {javaPath}");
        _diagnostics.Info("Launch", $"Version json: {versionJsonPath}");
        _diagnostics.Info("Launch", $"Maximum RAM: {maximumRamMb} MB");
        _diagnostics.Info("Launch", $"Launcher profile id: {manifest.LauncherProfileId}");

        if (!File.Exists(versionJsonPath))
        {
            throw new FileNotFoundException(
                "Не найден version profile для Minecraft/NeoForge. " +
                $"Ожидался файл: {versionJsonPath}. " +
                "Это значит, что pack manifest не синхронизировал профиль версии " +
                "или профиль NeoForge ещё не установлен локально.",
                versionJsonPath);
        }

        var minecraftPath = new MinecraftPath(_pathsService.GameDirectory);
        var launcher = new MinecraftLauncher(minecraftPath);

        var launchOption = new MLaunchOption
        {
            Session = MSession.CreateOfflineSession(nickname.Trim()),
            MaximumRamMb = maximumRamMb,
            JavaPath = javaPath
        };

        // Auto-connect to the selected server so the client joins it on launch.
        await _serverCatalog.EnsureLoadedAsync();
        var selected = _serverCatalog.GetSelectedServer();
        if (selected is not null && !string.IsNullOrWhiteSpace(selected.Host))
        {
            launchOption.ServerIp = selected.Host;
            launchOption.ServerPort = selected.Port > 0 ? selected.Port : 25565;
            _diagnostics.Info("Launch", $"Quick-connect to {selected.Host}:{launchOption.ServerPort} ({selected.Slug}).");
        }

        // GetVersionAsync reads from the local version JSON (which we keep in sync via pack manifest).
        // BuildProcess skips the Install step entirely — no Mojang network calls, no file downloads.
        var version = await launcher.GetVersionAsync(manifest.LauncherProfileId);
        var process = launcher.BuildProcess(version, launchOption);
        process.EnableRaisingEvents = true;
        process.Start();

        _diagnostics.Info("Launch", $"Minecraft process started for {nickname}.");
        return process;
    }
}