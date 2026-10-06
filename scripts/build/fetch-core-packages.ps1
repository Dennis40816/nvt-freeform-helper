param(
    [switch]$Offline
)

# Single entry for the NVT Core packages: downloads the packages that core-packages.json lists into
# artifacts/core-packages and verifies their SHA-256, so the restore that follows finds them through NuGet.config.
# Every script that restores calls this first; a failed download or hash check stops it before the restore.
# scripts/fetch_core_packages.py is Core's script, copied unchanged (see the pull request that added it).

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$fetchScript = Join-Path $repoRoot "scripts/fetch_core_packages.py"
$manifest = Join-Path $repoRoot "core-packages.json"

$python = Get-Command python -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
$pythonArgs = @()
if ($null -eq $python) {
    $python = Get-Command py -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    $pythonArgs = @("-3")
}
if ($null -eq $python) {
    throw "Python 3.10 or later is required to download the NVT Core packages (python or py on PATH)."
}

$fetchArgs = @($pythonArgs) + @("-B", $fetchScript, "--manifest", $manifest)
if ($Offline) {
    $fetchArgs += "--offline"
}

& $python.Source @fetchArgs
if ($LASTEXITCODE -ne 0) {
    throw "Downloading or verifying the NVT Core packages failed with exit code $LASTEXITCODE."
}
