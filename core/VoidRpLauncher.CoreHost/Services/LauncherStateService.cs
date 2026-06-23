using System.Linq;
using VoidRpLauncher.CoreHost.Configuration;
using VoidRpLauncher.CoreHost.Contracts;
using VoidRpLauncher.CoreHost.Models.Account;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class LauncherStateService
{
    private readonly object _lock = new();
    private bool _initialized;
    private bool _isBusy;
    private string _statusText = "Инициализация лаунчера...";
    private string _accountPrimaryText = "Гость";
    private string _accountSecondaryText = "Войдите, чтобы запустить игру";
    private string _emailVerifiedText = "Требуется вход";
    private LauncherProgressDto _progress = new();
    private LauncherAuthSnapshot? _snapshot;
    private LauncherDashboardResponseDto? _dashboard;

    public bool IsInitialized
    {
        get { lock (_lock) return _initialized; }
    }

    public bool IsBusy
    {
        get { lock (_lock) return _isBusy; }
    }

    public void SetInitialized(bool value)
    {
        lock (_lock)
        {
            _initialized = value;
        }
    }

    public void SetBusy(bool value)
    {
        lock (_lock)
        {
            _isBusy = value;
        }
    }

    public void SetStatus(string statusText)
    {
        lock (_lock)
        {
            _statusText = statusText;
        }
    }

    public void SetProgress(string title, string details, double percent)
    {
        lock (_lock)
        {
            _progress = new LauncherProgressDto
            {
                Visible = true,
                Title = title,
                Details = details,
                Percent = ClampPercent(percent)
            };
        }
    }

    public void ClearProgress()
    {
        lock (_lock)
        {
            _progress = new LauncherProgressDto();
        }
    }

    public void ApplySnapshot(LauncherAuthSnapshot? snapshot)
    {
        lock (_lock)
        {
            _snapshot = snapshot;
            if (snapshot?.IsAuthenticated != true)
            {
                _dashboard = null;
                _accountPrimaryText = "Гость";
                _accountSecondaryText = "Войдите, чтобы запустить игру";
                _emailVerifiedText = "Требуется вход";
                return;
            }

            _accountPrimaryText = string.IsNullOrWhiteSpace(snapshot.PlayerAccount.MinecraftNickname)
                ? snapshot.User.SiteLogin
                : snapshot.PlayerAccount.MinecraftNickname;
            _accountSecondaryText = $"{snapshot.User.SiteLogin} • {snapshot.User.Email}";
            _emailVerifiedText = snapshot.User.EmailVerified ? "Почта подтверждена" : "Почта не подтверждена";
        }
    }

    public void ApplyDashboard(LauncherDashboardResponseDto? dashboard)
    {
        lock (_lock)
        {
            _dashboard = dashboard;
        }
    }

    public LauncherStateDto BuildState(
        LauncherPathsService pathsService,
        LauncherSettingsService settingsService,
        DiagnosticsService diagnosticsService,
        AppVersionService appVersionService,
        AppEndpointsOptions endpoints)
    {
        lock (_lock)
        {
            var settings = settingsService.Load();
            return new LauncherStateDto
            {
                Initialized = _initialized,
                IsBusy = _isBusy,
                IsAuthenticated = _snapshot?.IsAuthenticated == true,
                StatusText = _statusText,
                LauncherVersionText = appVersionService.CurrentVersion,
                AccountPrimaryText = _accountPrimaryText,
                AccountSecondaryText = _accountSecondaryText,
                EmailVerifiedText = _emailVerifiedText,
                CurrentMemoryMb = settings.MaxRamMb,
                CurrentMemoryText = $"{settings.MaxRamMb / 1024.0:0.0} GB",
                LogsDirectory = pathsService.LogsDirectory,
                DataDirectory = pathsService.BaseDirectory,
                GameDirectory = pathsService.GameDirectory,
                DiagnosticsText = diagnosticsService.Text,
                Progress = new LauncherProgressDto
                {
                    Visible = _progress.Visible,
                    Title = _progress.Title,
                    Details = _progress.Details,
                    Percent = _progress.Percent
                },
                Links = new LauncherLinksDto
                {
                    RegisterUrl = endpoints.RegisterUrl,
                    ForgotPasswordUrl = endpoints.ForgotPasswordUrl,
                    VerifyEmailUrl = endpoints.VerifyEmailUrl
                },
                Security = new LauncherAccountSecurityDto
                {
                    ActiveRefreshSessions = _snapshot?.Security?.ActiveRefreshSessions ?? 0,
                    MustUseLauncher = _snapshot?.Security?.MustUseLauncher ?? false,
                    LegacyHashPresent = _snapshot?.Security?.LegacyHashPresent ?? false,
                    LegacyReady = _snapshot?.Security?.LegacyReady ?? false
                },
                Dashboard = BuildDashboardDto(_dashboard)
            };
        }
    }

    private static LauncherDashboardDto BuildDashboardDto(LauncherDashboardResponseDto? dashboard)
    {
        if (dashboard is null)
        {
            return new LauncherDashboardDto();
        }

        return new LauncherDashboardDto
        {
            WalletBalance = dashboard.WalletBalance,
            Nation = dashboard.Nation is null
                ? new LauncherDashboardNationDto()
                : new LauncherDashboardNationDto
                {
                    Id = dashboard.Nation.Id ?? string.Empty,
                    Slug = dashboard.Nation.Slug ?? string.Empty,
                    Title = dashboard.Nation.Title ?? string.Empty,
                    Tag = dashboard.Nation.Tag ?? string.Empty,
                    AccentColor = dashboard.Nation.AccentColor ?? string.Empty,
                    Role = dashboard.Nation.Role ?? string.Empty,
                    IconUrl = dashboard.Nation.IconUrl ?? string.Empty,
                    IconPreviewUrl = dashboard.Nation.IconPreviewUrl ?? string.Empty,
                    BannerUrl = dashboard.Nation.BannerUrl ?? string.Empty,
                    BannerPreviewUrl = dashboard.Nation.BannerPreviewUrl ?? string.Empty,
                    BackgroundUrl = dashboard.Nation.BackgroundUrl ?? string.Empty,
                    BackgroundPreviewUrl = dashboard.Nation.BackgroundPreviewUrl ?? string.Empty,
                    AllianceTitle = dashboard.Nation.AllianceTitle ?? string.Empty,
                    AllianceTag = dashboard.Nation.AllianceTag ?? string.Empty,
                },
            NationStats = dashboard.NationStats is null
                ? new LauncherDashboardNationStatsDto()
                : new LauncherDashboardNationStatsDto
                {
                    TreasuryBalance = dashboard.NationStats.TreasuryBalance,
                    TerritoryPoints = dashboard.NationStats.TerritoryPoints,
                    TotalPlaytimeMinutes = dashboard.NationStats.TotalPlaytimeMinutes,
                    PvpKills = dashboard.NationStats.PvpKills,
                    MobKills = dashboard.NationStats.MobKills,
                    BossKills = dashboard.NationStats.BossKills,
                    Deaths = dashboard.NationStats.Deaths,
                    BlocksPlaced = dashboard.NationStats.BlocksPlaced,
                    BlocksBroken = dashboard.NationStats.BlocksBroken,
                    EventsCompleted = dashboard.NationStats.EventsCompleted,
                    PrestigeScore = dashboard.NationStats.PrestigeScore,
                },
            PlayerStats = dashboard.PlayerStats is null
                ? new LauncherDashboardPlayerStatsDto()
                : new LauncherDashboardPlayerStatsDto
                {
                    MinecraftNickname = dashboard.PlayerStats.MinecraftNickname ?? string.Empty,
                    TotalPlaytimeMinutes = dashboard.PlayerStats.TotalPlaytimeMinutes,
                    PvpKills = dashboard.PlayerStats.PvpKills,
                    MobKills = dashboard.PlayerStats.MobKills,
                    Deaths = dashboard.PlayerStats.Deaths,
                    BlocksPlaced = dashboard.PlayerStats.BlocksPlaced,
                    BlocksBroken = dashboard.PlayerStats.BlocksBroken,
                    CurrentBalance = dashboard.PlayerStats.CurrentBalance,
                    CompletedQuests = dashboard.PlayerStats.CompletedQuests,
                    Source = dashboard.PlayerStats.Source ?? string.Empty,
                    LastSeenAt = dashboard.PlayerStats.LastSeenAt,
                    LastSyncedAt = dashboard.PlayerStats.LastSyncedAt,
                },
            RecentActivity = dashboard.RecentActivity?.Select(item => new LauncherDashboardActivityDto
            {
                EventType = item.EventType ?? string.Empty,
                Message = item.Message ?? string.Empty,
                CreatedAt = item.CreatedAt
            }).ToList() ?? new List<LauncherDashboardActivityDto>()
        };
    }

    private static double ClampPercent(double value)
    {
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value;
    }
}


