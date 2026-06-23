using System;
using System.Text.Json.Serialization;

namespace VoidRpLauncher.CoreHost.Models.Account;

public sealed class LoginRequestDto
{
    [JsonPropertyName("login")] public string Login { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
    [JsonPropertyName("device_name")] public string DeviceName { get; set; } = "VoidRP Launcher";
}

public sealed class RefreshRequestDto
{
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("device_name")] public string DeviceName { get; set; } = "VoidRP Launcher";
}

public sealed class LogoutRequestDto
{
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RevokeOtherSessionsRequestDto
{
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RevokeSessionsResponseDto
{
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("revoked_sessions")] public int RevokedSessions { get; set; }
}

public sealed class SimpleResponseDto
{
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
}

public sealed class IssuePlayTicketRequestDto
{
    [JsonPropertyName("launcher_version")] public string LauncherVersion { get; set; } = "unknown";
    [JsonPropertyName("launcher_platform")] public string LauncherPlatform { get; set; } = "unknown";
}

public sealed class IssuePlayTicketResponseDto
{
    [JsonPropertyName("ticket")] public string Ticket { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public DateTimeOffset ExpiresAt { get; set; }
    [JsonPropertyName("minecraft_nickname")] public string MinecraftNickname { get; set; } = string.Empty;
    [JsonPropertyName("ttl_seconds")] public int TtlSeconds { get; set; }
}

public sealed class UserReadDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("site_login")] public string SiteLogin { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("email_verified")] public bool EmailVerified { get; set; }
    [JsonPropertyName("is_active")] public bool IsActive { get; set; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlayerAccountReadDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("minecraft_nickname")] public string MinecraftNickname { get; set; } = string.Empty;
    [JsonPropertyName("nickname_locked")] public bool NicknameLocked { get; set; }
    [JsonPropertyName("legacy_auth_enabled")] public bool LegacyAuthEnabled { get; set; }
}

public sealed class AccountSecurityReadDto
{
    [JsonPropertyName("active_refresh_sessions")] public int ActiveRefreshSessions { get; set; }
    [JsonPropertyName("must_use_launcher")] public bool MustUseLauncher { get; set; }
    [JsonPropertyName("legacy_hash_present")] public bool LegacyHashPresent { get; set; }
    [JsonPropertyName("legacy_ready")] public bool LegacyReady { get; set; }
}

public sealed class PlayerSkinReadDto
{
    [JsonPropertyName("has_skin")] public bool HasSkin { get; set; }
    [JsonPropertyName("model_variant")] public string ModelVariant { get; set; } = string.Empty;
    [JsonPropertyName("skin_url")] public string SkinUrl { get; set; } = string.Empty;
    [JsonPropertyName("head_preview_url")] public string HeadPreviewUrl { get; set; } = string.Empty;
    [JsonPropertyName("body_preview_url")] public string BodyPreviewUrl { get; set; } = string.Empty;
    [JsonPropertyName("width")] public int? Width { get; set; }
    [JsonPropertyName("height")] public int? Height { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class PlayerSkinOperationResponseDto
{
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("skin")] public PlayerSkinReadDto Skin { get; set; } = new();
}

public sealed class TokenPairResponseDto
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "bearer";
    [JsonPropertyName("access_expires_in")] public int AccessExpiresIn { get; set; }
    [JsonPropertyName("refresh_expires_in")] public int RefreshExpiresIn { get; set; }
    [JsonPropertyName("user")] public UserReadDto User { get; set; } = new();
    [JsonPropertyName("player_account")] public PlayerAccountReadDto PlayerAccount { get; set; } = new();
}

public sealed class MeResponseDto
{
    [JsonPropertyName("user")] public UserReadDto User { get; set; } = new();
    [JsonPropertyName("player_account")] public PlayerAccountReadDto PlayerAccount { get; set; } = new();
    [JsonPropertyName("security")] public AccountSecurityReadDto Security { get; set; } = new();
}

public sealed class LauncherDashboardNationResponseDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("slug")] public string Slug { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("tag")] public string Tag { get; set; } = string.Empty;
    [JsonPropertyName("accent_color")] public string AccentColor { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("icon_url")] public string IconUrl { get; set; } = string.Empty;
    [JsonPropertyName("icon_preview_url")] public string IconPreviewUrl { get; set; } = string.Empty;
    [JsonPropertyName("banner_url")] public string BannerUrl { get; set; } = string.Empty;
    [JsonPropertyName("banner_preview_url")] public string BannerPreviewUrl { get; set; } = string.Empty;
    [JsonPropertyName("background_url")] public string BackgroundUrl { get; set; } = string.Empty;
    [JsonPropertyName("background_preview_url")] public string BackgroundPreviewUrl { get; set; } = string.Empty;
    [JsonPropertyName("alliance_title")] public string AllianceTitle { get; set; } = string.Empty;
    [JsonPropertyName("alliance_tag")] public string AllianceTag { get; set; } = string.Empty;
}

