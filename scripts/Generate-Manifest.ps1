param(
    [string]$PackRoot = "E:\VoidRP\LauncherDist\pack",
    [string]$OutputFile = "E:\VoidRP\LauncherDist\manifests\manifest.json",
    [string]$BaseUrl = "https://void-rp.ru/launcher/pack",
    [string]$PackName = "VoidRP Better MC 5",
    [string]$PackVersion = "1.0.0",
    [string]$MinecraftVersion = "1.21.1",
    [string]$Loader = "neoforge",
    [int]$JavaVersion = 21,
    [string]$ServerHost = "void-rp.ru",
    [int]$ServerPort = 25565,
    [string]$MinLauncherVersion = "0.1.0",
    [string]$PackDisplayVersion = "VOID-RP",
    [string]$LauncherProfileId = "neoforge-21.1.218",
    [string]$NeoForgeVersion = "21.1.218",
    [string]$FmlVersion = "4.0.42",
    [string]$NeoFormVersion = "20240808.144430"
)

$ErrorActionPreference = "Stop"

# Paths under these prefixes are always re-downloaded even if the player has
# modified them locally (alwaysOverwrite bypasses the IsPlayerWritable guard
# in CoreServices.NeedsDownload).
$alwaysOverwritePrefixes = @(
    "config/fancymenu/"
)

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$TargetPath
    )

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        throw "BasePath is null or empty"
    }

    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        throw "TargetPath is null or empty"
    }

    $baseFull = [System.IO.Path]::GetFullPath($BasePath)
    $targetFull = [System.IO.Path]::GetFullPath($TargetPath)

    $baseUri = [System.Uri]::new(($baseFull.TrimEnd('\') + '\'))
    $targetUri = [System.Uri]::new($targetFull)

    $relativeUri = $baseUri.MakeRelativeUri($targetUri)
    if ($null -eq $relativeUri) {
        throw "Failed to create relative URI for target: $TargetPath"
    }

    $relativePath = [System.Uri]::UnescapeDataString($relativeUri.ToString())
    if ([string]::IsNullOrWhiteSpace($relativePath)) {
        throw "Relative path resolved to null/empty for target: $TargetPath"
    }

    return ($relativePath -replace '\\', '/')
}

function Get-EncodedUrlPath {
    param(
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        throw "RelativePath is null or empty"
    }

    $segments = $RelativePath -split '/'
    $encodedSegments = @()

    foreach ($segment in $segments) {
        if ($null -eq $segment) {
            throw "Null segment found while encoding URL path: $RelativePath"
        }

        $encodedSegments += [System.Uri]::EscapeDataString([string]$segment)
    }

    return ($encodedSegments -join '/')
}

function Should-ExcludeFile {
    param(
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $true
    }

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')

    $excludedPrefixes = @(
        ".mixin.out/",
        "logs/",
        "log/",
        "crash-reports/",
        "screenshots/",
        "saves/",
        "downloads/",
        "tmp/",
        "temp/",
        "debug/",
        "fancymenu_data/",
        "local/",
        "dynamic-resource-pack-cache/",
        "moddata/",
        "moonlight-global-datapacks/",
        "patchouli_books/",
        "server-resource-packs/",
        "natives/",
        "telemetry/",
        "journeymap/data/",
        "xaeroworldmap/",
        "xaerominimap/",
        "xaero/",
        "mods/.connector/"
    )

    foreach ($prefix in $excludedPrefixes) {
        if ($normalized.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    $fileName = [System.IO.Path]::GetFileName($normalized)
    if ([string]::IsNullOrWhiteSpace($fileName)) {
        return $true
    }

    $excludedExactFiles = @(
        ".ds_store",
        "thumbs.db",
        "desktop.ini",
        "usercache.json",
        "servers.dat_old",
        "command_history.txt",
        "patchouli_data.json",
        "immersivetips.json",
        "hash.txt"
    )

    foreach ($name in $excludedExactFiles) {
        if ($fileName.Equals($name, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    $excludedPatterns = @(
        "*.tmp",
        "*.bak",
        "*.log",
        "*.log.gz",
        "*.info",
        "*.pid",
        "win_event*.txt",
        "renderer_pid*.tmp",
        "successful_launch_pid*.tmp"
    )

    foreach ($pattern in $excludedPatterns) {
        if ($fileName -like $pattern) {
            return $true
        }
    }

    return $false
}

function Should-AlwaysOverwrite {
    param(
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace('\', '/').TrimStart('/')

    foreach ($prefix in $alwaysOverwritePrefixes) {
        if ($normalized.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

if (-not (Test-Path -LiteralPath $PackRoot)) {
    throw "PackRoot not found: $PackRoot"
}

$packRootFull = [System.IO.Path]::GetFullPath($PackRoot)
$outputDir = Split-Path -Path $OutputFile -Parent

if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$files = New-Object System.Collections.Generic.List[object]
$processed = 0
$skipped = 0
$errors = 0
$alwaysOverwriteCount = 0

Get-ChildItem -LiteralPath $packRootFull -Recurse -File -Force | ForEach-Object {
    $currentItem = $_

    try {
        if ($null -eq $currentItem) {
            throw "Pipeline item is null"
        }

        $fullPath = $currentItem.FullName
        if ([string]::IsNullOrWhiteSpace($fullPath)) {
            throw "FullName is null or empty"
        }

        $relativePath = Get-RelativePath -BasePath $packRootFull -TargetPath $fullPath

        if (Should-ExcludeFile -RelativePath $relativePath) {
            $skipped++
            return
        }

        $hashResult = Get-FileHash -LiteralPath $fullPath -Algorithm SHA256
        if ($null -eq $hashResult) {
            throw "Get-FileHash returned null"
        }

        if ([string]::IsNullOrWhiteSpace($hashResult.Hash)) {
            throw "Hash is null or empty"
        }

        $hash = $hashResult.Hash.ToUpperInvariant()
        $size = [int64]$currentItem.Length
        $encodedPath = Get-EncodedUrlPath -RelativePath $relativePath
        $url = "$BaseUrl/$encodedPath"

        $overwrite = Should-AlwaysOverwrite -RelativePath $relativePath

        $entry = [PSCustomObject]@{
            path   = $relativePath
            size   = $size
            sha256 = $hash
            url    = $url
        }

        if ($overwrite) {
            $entry | Add-Member -MemberType NoteProperty -Name alwaysOverwrite -Value $true
            $alwaysOverwriteCount++
        }

        $files.Add($entry)
        $processed++
    }
    catch {
        $errors++
        Write-Warning "FAILED FILE: $($currentItem.FullName)"
        Write-Warning "REASON: $($_.Exception.Message)"
    }
}

$sortedFiles = $files | Sort-Object path

$manifest = [PSCustomObject]@{
    packName           = $PackName
    packVersion        = $PackVersion
    packDisplayVersion = $PackDisplayVersion
    launcherProfileId  = $LauncherProfileId
    neoForgeVersion    = $NeoForgeVersion
    fmlVersion         = $FmlVersion
    neoFormVersion     = $NeoFormVersion
    buildDateUtc       = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    minecraftVersion   = $MinecraftVersion
    loader             = $Loader
    javaVersion        = $JavaVersion
    minLauncherVersion = "0.1.0"
    fullSyncFallback   = $true
    notes              = "VoidRP launcher manifest for Better MC 5 NeoForge client"
    server             = [PSCustomObject]@{
        host = $ServerHost
        port = $ServerPort
    }
    files              = $sortedFiles
}

$manifest | ConvertTo-Json -Depth 20 | Set-Content -Path $OutputFile -Encoding UTF8

Write-Host ""
Write-Host "Manifest generated successfully:"
Write-Host $OutputFile
Write-Host "Processed files:       $processed"
Write-Host "Skipped files:         $skipped"
Write-Host "Errors:                $errors"
Write-Host "Always-overwrite:      $alwaysOverwriteCount  (config/fancymenu/)"
Write-Host "Total in manifest:     $($sortedFiles.Count)"
Write-Host ""
