using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VoidRpLauncher.CoreHost.Configuration;
using VoidRpLauncher.CoreHost.Models;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class RuntimeBootstrapService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    // The runtime is thousands of small files (assets above all); one at a time the round trips
    // alone took minutes. Same bound as the pack sync in FileSyncService.
    private const int DownloadConcurrency = 8;
    private const int DownloadAttempts = 3;

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        MaxConnectionsPerServer = DownloadConcurrency,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = DecompressionMethods.All
    })
    {
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
        Timeout = TimeSpan.FromMinutes(10)
    };

    private readonly AppEndpointsOptions _endpoints;
    private readonly LauncherPathsService _pathsService;
    private readonly HashService _hashService;
    private readonly DiagnosticsService _diagnostics;
    private readonly ServerCatalogService _serverCatalog;

    // One run at a time. The background prefetch and Play share it: whoever asks for the same
    // server while a run is going joins that run instead of starting a second one, and the
    // latest caller's progress callback is the one that gets the updates.
    private readonly object _gate = new();
    private RuntimeRun? _current;
    private volatile Action<string, double>? _listener;

    public RuntimeBootstrapService(
        AppEndpointsOptions endpoints,
        LauncherPathsService pathsService,
        HashService hashService,
        DiagnosticsService diagnostics,
        ServerCatalogService serverCatalog)
    {
        _endpoints = endpoints;
        _pathsService = pathsService;
        _hashService = hashService;
        _diagnostics = diagnostics;
        _serverCatalog = serverCatalog;
    }

    public async Task EnsureRuntimeAsync(Action<string, double>? progress = null, CancellationToken cancellationToken = default)
    {
        // Resolve the active server first so all per-server directories
        // (game, java, runtime state) point at servers/<slug>/. Loads the
        // catalogue so per-server runtime seed/manifest overrides resolve too.
        await _serverCatalog.EnsureLoadedAsync(cancellationToken);

        Task task;
        lock (_gate)
        {
            _serverCatalog.ApplyActiveServerToPaths();
            // Paths follow the selected server and change the moment the player picks another
            // one; pin this server's folders now so a running download never lands elsewhere.
            var plan = CapturePlan();
            if (progress is not null)
            {
                _listener = progress;
            }

            if (_current is { } running && !running.Task.IsCompleted && running.Key == plan.Key)
            {
                task = running.Task;
            }
            else
            {
                // A different server (or game folder) now: the old run is no longer wanted.
                var previous = _current;
                previous?.Cts.Cancel();
                var cts = new CancellationTokenSource();
                task = Task.Run(() => RunAsync(plan, previous?.Task, cts.Token));
                _current = new RuntimeRun(plan.Key, task, cts);
            }
        }

        await task.WaitAsync(cancellationToken);
    }

    /// <summary>Stops a running download and waits for it to let go of the files
    /// (before a repair or a move of the game folder).</summary>
    public async Task CancelAsync()
    {
        RuntimeRun? running;
        lock (_gate)
        {
            running = _current;
        }

        if (running is null || running.Task.IsCompleted)
        {
            return;
        }

        running.Cts.Cancel();
        try
        {
            await running.Task;
        }
        catch
        {
            // cancelled or failed — either way it is done with the files
        }
    }

    private RuntimePlan CapturePlan()
    {
        var gameDirectory = _pathsService.GameDirectory;
        return new RuntimePlan(
            gameDirectory,
            gameDirectory,
            _pathsService.JavaDirectory,
            _pathsService.ServerStateDirectory,
            // Per-server seed URL when the selected server defines one, else the
            // launcher-global endpoint.
            _serverCatalog.ResolveRuntimeSeedUrl(),
            BuildFallbackManifestUrl());
    }

    private void Report(string details, double percent) => _listener?.Invoke(details, percent);

    private async Task RunAsync(RuntimePlan plan, Task? previous, CancellationToken cancellationToken)
    {
        if (previous is not null)
        {
            // Let the cancelled run finish its current files first; two runs never write at once.
            try { await previous; } catch { }
        }

        cancellationToken.ThrowIfCancellationRequested();
        _pathsService.EnsureBaseDirectories();
        Report("Проверяем игровые файлы...", 0);

        var hasJava = TryResolveExistingRuntime(plan, out var existingJavaPath);
        if (hasJava && !string.IsNullOrWhiteSpace(existingJavaPath))
        {
            _diagnostics.Info("Runtime", $"Using existing runtime: {existingJavaPath}");
        }
        else
        {
            _diagnostics.Info("Runtime", "Local runtime not found. Resolving runtime-seed.");
        }

        Report("Получаем список файлов...", 1);

        var manifestUrl = await ResolveManifestUrlAsync(plan, cancellationToken);
        _diagnostics.Info("Runtime", $"Runtime manifest: {manifestUrl}");

        var manifest = await LoadRuntimeManifestAsync(manifestUrl, cancellationToken);
        if (manifest.Files.Count == 0)
        {
            throw new InvalidOperationException("Runtime manifest не содержит файлов для загрузки.");
        }

        var stampPath = Path.Combine(plan.StateDirectory, "runtime.stamp");

        // Fast path: Java exists AND manifest fingerprint matches stored stamp → skip file sync.
        if (hasJava && !string.IsNullOrWhiteSpace(existingJavaPath))
        {
            var fingerprint = ComputeManifestFingerprint(manifest);
            // The stamp only says the runtime was synced once; files may have gone since
            // (deleted by hand, an antivirus, a cleanup). A missing asset leaves the game
            // on a black screen, so check every file is still there before trusting it.
            if (File.Exists(stampPath) && File.ReadAllText(stampPath).Trim() == fingerprint
                && AllRuntimeFilesPresent(plan, manifest))
            {
                _diagnostics.Info("Runtime", "Runtime stamp matches manifest fingerprint — skipping file sync.");
                Report("Игровые файлы готовы.", 100);
                return;
            }
        }

        await SyncRuntimeFilesAsync(plan, manifest, cancellationToken);

        if (!TryResolveExistingRuntime(plan, out var installedJavaPath))
        {
            throw new InvalidOperationException("Java runtime был синхронизирован, но java.exe/javaw.exe не найден.");
        }

        // Write manifest fingerprint so next launch can skip file sync.
        try
        {
            Directory.CreateDirectory(plan.StateDirectory);
            File.WriteAllText(stampPath, ComputeManifestFingerprint(manifest));
        }
        catch { }

        _diagnostics.Info("Runtime", $"Runtime prepared: {installedJavaPath}");
        Report("Игровые файлы готовы.", 100);
    }

    private bool AllRuntimeFilesPresent(RuntimePlan plan, RuntimeManifestPayload manifest)
    {
        foreach (var entry in manifest.Files)
        {
            var relativePath = NormalizeRelativePath(entry.Path);
            if (!File.Exists(ResolveLocalPath(plan, relativePath)))
            {
                _diagnostics.Warn("Runtime", $"Runtime file missing, re-syncing: {relativePath}");
                return false;
            }
        }
        return true;
    }

    private static string ComputeManifestFingerprint(RuntimeManifestPayload manifest)
        => $"{manifest.NeoForgeVersion}|{manifest.BuildDateUtc}|{manifest.Files.Count}";

    private bool TryResolveExistingRuntime(RuntimePlan plan, out string? javaPath)
    {
        try
        {
            javaPath = _pathsService.ResolveJavaExecutablePath(plan.JavaDirectory);
            return !string.IsNullOrWhiteSpace(javaPath);
        }
        catch
        {
            javaPath = null;
            return false;
        }
    }

    private async Task<string> ResolveManifestUrlAsync(RuntimePlan plan, CancellationToken cancellationToken)
    {
        var fallbackUrl = plan.FallbackManifestUrl;
        var seedUrl = plan.SeedUrl;

        if (string.IsNullOrWhiteSpace(seedUrl))
        {
            _diagnostics.Warn("Runtime", $"RuntimeSeedUrl is empty. Fallback to {fallbackUrl}");
            return fallbackUrl;
        }

        try
        {
            using var response = await Http.GetAsync(AddCacheBuster(seedUrl), cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var resolvedUrl = TryResolveManifestUrlFromSeed(document.RootElement);
            if (!string.IsNullOrWhiteSpace(resolvedUrl))
            {
                return resolvedUrl;
            }

            _diagnostics.Warn("Runtime", $"runtime-seed answered but manifest url was not found. Fallback to {fallbackUrl}");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _diagnostics.Warn("Runtime", $"runtime-seed request failed: {ex.Message}. Fallback to {fallbackUrl}");
        }

        return fallbackUrl;
    }

    private string BuildFallbackManifestUrl()
    {
        var fileName = _pathsService.Platform.RuntimeManifestFileName;

        // Per-server override: a direct .json link is used as-is, anything else
        // is treated as a base URL and gets the platform manifest file appended.
        var overrideUrl = _serverCatalog.ResolveRuntimeManifestUrlOverride();
        if (!string.IsNullOrWhiteSpace(overrideUrl))
        {
            var trimmed = overrideUrl.TrimEnd('/');
            return trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : $"{trimmed}/{fileName}";
        }

        var baseUrl = _endpoints.RuntimeManifestBaseUrl?.TrimEnd('/') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("RuntimeManifestBaseUrl is not configured.");
        }

        return $"{baseUrl}/{fileName}";
    }

    private string? TryResolveManifestUrlFromSeed(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.String)
        {
            var direct = root.GetString();
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryGetString(root, "manifestUrl", out var manifestUrl))
        {
            return manifestUrl;
        }

        if (TryGetString(root, "runtimeManifestUrl", out var runtimeManifestUrl))
        {
            return runtimeManifestUrl;
        }

        if (TryGetString(root, "url", out var url))
        {
            return url;
        }

        if (root.TryGetProperty("artifacts", out var artifactsElement) &&
            artifactsElement.ValueKind == JsonValueKind.Object)
        {
            var rid = _pathsService.Platform.Rid;

            if (artifactsElement.TryGetProperty(rid, out var platformElement) &&
                platformElement.ValueKind == JsonValueKind.Object)
            {
                if (TryGetString(platformElement, "manifestUrl", out var platformManifestUrl))
                {
                    return platformManifestUrl;
                }

                if (TryGetString(platformElement, "url", out var platformUrl))
                {
                    return platformUrl;
                }
            }
        }

        return null;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static async Task<RuntimeManifestPayload> LoadRuntimeManifestAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        var payload = await Http.GetFromJsonAsync<RuntimeManifestDto>(AddCacheBuster(manifestUrl), JsonOptions, cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Runtime manifest is empty or could not be parsed.");
        }

        var files = payload.Files?
            .Where(static x => !string.IsNullOrWhiteSpace(x.Path) && !string.IsNullOrWhiteSpace(x.Url))
            .Select(static x => new LauncherManifestFile
            {
                Path = x.Path ?? string.Empty,
                Url = x.Url ?? string.Empty,
                Size = x.Size,
                Sha256 = x.Sha256 ?? string.Empty,
                AlwaysOverwrite = x.AlwaysOverwrite
            })
            .ToList() ?? new List<LauncherManifestFile>();

        return new RuntimeManifestPayload(
            manifestUrl,
            payload.NeoForgeVersion ?? string.Empty,
            payload.BuildDateUtc ?? string.Empty,
            files);
    }

    private async Task SyncRuntimeFilesAsync(RuntimePlan plan, RuntimeManifestPayload manifest, CancellationToken cancellationToken)
    {
        var totalFiles = manifest.Files.Count;
        var totalBytes = manifest.Files.Sum(static f => Math.Max(0, f.Size));
        long doneBytes = 0;
        var doneFiles = 0;
        var downloaded = 0;

        // Percent by bytes: a few big jars and thousands of tiny sounds make a file count jumpy.
        void ReportBytes(string verb)
        {
            var bytes = Interlocked.Read(ref doneBytes);
            var percent = totalBytes > 0 ? (double)bytes / totalBytes * 100.0 : (double)Volatile.Read(ref doneFiles) / totalFiles * 100.0;
            Report($"{verb}: {FormatBytes(bytes)} из {FormatBytes(totalBytes)} · файлов {Volatile.Read(ref doneFiles)} из {totalFiles}", percent);
        }

        await Parallel.ForEachAsync(manifest.Files, new ParallelOptions
        {
            MaxDegreeOfParallelism = DownloadConcurrency,
            CancellationToken = cancellationToken
        }, async (entry, ct) =>
        {
            var relativePath = NormalizeRelativePath(entry.Path);
            var localPath = ResolveLocalPath(plan, relativePath);
            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (NeedsDownload(entry, localPath))
            {
                _diagnostics.Info("Runtime", $"Downloading runtime file: {relativePath}");
                Interlocked.Increment(ref downloaded);
                await DownloadRuntimeFileAsync(entry, localPath, read =>
                {
                    Interlocked.Add(ref doneBytes, read);
                    ReportBytes("Скачиваем игровые файлы");
                }, ct);
            }
            else
            {
                Interlocked.Add(ref doneBytes, Math.Max(0, entry.Size));
            }

            Interlocked.Increment(ref doneFiles);
            ReportBytes(Volatile.Read(ref downloaded) > 0 ? "Скачиваем игровые файлы" : "Проверяем игровые файлы");
        });

        _hashService.Flush();
    }

    private static string ResolveLocalPath(RuntimePlan plan, string relativePath)
        => Path.Combine(ResolveTargetBaseDirectory(plan, relativePath), relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string ResolveTargetBaseDirectory(RuntimePlan plan, string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);

        // Всё игровое должно лежать в корне game, а не в game\java
        if (normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("libraries/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("versions/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("mods/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("defaultconfigs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("resourcepacks/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("shaderpacks/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("kubejs/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
        {
            return plan.GameDirectory;
        }

        // Всё остальное считаем Java runtime
        return plan.JavaDirectory;
    }

    private bool NeedsDownload(LauncherManifestFile entry, string localPath)
    {
        if (!File.Exists(localPath))
        {
            return true;
        }

        if (entry.Size > 0)
        {
            var fileInfo = new FileInfo(localPath);
            if (fileInfo.Length != entry.Size)
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(entry.Sha256))
        {
            return false;
        }

        return !_hashService.ComputeSha256(localPath).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadRuntimeFileAsync(
        LauncherManifestFile entry,
        string localPath,
        Action<long> onBytes,
        CancellationToken cancellationToken)
    {
        // A dropped connection on one of thousands of files should not fail the whole run.
        for (var attempt = 1; ; attempt++)
        {
            long attemptBytes = 0;
            try
            {
                await DownloadRuntimeFileOnceAsync(entry, localPath, read =>
                {
                    attemptBytes += read;
                    onBytes(read);
                }, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < DownloadAttempts && !cancellationToken.IsCancellationRequested
                                       && ex is HttpRequestException or IOException or TaskCanceledException)
            {
                // Take back what the failed attempt counted, the retry reports it again.
                onBytes(-attemptBytes);
                _diagnostics.Warn("Runtime", $"Retry {attempt}/{DownloadAttempts - 1} for {entry.Path}: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }

    private async Task DownloadRuntimeFileOnceAsync(
        LauncherManifestFile entry,
        string localPath,
        Action<long> onBytes,
        CancellationToken cancellationToken)
    {
        var tempPath = localPath + ".download";
        if (File.Exists(tempPath))
        {
            TryDeleteFileWithRetries(tempPath);
        }

        try
        {
            using var response = await Http.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var targetStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read,
                1024 * 64,
                useAsync: true))
            {
                var buffer = new byte[1024 * 64];

                while (true)
                {
                    var read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    onBytes(read);
                }

                await targetStream.FlushAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha256))
            {
                var downloadedHash = _hashService.ComputeSha256(tempPath);
                if (!downloadedHash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"Hash mismatch for runtime file {entry.Path}. Expected {entry.Sha256}, got {downloadedHash}.");
                }
            }

            if (File.Exists(localPath))
            {
                TryDeleteFileWithRetries(localPath);
            }

            File.Move(tempPath, localPath);
            _hashService.Moved(tempPath, localPath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    TryDeleteFileWithRetries(tempPath);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static void TryDeleteFileWithRetries(string path)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= 8; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                File.Delete(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(100 * attempt);
            }
        }

        throw new IOException($"Unable to delete file: {path}", lastError);
    }

    private static string AddCacheBuster(string url)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim('/');

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

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

    // Key = the game folder: it differs per server and changes with a moved game location.
    private sealed record RuntimePlan(
        string Key,
        string GameDirectory,
        string JavaDirectory,
        string StateDirectory,
        string? SeedUrl,
        string FallbackManifestUrl);

    private sealed record RuntimeRun(string Key, Task Task, CancellationTokenSource Cts);

    private sealed record RuntimeManifestPayload(string ManifestUrl, string NeoForgeVersion, string BuildDateUtc, List<LauncherManifestFile> Files);

    private sealed class RuntimeManifestDto
    {
        public string? NeoForgeVersion { get; set; }
        public string? BuildDateUtc { get; set; }
        public List<RuntimeManifestFileDto>? Files { get; set; }
    }

    private sealed class RuntimeManifestFileDto
    {
        public string? Path { get; set; }
        public long Size { get; set; }
        public string? Sha256 { get; set; }
        public string? Url { get; set; }
        public bool AlwaysOverwrite { get; set; }
    }
}