public sealed class LauncherDashboardNationStatsResponseDto
{
    [JsonPropertyName("treasury_balance")] public double TreasuryBalance { get; set; }
    [JsonPropertyName("territory_points")] public int TerritoryPoints { get; set; }
    [JsonPropertyName("total_playtime_minutes")] public int TotalPlaytimeMinutes { get; set; }
    [JsonPropertyName("pvp_kills")] public int PvpKills { get; set; }
    [JsonPropertyName("mob_kills")] public int MobKills { get; set; }
    [JsonPropertyName("boss_kills")] public int BossKills { get; set; }
    [JsonPropertyName("deaths")] public int Deaths { get; set; }
    [JsonPropertyName("blocks_placed")] public long BlocksPlaced { get; set; }
    [JsonPropertyName("blocks_broken")] public long BlocksBroken { get; set; }
    [JsonPropertyName("events_completed")] public int EventsCompleted { get; set; }
    [JsonPropertyName("prestige_score")] public int PrestigeScore { get; set; }
}

public sealed class LauncherDashboardPlayerStatsResponseDto
{
    [JsonPropertyName("minecraft_nickname")] public string MinecraftNickname { get; set; } = string.Empty;
    [JsonPropertyName("total_playtime_minutes")] public int TotalPlaytimeMinutes { get; set; }
    [JsonPropertyName("pvp_kills")] public int PvpKills { get; set; }
    [JsonPropertyName("mob_kills")] public int MobKills { get; set; }
    [JsonPropertyName("deaths")] public int Deaths { get; set; }
    [JsonPropertyName("blocks_placed")] public long BlocksPlaced { get; set; }
    [JsonPropertyName("blocks_broken")] public long BlocksBroken { get; set; }
    [JsonPropertyName("current_balance")] public double CurrentBalance { get; set; }
    [JsonPropertyName("completed_quests")] public int CompletedQuests { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("last_seen_at")] public DateTimeOffset? LastSeenAt { get; set; }
    [JsonPropertyName("last_synced_at")] public DateTimeOffset? LastSyncedAt { get; set; }
}

public sealed class LauncherDashboardActivityResponseDto
{
    [JsonPropertyName("event_type")] public string EventType { get; set; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
    [JsonPropertyName("created_at")] public DateTimeOffset? CreatedAt { get; set; }
}

public sealed class LauncherDashboardResponseDto
{
    [JsonPropertyName("nation")] public LauncherDashboardNationResponseDto? Nation { get; set; }
    [JsonPropertyName("nation_stats")] public LauncherDashboardNationStatsResponseDto? NationStats { get; set; }
    [JsonPropertyName("player_stats")] public LauncherDashboardPlayerStatsResponseDto? PlayerStats { get; set; }
    [JsonPropertyName("recent_activity")] public List<LauncherDashboardActivityResponseDto> RecentActivity { get; set; } = new();
    [JsonPropertyName("wallet_balance")] public double WalletBalance { get; set; }
}

public sealed class LauncherAuthSnapshot
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public UserReadDto User { get; set; } = new();
    public PlayerAccountReadDto PlayerAccount { get; set; } = new();
    public AccountSecurityReadDto Security { get; set; } = new();
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(RefreshToken) && !string.IsNullOrWhiteSpace(PlayerAccount?.MinecraftNickname);
}

public sealed class LauncherPlayTicketEnvelope
{
    [JsonPropertyName("ticket")] public string Ticket { get; set; } = string.Empty;
    [JsonPropertyName("minecraftNickname")] public string MinecraftNickname { get; set; } = string.Empty;
    [JsonPropertyName("expiresAtUtc")] public DateTimeOffset ExpiresAtUtc { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; } = "VoidRP Launcher";
    [JsonPropertyName("launcherProof")] public string LauncherProof { get; set; } = string.Empty;
}

public sealed class LauncherTokenEnvelope
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LauncherPreferencesDto
{
    [JsonPropertyName("disabled_mods")] public List<string> DisabledMods { get; set; } = new();
    [JsonPropertyName("config_files")] public Dictionary<string, string> ConfigFiles { get; set; } = new();
}

public sealed class LauncherConfigFileDto
{
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("found")] public bool Found { get; set; }
    [JsonPropertyName("content_b64")] public string? ContentB64 { get; set; }
}







