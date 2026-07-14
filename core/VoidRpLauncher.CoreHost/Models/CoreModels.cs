using System;
using System.Collections.Generic;

namespace VoidRpLauncher.CoreHost.Models;

public sealed class LauncherManifest
{
    public string PackName { get; set; } = string.Empty;
    public string PackVersion { get; set; } = string.Empty;
    public string PackDisplayVersion { get; set; } = string.Empty;
    public string LauncherProfileId { get; set; } = string.Empty;
    public string BuildDateUtc { get; set; } = string.Empty;
    public string MinecraftVersion { get; set; } = string.Empty;
    public string Loader { get; set; } = string.Empty;
    public int JavaVersion { get; set; }
    public string MinLauncherVersion { get; set; } = string.Empty;
    public bool FullSyncFallback { get; set; }
    public string Notes { get; set; } = string.Empty;
    public LauncherServerInfo Server { get; set; } = new();
    public List<LauncherManifestFile> Files { get; set; } = new();
}

public sealed class LauncherManifestFile
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool AlwaysOverwrite { get; set; }
    // Managed asset: hash-synced even if it lives under a player-writable dir (config/).
    // Downloaded when missing or when its SHA-256 differs from the manifest, but not
    // force-overwritten blindly — so shipping a new version updates existing clients,
    // while a matching local copy is skipped (no needless re-download).
    public bool Managed { get; set; }
    public bool Optional { get; set; }
    public bool Required { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class LauncherServerInfo
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
}

public sealed class LauncherUpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public Dictionary<string, LauncherUpdatePlatformArtifacts> Artifacts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LauncherUpdatePlatformArtifacts
{
    public LauncherUpdateArtifact Launcher { get; set; } = new();
    public LauncherUpdateArtifact Updater { get; set; } = new();
}

public sealed class LauncherUpdateArtifact
{
    public string Kind { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class LaunchProgressInfo
{
    public string Stage { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public double Percent { get; init; }
}

public sealed class SyncProgressInfo
{
    public string Stage { get; init; } = string.Empty;
    public string CurrentFile { get; init; } = string.Empty;
    public int ProcessedFiles { get; init; }
    public int TotalFiles { get; init; }
    public double Percent { get; init; }
    public string DetailMessage { get; init; } = string.Empty;
    public long CurrentFileBytesDownloaded { get; init; }
    public long CurrentFileBytesTotal { get; init; }
    public string LocalPath { get; init; } = string.Empty;
    public string SourceUrl { get; init; } = string.Empty;
}

public sealed class LauncherUserSettings
{
    public int MaxRamMb { get; set; } = 4096;
    public List<string> DisabledMods { get; set; } = new();
}



