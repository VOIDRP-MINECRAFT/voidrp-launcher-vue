using System.IO;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VoidRpLauncher.CoreHost.Models;
using VoidRpLauncher.CoreHost.Models.Account;
using VoidRpLauncher.CoreHost.Security;
using VoidRpLauncher.CoreHost.Services;

namespace VoidRpLauncher.CoreHost.Services.Account;

/// <summary>Брошен когда сервер вернул 401/403 — токен недействителен или истёк.</summary>
public sealed class LauncherAuthException(string message) : Exception(message);

public sealed class LauncherAccountApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    public LauncherAccountApiClient(HttpClient httpClient, string apiBaseUrl)
    {
        _httpClient = httpClient;
        if (_httpClient.BaseAddress is null) _httpClient.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    }

    public Task<TokenPairResponseDto> LoginAsync(string login, string password, string deviceName, CancellationToken cancellationToken = default)
        => PostJsonAsync<TokenPairResponseDto>("auth/login", new LoginRequestDto { Login = login, Password = password, DeviceName = deviceName }, cancellationToken);

    public Task<TokenPairResponseDto> RefreshAsync(string refreshToken, string deviceName, CancellationToken cancellationToken = default)
        => PostJsonAsync<TokenPairResponseDto>("auth/refresh", new RefreshRequestDto { RefreshToken = refreshToken, DeviceName = deviceName }, cancellationToken);

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("auth/logout", new LogoutRequestDto { RefreshToken = refreshToken }, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NoContent)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Logout failed: {text}");
        }
    }

    public async Task<RevokeSessionsResponseDto> RevokeOtherSessionsAsync(string accessToken, string refreshToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "account/revoke-other-sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new RevokeOtherSessionsRequestDto { RefreshToken = refreshToken }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<RevokeSessionsResponseDto>(response, cancellationToken);
    }

    public async Task<MeResponseDto> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<MeResponseDto>(response, cancellationToken);
    }

    public async Task<IssuePlayTicketResponseDto> RequestPlayTicketAsync(string accessToken, string launcherVersion, string launcherPlatform, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "launcher/play-ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new IssuePlayTicketRequestDto { LauncherVersion = launcherVersion, LauncherPlatform = launcherPlatform }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<IssuePlayTicketResponseDto>(response, cancellationToken);
    }

    public async Task<LauncherDashboardResponseDto> GetLauncherDashboardAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "launcher/me/dashboard");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<LauncherDashboardResponseDto>(response, cancellationToken);
    }

    public async Task<PlayerSkinReadDto> GetSkinAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "account/skin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<PlayerSkinReadDto>(response, cancellationToken);
    }

    public async Task<PlayerSkinOperationResponseDto> UploadSkinAsync(
        string accessToken,
        IFormFile file,
        string modelVariant,
        CancellationToken cancellationToken = default)
    {
        if (file is null) throw new InvalidOperationException("Skin file was not provided.");

        using var multipart = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var content = new StreamContent(stream);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType);
        multipart.Add(content, "file", file.FileName);
        multipart.Add(new StringContent(string.IsNullOrWhiteSpace(modelVariant) ? "classic" : modelVariant), "model_variant");

        using var request = new HttpRequestMessage(HttpMethod.Post, "account/skin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = multipart;
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<PlayerSkinOperationResponseDto>(response, cancellationToken);
    }

    public async Task<PlayerSkinOperationResponseDto> DeleteSkinAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, "account/skin");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<PlayerSkinOperationResponseDto>(response, cancellationToken);
    }

    public async Task<LauncherPreferencesDto> GetPreferencesAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "launcher/me/prefs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<LauncherPreferencesDto>(response, cancellationToken);
    }

    public async Task<SimpleResponseDto> PostModSuggestionAsync(string accessToken, string url, string? comment, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "mod-suggestions/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { url, comment }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<SimpleResponseDto>(response, cancellationToken);
    }

    public async Task<SimpleResponseDto> PostPlayerFeedbackAsync(string accessToken, string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "player-feedback/");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { type, title, body }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<SimpleResponseDto>(response, cancellationToken);
    }

    public async Task SaveModPrefsAsync(string accessToken, List<string> disabledMods, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "launcher/me/prefs/mods");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { disabled_mods = disabledMods }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"SaveModPrefs failed: {(int)response.StatusCode}" : detail);
        }
    }

    public async Task<LauncherConfigFileDto> GetConfigFileAsync(string accessToken, string path, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"launcher/me/prefs/config?path={Uri.EscapeDataString(path)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadJsonAsync<LauncherConfigFileDto>(response, cancellationToken);
    }

    public async Task SaveConfigFileAsync(string accessToken, string path, string contentB64, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "launcher/me/prefs/config");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { path, content_b64 = contentB64 }, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail) ? $"SaveConfigFile failed: {(int)response.StatusCode}" : detail);
        }
    }

    public async Task ReportCrashAsync(string accessToken, int exitCode, string? crashReport, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "launcher/me/crash-report");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(new { exit_code = exitCode, crash_report = crashReport }, options: JsonOptions);
        try { using var response = await _httpClient.SendAsync(request, cancellationToken); }
        catch { }
    }

    private async Task<T> PostJsonAsync<T>(string url, object payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);
        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var userMessage = ExtractUserMessage(body)
                ?? (string.IsNullOrWhiteSpace(body) ? null : body)
                ?? $"Ошибка сервера ({(int)response.StatusCode})";

            var isAuthError = response.StatusCode is
                System.Net.HttpStatusCode.Unauthorized or
                System.Net.HttpStatusCode.Forbidden;

            if (isAuthError)
                throw new LauncherAuthException(userMessage);
            throw new InvalidOperationException(userMessage);
        }
        var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException("Пустой ответ сервера.");
    }

    private static string? ExtractUserMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            foreach (var key in new[] { "detail", "message", "Message", "error", "title" })
            {
                if (root.TryGetProperty(key, out var el) &&
                    el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var val = el.GetString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
        }
        catch { }
        return null;
    }
}

