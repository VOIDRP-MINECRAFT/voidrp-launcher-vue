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

    private readonly AppEndpointsOptions _endpoints;
    private readonly LauncherPathsService _pathsService;
    private readonly HashService _hashService;
    private readonly DiagnosticsService _diagnostics;
    private readonly ServerCatalogService _serverCatalog;
    private readonly SemaphoreSlim _runtimeLock = new(1, 1);

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
        await _runtimeLock.WaitAsync(cancellationToken);
        try
        {
            _pathsService.EnsureBaseDirectories();
            progress?.Invoke("Проверяем Java runtime...", 0);

            // Best-effort: make sure the server catalogue is loaded so the
            // per-server runtime seed/manifest overrides can be resolved.
            await _serverCatalog.EnsureLoadedAsync(cancellationToken);

            var hasJava = TryResolveExistingRuntime(out var existingJavaPath);
            if (hasJava && !string.IsNullOrWhiteSpace(existingJavaPath))
            {
                _diagnostics.Info("Runtime", $"Using existing runtime: {existingJavaPath}");
            }
            else
            {
                _diagnostics.Info("Runtime", "Local runtime not found. Resolving runtime-seed.");
            }

            progress?.Invoke("Получаем runtime manifest...", 6);

            var manifestUrl = await ResolveManifestUrlAsync(cancellationToken);
            _diagnostics.Info("Runtime", $"Runtime manifest: {manifestUrl}");
            progress?.Invoke("Загружаем runtime manifest...", 12);

            var manifest = await LoadRuntimeManifestAsync(manifestUrl, cancellationToken);
            if (manifest.Files.Count == 0)
            {
                throw new InvalidOperationException("Runtime manifest не содержит файлов для загрузки.");
            }

            // Fast path: Java exists AND manifest fingerprint matches stored stamp → skip file sync.
            if (hasJava && !string.IsNullOrWhiteSpace(existingJavaPath))
            {
                var stampPath = Path.Combine(_pathsService.StateDirectory, "runtime.stamp");
                var fingerprint = ComputeManifestFingerprint(manifest);
                if (File.Exists(stampPath) && File.ReadAllText(stampPath).Trim() == fingerprint)
                {
                    _diagnostics.Info("Runtime", "Runtime stamp matches manifest fingerprint — skipping file sync.");
                    progress?.Invoke("Java runtime готов.", 100);
                    return;
                }
            }

            await SyncRuntimeFilesAsync(manifest, progress, cancellationToken);

            if (!TryResolveExistingRuntime(out var installedJavaPath))
            {
                throw new InvalidOperationException("Java runtime был синхронизирован, но java.exe/javaw.exe не найден.");
            }

            // Write manifest fingerprint so next launch can skip file sync.
            try { File.WriteAllText(Path.Combine(_pathsService.StateDirectory, "runtime.stamp"), ComputeManifestFingerprint(manifest)); }
            catch { }

            _diagnostics.Info("Runtime", $"Runtime prepared: {installedJavaPath}");
            progress?.Invoke("Java runtime готов.", 100);
        }
        finally
        {
            _runtimeLock.Release();
        }
    }

    private static string ComputeManifestFingerprint(RuntimeManifestPayload manifest)
        => $"{manifest.NeoForgeVersion}|{manifest.BuildDateUtc}|{manifest.Files.Count}";

    private bool TryResolveExistingRuntime(out string? javaPath)
    {
        try
        {
            javaPath = _pathsService.ResolveJavaExecutablePath();
            return !string.IsNullOrWhiteSpace(javaPath);
        }
        catch
        {
            javaPath = null;
            return false;
        }
    }

    private async Task<string> ResolveManifestUrlAsync(CancellationToken cancellationToken)
    {
        var fallbackUrl = BuildFallbackManifestUrl();

        // Per-server seed URL when the selected server defines one, else the
        // launcher-global endpoint.
        var seedUrl = _serverCatalog.ResolveRuntimeSeedUrl();

        if (string.IsNullOrWhiteSpace(seedUrl))
        {
            _diagnostics.Warn("Runtime", $"RuntimeSeedUrl is empty. Fallback to {fallbackUrl}");
            return fallbackUrl;
        }

        try
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(AddCacheBuster(seedUrl), cancellationToken);
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
        catch (Exception ex)
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

    private async Task<RuntimeManifestPayload> LoadRuntimeManifestAsync(string manifestUrl, CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var payload = await client.GetFromJsonAsync<RuntimeManifestDto>(AddCacheBuster(manifestUrl), JsonOptions, cancellationToken);
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

    private async Task SyncRuntimeFilesAsync(
        RuntimeManifestPayload manifest,
        Action<string, double>? progress,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        var totalFiles = manifest.Files.Count;

        for (var index = 0; index < totalFiles; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = manifest.Files[index];
            var relativePath = NormalizeRelativePath(entry.Path);

            var baseDirectory = ResolveTargetBaseDirectory(relativePath);
            var localPath = Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var directory = Path.GetDirectoryName(localPath);

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            progress?.Invoke($"Подготавливаем runtime: {relativePath}", CalculatePercent(index, totalFiles, 0));

            if (!NeedsDownload(entry, localPath))
            {
                progress?.Invoke($"Runtime готов: {relativePath}", CalculatePercent(index + 1, totalFiles, 0));
                continue;
            }

            _diagnostics.Info("Runtime", $"Downloading runtime file: {relativePath}");
            await DownloadRuntimeFileAsync(client, entry, localPath, index, totalFiles, progress, cancellationToken);
        }

        _hashService.Flush();
    }

    private string ResolveTargetBaseDirectory(string relativePath)
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
            return _pathsService.GameDirectory;
        }

        // Всё остальное считаем Java runtime
        return _pathsService.JavaDirectory;
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
        HttpClient client,
        LauncherManifestFile entry,
        string localPath,
        int index,
        int totalFiles,
        Action<string, double>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = localPath + ".download";
        var tempDirectory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrWhiteSpace(tempDirectory))
        {
            Directory.CreateDirectory(tempDirectory);
        }

        if (File.Exists(tempPath))
        {
            TryDeleteFileWithRetries(tempPath);
        }

        try
        {
            using var response = await client.GetAsync(entry.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? entry.Size;

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
                long downloadedBytes = 0;

                while (true)
                {
                    var read = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read <= 0)
                    {
                        break;
                    }

                    await targetStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedBytes += read;

                    var fileRatio = totalBytes > 0 ? (double)downloadedBytes / totalBytes : 0;
                    progress?.Invoke(
                        $"Скачиваем runtime: {entry.Path} ({FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)})",
                        CalculatePercent(index, totalFiles, fileRatio));
                }

                await targetStream.FlushAsync(cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha256))
            {
                var downloadedHash = _hashService.ComputeSha256(tempPath);
                if (!downloadedHash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Hash mismatch for runtime file {entry.Path}. Expected {entry.Sha256}, got {downloadedHash}.");
                }
            }

            if (File.Exists(localPath))
            {
                TryDeleteFileWithRetries(localPath);
            }

            File.Move(tempPath, localPath);
            progress?.Invoke($"Runtime готов: {entry.Path}", CalculatePercent(index + 1, totalFiles, 0));
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

    private static double CalculatePercent(int fileIndex, int totalFiles, double fileRatio)
    {
        if (totalFiles <= 0)
        {
            return 100;
        }

        fileRatio = Math.Max(0, Math.Min(1, fileRatio));
        return Math.Max(0, Math.Min(100, (((double)fileIndex / totalFiles) + (fileRatio / totalFiles)) * 100.0));
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