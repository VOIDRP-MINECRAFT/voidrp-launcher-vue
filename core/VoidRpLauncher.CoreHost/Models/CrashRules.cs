using System.Collections.Generic;

namespace VoidRpLauncher.CoreHost.Models;

/// <summary>
/// One crash-recognition rule. The same shape comes from the built-in list in
/// <see cref="Services.CrashAdvisor"/> and from the backend (/launcher/crash-rules),
/// where admins add rules without a launcher release. A backend rule with the same
/// key replaces the built-in one; Enabled=false switches it off.
/// </summary>
public sealed class CrashRule
{
    public string Key { get; set; } = string.Empty;
    // Higher first; ties keep the list order (built-ins before server additions).
    public int Priority { get; set; }
    // Case-insensitive .NET regexes over log tail + crash report. Every pattern in
    // PatternsAll must match; if PatternsAny is non-empty at least one must match too.
    // Named groups (?<file>...) become {file} placeholders in texts, labels and paths.
    public List<string> PatternsAll { get; set; } = new();
    public List<string> PatternsAny { get; set; } = new();
    // When non-empty, the process exit code must be one of these.
    public List<int> ExitCodes { get; set; } = new();
    // Placeholders: {name} or {name|fallback text} when the group did not match.
    public string Title { get; set; } = string.Empty;
    public string Cause { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public List<CrashRuleAction> Actions { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

public sealed class CrashRuleAction
{
    // One of CrashActionTypes.
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    // fix_files only: game-dir relative paths (placeholders allowed), restricted to config/.
    public List<string> Paths { get; set; } = new();
}

public static class CrashActionTypes
{
    // Executed by CoreHost.
    public const string FixFiles = "fix_files";
    public const string ResetConfig = "reset_config";
    public const string ResetAllConfigs = "reset_all_configs";
    public const string Repair = "repair";

    // Handled by the renderer.
    public const string OpenSettings = "open_settings";
    public const string Relaunch = "relaunch";
    public const string CopyReport = "copy_report";
    // Re-opens the last crash advice window (pre-launch reminder).
    public const string ShowCrash = "show_crash";

    public static readonly HashSet<string> All = new()
    {
        FixFiles, ResetConfig, ResetAllConfigs, Repair, OpenSettings, Relaunch, CopyReport, ShowCrash,
    };
}

/// <summary>Payload of the backend's /launcher/crash-rules (also cached on disk).</summary>
public sealed class CrashRulesPayload
{
    public int? RecommendedRamMb { get; set; }
    public List<CrashRule> Rules { get; set; } = new();
}
