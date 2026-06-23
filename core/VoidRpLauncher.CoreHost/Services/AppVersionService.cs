using System.Reflection;

namespace VoidRpLauncher.CoreHost.Services;

public sealed class AppVersionService
{
    public string CurrentVersion =>
        Environment.GetEnvironmentVariable("VOIDRP_LAUNCHER_VERSION")
        ?? (Assembly.GetEntryAssembly()?.GetName().Version ?? Assembly.GetExecutingAssembly().GetName().Version)?.ToString(3)
        ?? "0.0.0";

    public bool IsCurrentVersionAtLeast(string versionText)
        => ParseVersionOrZero(CurrentVersion) >= ParseVersionOrZero(versionText);

    private static Version ParseVersionOrZero(string value)
        => Version.TryParse(value, out var version) ? version : new Version(0, 0, 0, 0);
}



