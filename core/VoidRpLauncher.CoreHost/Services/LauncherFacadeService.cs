using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Http;
using VoidRpLauncher.CoreHost.Configuration;
using VoidRpLauncher.CoreHost.Contracts;
using VoidRpLauncher.CoreHost.Models;
using VoidRpLauncher.CoreHost.Models.Account;
using VoidRpLauncher.CoreHost.Services.Account;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class LauncherFacadeService
{
    private static readonly string[] ConfigPathsToSync =
    {
        "options.txt",
        "config/sodium-options.json",
        "config/sodium-extra-options.json",
        "config/sodium-extra.json",
    };

    private readonly AppEndpointsOptions _endpoints;
    private readonly ManifestService _manifestService;
    private readonly FileSyncService _fileSyncService;
    private readonly ClientRepairService _clientRepairService;
    private readonly RuntimeBootstrapService _runtimeBootstrapService;
    private readonly LauncherAuthSessionService _authSessionService;
    private readonly AuthenticatedLaunchService _authenticatedLaunchService;
    private readonly LauncherSettingsService _settingsService;
    private readonly LauncherPathsService _pathsService;
    private readonly LauncherStateService _stateService;
    private readonly DiagnosticsService _diagnostics;
    private readonly AppVersionService _appVersionService;
    private readonly ServerCatalogService _serverCatalog;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private LauncherManifest? _cachedManifest;

    public LauncherFacadeService(
        AppEndpointsOptions endpoints,
        ManifestService manifestService,
        FileSyncService fileSyncService,
        ClientRepairService clientRepairService,
        RuntimeBootstrapService runtimeBootstrapService,
        LauncherAuthSessionService authSessionService,
        AuthenticatedLaunchService authenticatedLaunchService,
        LauncherSettingsService settingsService,
        LauncherPathsService pathsService,
        LauncherStateService stateService,
        DiagnosticsService diagnostics,
        AppVersionService appVersionService,
        ServerCatalogService serverCatalog)
    {
        _endpoints = endpoints;
        _serverCatalog = serverCatalog;
        _manifestService = manifestService;
        _fileSyncService = fileSyncService;
        _clientRepairService = clientRepairService;
        _runtimeBootstrapService = runtimeBootstrapService;
        _authSessionService = authSessionService;
        _authenticatedLaunchService = authenticatedLaunchService;
        _settingsService = settingsService;
        _pathsService = pathsService;
        _stateService = stateService;
        _diagnostics = diagnostics;
        _appVersionService = appVersionService;
    }

    public LauncherStateDto GetState()
        => _stateService.BuildState(_pathsService, _settingsService, _diagnostics, _appVersionService, _endpoints);

    public async Task<OperationResponseDto> InitializeAsync(CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            if (_stateService.IsInitialized)
            {
                return OperationResponseDto.Success(GetState());
            }

            _pathsService.EnsureBaseDirectories();
            _diagnostics.Info("Core", "Launcher core bootstrap started.");
            _stateService.SetStatus("Подготавливаем окружение...");
            _stateService.SetProgress("Java runtime", "Проверяем игровой Java runtime...", 0);

            try
            {
                await _runtimeBootstrapService.EnsureRuntimeAsync((details, percent) =>
                {
                    _stateService.SetProgress("Java runtime", details, percent);
                }, cancellationToken);

                var snapshot = await _authSessionService.TryRestoreAsync(cancellationToken);
                if (snapshot is not null)
                {
                    try
                    {
                        snapshot = await _authSessionService.ReloadMeAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.Warn("Auth", $"ReloadMe failed during init: {ex.Message}");
                    }
                }

                LauncherDashboardResponseDto? dashboard = null;
                if (snapshot?.IsAuthenticated == true)
                {
                    var dashboardTask = TryLoadDashboardAsync(cancellationToken);
                    var modPrefsTask = TryLoadModPrefsAsync(cancellationToken);
                    await Task.WhenAll(dashboardTask, modPrefsTask);
                    dashboard = await dashboardTask;
                }

                _stateService.ApplySnapshot(snapshot);
                _stateService.ApplyDashboard(dashboard);
                _stateService.SetInitialized(true);
                _stateService.ClearProgress();
                _stateService.SetStatus(snapshot?.IsAuthenticated == true
                    ? "Лаунчер готов. Можно запускать игру."
                    : "Лаунчер готов. Выполните вход для запуска.");

                return OperationResponseDto.Success(GetState());
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка инициализации: {ex.Message}");
                _diagnostics.AppendException("Core", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<OperationResponseDto> LoginAsync(string login, string password, CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            _stateService.SetStatus("Выполняем вход...");
            _stateService.ClearProgress();

            try
            {
                var snapshot = await _authSessionService.LoginAsync(login.Trim(), password, cancellationToken);

                try
                {
                    snapshot = await _authSessionService.ReloadMeAsync(cancellationToken);
                }
                catch
                {
                }

                var dashboard = await TryLoadDashboardAsync(cancellationToken);
                await TryLoadModPrefsAsync(cancellationToken);
                _stateService.ApplySnapshot(snapshot);
                _stateService.ApplyDashboard(dashboard);
                _stateService.SetStatus("Вход выполнен.");
                return OperationResponseDto.Success(GetState(), "Вход выполнен.");
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка входа: {ex.Message}");
                _diagnostics.AppendException("Auth", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<OperationResponseDto> LogoutAsync(CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            _stateService.SetStatus("Завершаем сессию...");
            _stateService.ClearProgress();

            try
            {
                await _authSessionService.LogoutAsync(cancellationToken);
                _stateService.ApplySnapshot(null);
                _stateService.ApplyDashboard(null);
                _stateService.SetStatus("Вы вышли из аккаунта.");
                return OperationResponseDto.Success(GetState(), "Вы вышли из аккаунта.");
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка выхода: {ex.Message}");
                _diagnostics.AppendException("Auth", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<OperationResponseDto> RevokeOtherSessionsAsync(CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            try
            {
                var response = await _authSessionService.RevokeOtherSessionsAsync(cancellationToken);
                try
                {
                    var snapshot = await _authSessionService.ReloadMeAsync(cancellationToken);
                    _stateService.ApplySnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn("Auth", $"ReloadMe after revoke failed: {ex.Message}");
                }

                _stateService.SetStatus("Другие активные сессии завершены.");
                return OperationResponseDto.Success(GetState(), string.IsNullOrWhiteSpace(response.Message) ? "Другие активные сессии завершены." : response.Message);
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка завершения других сессий: {ex.Message}");
                _diagnostics.AppendException("Auth", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<OperationResponseDto> PlayAsync(CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            if (!_stateService.IsInitialized)
            {
                _stateService.SetStatus("Лаунчер ещё не завершил инициализацию. Дождитесь подготовки.");
                return OperationResponseDto.Failure(GetState(), "Лаунчер ещё не завершил инициализацию.");
            }

            _stateService.SetStatus("Проверяем клиент перед запуском...");
            _stateService.SetProgress("Java runtime", "Проверяем игровой Java runtime...", 0);

            try
            {
                await _runtimeBootstrapService.EnsureRuntimeAsync((details, percent) =>
                {
                    _stateService.SetProgress("Java runtime", details, percent);
                }, cancellationToken);

                _stateService.SetProgress("Подготовка", "Загружаем pack manifest...", 0);

                var manifest = await _manifestService.LoadAsync(_serverCatalog.ResolveManifestUrl(), cancellationToken);
                _cachedManifest = manifest;
                if (!string.IsNullOrWhiteSpace(manifest.MinLauncherVersion) &&
                    !_appVersionService.IsCurrentVersionAtLeast(manifest.MinLauncherVersion))
                {
                    throw new InvalidOperationException($"Требуется обновление лаунчера до версии {manifest.MinLauncherVersion} или выше.");
                }

                var settings = _settingsService.Load();
                var disabledMods = settings.DisabledMods.Count > 0
                    ? new HashSet<string>(settings.DisabledMods, StringComparer.OrdinalIgnoreCase)
                    : null;

                var syncProgress = new Progress<SyncProgressInfo>(info =>
                    _stateService.SetProgress(
                        string.IsNullOrWhiteSpace(info.Stage) ? "Синхронизация" : info.Stage,
                        BuildSyncDetails(info),
                        ClampPercent(info.Percent)));

                await _fileSyncService.SyncAsync(manifest, (IReadOnlySet<string>?)disabledMods, syncProgress, cancellationToken);

                // Restore per-account config files from server before launching
                if (_authSessionService.IsAuthenticated)
                    await RestoreConfigFilesAsync(cancellationToken);

                // Kill any jcef_helper.exe processes left from a previous MCEF/CEF session.
                // These lock CEF binary files (e.g. chrome_100_percent.pak), preventing MCEF
                // from extracting updates and causing startup crashes.
                _stateService.SetProgress("Подготовка", "Завершаем зависшие CEF-процессы...", 0);
                KillLingeringCefProcesses();

                var memoryMb = settings.MaxRamMb;
                var launchProgress = new Progress<LaunchProgressInfo>(info =>
                    _stateService.SetProgress(
                        string.IsNullOrWhiteSpace(info.Stage) ? "Запуск" : info.Stage,
                        string.IsNullOrWhiteSpace(info.Details) ? "Запускаем Minecraft..." : info.Details,
                        ClampPercent(info.Percent)));

                var gameProcess = await _authenticatedLaunchService.LaunchAsync(manifest, memoryMb, launchProgress, cancellationToken);

                // Fire-and-forget: upload config files and report crashes when game exits
                if (_authSessionService.IsAuthenticated)
                    _ = WatchGameAndUploadConfigsAsync(gameProcess, DateTime.UtcNow);

                _stateService.ClearProgress();
                _stateService.SetStatus("Minecraft запущен.");
                return OperationResponseDto.Success(GetState(), "Minecraft запущен.");
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка запуска: {ex.Message}");
                _diagnostics.AppendException("Play", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<OperationResponseDto> RepairAsync(CancellationToken cancellationToken = default)
        => await RunExclusiveAsync(async () =>
        {
            _stateService.SetStatus("Выполняем ремонт клиента...");
            _stateService.SetProgress("Ремонт", "Чистим managed-файлы и state...", 0);

            try
            {
                var repairProgress = new Progress<string>(message =>
                    _stateService.SetProgress("Ремонт", message, 50));

                await _clientRepairService.RepairAsync(repairProgress, cancellationToken);

                _stateService.SetProgress("Ремонт", "Готово.", 100);
                _stateService.SetStatus("Ремонт завершён. Теперь можно заново синхронизировать клиент.");
                return OperationResponseDto.Success(GetState(), "Ремонт завершён.");
            }
            catch (Exception ex)
            {
                _stateService.SetStatus($"Ошибка ремонта: {ex.Message}");
                _diagnostics.AppendException("Repair", ex);
                return OperationResponseDto.Failure(GetState(), ex.Message);
            }
        });

    public async Task<LauncherPlayerSkinDto> GetSkinAsync(CancellationToken cancellationToken = default)
    {
        var skin = await _authSessionService.GetSkinAsync(cancellationToken);
        return MapSkin(skin);
    }

    public async Task<LauncherPlayerSkinOperationDto> UploadSkinAsync(IFormFile file, string modelVariant, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _authSessionService.UploadSkinAsync(file, modelVariant, cancellationToken);
            return LauncherPlayerSkinOperationDto.Success(MapSkin(response.Skin), string.IsNullOrWhiteSpace(response.Message) ? "Скин сохранён." : response.Message);
        }
        catch (Exception ex)
        {
            _diagnostics.AppendException("Skin", ex);
            return LauncherPlayerSkinOperationDto.Failure(ex.Message);
        }
    }

    public async Task<LauncherPlayerSkinOperationDto> DeleteSkinAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _authSessionService.DeleteSkinAsync(cancellationToken);
            return LauncherPlayerSkinOperationDto.Success(MapSkin(response.Skin), string.IsNullOrWhiteSpace(response.Message) ? "Скин удалён." : response.Message);
        }
        catch (Exception ex)
        {
            _diagnostics.AppendException("Skin", ex);
            return LauncherPlayerSkinOperationDto.Failure(ex.Message);
        }
    }

    public OperationResponseDto SaveSettings(int maxRamMb)
    {
        try
        {
            _settingsService.Save(new LauncherUserSettings { MaxRamMb = maxRamMb });
            return OperationResponseDto.Success(GetState(), "Настройки сохранены.");
        }
        catch (Exception ex)
        {
            _stateService.SetStatus($"Ошибка сохранения настроек: {ex.Message}");
            _diagnostics.AppendException("Settings", ex);
            return OperationResponseDto.Failure(GetState(), ex.Message);
        }
    }

    public OperationResponseDto ResetSettings()
    {
        try
        {
            _settingsService.Save(new LauncherUserSettings { MaxRamMb = 4096 });
            return OperationResponseDto.Success(GetState(), "Настройки сброшены к значению 4.0 GB.");
        }
        catch (Exception ex)
        {
            _stateService.SetStatus($"Ошибка сброса настроек: {ex.Message}");
            _diagnostics.AppendException("Settings", ex);
            return OperationResponseDto.Failure(GetState(), ex.Message);
        }
    }

    public OperationResponseDto ClearDiagnostics()
    {
        _diagnostics.Clear();
        return OperationResponseDto.Success(GetState(), "Диагностика очищена.");
    }

    public async Task<ModListDto> GetModsAsync(CancellationToken cancellationToken = default)
    {
        LauncherManifest manifest;
        try
        {
            manifest = _cachedManifest ?? await _manifestService.LoadAsync(_serverCatalog.ResolveManifestUrl(), cancellationToken);
            _cachedManifest = manifest;
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Mods", $"Failed to load manifest for mods list: {ex.Message}");
            return new ModListDto { Mods = new List<ModInfoDto>() };
        }

        var settings = _settingsService.Load();
        var disabled = new HashSet<string>(settings.DisabledMods, StringComparer.OrdinalIgnoreCase);

        var mods = manifest.Files
            .Where(f => f.Optional)
            .Select(f =>
            {
                var rel = f.Path.Replace('\\', '/').Trim('/');
                var display = string.IsNullOrWhiteSpace(f.DisplayName)
                    ? System.IO.Path.GetFileNameWithoutExtension(f.Path)
                    : f.DisplayName;
                return new ModInfoDto
                {
                    Path = rel,
                    DisplayName = display,
                    Description = f.Description,
                    Optional = f.Optional,
                    Required = f.Required,
                    Enabled = f.Required || !disabled.Contains(rel),
                };
            })
            .ToList();

        return new ModListDto { Mods = mods };
    }

    public async Task<ModToggleResponseDto> ToggleModAsync(string path, bool enabled, CancellationToken cancellationToken = default)
    {
        var rel = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(rel))
            return new ModToggleResponseDto { Ok = false, Message = "Путь к моду не указан." };

        // Check if mod is required
        var manifest = _cachedManifest;
        if (manifest is not null)
        {
            var entry = manifest.Files.FirstOrDefault(f =>
                string.Equals(f.Path.Replace('\\', '/').Trim('/'), rel, StringComparison.OrdinalIgnoreCase));
            if (entry?.Required == true)
                return new ModToggleResponseDto { Ok = false, Message = "Этот мод обязателен и не может быть отключён." };
        }

        var settings = _settingsService.Load();
        if (enabled)
            settings.DisabledMods.RemoveAll(m => string.Equals(m, rel, StringComparison.OrdinalIgnoreCase));
        else if (!settings.DisabledMods.Any(m => string.Equals(m, rel, StringComparison.OrdinalIgnoreCase)))
            settings.DisabledMods.Add(rel);

        _settingsService.Save(settings);

        if (_authSessionService.IsAuthenticated)
        {
            try { await _authSessionService.SaveModPrefsAsync(settings.DisabledMods, cancellationToken); }
            catch (Exception ex) { _diagnostics.Warn("Mods", $"Failed to sync mod prefs to server: {ex.Message}"); }
        }

        var modList = await GetModsAsync(cancellationToken);
        return new ModToggleResponseDto
        {
            Ok = true,
            Message = enabled ? "Мод включён." : "Мод отключён. Изменения вступят в силу при следующем запуске игры.",
            Mods = modList.Mods,
        };
    }

    private async Task TryLoadModPrefsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var prefs = await _authSessionService.GetPreferencesAsync(cancellationToken);
            var settings = _settingsService.Load();
            settings.DisabledMods = prefs.DisabledMods ?? new List<string>();
            _settingsService.Save(settings);
            _diagnostics.Info("Mods", $"Loaded {settings.DisabledMods.Count} disabled mod(s) from server.");
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Mods", $"Failed to load mod prefs from server: {ex.Message}");
        }
    }

    private async Task RestoreConfigFilesAsync(CancellationToken cancellationToken)
    {
        foreach (var configPath in ConfigPathsToSync)
        {
            try
            {
                var fileDto = await _authSessionService.GetConfigFileAsync(configPath, cancellationToken);
                if (!fileDto.Found || string.IsNullOrWhiteSpace(fileDto.ContentB64)) continue;

                var localPath = System.IO.Path.Combine(
                    _pathsService.GameDirectory,
                    configPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                var dir = System.IO.Path.GetDirectoryName(localPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(localPath, Convert.FromBase64String(fileDto.ContentB64), cancellationToken);
                _diagnostics.Info("Config", $"Restored per-account config: {configPath}");
            }
            catch (Exception ex)
            {
                _diagnostics.Warn("Config", $"Failed to restore config '{configPath}': {ex.Message}");
            }
        }
    }

    private async Task WatchGameAndUploadConfigsAsync(Process process, DateTime gameStartedAt)
    {
        try
        {
            await process.WaitForExitAsync();
            var exitCode = process.ExitCode;
            _diagnostics.Info("Config", $"Game exited (code {exitCode}). Uploading per-account config files...");

            foreach (var configPath in ConfigPathsToSync)
            {
                var localPath = System.IO.Path.Combine(
                    _pathsService.GameDirectory,
                    configPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!File.Exists(localPath)) continue;

                try
                {
                    var bytes = await File.ReadAllBytesAsync(localPath);
                    var contentB64 = Convert.ToBase64String(bytes);
                    if (contentB64.Length > 512 * 1024) continue;
                    await _authSessionService.SaveConfigFileAsync(configPath, contentB64, CancellationToken.None);
                    _diagnostics.Info("Config", $"Uploaded per-account config: {configPath}");
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn("Config", $"Failed to upload config '{configPath}': {ex.Message}");
                }
            }

            // Exit code -1073740791 (0xC0000409) = MCEF/jcef_helper.exe crash (STATUS_STACK_BUFFER_OVERRUN).
            // This is a known CEF crash — do not spam the backend with these reports.
            const int McefCrashCode = -1073740791;
            if (exitCode == McefCrashCode)
            {
                _diagnostics.Warn("Crash", $"Game exited with MCEF crash code {exitCode} (0xC0000409). Skipping crash report.");
                _stateService.SetStatus("Minecraft завершился с ошибкой MCEF (код -1073740791). Попробуйте переустановить лаунчер или обратитесь в поддержку.");
                return;
            }

            // Crash detection: exit code != 0 OR a new crash report file appeared since launch.
            var crashReportContent = TryReadLatestCrashReport(gameStartedAt);
            var isCrash = exitCode != 0 || crashReportContent != null;
            if (isCrash)
            {
                _diagnostics.Warn("Crash", $"Crash detected (exit={exitCode}). Sending report...");
                await _authSessionService.ReportCrashAsync(exitCode, crashReportContent, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Config", $"Game watcher error: {ex.Message}");
        }
    }

    private string? TryReadLatestCrashReport(DateTime since)
    {
        try
        {
            var crashDir = System.IO.Path.Combine(_pathsService.GameDirectory, "crash-reports");
            if (!Directory.Exists(crashDir)) return null;

            var newest = Directory.GetFiles(crashDir, "*.txt")
                .Select(f => new FileInfo(f))
                .Where(fi => fi.LastWriteTimeUtc > since)
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .FirstOrDefault();

            if (newest == null) return null;

            var content = File.ReadAllText(newest.FullName);
            if (content.Length > 64 * 1024)
                content = content[..(64 * 1024)] + "\n... [truncated]";
            return content;
        }
        catch
        {
            return null;
        }
    }

    private void KillLingeringCefProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName("jcef_helper");
            foreach (var p in processes)
            {
                try
                {
                    _diagnostics.Info("Launch", $"Killing lingering jcef_helper process (PID {p.Id})");
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2000);
                }
                catch (Exception ex)
                {
                    _diagnostics.Warn("Launch", $"Failed to kill jcef_helper (PID {p.Id}): {ex.Message}");
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Launch", $"CEF process cleanup error: {ex.Message}");
        }
    }

    private async Task<OperationResponseDto> RunExclusiveAsync(Func<Task<OperationResponseDto>> action)
    {
        if (!await _operationLock.WaitAsync(0))
        {
            return OperationResponseDto.Failure(GetState(), "Лаунчер уже выполняет другую операцию.");
        }

        _stateService.SetBusy(true);
        try
        {
            return await action();
        }
        finally
        {
            _stateService.SetBusy(false);
            _operationLock.Release();
        }
    }

    private static string BuildSyncDetails(SyncProgressInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.DetailMessage))
        {
            return info.DetailMessage;
        }

        if (!string.IsNullOrWhiteSpace(info.CurrentFile) && info.TotalFiles > 0)
        {
            return $"{info.CurrentFile} • {info.ProcessedFiles}/{info.TotalFiles}";
        }

        if (!string.IsNullOrWhiteSpace(info.CurrentFile))
        {
            return info.CurrentFile;
        }

        return info.Stage;
    }

    private static double ClampPercent(double value)
    {
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value;
    }

    private async Task<LauncherDashboardResponseDto?> TryLoadDashboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _authSessionService.GetDashboardAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.Warn("Dashboard", $"Launcher dashboard load failed: {ex.Message}");
            return null;
        }
    }

    private static LauncherPlayerSkinDto MapSkin(PlayerSkinReadDto? skin)
    {
        return new LauncherPlayerSkinDto
        {
            HasSkin = skin?.HasSkin ?? false,
            ModelVariant = skin?.ModelVariant ?? "classic",
            SkinUrl = skin?.SkinUrl ?? string.Empty,
            HeadPreviewUrl = skin?.HeadPreviewUrl ?? string.Empty,
            BodyPreviewUrl = skin?.BodyPreviewUrl ?? string.Empty,
            Sha256 = skin?.Sha256 ?? string.Empty,
            Width = skin?.Width ?? 0,
            Height = skin?.Height ?? 0,
            UpdatedAt = skin?.UpdatedAt
        };
    }
}
