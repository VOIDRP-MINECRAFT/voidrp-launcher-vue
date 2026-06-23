param(
    [string]$ProjectRoot = $PSScriptRoot,
    [string]$SelfUpdateBaseUrl = "https://void-rp.ru/launcher/self-update",
    [string]$PublicLauncherFileName = "VoidRpLauncher.exe",
    [string]$UpdaterFileName = "VoidRpLauncher.Updater.exe",
    [string]$ExistingManifestPath = "",
    [switch]$SkipNpmInstall,
    [switch]$SkipDotnetRestore
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Не найдено требуемое приложение: $Name"
    }
}

function Remove-DirectorySafe {
    param([string]$Path)
    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }
}

function Ensure-Directory {
    param([string]$Path)
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-Sha256Upper {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToUpperInvariant()
}

function Get-AssemblyVersionFromSemVer {
    param([string]$SemVer)

    $baseVersion = $SemVer.Split("-")[0]
    $parts = $baseVersion.Split(".")
    $normalized = @()

    foreach ($part in $parts) {
        if ($part -match '^\d+$') {
            $normalized += $part
        } else {
            $normalized += "0"
        }
    }

    while ($normalized.Count -lt 3) {
        $normalized += "0"
    }

    return "$($normalized[0]).$($normalized[1]).$($normalized[2]).0"
}

function Load-ExistingManifest {
    param([string]$PreferredPath, [string]$FallbackPath)

    $candidatePaths = @()

    if (-not [string]::IsNullOrWhiteSpace($PreferredPath)) {
        $candidatePaths += $PreferredPath
    }

    if (-not [string]::IsNullOrWhiteSpace($FallbackPath)) {
        $candidatePaths += $FallbackPath
    }

    foreach ($candidate in $candidatePaths) {
        if (Test-Path $candidate) {
            Write-Host "Используем существующий manifest для merge: $candidate" -ForegroundColor Yellow
            return (Get-Content -Raw -Path $candidate | ConvertFrom-Json -AsHashtable)
        }
    }

    return @{
        version   = ""
        notes     = ""
        artifacts = @{}
    }
}

Write-Step "Проверяем окружение"
Require-Command "dotnet"
Require-Command "npm"
Require-Command "npx"

Set-Location $ProjectRoot

$packageJsonPath        = Join-Path $ProjectRoot "package.json"
$electronBuilderPath    = Join-Path $ProjectRoot "electron-builder.yml"
$coreProjectPath        = Join-Path $ProjectRoot "core/VoidRpLauncher.CoreHost/VoidRpLauncher.CoreHost.csproj"
$updaterProjectPath     = Join-Path $ProjectRoot "VoidRpLauncher.Updater/VoidRpLauncher.Updater.csproj"

if (-not (Test-Path $packageJsonPath))     { throw "Не найден package.json: $packageJsonPath" }
if (-not (Test-Path $electronBuilderPath)) { throw "Не найден electron-builder.yml: $electronBuilderPath" }
if (-not (Test-Path $coreProjectPath))     { throw "Не найден CoreHost csproj: $coreProjectPath" }
if (-not (Test-Path $updaterProjectPath))  { throw "Не найден Updater csproj: $updaterProjectPath" }

$package = Get-Content -Raw -Path $packageJsonPath | ConvertFrom-Json
$version = [string]$package.version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Не удалось определить version из package.json"
}

$assemblyVersion = Get-AssemblyVersionFromSemVer $version

Write-Host "Version: $version"
Write-Host "AssemblyVersion: $assemblyVersion"

$buildCoreOut            = Join-Path $ProjectRoot "build/core/win-x64"
$updaterPublishOut       = Join-Path $ProjectRoot "build/updater/win-x64"
$distReleaseDir          = Join-Path $ProjectRoot "dist-release"
$distRendererDir         = Join-Path $ProjectRoot "dist-renderer"
$distElectronDir         = Join-Path $ProjectRoot "dist-electron"
$distSelfUpdateDir       = Join-Path $ProjectRoot "dist-self-update"
$distSelfUpdateWinDir    = Join-Path $distSelfUpdateDir "win-x64"
$distDeployLauncherDir   = Join-Path $ProjectRoot "dist-deploy/launcher"
$distDeploySelfUpdateDir = Join-Path $distDeployLauncherDir "self-update"
$distDeploySelfUpdateWin = Join-Path $distDeploySelfUpdateDir "win-x64"

Write-Step "Чистим старые release-артефакты"
Remove-DirectorySafe $buildCoreOut
Remove-DirectorySafe $updaterPublishOut
Remove-DirectorySafe $distReleaseDir
Remove-DirectorySafe $distRendererDir
Remove-DirectorySafe $distElectronDir
Remove-DirectorySafe $distSelfUpdateDir
Remove-DirectorySafe $distDeployLauncherDir

Ensure-Directory $buildCoreOut
Ensure-Directory $updaterPublishOut
Ensure-Directory $distSelfUpdateWinDir
Ensure-Directory $distDeploySelfUpdateWin

if (-not $SkipNpmInstall) {
    if (-not (Test-Path (Join-Path $ProjectRoot "node_modules"))) {
        Write-Step "node_modules не найден, ставим npm зависимости"
        npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci завершился с ошибкой" }
    } else {
        Write-Step "node_modules уже существует, npm ci пропускаем"
    }
} else {
    Write-Step "Пропускаем npm install"
}

if (-not $SkipDotnetRestore) {
    Write-Step "Делаем dotnet restore"
    dotnet restore $coreProjectPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore CoreHost завершился с ошибкой" }

    dotnet restore $updaterProjectPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore Updater завершился с ошибкой" }
} else {
    Write-Step "Пропускаем dotnet restore"
}

