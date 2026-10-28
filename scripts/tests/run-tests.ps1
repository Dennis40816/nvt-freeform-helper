param(
    [ValidateSet("all", "workflow", "application", "notch-core", "notch-golden", "infrastructure", "ui-core", "ui-stable", "ui-viewmodel", "ui-snapshots", "smoke", "uncategorized")]
    [string]$Group = "all",
    [string]$Configuration = "Debug",
    [switch]$UseNoAppHost,
    [switch]$ValidateOnly,
    [switch]$AllowMissingExampleData,
    # Internal CI result collection. Policy and summaries belong to verify.ps1, which owns shards.
    [string]$ResultsDirectory = "",
    # Internal CI status channel: the native exit code and fixed host-error labels, never tool output.
    [System.Collections.IDictionary]$RunStatus
)

$ErrorActionPreference = "Stop"
if ($null -ne $RunStatus) {
    $RunStatus['ExitCode'] = $null
    $RunStatus['HostErrors'] = @()
}

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

# Fixed labels for tool messages that explain why a test run ended without results. The run output can hold
# private example data, so only these label strings leave the run, never a matched line.
$hostErrorPatterns = [ordered]@{
    'test-host-crashed' = 'test host process crashed|testhost.{0,40}crashed'
    'test-run-aborted' = 'test run aborted|active test run was aborted'
    'hang-timeout' = 'hang timeout|hang detected|blame.{0,40}hang'
    'test-host-start-failed' = 'failed to (launch|start) testhost|could not (find|load).{0,40}testhost|testhost process.{0,60}exited'
    'no-test-matches-filter' = 'no test matches the given testcase filter'
    'no-test-available' = 'no test is available'
    'dispatcher-uithread-race' = 'another thread created dispatcher\.uithread'
    'fatal-runtime-error' = 'stack overflow|out of memory|fatal error'
    'unhandled-exception' = 'unhandled exception\.'
    'build-error' = '\berror (MSB|CS|NETSDK|NU)[0-9]{3,5}\b'
}
$maxHostErrorLabels = 6

function Get-HostErrorLabels {
    param(
        [object[]]$Output
    )

    $labels = [System.Collections.Generic.List[string]]::new()
    foreach ($item in $Output) {
        $line = [string]$item
        foreach ($label in $hostErrorPatterns.Keys) {
            if ($labels.Contains($label) -or $line -notmatch $hostErrorPatterns[$label]) { continue }
            $labels.Add($label)
            if ($labels.Count -ge $maxHostErrorLabels) { return $labels.ToArray() }
        }
    }

    return $labels.ToArray()
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

if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $args += @("-m:4", "-nodeReuse:false", "--logger", "trx;LogFileName=$Group.trx",
        "--results-directory", $ResultsDirectory)
} else {
    Write-Host "Running test group '$Group' ($Configuration)..."
    Write-Host "Command: dotnet $($args -join ' ')"
}

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
    $hostOutput = @()
    if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        # Even a console logger can expose theory arguments or assertions. The output stays in memory and is
        # never printed: only the fixed labels from Get-HostErrorLabels and the safe summary leave CI.
        $hostOutput = @(dotnet @args *>&1)
    } else {
        dotnet @args
    }
    $testExitCode = $LASTEXITCODE
    if ($null -ne $RunStatus) {
        $RunStatus['ExitCode'] = [int]$testExitCode
        if ($testExitCode -ne 0) { $RunStatus['HostErrors'] = @(Get-HostErrorLabels -Output $hostOutput) }
    }
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
