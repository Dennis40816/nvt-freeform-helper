param(
    [ValidateSet("single-file", "folder")]
    [string]$Profile = "single-file",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "build/publish"
)

$ErrorActionPreference = "Stop"

$project = "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
$profileOut = Join-Path $OutputRoot "$Runtime/$Profile"

& (Join-Path $PSScriptRoot "fetch-core-packages.ps1")

# Restore on its own, without the publish properties: a single-file or single-runtime restore does not match
# the lock files, which fails a locked restore and rewrites the lock files otherwise. Directory.Build.props
# lists the runtimes this restore covers.
& dotnet restore $project

if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE."
}

$publishArgs = @(
    "publish", $project,
    "--no-restore",
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishTrimmed=false",
    "-o", $profileOut
)

if ($Profile -eq "single-file") {
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
} else {
    $publishArgs += "-p:PublishSingleFile=false"
}

Write-Host "Publishing FreeformHelper.UI..."
Write-Host "  Profile : $Profile"
Write-Host "  Runtime : $Runtime"
Write-Host "  Output  : $profileOut"

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exePath = Join-Path $profileOut "FreeformHelper.UI.exe"
if (Test-Path $exePath) {
    Write-Host "Done: $exePath"
} else {
    Write-Warning "Publish completed but EXE not found at expected path: $exePath"
}
