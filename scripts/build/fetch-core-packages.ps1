param(
    [switch]$Offline
)

# Single entry for the NVT Core packages: downloads the packages that core-packages.json lists into
# artifacts/core-packages and verifies their SHA-256, so the restore that follows finds them through NuGet.config.
# Every script that restores calls this first; a failed download or hash check stops it before the restore.
# scripts/fetch_core_packages.py is Core's script, copied unchanged (see the pull request that added it).
# It downloads from the web path of the Release. When that path fails (for example HTTP 504), this script
# downloads the same assets again through the GitHub API and checks the same SHA-256 and the size from the API.

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$fetchScript = Join-Path $repoRoot "scripts/fetch_core_packages.py"
$manifest = Join-Path $repoRoot "core-packages.json"
$destination = Join-Path $repoRoot "artifacts/core-packages"
$maxPackageBytes = 256MB

function Get-ApiToken {
    foreach ($name in @("GITHUB_TOKEN", "GH_TOKEN")) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value.Trim()
        }
    }

    return $null
}

function Invoke-WithRetry {
    param([Parameter(Mandatory)][scriptblock]$Action, [Parameter(Mandatory)][string]$Description)

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            return & $Action
        }
        catch {
            if ($attempt -eq 3) {
                throw "$Description failed: $($_.Exception.Message)"
            }

            Start-Sleep -Seconds (2 * $attempt)
        }
    }
}

function Save-CorePackageThroughApi {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][object[]]$Packages,
        [Parameter(Mandatory)][string]$Destination
    )

    $headers = @{ "User-Agent" = "freeform-helper-fetch-core-packages"; "X-GitHub-Api-Version" = "2022-11-28" }
    $token = Get-ApiToken
    if ($null -ne $token) {
        $headers["Authorization"] = "Bearer $token"
    }

    $releases = @{}
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    foreach ($package in $Packages) {
        $context = "$($package.asset) ($($package.release))"
        $target = Join-Path $Destination $package.asset
        if ((Test-Path -LiteralPath $target) -and
            ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ceq $package.sha256)) {
            Write-Host "core-packages: ${context}: verified"
            continue
        }

        if (-not $releases.ContainsKey($package.release)) {
            $releaseUri = "https://api.github.com/repos/$Repository/releases/tags/$($package.release)"
            $releases[$package.release] = Invoke-WithRetry -Description "release lookup $releaseUri" -Action {
                Invoke-RestMethod -Uri $releaseUri -Headers $headers -TimeoutSec 60
            }
        }

        $asset = @($releases[$package.release].assets | Where-Object { $_.name -ceq $package.asset })
        if ($asset.Count -ne 1) {
            throw "core-packages: ${context}: the Release has $($asset.Count) assets with this name; expected 1."
        }

        $size = [long]$asset[0].size
        if ($size -le 0 -or $size -gt $maxPackageBytes) {
            throw "core-packages: ${context}: the API reports an asset size of $size bytes, outside 1 to $maxPackageBytes."
        }

        $assetUri = [string]$asset[0].url
        $downloadHeaders = $headers.Clone()
        $downloadHeaders["Accept"] = "application/octet-stream"
        $temporary = Join-Path $Destination ".$($package.asset).api.tmp"
        try {
            Invoke-WithRetry -Description "API download $assetUri" -Action {
                Invoke-WebRequest -Uri $assetUri -Headers $downloadHeaders -OutFile $temporary -TimeoutSec 120 | Out-Null
            }

            $actualSize = (Get-Item -LiteralPath $temporary).Length
            if ($actualSize -ne $size) {
                throw "core-packages: ${context}: size mismatch: the API reports $size bytes, downloaded $actualSize bytes."
            }

            $actual = (Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -cne $package.sha256) {
                throw "core-packages: ${context}: SHA-256 mismatch: expected $($package.sha256), actual $actual."
            }

            Move-Item -LiteralPath $temporary -Destination $target -Force
            Write-Host "core-packages: ${context}: downloaded through the API and verified"
        }
        finally {
            if (Test-Path -LiteralPath $temporary) {
                Remove-Item -LiteralPath $temporary -Force
            }
        }
    }

    Write-Host "core-packages: $Destination"
}

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
$primaryExitCode = $LASTEXITCODE
if ($primaryExitCode -eq 0) {
    return
}

# Exit code 1 is a failed download or check. Other codes are a manifest or usage error, which the API path cannot fix.
if ($Offline -or $primaryExitCode -ne 1) {
    throw "Downloading or verifying the NVT Core packages failed with exit code $primaryExitCode."
}

Write-Warning "The web download of the NVT Core packages failed (exit code $primaryExitCode). Downloading through the GitHub API."
$manifestData = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
try {
    Save-CorePackageThroughApi -Repository $manifestData.repository -Packages @($manifestData.packages) -Destination $destination
}
catch {
    throw "Downloading or verifying the NVT Core packages failed: the web download exited with code $primaryExitCode and the API download failed: $($_.Exception.Message)"
}
