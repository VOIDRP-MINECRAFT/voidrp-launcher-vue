using System.Security.Cryptography;
using System.Text;

namespace VoidRpLauncher.CoreHost.Security;

internal static class LauncherProof
{
    // Must match LAUNCHER_HMAC_SECRET in backend .env.
    // Never expose this in public repositories.
    // Apply code obfuscation (e.g. ConfuserEx / .NET Reactor) to release builds.
    private const string HmacKey = "86b03b8adbf76bc0dd0269651e2c0cc0bf523fb1b71b8b806c06394d3c5b9e67";

    internal static string Compute(string rawTicket)
    {
        var key = Encoding.UTF8.GetBytes(HmacKey);
        var data = Encoding.UTF8.GetBytes(rawTicket);
        var hash = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
