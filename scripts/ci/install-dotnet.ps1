param(
    [string]$InstallDir = ""
)

# Installs the SDK pinned by global.json and the runtime the projects target into a repository-local
# folder, so a CI runner builds with the same toolchain regardless of what its image ships.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Microsoft's installer, pinned to a reviewed commit of dotnet/install-scripts.
$installerCommit = "cbd31355adcf0c63eaeff601fb2eaa5fd0778f2b"
$installerUri = "https://raw.githubusercontent.com/dotnet/install-scripts/$installerCommit/src/dotnet-install.ps1"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$globalJson = Get-Content -LiteralPath (Join-Path $repoRoot "global.json") -Raw | ConvertFrom-Json
$sdkVersion = [string]$globalJson.sdk.version
if ($sdkVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "global.json must pin a stable SDK version; found '$sdkVersion'."
}

$testProject = Join-Path $repoRoot "tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj"
$targetFramework = [regex]::Match((Get-Content -LiteralPath $testProject -Raw), '<TargetFramework>net(\d+\.\d+)</TargetFramework>')
if (-not $targetFramework.Success) {
    throw "Cannot read the target framework from $testProject."
}

$runtimeChannel = $targetFramework.Groups[1].Value

if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    $InstallDir = Join-Path $repoRoot ".dotnet"
}

$InstallDir = [System.IO.Path]::GetFullPath($InstallDir)
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("freeformhelper-dotnet-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
try {
    $installer = Join-Path $tempRoot "dotnet-install.ps1"
    Invoke-WebRequest -UseBasicParsing $installerUri -OutFile $installer -MaximumRetryCount 3 -RetryIntervalSec 5

    & $installer -Version $sdkVersion -InstallDir $InstallDir -NoPath
    if (-not $?) {
        throw "dotnet-install.ps1 failed for SDK $sdkVersion."
    }

    & $installer -Channel $runtimeChannel -Runtime dotnet -InstallDir $InstallDir -NoPath
    if (-not $?) {
        throw "dotnet-install.ps1 failed for runtime $runtimeChannel."
    }
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$dotnetExe = Join-Path $InstallDir "dotnet.exe"
$installedSdks = & $dotnetExe --list-sdks
if (-not ($installedSdks | Where-Object { $_ -match ('^' + [regex]::Escape($sdkVersion) + '\s') })) {
    throw ".NET SDK $sdkVersion was not found in $InstallDir after installation."
}

$env:DOTNET_ROOT = $InstallDir
$env:PATH = "$InstallDir$([System.IO.Path]::PathSeparator)$env:PATH"

# Later workflow steps start new shells; publish the toolchain through the runner's files.
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_PATH)) {
    $InstallDir | Out-File -FilePath $env:GITHUB_PATH -Encoding utf8 -Append
    "DOTNET_ROOT=$InstallDir" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
}

Write-Host "SDK: $(& $dotnetExe --version)"
Write-Host "Runtime channel: $runtimeChannel"
Write-Host "DOTNET_ROOT: $InstallDir"
