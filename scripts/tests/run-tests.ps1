param(
    [ValidateSet("all", "workflow", "application", "notch-core", "notch-golden", "infrastructure", "ui-core", "ui-stable", "ui-viewmodel", "ui-snapshots", "smoke", "uncategorized")]
    [string]$Group = "all",
    [string]$Configuration = "Debug",
    [switch]$UseNoAppHost,
    [switch]$ValidateOnly,
    [switch]$AllowMissingExampleData,
    # Build the test project once and run nothing. verify.ps1 does this once for a shard, then runs each group with
    # -NoBuild, so the first group no longer builds inside its own 'dotnet test'.
    [switch]$BuildOnly,
    [switch]$NoBuild,
    # Internal CI result collection. Policy and summaries belong to verify.ps1, which owns shards.
    [string]$ResultsDirectory = "",
    # Internal CI status channel: the native exit code and fixed host-error labels, never tool output.
    [System.Collections.IDictionary]$RunStatus
)

$ErrorActionPreference = "Stop"
if ($null -ne $RunStatus) {
    $RunStatus['BuildErrorCodes'] = @()
    $RunStatus['ExitCode'] = $null
    $RunStatus['HostErrors'] = @()
    $RunStatus['ElapsedSeconds'] = $null
    $RunStatus['ListedTests'] = $null
    $RunStatus['RetryCount'] = 0
    $RunStatus['RetryListedTests'] = $null
    $RunStatus['ExecutedTests'] = $null
    $RunStatus['RedactedOutput'] = @()
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
# The host sometimes starts without finding any test, although the same filter lists tests afterwards.
# A group that ends this way is run once more. Every other failure is final.
$maxNoTestRetries = 1

# After a run that found no test, ask the test host again how many tests it can list without the filter being
# applied to a build. Only the number leaves this function, never a test name. -1 means the listing itself failed.
function Get-ListedTestCount {
    param(
        [string[]]$ListArguments
    )

    try {
        $listOutput = @(dotnet @ListArguments *>&1)
        if ($LASTEXITCODE -ne 0) { return -1 }
        $headerIndex = -1
        for ($i = 0; $i -lt $listOutput.Count; $i++) {
            if ([string]$listOutput[$i] -match 'The following Tests are available') { $headerIndex = $i; break }
        }
        if ($headerIndex -lt 0 -or $headerIndex + 1 -ge $listOutput.Count) { return 0 }
        return @($listOutput[($headerIndex + 1)..($listOutput.Count - 1)] |
            Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) }).Count
    } catch {
        return -1
    }
}

# The number of tests the host reports in its summary lines ("Total: n"), or -1 when there is no summary line.
# Only the number leaves this function, never a line of the output.
function Get-ExecutedTestCount {
    param(
        [object[]]$Output
    )

    $total = -1
    foreach ($item in $Output) {
        if ([string]$item -match '\bTotal:\s+(?<count>[0-9]+)\b') {
            $total = [math]::Max($total, 0) + [int]$Matches['count']
        }
    }

    return $total
}

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

