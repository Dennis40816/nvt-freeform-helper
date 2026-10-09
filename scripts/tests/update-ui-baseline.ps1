param(
    [ValidateSet("DryRun", "Apply")]
    [string]$Mode = "DryRun",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    # Use existing test binaries without building or restoring packages.
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$testProject = Join-Path $repoRoot "tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj"

$modeValue = if ($Mode -eq "Apply") { "apply" } else { "dry-run" }
$env:FH_UI_BASELINE_MODE = $modeValue

Write-Host "UI baseline mode: $Mode ($modeValue)"
Write-Host "Project: $testProject"

try {
    $filters = @(
        "FullyQualifiedName~UiVisualSnapshotTests",
        "FullyQualifiedName~UiRenderedVisualSnapshotTests"
    )

    for ($i = 0; $i -lt $filters.Count; $i++) {
        $filter = $filters[$i]
        $args = @("test", $testProject, "-c", $Configuration, "--nologo", "--filter", $filter)
        if ($NoBuild -or $i -gt 0) {
            $args += "--no-build"
        }

        Write-Host ("Running: dotnet {0}" -f ($args -join " "))
        & dotnet @args
        if ($LASTEXITCODE -ne 0) {
            throw "UI baseline update failed on filter: $filter"
        }
    }

    if ($Mode -eq "Apply") {
        Write-Host "UI baseline apply completed."
    }
    else {
        Write-Host "UI baseline dry-run completed (no file written)."
    }
}
finally {
    Remove-Item Env:FH_UI_BASELINE_MODE -ErrorAction SilentlyContinue
}
