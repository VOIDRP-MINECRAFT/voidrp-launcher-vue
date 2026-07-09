using System.Text.Json;
using VoidRpLauncher.CoreHost.Configuration;

namespace VoidRpLauncher.CoreHost.Services;

/// <summary>
/// Multi-server catalogue: fetches the public server list from the backend and
/// persists the player's selected server. Backward compatible — when nothing is
/// selected (or the catalogue is unavailable) the launcher falls back to the
/// single hardcoded <see cref="AppEndpointsOptions.PackManifestUrl"/>.
/// </summary>
public sealed class ServerCatalogService
{
    private readonly AppEndpointsOptions _endpoints;
    private readonly LauncherPathsService _paths;
    private readonly DiagnosticsService _diagnostics;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // The backend (/servers) returns snake_case fields (icon_url, mc_version,
    // manifest_url, is_default, players_online, ...).
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private IReadOnlyList<GameServerDto> _cache = Array.Empty<GameServerDto>();
    private string? _selectedSlug;
    private bool _selectionLoaded;

    public ServerCatalogService(
        AppEndpointsOptions endpoints,
        LauncherPathsService paths,
        DiagnosticsService diagnostics)
    {
        _endpoints = endpoints;
        _paths = paths;
        _diagnostics = diagnostics;
    }

    private string SelectionFilePath => Path.Combine(_paths.StateDirectory, "selected-server.json");

    private void EnsureSelectionLoaded()
    {
        if (_selectionLoaded)
        {
            return;
        }

        _selectionLoaded = true;
        try
        {
            if (File.Exists(SelectionFilePath))
            {
                var json = File.ReadAllText(SelectionFilePath);
                var saved = JsonSerializer.Deserialize<SelectionState>(json, JsonOptions);
                _selectedSlug = saved?.Slug;
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("ServerCatalog", $"Failed to read saved server selection: {ex.Message}");
        }
    }

    /// <summary>Fetches the server catalogue from the backend; returns the cached list on failure.</summary>
    public async Task<IReadOnlyList<GameServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_endpoints.ServerListUrl))
        {
            return _cache;
        }

        try
        {
            var body = await _http.GetStringAsync(_endpoints.ServerListUrl, cancellationToken);
            var list = JsonSerializer.Deserialize<List<GameServerDto>>(body, JsonOptions);
            if (list is not null)
            {
                _cache = list;
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("ServerCatalog", $"Failed to fetch server list: {ex.Message}");
        }

        // Now that the catalogue (and thus the default server / IsDefault flags)
        // is known, make the on-disk paths reflect the active server.
        ApplyActiveServerToPaths();
        return _cache;
    }

    /// <summary>
    /// Pushes the effective server slug into <see cref="LauncherPathsService"/>
    /// so every per-server directory (game, java, versions, sync/runtime state)
    /// resolves under servers/&lt;slug&gt;. Fully slug-driven: any server added
    /// to the backend catalogue is isolated automatically, no code changes.
    /// </summary>
    public void ApplyActiveServerToPaths()
    {
        var server = GetSelectedServer();
        if (server is not null && !string.IsNullOrWhiteSpace(server.Slug))
        {
            _paths.SetActiveServer(server.Slug, server.IsDefault);
            return;
        }

        // Offline / catalogue unavailable: use the saved slug if we have one.
        // IsDefault is unknown here, so legacy migration is deferred until the
        // catalogue loads and this runs again with the real default flag.
        var slug = GetSelectedSlug();
        if (!string.IsNullOrWhiteSpace(slug))
        {
            _paths.SetActiveServer(slug, isDefault: false);
        }
    }

    /// <summary>Loads the catalogue once if it hasn't been fetched yet (best-effort).</summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.Count == 0)
        {
            await GetServersAsync(cancellationToken);
        }
    }

    public string? GetSelectedSlug()
    {
        EnsureSelectionLoaded();
        return _selectedSlug;
    }

    public GameServerDto? GetSelectedServer()
    {
        var slug = GetSelectedSlug();
        if (string.IsNullOrWhiteSpace(slug))
        {
            return _cache.FirstOrDefault(s => s.IsDefault) ?? _cache.FirstOrDefault();
        }

        return _cache.FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    public void SelectServer(string slug)
    {
        EnsureSelectionLoaded();
        _selectedSlug = slug;
        try
        {
            Directory.CreateDirectory(_paths.StateDirectory);
            var json = JsonSerializer.Serialize(new SelectionState { Slug = slug }, JsonOptions);
            File.WriteAllText(SelectionFilePath, json);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("ServerCatalog", $"Failed to persist server selection: {ex.Message}");
        }

        // Switch on-disk paths to the newly selected server immediately.
        ApplyActiveServerToPaths();
    }

    /// <summary>
    /// Resolves the manifest URL for file sync: the selected server's manifest if
    /// available, otherwise the legacy single-server <see cref="AppEndpointsOptions.PackManifestUrl"/>.
    /// </summary>
    public string ResolveManifestUrl()
    {
        var selected = GetSelectedServer();
        if (selected is not null && !string.IsNullOrWhiteSpace(selected.ManifestUrl))
        {
            return selected.ManifestUrl!;
        }

        return _endpoints.PackManifestUrl;
    }

    /// <summary>
    /// Runtime-seed URL for the selected server; falls back to the launcher-global
    /// <see cref="AppEndpointsOptions.RuntimeSeedUrl"/> when the server doesn't set one.
    /// </summary>
    public string ResolveRuntimeSeedUrl()
    {
        var selected = GetSelectedServer();
        if (selected is not null && !string.IsNullOrWhiteSpace(selected.RuntimeSeedUrl))
        {
            return selected.RuntimeSeedUrl!;
        }

        return _endpoints.RuntimeSeedUrl;
    }

    /// <summary>
    /// Per-server runtime-manifest URL override (direct .json link or a base URL);
    /// null when the selected server doesn't define one.
    /// </summary>
    public string? ResolveRuntimeManifestUrlOverride()
    {
        var selected = GetSelectedServer();
        var url = selected?.RuntimeManifestUrl;
        return string.IsNullOrWhiteSpace(url) ? null : url;
    }

    private sealed class SelectionState
    {
        public string? Slug { get; set; }
    }
}

/// <summary>
/// Stamps every outgoing backend request with the currently-selected server's
/// slug (X-Server-Slug) so game data (dashboard, nations, leaderboards, ...) is
/// scoped to it. Global endpoints ignore the header, so it is safe everywhere.
/// </summary>
public sealed class ServerSlugHandler : DelegatingHandler
{
    private readonly Func<string?> _slugProvider;

    public ServerSlugHandler(Func<string?> slugProvider) => _slugProvider = slugProvider;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var slug = _slugProvider();
        if (!string.IsNullOrWhiteSpace(slug) && !request.Headers.Contains("X-Server-Slug"))
        {
            request.Headers.Add("X-Server-Slug", slug);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

public sealed class GameServerDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25565;
    public string McVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public string? ManifestUrl { get; set; }
    public string? RuntimeSeedUrl { get; set; }
    public string? RuntimeManifestUrl { get; set; }
    public int MaxPlayers { get; set; }
    public string WhitelistMode { get; set; } = "public";
    public bool Maintenance { get; set; }
    public bool IsDefault { get; set; }
    public string? MapUrl { get; set; }
    public string? AccentColor { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
    public GameServerStatusDto? Status { get; set; }
}

public sealed class GameServerStatusDto
{
    public bool Online { get; set; }
    public int PlayersOnline { get; set; }
    public int PlayersMax { get; set; }
    public string? Version { get; set; }
}
