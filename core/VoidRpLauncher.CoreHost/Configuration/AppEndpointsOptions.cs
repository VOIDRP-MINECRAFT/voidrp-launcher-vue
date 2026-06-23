namespace VoidRpLauncher.CoreHost.Configuration;

public sealed class AppEndpointsOptions
{
    public string AccountApiBaseUrl { get; set; } = string.Empty;
    public string PackManifestUrl { get; set; } = string.Empty;
    public string SelfUpdateManifestUrl { get; set; } = string.Empty;
    public string RuntimeSeedUrl { get; set; } = string.Empty;
    public string RuntimeManifestBaseUrl { get; set; } = string.Empty;
    public string RegisterUrl { get; set; } = string.Empty;
    public string ForgotPasswordUrl { get; set; } = string.Empty;
    public string VerifyEmailUrl { get; set; } = string.Empty;
}