Write-Step "Публикуем CoreHost в Release для упаковки в Electron"
dotnet publish $coreProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $buildCoreOut `
    /p:Version=$version `
    /p:AssemblyVersion=$assemblyVersion `
    /p:FileVersion=$assemblyVersion `
    /p:PublishSingleFile=false `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw "dotnet publish CoreHost завершился с ошибкой" }

Write-Step "Публикуем кастомный Updater в single-file"
dotnet publish $updaterProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $updaterPublishOut `
    /p:Version=$version `
    /p:AssemblyVersion=$assemblyVersion `
    /p:FileVersion=$assemblyVersion `
    /p:PublishSingleFile=true `
    /p:EnableCompressionInSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) { throw "dotnet publish Updater завершился с ошибкой" }

$updaterExePath = Join-Path $updaterPublishOut $UpdaterFileName
if (-not (Test-Path $updaterExePath)) {
    $candidateUpdater = Get-ChildItem -Path $updaterPublishOut -Filter "*.exe" -File | Select-Object -First 1
    if ($null -eq $candidateUpdater) {
        throw "Не найден exe Updater после publish: $updaterPublishOut"
    }
    $updaterExePath = $candidateUpdater.FullName
}

Write-Step "Проверяем TypeScript toolchain"
$tscCmd = Join-Path $ProjectRoot "node_modules/.bin/tsc.cmd"
if (-not (Test-Path $tscCmd)) {
    throw "Не найден local TypeScript compiler: $tscCmd. Выполни npm install в корне проекта."
}

Write-Step "Собираем Electron main/preload"
npm run build:electron
if ($LASTEXITCODE -ne 0) { throw "npm run build:electron завершился с ошибкой" }

Write-Step "Собираем Vue renderer"
npm run build:renderer
if ($LASTEXITCODE -ne 0) { throw "npm run build:renderer завершился с ошибкой" }

Write-Step "Собираем Windows portable launcher"
$env:VOIDRP_LAUNCHER_VERSION = $version
npx electron-builder --win portable --publish never
if ($LASTEXITCODE -ne 0) { throw "electron-builder завершился с ошибкой" }

$portableArtifact = Get-ChildItem -Path $distReleaseDir -Filter "*portable.exe" -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $portableArtifact) {
    throw "Не найден portable exe в $distReleaseDir"
}

$canonicalLauncherPath = Join-Path $distSelfUpdateWinDir $PublicLauncherFileName
Copy-Item -Path $portableArtifact.FullName -Destination $canonicalLauncherPath -Force

$canonicalUpdaterPath = Join-Path $distSelfUpdateWinDir $UpdaterFileName
Copy-Item -Path $updaterExePath -Destination $canonicalUpdaterPath -Force

Write-Step "Собираем self-update manifest"
$manifestOutputPath = Join-Path $distSelfUpdateDir "manifest.json"
$mergedManifest = Load-ExistingManifest `
    -PreferredPath $ExistingManifestPath `
    -FallbackPath $manifestOutputPath

if (-not $mergedManifest.ContainsKey("artifacts")) {
    $mergedManifest["artifacts"] = @{}
}

$launcherSha = Get-Sha256Upper $canonicalLauncherPath
$updaterSha  = Get-Sha256Upper $canonicalUpdaterPath

$mergedManifest["version"] = $version
$mergedManifest["notes"]   = "Auto-generated on $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')"
$mergedManifest["artifacts"]["win-x64"] = @{
    launcher = @{
        kind   = "single-file"
        url    = "$SelfUpdateBaseUrl/win-x64/$PublicLauncherFileName"
        sha256 = $launcherSha
    }
    updater = @{
        kind   = "single-file"
        url    = "$SelfUpdateBaseUrl/win-x64/$UpdaterFileName"
        sha256 = $updaterSha
    }
}

$manifestJson = $mergedManifest | ConvertTo-Json -Depth 20
Set-Content -Path $manifestOutputPath -Value $manifestJson -Encoding UTF8

Write-Step "Готовим deploy-папку для выгрузки на сервер"
Ensure-Directory $distDeployLauncherDir
Ensure-Directory $distDeploySelfUpdateDir
Ensure-Directory $distDeploySelfUpdateWin

Copy-Item -Path $canonicalLauncherPath -Destination (Join-Path $distDeployLauncherDir $PublicLauncherFileName) -Force
Copy-Item -Path $manifestOutputPath -Destination (Join-Path $distDeploySelfUpdateDir "manifest.json") -Force
Copy-Item -Path $canonicalLauncherPath -Destination (Join-Path $distDeploySelfUpdateWin $PublicLauncherFileName) -Force
Copy-Item -Path $canonicalUpdaterPath -Destination (Join-Path $distDeploySelfUpdateWin $UpdaterFileName) -Force

Write-Step "Готово"
Write-Host "Portable artifact:      $($portableArtifact.FullName)" -ForegroundColor Green
Write-Host "Self-update launcher:   $canonicalLauncherPath" -ForegroundColor Green
Write-Host "Self-update updater:    $canonicalUpdaterPath" -ForegroundColor Green
Write-Host "Self-update manifest:   $manifestOutputPath" -ForegroundColor Green
Write-Host "Deploy root:            $distDeployLauncherDir" -ForegroundColor Green

Write-Host ""
Write-Host "Загружать на сервер отсюда:" -ForegroundColor Yellow
Write-Host "  $distDeployLauncherDir"
Write-Host ""
Write-Host "Структура для сервера будет такой:"
Write-Host "  launcher/"
Write-Host "    $PublicLauncherFileName"
Write-Host "    self-update/"
Write-Host "      manifest.json"
Write-Host "      win-x64/"
Write-Host "        $PublicLauncherFileName"
Write-Host "        $UpdaterFileName"