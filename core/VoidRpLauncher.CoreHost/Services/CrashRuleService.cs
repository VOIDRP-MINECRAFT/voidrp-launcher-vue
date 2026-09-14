using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VoidRpLauncher.CoreHost.Models;
using VoidRpLauncher.CoreHost.Services.Account;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Crash rules the backend serves for the selected server, merged over the built-in ones.
/// Cached per server on disk so advice still works offline or when the API is down;
/// a failed refresh keeps whatever was loaded before.
/// </summary>
public sealed class CrashRuleService
{
    public const int DefaultRecommendedRamMb = 6144;
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(6);
    private static readonly JsonSerializerOptions CacheJson = new() { PropertyNameCaseInsensitive = true };

    private readonly LauncherAccountApiClient _apiClient;
    private readonly LauncherPathsService _pathsService;
    private readonly ServerCatalogService _serverCatalog;
    private readonly DiagnosticsService _diagnostics;
    private readonly object _lock = new();
    private string? _loadedSlug;
    private CrashRulesPayload _payload = new();

    public CrashRuleService(LauncherAccountApiClient apiClient, LauncherPathsService pathsService, ServerCatalogService serverCatalog, DiagnosticsService diagnostics)
    {
        _apiClient = apiClient;
        _pathsService = pathsService;
        _serverCatalog = serverCatalog;
        _diagnostics = diagnostics;
    }

    public List<CrashRule> EffectiveRules() => CrashAdvisor.EffectiveRules(Current().Rules);

    public int RecommendedRamMb()
    {
        var value = Current().RecommendedRamMb;
        return value is >= 1024 and <= 65536 ? value.Value : DefaultRecommendedRamMb;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(RefreshTimeout);
        var slug = SlugOrDefault();
        try
        {
            var payload = await _apiClient.GetCrashRulesAsync(cts.Token);
            lock (_lock)
            {
                _payload = payload;
                _loadedSlug = slug;
            }
            Directory.CreateDirectory(_pathsService.StateDirectory);
            await File.WriteAllTextAsync(CachePath(slug), JsonSerializer.Serialize(payload, CacheJson), CancellationToken.None);
            _diagnostics.Info("CrashRules", $"Loaded {payload.Rules.Count} server crash rule(s) for '{slug}'.");
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("CrashRules", $"Crash rules refresh failed, using cached/built-in rules: {ex.Message}");
        }
    }

    private CrashRulesPayload Current()
    {
        var slug = SlugOrDefault();
        lock (_lock)
        {
            if (_loadedSlug == slug) return _payload;
            _payload = LoadCache(slug);
            _loadedSlug = slug;
            return _payload;
        }
    }

    private CrashRulesPayload LoadCache(string slug)
    {
        try
        {
            var path = CachePath(slug);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<CrashRulesPayload>(File.ReadAllText(path), CacheJson) ?? new CrashRulesPayload()
                : new CrashRulesPayload();
        }
        catch
        {
            return new CrashRulesPayload();
        }
    }

    private string SlugOrDefault()
    {
        var slug = _serverCatalog.GetSelectedSlug();
        return string.IsNullOrWhiteSpace(slug) ? "default" : slug;
    }

    private string CachePath(string slug)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) slug = slug.Replace(c, '_');
        return Path.Combine(_pathsService.StateDirectory, $"crash-rules-{slug}.json");
    }
}