public sealed class LauncherTokenStore
{
    private readonly string _directoryPath;
    private readonly string _filePath;
    public LauncherTokenStore(string stateDirectoryPath) { _directoryPath = stateDirectoryPath; _filePath = Path.Combine(_directoryPath, "launcher-auth.json"); }

    public async Task SaveAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directoryPath);
        var json = JsonSerializer.Serialize(new LauncherTokenEnvelope { RefreshToken = refreshToken });
        var bytes = Encoding.UTF8.GetBytes(json);
        var protectedBytes = Protect(bytes);
        await File.WriteAllBytesAsync(_filePath, protectedBytes, cancellationToken);
        TryRestrictFilePermissions(_filePath);
    }

    public async Task<string?> LoadRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return null;
        var raw = await File.ReadAllBytesAsync(_filePath, cancellationToken);
        var json = Encoding.UTF8.GetString(Unprotect(raw));
        var envelope = JsonSerializer.Deserialize<LauncherTokenEnvelope>(json);
        return string.IsNullOrWhiteSpace(envelope?.RefreshToken) ? null : envelope.RefreshToken;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        return Task.CompletedTask;
    }

    private static byte[] Protect(byte[] input) => OperatingSystem.IsWindows() ? ProtectedData.Protect(input, null, DataProtectionScope.CurrentUser) : input;
    private static byte[] Unprotect(byte[] input) => OperatingSystem.IsWindows() ? ProtectedData.Unprotect(input, null, DataProtectionScope.CurrentUser) : input;

    private static void TryRestrictFilePermissions(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
        }
    }
}

public sealed class LauncherPlayTicketStore
{
    private readonly string _directoryPath;
    private readonly string _filePath;
    public LauncherPlayTicketStore(string stateDirectoryPath) { _directoryPath = stateDirectoryPath; _filePath = Path.Combine(_directoryPath, "play-ticket.json"); }
    public string FilePath => _filePath;

    public async Task SaveAsync(LauncherPlayTicketEnvelope envelope, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directoryPath);
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        TryRestrictFilePermissions(_filePath);
    }

    public async Task<LauncherPlayTicketEnvelope?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath)) return null;
        return JsonSerializer.Deserialize<LauncherPlayTicketEnvelope>(await File.ReadAllTextAsync(_filePath, cancellationToken));
    }

    public void ClearIfExpired()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var envelope = JsonSerializer.Deserialize<LauncherPlayTicketEnvelope>(File.ReadAllText(_filePath));
            if (envelope is null || envelope.ExpiresAtUtc <= DateTimeOffset.UtcNow) File.Delete(_filePath);
        }
        catch
        {
            try { File.Delete(_filePath); } catch { }
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath)) File.Delete(_filePath);
        return Task.CompletedTask;
    }

    private static void TryRestrictFilePermissions(string filePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
        }
    }
}


public sealed class LauncherAuthSessionService
{
    private readonly LauncherAccountApiClient _apiClient;
    private readonly LauncherTokenStore _tokenStore;
    private readonly DiagnosticsService _diagnostics;
    private readonly string _deviceName;
    private LauncherAuthSnapshot? _snapshot;