# The run output can hold private example data and test names. A line leaves the run only when it matches a known
# dotnet, MSBuild or VSTest message shape, and then only the fixed words and the numbers of that shape are printed.
# Every other line prints as <line>. Each rule has a named group only where the value is a number, a version or a
# fixed word, so a name or a path can never reach the output through a group.
$maxRedactedHostRows = 60
$redactedHostRules = @(
    @{ Pattern = '^\s*Test run for .+\(\.NETCoreApp,Version=(?<v>v[0-9.]+)\)\s*$'; Format = 'Test run for <path>(.NETCoreApp,Version={v})' },
    @{ Pattern = '^\s*VSTest version (?<v>[0-9][0-9A-Za-z.\-]*) \((?<a>x64|x86|arm64)\)\s*$'; Format = 'VSTest version {v} ({a})' },
    @{ Pattern = '^\s*Starting test execution, please wait\.\.\.\s*$'; Format = 'Starting test execution, please wait...' },
    @{ Pattern = '^\s*A total of (?<n>[0-9]+) test files matched the specified pattern\.\s*$'; Format = 'A total of {n} test files matched the specified pattern.' },
    @{ Pattern = '^\s*No test matches the given testcase filter'; Format = 'No test matches the given testcase filter <filter> in <path>' },
    @{ Pattern = '^\s*No test is available in '; Format = 'No test is available in <path>. Make sure that test discoverers & executors are registered and platform & framework version settings are appropriate and try again.' },
    @{ Pattern = '^\s*(?<r>Passed|Failed)!\s+- Failed:\s+(?<f>[0-9]+), Passed:\s+(?<p>[0-9]+), Skipped:\s+(?<s>[0-9]+), Total:\s+(?<t>[0-9]+)'; Format = '{r}! - Failed: {f}, Passed: {p}, Skipped: {s}, Total: {t}' },
    @{ Pattern = '^\s*Test Run (?<r>Aborted|Failed|Successful)\.\s*$'; Format = 'Test Run {r}.' },
    @{ Pattern = '^\s*(?<k>Total tests|Passed|Failed|Skipped): (?<n>[0-9]+)\s*$'; Format = '{k}: {n}' },
    @{ Pattern = '^\s*Total time: (?<t>[0-9.,]+ (?:Milliseconds|Seconds|Minutes|Hours))\s*$'; Format = 'Total time: {t}' },
    @{ Pattern = '^\s*Determining projects to restore\.\.\.\s*$'; Format = 'Determining projects to restore...' },
    @{ Pattern = '^\s*All projects are up-to-date for restore\.\s*$'; Format = 'All projects are up-to-date for restore.' },
    @{ Pattern = '^\s*Restored .+ \(in (?<t>[0-9.,]+ ?(?:ms|sec|s|min))\)\.?\s*$'; Format = 'Restored <project> (in {t})' },
    @{ Pattern = '^\s*Build (?<r>succeeded|FAILED)\.\s*$'; Format = 'Build {r}.' },
    @{ Pattern = '^\s*(?<n>[0-9]+) (?<k>Warning|Error)\(s\)\s*$'; Format = '{n} {k}(s)' },
    @{ Pattern = '^\s*Time Elapsed (?<t>[0-9:.]+)\s*$'; Format = 'Time Elapsed {t}' },
    @{ Pattern = '^\s*\S+ -> .+$'; Format = '<project> -> <path>' },
    @{ Pattern = '\b(?<s>warning|error) (?<c>(?:MSB|CS|NETSDK|NU|IDE|CA|RS)[0-9]{3,5}):'; Format = '<location>: {s} {c}: <text>' },
    @{ Pattern = '^\[xUnit\.net (?<t>[0-9:.]+)\]\s+(?<w>Discovering|Discovered|Starting|Finished): '; Format = '[xUnit.net {t}] {w}: <assembly>' },
    @{ Pattern = '^\[xUnit\.net (?<t>[0-9:.]+)\]'; Format = '[xUnit.net {t}] <text>' },
    @{ Pattern = '^\s*Testhost process for source\(s\) .+ exited with error:'; Format = 'Testhost process for source(s) <path> exited with error: <text>' }
)

function ConvertTo-RedactedHostLine {
    param(
        [string]$Line
    )

    foreach ($rule in $redactedHostRules) {
        if ($Line -cnotmatch $rule.Pattern) { continue }
        $text = $rule.Format
        foreach ($name in @($Matches.Keys | Where-Object { $_ -is [string] })) {
            $text = $text.Replace('{' + $name + '}', [string]$Matches[$name])
        }
        return $text
    }

    return '<line>'
}

