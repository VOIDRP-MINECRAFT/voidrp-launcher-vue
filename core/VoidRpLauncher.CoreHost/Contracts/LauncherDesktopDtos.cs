namespace VoidRpLauncher.CoreHost.Contracts;

public sealed class LauncherProgressDto
{
    public bool Visible { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public double Percent { get; set; }
}

public sealed class LauncherLinksDto
{
    public string RegisterUrl { get; set; } = string.Empty;
    public string ForgotPasswordUrl { get; set; } = string.Empty;
    public string VerifyEmailUrl { get; set; } = string.Empty;
}

public sealed class LauncherDashboardNationDto
{
    public string Id { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string AccentColor { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string IconPreviewUrl { get; set; } = string.Empty;
    public string BannerUrl { get; set; } = string.Empty;
    public string BannerPreviewUrl { get; set; } = string.Empty;
    public string BackgroundUrl { get; set; } = string.Empty;
    public string BackgroundPreviewUrl { get; set; } = string.Empty;
    public string AllianceTitle { get; set; } = string.Empty;
    public string AllianceTag { get; set; } = string.Empty;
}

public sealed class LauncherDashboardNationStatsDto
{
    public double TreasuryBalance { get; set; }
    public int TerritoryPoints { get; set; }
    public int TotalPlaytimeMinutes { get; set; }
    public int PvpKills { get; set; }
    public int MobKills { get; set; }
    public int BossKills { get; set; }
    public int Deaths { get; set; }
    public long BlocksPlaced { get; set; }
    public long BlocksBroken { get; set; }
    public int EventsCompleted { get; set; }
    public int PrestigeScore { get; set; }
}

public sealed class LauncherDashboardPlayerStatsDto
{
    public string MinecraftNickname { get; set; } = string.Empty;
    public int TotalPlaytimeMinutes { get; set; }
    public int PvpKills { get; set; }
    public int MobKills { get; set; }
    public int Deaths { get; set; }
    public long BlocksPlaced { get; set; }
    public long BlocksBroken { get; set; }
    public double CurrentBalance { get; set; }
    public int CompletedQuests { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}

public sealed class LauncherDashboardActivityDto
{
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class LauncherAccountSecurityDto
{
    public int ActiveRefreshSessions { get; set; }
    public bool MustUseLauncher { get; set; }
    public bool LegacyHashPresent { get; set; }
    public bool LegacyReady { get; set; }
}

public sealed class LauncherDashboardDto
{
    public LauncherDashboardNationDto Nation { get; set; } = new();
    public LauncherDashboardNationStatsDto NationStats { get; set; } = new();
    public LauncherDashboardPlayerStatsDto PlayerStats { get; set; } = new();
    public List<LauncherDashboardActivityDto> RecentActivity { get; set; } = new();
    public double WalletBalance { get; set; }
}

public sealed class LauncherStateDto
{
    public bool Initialized { get; set; }
    public bool IsBusy { get; set; }
    public bool IsAuthenticated { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string LauncherVersionText { get; set; } = string.Empty;
    public string AccountPrimaryText { get; set; } = string.Empty;
    public string AccountSecondaryText { get; set; } = string.Empty;
    public bool AccountIsAdmin { get; set; }
    public string EmailVerifiedText { get; set; } = string.Empty;
    public int CurrentMemoryMb { get; set; }
    public string CurrentMemoryText { get; set; } = string.Empty;
    public string LogsDirectory { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public string GameDirectory { get; set; } = string.Empty;
    public string DiagnosticsText { get; set; } = string.Empty;
    public LauncherProgressDto Progress { get; set; } = new();
    public LauncherLinksDto Links { get; set; } = new();
    public LauncherAccountSecurityDto Security { get; set; } = new();
    public LauncherDashboardDto Dashboard { get; set; } = new();
}

public sealed class OperationResponseDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool PendingElectronExit { get; set; }
    public LauncherStateDto State { get; set; } = new();

    public static OperationResponseDto Success(LauncherStateDto state, string message = "", bool pendingElectronExit = false)
        => new() { Ok = true, Message = message, State = state, PendingElectronExit = pendingElectronExit };

    public static OperationResponseDto Failure(LauncherStateDto state, string message)
        => new() { Ok = false, Message = message, State = state, PendingElectronExit = false };
}

public sealed class LauncherPlayerSkinDto
{
    public bool HasSkin { get; set; }
    public string ModelVariant { get; set; } = "classic";
    public string SkinUrl { get; set; } = string.Empty;
    public string HeadPreviewUrl { get; set; } = string.Empty;
    public string BodyPreviewUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class LauncherPlayerSkinOperationDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public LauncherPlayerSkinDto Skin { get; set; } = new();

    public static LauncherPlayerSkinOperationDto Success(LauncherPlayerSkinDto skin, string message = "")
        => new() { Ok = true, Message = message, Skin = skin ?? new LauncherPlayerSkinDto() };

    public static LauncherPlayerSkinOperationDto Failure(string message, LauncherPlayerSkinDto? skin = null)
        => new() { Ok = false, Message = message, Skin = skin ?? new LauncherPlayerSkinDto() };
}

public sealed class LoginCommandDto
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SettingsUpdateDto
{
    public int MaxRamMb { get; set; }
}

public sealed class ModInfoDto
{
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Optional { get; set; }
    public bool Required { get; set; }
    public bool Enabled { get; set; }
}

public sealed class ModListDto
{
    public List<ModInfoDto> Mods { get; set; } = new();
}

public sealed class ModToggleDto
{
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class ServerSelectDto
{
    public string Slug { get; set; } = string.Empty;
}

public sealed class ModToggleResponseDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<ModInfoDto> Mods { get; set; } = new();
}

public sealed class ModSuggestionCommandDto
{
    public string Url { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public sealed class ModSuggestionResponseDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class PlayerFeedbackCommandDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
}

public sealed class PlayerFeedbackResponseDto
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
}