    public LauncherAuthSessionService(LauncherAccountApiClient apiClient, LauncherTokenStore tokenStore, DiagnosticsService diagnostics, string deviceName = "VoidRP Launcher")
    {
        _apiClient = apiClient;
        _tokenStore = tokenStore;
        _diagnostics = diagnostics;
        _deviceName = deviceName;
    }

    public LauncherAuthSnapshot? Snapshot => _snapshot;
    public bool IsAuthenticated => _snapshot?.IsAuthenticated == true;

    public async Task<LauncherAuthSnapshot?> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await _tokenStore.LoadRefreshTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _snapshot = null;
            _diagnostics.Info("Auth", "No saved refresh token found.");
            return null;
        }
        try
        {
            var refreshed = await _apiClient.RefreshAsync(refreshToken, _deviceName, cancellationToken);
            _snapshot = ToSnapshot(refreshed);
            await _tokenStore.SaveAsync(_snapshot.RefreshToken, cancellationToken);
            _diagnostics.Info("Auth", $"Session restored for {_snapshot.User.SiteLogin}.");
            return _snapshot;
        }
        catch (LauncherAuthException ex)
        {
            // Токен недействителен или отозван — удаляем, пользователь должен войти заново
            _snapshot = null;
            await _tokenStore.ClearAsync(cancellationToken);
            _diagnostics.Warn("Auth", $"Session token invalid/expired, cleared. {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            // Сетевая или временная ошибка — токен оставляем, попробуем восстановить при следующем запуске
            _snapshot = null;
            _diagnostics.Warn("Auth", $"Session restore failed (transient, token kept): {ex.Message}");
            return null;
        }
    }

    public async Task<LauncherAuthSnapshot> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var payload = await _apiClient.LoginAsync(login, password, _deviceName, cancellationToken);
        _snapshot = ToSnapshot(payload);
        await _tokenStore.SaveAsync(_snapshot.RefreshToken, cancellationToken);
        _diagnostics.Info("Auth", $"Login succeeded for {_snapshot.User.SiteLogin}.");
        return _snapshot;
    }

    public async Task<LauncherAuthSnapshot> ReloadMeAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        var me = await _apiClient.GetMeAsync(_snapshot.AccessToken, cancellationToken);
        _snapshot.User = me.User;
        _snapshot.PlayerAccount = me.PlayerAccount;
        _snapshot.Security = me.Security ?? new AccountSecurityReadDto();
        _diagnostics.Info("Auth", $"Profile reloaded for {_snapshot.User.SiteLogin}.");
        return _snapshot;
    }

    public async Task<IssuePlayTicketResponseDto> RequestPlayTicketAsync(string launcherVersion, string launcherPlatform, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.RequestPlayTicketAsync(_snapshot.AccessToken, launcherVersion, launcherPlatform, cancellationToken);
    }

    public async Task<LauncherDashboardResponseDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.GetLauncherDashboardAsync(_snapshot.AccessToken, cancellationToken);
    }

    public async Task<PlayerSkinReadDto> GetSkinAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.GetSkinAsync(_snapshot.AccessToken, cancellationToken);
    }

    public async Task<PlayerSkinOperationResponseDto> UploadSkinAsync(IFormFile file, string modelVariant, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.UploadSkinAsync(_snapshot.AccessToken, file, modelVariant, cancellationToken);
    }

    public async Task<PlayerSkinOperationResponseDto> DeleteSkinAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.DeleteSkinAsync(_snapshot.AccessToken, cancellationToken);
    }

    public async Task<RevokeSessionsResponseDto> RevokeOtherSessionsAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken) || string.IsNullOrWhiteSpace(_snapshot.RefreshToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.RevokeOtherSessionsAsync(_snapshot.AccessToken, _snapshot.RefreshToken, cancellationToken);
    }

    public async Task<SimpleResponseDto> SuggestModAsync(string url, string? comment, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.PostModSuggestionAsync(_snapshot.AccessToken, url, comment, cancellationToken);
    }

    public async Task<SimpleResponseDto> SubmitFeedbackAsync(string type, string title, string? body, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.PostPlayerFeedbackAsync(_snapshot.AccessToken, type, title, body, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = _snapshot?.RefreshToken ?? await _tokenStore.LoadRefreshTokenAsync(cancellationToken);
        _snapshot = null;
        await _tokenStore.ClearAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try { await _apiClient.LogoutAsync(refreshToken, cancellationToken); } catch { }
        }
        _diagnostics.Info("Auth", "Local session cleared.");
    }

    public string RequireMinecraftNickname()
    {
        if (!IsAuthenticated || string.IsNullOrWhiteSpace(_snapshot?.PlayerAccount?.MinecraftNickname)) throw new InvalidOperationException("No authenticated launcher account");
        return _snapshot.PlayerAccount.MinecraftNickname;
    }

    public async Task<LauncherPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.GetPreferencesAsync(_snapshot.AccessToken, cancellationToken);
    }

    public async Task SaveModPrefsAsync(List<string> disabledMods, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        await _apiClient.SaveModPrefsAsync(_snapshot.AccessToken, disabledMods, cancellationToken);
    }

    public async Task<LauncherConfigFileDto> GetConfigFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        return await _apiClient.GetConfigFileAsync(_snapshot.AccessToken, path, cancellationToken);
    }

    public async Task SaveConfigFileAsync(string path, string contentB64, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) throw new InvalidOperationException("Launcher user is not authenticated");
        await _apiClient.SaveConfigFileAsync(_snapshot.AccessToken, path, contentB64, cancellationToken);
    }

    public async Task ReportCrashAsync(int exitCode, string? crashReport, CancellationToken cancellationToken = default)
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_snapshot.AccessToken)) return;
        try { await _apiClient.ReportCrashAsync(_snapshot.AccessToken, exitCode, crashReport, cancellationToken); }
        catch { }
    }

    private static LauncherAuthSnapshot ToSnapshot(TokenPairResponseDto payload)
        => new() { AccessToken = payload.AccessToken, RefreshToken = payload.RefreshToken, User = payload.User, PlayerAccount = payload.PlayerAccount, Security = new AccountSecurityReadDto() };
}