# Returns the redacted shape of a run that found no test, at most $maxRedactedHostRows rows, so the log can show what
# the host said without a test name. Equal rows in a row are returned once with a count. The caller prints the rows:
# verify.ps1 drops everything this script writes, so the rows travel in $RunStatus.
function Get-RedactedHostOutput {
    param(
        [object[]]$Output,
        [string]$GroupName,
        [int]$Attempt
    )

    $lines = @($Output | ForEach-Object { ([string]$_) -split "\r?\n" } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $rows = [System.Collections.Generic.List[string]]::new()
    $counts = [System.Collections.Generic.List[int]]::new()
    foreach ($line in $lines) {
        $redacted = ConvertTo-RedactedHostLine -Line $line
        if ($rows.Count -gt 0 -and $rows[$rows.Count - 1] -ceq $redacted) {
            $counts[$counts.Count - 1]++
            continue
        }
        $rows.Add($redacted)
        $counts.Add(1)
    }

    $shown = [math]::Min($rows.Count, $maxRedactedHostRows)
    $result = [System.Collections.Generic.List[string]]::new()
    $result.Add(('[verify] redacted-host-output group={0} attempt={1} lines={2} rows={3} shown={4}' -f
        $GroupName, $Attempt, $lines.Count, $rows.Count, $shown))
    for ($i = 0; $i -lt $shown; $i++) {
        $suffix = if ($counts[$i] -gt 1) { ' (x{0})' -f $counts[$i] } else { '' }
        $result.Add(('  {0}{1}' -f $rows[$i], $suffix))
    }
    $result.Add('[verify] redacted-host-output end')
    return $result.ToArray()
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

if ($NoBuild) {
    $args += "--no-build"
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
    if ($BuildOnly) {
        # The build output stays in memory: it can hold file names. Only the exit code, the seconds and the
        # compiler or MSBuild error codes (such as CS0103) leave this script.
        $buildArguments = @("build", $project, "-c", $Configuration, "--nologo", "-m:4", "-nodeReuse:false")
        if ($UseNoAppHost) { $buildArguments += "/p:UseAppHost=false" }
        $buildTimer = [System.Diagnostics.Stopwatch]::StartNew()
        $buildOutput = @(dotnet @buildArguments *>&1)
        $buildExitCode = $LASTEXITCODE
        $buildTimer.Stop()
        if ($null -ne $RunStatus) {
            $RunStatus['ExitCode'] = [int]$buildExitCode
            $RunStatus['ElapsedSeconds'] = [int][math]::Round($buildTimer.Elapsed.TotalSeconds)
            $RunStatus['BuildErrorCodes'] = @($buildOutput | ForEach-Object { [regex]::Matches([string]$_, '\berror ((?:MSB|CS|NETSDK|NU)[0-9]{3,5})\b') } |
                ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique -First 10)
        }
        if ($buildExitCode -ne 0) { throw "Build failed with exit code $buildExitCode." }
        return
    }
    $hostOutput = @()
    $runTimer = [System.Diagnostics.Stopwatch]::StartNew()
    if (-not [string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        # Even a console logger can expose theory arguments or assertions. The output stays in memory and is
        # never printed: only the fixed labels from Get-HostErrorLabels and the safe summary leave CI.
        $hostOutput = @(dotnet @args *>&1)
    } else {
        dotnet @args
    }
    $testExitCode = $LASTEXITCODE
    $runTimer.Stop()
    $listArguments = @("test", $project, "-c", $Configuration, "--nologo", "--no-build", "--list-tests")
    if ($UseNoAppHost) { $listArguments += "/p:UseAppHost=false" }
    if (-not [string]::IsNullOrWhiteSpace($filter)) { $listArguments += @("--filter", $filter) }
    $noTestRetries = 0
    $retryListedTests = $null
    $redactedOutputPrinted = $false
    # Only a captured run can be classified, so only a run with a results directory is retried.
    while ($testExitCode -ne 0 -and $noTestRetries -lt $maxNoTestRetries -and
        -not [string]::IsNullOrWhiteSpace($ResultsDirectory) -and
        @(Get-HostErrorLabels -Output $hostOutput) -contains 'no-test-matches-filter') {
        # Keep what the host said before the next start replaces it. Only redacted rows leave the run.
        if ($null -ne $RunStatus) {
            $RunStatus['RedactedOutput'] = @($RunStatus['RedactedOutput']) +
                @(Get-RedactedHostOutput -Output $hostOutput -GroupName $Group -Attempt ($noTestRetries + 1))
        }
        $redactedOutputPrinted = $true
        # Run again only when the host lists tests for the same filter now. A filter that matches nothing stays failed.
        $retryListedTests = Get-ListedTestCount -ListArguments $listArguments
        if ($retryListedTests -le 0) { break }
        $noTestRetries++
        $hostOutput = @(dotnet @args *>&1)
        $testExitCode = $LASTEXITCODE
        $redactedOutputPrinted = $false
    }
    # The last start is kept too when it ended with no test and the loop above did not keep it yet.
    if (-not $redactedOutputPrinted -and $testExitCode -ne 0 -and -not [string]::IsNullOrWhiteSpace($ResultsDirectory) -and
        @(Get-HostErrorLabels -Output $hostOutput) -contains 'no-test-matches-filter') {
        if ($null -ne $RunStatus) {
            $RunStatus['RedactedOutput'] = @($RunStatus['RedactedOutput']) +
                @(Get-RedactedHostOutput -Output $hostOutput -GroupName $Group -Attempt ($noTestRetries + 1))
        }
    }
    if ($null -ne $RunStatus) {
        $RunStatus['ExitCode'] = [int]$testExitCode
        $RunStatus['RetryCount'] = $noTestRetries
        if ($noTestRetries -gt 0) {
            $RunStatus['RetryListedTests'] = $retryListedTests
            $RunStatus['ExecutedTests'] = Get-ExecutedTestCount -Output $hostOutput
        }
        if ($testExitCode -ne 0) { $RunStatus['HostErrors'] = @(Get-HostErrorLabels -Output $hostOutput) }
        if (@($RunStatus['HostErrors']) -contains 'no-test-matches-filter') {
            $RunStatus['ElapsedSeconds'] = [int][math]::Round($runTimer.Elapsed.TotalSeconds)
            $RunStatus['ListedTests'] = Get-ListedTestCount -ListArguments $listArguments
        }
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
