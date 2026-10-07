param(
    [ValidateSet("all", "workflow", "application", "notch-core", "notch-golden", "infrastructure", "ui-core", "ui-stable", "ui-viewmodel", "ui-snapshots", "smoke", "uncategorized")]
    [string]$Group = "all",
    [string]$Configuration = "Debug",
    [switch]$UseNoAppHost,
    [switch]$ValidateOnly,
    [switch]$AllowMissingExampleData
)

$ErrorActionPreference = "Stop"

$project = "tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj"

function New-FilterExpression {
    param(
        [string[]]$ClassNames
    )

    if ($null -eq $ClassNames -or $ClassNames.Count -eq 0) {
        return $null
    }

    # The dots anchor the match to a whole class name, so a class whose name merely contains a listed
    # name is not pulled into that group.
    return ($ClassNames | ForEach-Object { "FullyQualifiedName~.$_." }) -join "|"
}

$notchGoldenClasses = @(
    "NotchExampleCExportDriftTests",
    "NotchGoldenBaselineTests",
    "Tm81NotchAcceptanceMatrixTests"
)

$notchCoreClasses = @(
    $notchGoldenClasses
    "CadPadGeometrySignatureTests",
    "NotchExportGenerationCacheServiceTests",
    "NotchTableExporterTests",
    "NotchTableGeneratorTests",
    "NotchV22CompensationServiceTests",
    "NotchV22TargetAllocationServiceTests",
    "NotchV22ResolvedResultServiceTests",
    "NotchV22FinalOutlineServiceTests",
    "NotchDiffIdentityPipelineTests",
    "NotchCadOutputFwDiffProjectionServiceTests",
    "NotchApplySimulationServiceTests",
    "NotchApplySimulationReviewUseCaseTests",
    "NotchSettingsTests",
    "RuntimeQueryUseCaseTests"
)

$applicationClasses = @(
    "CadAreaBucketServiceTests",
    "CadLoadUseCaseTests",
    "CadPadGeometrySignatureTests",
    "CadPadUnionServiceTests",
    "DxfIndexAssignerTests",
    "CadOutputFwDiffIndexAssignmentServiceTests",
    "DxfLayerImageExportServiceTests",
    "DxfOverlapAnalyzerTests",
    "DxfPadImporterTests",
    "DxfRegularLayerGridBuilderTests",
    "DxfRegularMappingAnalyzerTests",
    "FreeformDetectorTests",
    "FreeformStatisticsBuilderTests",
    "NotchTableExporterTests",
    "NotchTableGeneratorTests",
    "NotchV22CompensationServiceTests",
    "PadMatcherTests",
    "PadOverrideServiceTests",
    "RegularGridBuilderTests",
    "ProjectFileMigrationTests",
    "ProjectPersistenceUseCaseTests",
    "ProjectStoreTests",
    "ManualSizingServiceTests",
    "ManualSizingUseCaseTests",
    "SizingScopeDefaultsTests",
    "WorkflowPipelineServiceTests"
)

$infrastructureClasses = @(
    "ConsoleLinkParserTests",
    "AppLogFormattingTests",
    "LoggingConfigurationTests"
)

# FreeformHelperViewModelTests is unstable on machines with few cores (ROADMAP.md S15.002), so CI runs it
# as its own non-blocking shard. 'ui-core' stays the union of both lists for the local gate.
$uiViewModelClasses = @(
    "FreeformHelperViewModelTests"
)

$uiStableClasses = @(
    "HeadlessUiSmokeTests",
    "PadCanvasCacheInvalidationTests",
    "PadCanvasHitTestTests",
    "IndexMappingReportViewModelTests",
    "IndexMappingSettingsTests",
    "NotchDetailUseCaseTests",
    "NotchExportSelectionViewModelTests",
    "PadInfoViewModelTests",
    "WorkspaceInteractionStateTests"
)

$uiCoreClasses = @(
    $uiViewModelClasses
    $uiStableClasses
)

$uiSnapshotClasses = @(
    "UiLayoutGuardTests",
    "UiRenderedVisualSnapshotTests",
    "UiSnapshotPersistenceContractTests",
    "UiVisualSnapshotTests"
)

$smokeClasses = @(
    "HeadlessUiSmokeTests",
    "WorkflowPipelineServiceTests"
)

# The groups the gates run. 'smoke' and 'workflow' are subsets for quick local checks and are left out on
# purpose: a class named only there must still land in 'uncategorized'.
$listedClasses = @(
    $notchCoreClasses
    $applicationClasses
    $infrastructureClasses
    $uiCoreClasses
    $uiSnapshotClasses
) | Sort-Object -Unique