public sealed class AuthenticatedLaunchService
{
    private readonly LauncherAuthSessionService _authSessionService;
    private readonly LocalMinecraftLaunchService _localMinecraftLaunchService;
    private readonly LauncherPlayTicketStore _playTicketStore;
    private readonly AppVersionService _appVersionService;
    private readonly LauncherPathsService _launcherPathsService;
    private readonly DiagnosticsService _diagnostics;

    public AuthenticatedLaunchService(LauncherAuthSessionService authSessionService, LocalMinecraftLaunchService localMinecraftLaunchService, LauncherPlayTicketStore playTicketStore, AppVersionService appVersionService, LauncherPathsService launcherPathsService, DiagnosticsService diagnostics)
    {
        _authSessionService = authSessionService;
        _localMinecraftLaunchService = localMinecraftLaunchService;
        _playTicketStore = playTicketStore;
        _appVersionService = appVersionService;
        _launcherPathsService = launcherPathsService;
        _diagnostics = diagnostics;
    }

    public async Task<System.Diagnostics.Process> LaunchAsync(LauncherManifest manifest, int maximumRamMb, IProgress<LaunchProgressInfo>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nickname = _authSessionService.RequireMinecraftNickname();
        _playTicketStore.ClearIfExpired();
        progress?.Report(new LaunchProgressInfo { Stage = "Подготавливаем вход", Details = "Запрашиваем игровой ticket для входа через официальный лаунчер...", Percent = 5 });
        _diagnostics.Info("Launch", $"Requesting play ticket for {nickname}.");
        var playTicket = await _authSessionService.RequestPlayTicketAsync(_appVersionService.CurrentVersion, _launcherPathsService.Platform.DisplayName, cancellationToken);
        // Proof is computed locally using the embedded key — never comes from the API.
        // This ensures only the official launcher binary can produce a valid proof.
        var launcherProof = LauncherProof.Compute(playTicket.Ticket);
        await _playTicketStore.SaveAsync(new LauncherPlayTicketEnvelope { Ticket = playTicket.Ticket, MinecraftNickname = playTicket.MinecraftNickname, ExpiresAtUtc = playTicket.ExpiresAt, Source = "VoidRP Launcher", LauncherProof = launcherProof }, cancellationToken);
        progress?.Report(new LaunchProgressInfo { Stage = "Авторизация", Details = $"Используем аккаунт {nickname}. Ticket сохранён во временный state-файл.", Percent = 15 });
        var process = await _localMinecraftLaunchService.LaunchAsync(nickname, manifest, maximumRamMb);
        progress?.Report(new LaunchProgressInfo { Stage = "Запуск", Details = "Minecraft запущен.", Percent = 100 });
        _diagnostics.Info("Launch", $"Minecraft launched for {nickname} with {maximumRamMb} MB RAM.");
        return process;
    }
}

