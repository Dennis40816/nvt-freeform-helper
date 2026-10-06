param(
    [switch]$NoTest,
    [switch]$SkipDependencyGraph,
    [switch]$AllowMissingExampleData
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "fetch-core-packages.ps1")

dotnet build FreeformHelper.sln
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

if (-not $SkipDependencyGraph) {
    & (Join-Path $PSScriptRoot "generate-dependency-graph.ps1")
}

if (-not $NoTest) {
    # Through the shared entry, so the example data check, hang detection and exit code handling apply.
    & (Join-Path $PSScriptRoot "..\tests\run-tests.ps1") -Group all -AllowMissingExampleData:$AllowMissingExampleData
}