# A renamed or deleted class would silently shrink its group, so every listed name must still exist.
$testRoot = Join-Path $PSScriptRoot "..\..\tests\FreeformHelper.Tests"
$declaredClasses = Get-ChildItem -LiteralPath $testRoot -Recurse -Filter *.cs |
    Select-String -Pattern '\bclass\s+([A-Za-z0-9_]+)' |
    ForEach-Object { $_.Matches[0].Groups[1].Value } |
    Sort-Object -Unique
$staleClasses = @(@($listedClasses + $smokeClasses) | Sort-Object -Unique | Where-Object { $declaredClasses -notcontains $_ })
if ($staleClasses.Count -gt 0) {
    throw "Test group lists name classes that no longer exist: $($staleClasses -join ', ')"
}

if ($ValidateOnly) {
    Write-Host "Test group lists are valid ($($listedClasses.Count) listed classes)."
    return
}

# Every test no list names lands here, so the named groups plus 'uncategorized' always cover 'all'.
$uncategorizedFilter = ($listedClasses | ForEach-Object { "FullyQualifiedName!~.$_." }) -join "&"

$filter = switch ($Group) {
    "all" { $null }
    "workflow" { New-FilterExpression @("WorkflowPipelineServiceTests") }
    "application" { New-FilterExpression $applicationClasses }
    "notch-core" { New-FilterExpression $notchCoreClasses }
    "notch-golden" { New-FilterExpression $notchGoldenClasses }
    "infrastructure" { New-FilterExpression $infrastructureClasses }
    "ui-core" { New-FilterExpression $uiCoreClasses }
    "ui-stable" { New-FilterExpression $uiStableClasses }
    "ui-viewmodel" { New-FilterExpression $uiViewModelClasses }
    "ui-snapshots" { New-FilterExpression $uiSnapshotClasses }
    "smoke" { New-FilterExpression $smokeClasses }
    "uncategorized" { $uncategorizedFilter }
}

$args = @(
    "test",
    $project,
    "-c", $Configuration,
    "--nologo",
    # A stuck test becomes a named failure instead of a run that never ends; no dump, to save disk space.
    "--blame-hang",
    "--blame-hang-timeout", "5m",
    "--blame-hang-dump-type", "none"
)

if ($UseNoAppHost) {
    $args += "/p:UseAppHost=false"
}

if (-not [string]::IsNullOrWhiteSpace($filter)) {
    $args += @("--filter", $filter)
}

Write-Host "Running test group '$Group' ($Configuration)..."
Write-Host "Command: dotnet $($args -join ' ')"

# Tests that read the private example data skip when it is absent. A gate must not pass that way, so the
# data is required unless the caller says it has no access. Data that is present is always checked, so
# -AllowMissingExampleData never lets a drifted or modified checkout through.
$requireExampleDataEnv = "FREEFORMHELPER_REQUIRE_EXAMPLE_DATA"
$previousRequireExampleData = [Environment]::GetEnvironmentVariable($requireExampleDataEnv)
$exampleDataPresent = & (Join-Path $PSScriptRoot "assert-example-data.ps1") -AllowMissing:$AllowMissingExampleData

$testArea = $env:FREEFORMHELPER_TEST_AREA
if ([string]::IsNullOrWhiteSpace($testArea)) {
    if (Test-Path -LiteralPath "D:\" -PathType Container) {
        $testArea = "D:\FreeformHelper-TestArea"
    } else {
        Write-Host "Test area drive D: is unavailable; keeping the system temporary environment."
    }
}

$previousTemp = [Environment]::GetEnvironmentVariable("TEMP")
$previousTmp = [Environment]::GetEnvironmentVariable("TMP")
$previousTmpDir = [Environment]::GetEnvironmentVariable("TMPDIR")

try {
    if (-not [string]::IsNullOrWhiteSpace($testArea)) {
        $testTempPath = (New-Item -ItemType Directory -Path (Join-Path $testArea "temp") -Force).FullName
        [Environment]::SetEnvironmentVariable("TEMP", $testTempPath)
        [Environment]::SetEnvironmentVariable("TMP", $testTempPath)
        [Environment]::SetEnvironmentVariable("TMPDIR", $testTempPath)
    }
    [Environment]::SetEnvironmentVariable($requireExampleDataEnv, $(if ($exampleDataPresent) { "1" } else { [NullString]::Value }))
    dotnet @args
    $testExitCode = $LASTEXITCODE
}
finally {
    [Environment]::SetEnvironmentVariable($requireExampleDataEnv, ($previousRequireExampleData ?? [NullString]::Value))
    [Environment]::SetEnvironmentVariable("TEMP", ($previousTemp ?? [NullString]::Value))
    [Environment]::SetEnvironmentVariable("TMP", ($previousTmp ?? [NullString]::Value))
    [Environment]::SetEnvironmentVariable("TMPDIR", ($previousTmpDir ?? [NullString]::Value))
}

# A failing native command does not stop the script by itself, so callers would report the stage as done.
if ($testExitCode -ne 0) {
    throw "Test group '$Group' failed with exit code $testExitCode."
}
