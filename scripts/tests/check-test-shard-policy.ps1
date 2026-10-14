param([switch]$SkipCallerEnvironmentCase)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "../ci/read-trx.ps1")
. (Join-Path $PSScriptRoot "with-temporary-environment.ps1")

$ciRoot = Join-Path $PSScriptRoot "../ci"
$checkScript = Join-Path $ciRoot "check-test-shard.ps1"
$updateScript = Join-Path $ciRoot "update-test-baseline.ps1"
$baselineSchema = Join-Path $ciRoot "test-baseline.schema.json"
$summarySchema = Join-Path $ciRoot "shard-summary.schema.json"
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("freeformhelper-shard-policy-{0}" -f [guid]::NewGuid().ToString("N"))
$caseCount = 0
$policyScript = $PSCommandPath
$updateEnvironment = @{}
foreach ($name in @('FREEFORMHELPER_UPDATE_NOTCH_BASELINE', 'FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX')) {
    $updateEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Assert-Policy {
    param([bool]$Condition)
    if (-not $Condition) { throw ("Shard policy test failed {0}." -f $script:caseCount) }
    $script:caseCount++
}

function Write-FixtureTrx {
    param([string]$Directory, [object[]]$Tests, [int]$ExtraTotal = 0, [string]$RunOutcome = 'Completed')
    $null = New-Item -ItemType Directory -Path $Directory -Force
    $definitions = @()
    $results = @()
    $index = 0
    foreach ($test in $Tests) {
        $index++
        $separator = $test.Name.LastIndexOf('.')
        $class = $test.Name.Substring(0, $separator)
        $method = $test.Name.Substring($separator + 1)
        # Synthetic sentinels prove that neither display names, parameters nor Output are exported.
        $definitions += "<UnitTest id='$index'><TestMethod className='$class, Assembly' name='$method(CONFIDENTIAL_SENTINEL)' /></UnitTest>"
        $results += "<UnitTestResult testId='$index' testName='CONFIDENTIAL_SENTINEL' outcome='$($test.Outcome)'><Output><ErrorInfo><Message>CONFIDENTIAL_SENTINEL</Message><StackTrace>CONFIDENTIAL_SENTINEL</StackTrace></ErrorInfo></Output></UnitTestResult>"
    }
    $passed = @($Tests | Where-Object Outcome -eq 'Passed').Count
    $failed = @($Tests | Where-Object Outcome -eq 'Failed').Count
    $total = $Tests.Count + $ExtraTotal
    $xml = "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><TestDefinitions>$($definitions -join '')</TestDefinitions><Results>$($results -join '')</Results><ResultSummary outcome='$RunOutcome'><Counters total='$total' passed='$passed' failed='$failed'/></ResultSummary></TestRun>"
    [System.IO.File]::WriteAllText((Join-Path $Directory "fixture.trx"), $xml)
}

function Write-FixtureBaseline {
    param([int]$Floor = 3, [int]$ExampleFloor = 11, [string]$Shard = 'core')
    $baseline = Get-Content -LiteralPath (Join-Path $ciRoot "test-baseline.json") -Raw | ConvertFrom-Json -AsHashtable
    $baseline.shards[$Shard].minTotal = $Floor
    $baseline.shards[$Shard].exampleMinTests = $ExampleFloor
    $baseline | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $testRoot "baseline.json")
}

function Invoke-FixtureCheck {
    param([bool]$Present = $false, [switch]$Unverified, [switch]$TestRunFailed, [string]$Shard = 'core',
        [object[]]$RunDiagnostics = @(), [string]$ResultsDirectory = (Join-Path $testRoot 'results'))
    $message = ""
    $output = @(
        try {
            & $checkScript -Shard $Shard -ResultsDirectory $ResultsDirectory `
                -BaselinePath (Join-Path $testRoot "baseline.json") -ExampleDataPresent $Present `
                -SummaryPath (Join-Path $testRoot "summary.json") -Commit ("a" * 40) `
                -Unverified:$Unverified -TestRunFailed:$TestRunFailed -RunDiagnostics $RunDiagnostics 6>&1
        } catch { $message = $_.Exception.Message }
    )
    $json = Get-Content -LiteralPath (Join-Path $testRoot "summary.json") -Raw
    Assert-Policy (Test-Json -Json $json -SchemaFile $summarySchema)
    Assert-Policy (($json + ($output -join '') + $message) -notmatch 'CONFIDENTIAL_SENTINEL|StackTrace|ErrorInfo|\(|\)')
    return @{ Summary = ($json | ConvertFrom-Json); Message = $message; Output = $output }
}

function Assert-FixtureUpdateRejected {
    param([switch]$Present)
    $before = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($baselinePath))
    $arguments = @('-NoProfile', '-File', $updateScript, '-Shard', 'core', '-ResultsDirectory', $resultsPath, '-BaselinePath', $baselinePath)
    if ($Present) { $arguments += '-ExampleDataPresent' }
    $output = @(& pwsh @arguments 2>&1)
    Assert-Policy ($LASTEXITCODE -ne 0)
    Assert-Policy (($output -join '') -match 'Test baseline update failed\.' -and ($output -join '') -notmatch 'CONFIDENTIAL_SENTINEL')
    Assert-Policy ($before -ceq [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($baselinePath)))
}

try {
    foreach ($name in $updateEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, [NullString]::Value)
    }
    $null = New-Item -ItemType Directory -Path $testRoot
    $baselineText = Get-Content -LiteralPath (Join-Path $ciRoot "test-baseline.json") -Raw
    Assert-Policy (Test-Json -Json $baselineText -SchemaFile $baselineSchema)
    $schemaCases = @(
        { param($b) $b.privatePayload = 'fixture' },
        { param($b) $b.Remove('schemaVersion') | Out-Null },
        { param($b) $b.measuredAtCommit = 'bad' },
        { param($b) $b.shards.core.minTotal = 0 },
        { param($b) $b.shards.core.minTotal = 1.5 },
        { param($b) $b.shards.core.maxSkipped = 1 },
        { param($b) $b.shards.core.requiredClasses = @('Tests.Class', 'Tests.Class') },
        { param($b) $b.shards.core.requiredClasses = @('Tests.Class(private)') },
        { param($b) $b.shards.core.exampleMinTests = -1 },
        { param($b) $b.shards.core.exampleMinTests = 1.5 },
        { param($b) $b.shards.core.Remove('exampleMinTests') | Out-Null },
        { param($b) $b.shards.core.Remove('requiredClasses') | Out-Null },
        { param($b) $b.schemaVersion = 1 },
        { param($b) $b.exampleData = @{ minTests = 26; requiredClasses = @('Tests.Class') } }
    )
    foreach ($mutate in $schemaCases) {
        $broken = $baselineText | ConvertFrom-Json -AsHashtable
        & $mutate $broken
        $valid = Test-Json -Json ($broken | ConvertTo-Json -Depth 6) -SchemaFile $baselineSchema -ErrorAction SilentlyContinue
        Assert-Policy (-not $valid)
    }

    $samples = @(
        @{ Name = 'Tests.Sample.One'; Outcome = 'Passed' },
        @{ Name = 'Tests.Sample.Two'; Outcome = 'Passed' },
        @{ Name = 'Tests.Sample.Three'; Outcome = 'Passed' }
    )
    $resultsPath = Join-Path $testRoot "results"
    $baselinePath = Join-Path $testRoot "baseline.json"
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    Write-FixtureBaseline
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and $actual.Summary.total -eq 3 -and -not $actual.Summary.exampleData.present)
    $normalOutput = '[check-test-shard] total 3 passed 3 failed 0 skipped 0 notExecuted 0'
    Assert-Policy (($actual.Output -join "`n") -ceq $normalOutput)
    $fixtureDiagnostics = @([pscustomobject]@{
        Group = 'notch-core'; Phase = 'read-results'; ExitCode = 0; ExceptionType = ''; TrxFiles = 1; DirectoryPresent = $true
    })
    $actual = Invoke-FixtureCheck -RunDiagnostics $fixtureDiagnostics
    Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and ($actual.Output -join "`n") -ceq $normalOutput)
    # The summary lists each known group with only its name, exit code, TRX file count and total.
    Assert-Policy (@($actual.Summary.groups).Count -eq 1 -and $actual.Summary.groups[0].name -ceq 'notch-core' -and
        $actual.Summary.groups[0].exitCode -eq 0 -and $actual.Summary.groups[0].trxFileCount -eq 1)
    Assert-Policy (@($actual.Summary.groups[0].psobject.Properties.Name) -join ',' -ceq 'name,exitCode,trxFileCount,total')
    $unknownDiagnostics = @([pscustomobject]@{
        Group = 'C:private-folder'; Phase = 'read-results'; ExitCode = 0; ExceptionType = ''; TrxFiles = 1; DirectoryPresent = $true
    })
    $actual = Invoke-FixtureCheck -RunDiagnostics $unknownDiagnostics
    Assert-Policy (@($actual.Summary.groups).Count -eq 0)
    $nullExitDiagnostics = @([pscustomobject]@{
        Group = 'application'; Phase = 'run-tests'; ExitCode = $null; ExceptionType = 'System.Exception'; TrxFiles = 0; DirectoryPresent = $false
    })
    $actual = Invoke-FixtureCheck -RunDiagnostics $nullExitDiagnostics
    Assert-Policy (@($actual.Summary.groups).Count -eq 1 -and $null -eq $actual.Summary.groups[0].exitCode -and
        $actual.Summary.groups[0].total -eq 0)
    Write-FixtureBaseline -Floor 1000000
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.checks.belowFloor -and $actual.Message -ceq 'Shard validation failed.')
    $actual = Invoke-FixtureCheck -RunDiagnostics $fixtureDiagnostics
    Assert-Policy ($actual.Summary.checks.belowFloor -and $actual.Message -ceq 'Shard validation failed.' -and
        ($actual.Output -join "`n") -ceq $normalOutput)
    Write-FixtureBaseline
    $outcomeCases = @(
        @{ Outcome = 'NotExecuted'; RunOutcome = 'Completed'; Skipped = 1; Failed = 0; NotExecuted = 0 },
        @{ Outcome = 'Failed'; RunOutcome = 'Failed'; Skipped = 0; Failed = 1; NotExecuted = 0 },
        @{ Outcome = 'Timeout'; RunOutcome = 'Aborted'; Skipped = 0; Failed = 0; NotExecuted = 1 }
    )
    foreach ($case in $outcomeCases) {
        $tests = @($samples[0], $samples[1], @{ Name = 'Tests.Sample.Three'; Outcome = $case.Outcome })
        Write-FixtureTrx -Directory $resultsPath -Tests $tests -RunOutcome $case.RunOutcome
        $actual = Invoke-FixtureCheck
        Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.skipped -eq $case.Skipped -and
            $actual.Summary.failed -eq $case.Failed -and $actual.Summary.notExecuted -eq $case.NotExecuted)
        Assert-Policy ($actual.Summary.checks.skippedNotZero -eq ($case.Skipped -gt 0))
    }
    Assert-Policy (@($actual.Summary.failedTests).Count -eq 0)
    foreach ($runOutcome in @('Aborted', 'Failed', '', 'Passed')) {
        Write-FixtureTrx -Directory $resultsPath -Tests $samples -RunOutcome $runOutcome
        $actual = Invoke-FixtureCheck
        Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Message -ceq 'Shard validation failed.')
        Assert-Policy ($actual.Summary.total -eq 3 -and $actual.Summary.passed -eq 3 -and $actual.Summary.failed -eq 0 -and
            $actual.Summary.notExecuted -eq 0 -and @($actual.Summary.checks.psobject.Properties.Value | Where-Object { $_ }).Count -eq 0)
        Assert-FixtureUpdateRejected
    }
    Write-FixtureTrx -Directory $resultsPath -Tests @(@{ Name = 'Tests.Sample.One'; Outcome = 'Failed' }) -RunOutcome Failed
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.failedTests[0] -ceq 'Tests.Sample.One')
    Write-FixtureTrx -Directory $resultsPath -Tests $samples -ExtraTotal 1
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.notExecuted -eq 1 -and $actual.Summary.outcome -ceq 'failed')
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    $actual = Invoke-FixtureCheck -TestRunFailed
    Assert-Policy ($actual.Summary.outcome -ceq 'failed')
    $actual = Invoke-FixtureCheck -Unverified
    Assert-Policy ($actual.Summary.outcome -ceq 'unverified' -and $actual.Message -ceq 'unverified, cannot merge')

    foreach ($variable in @('FREEFORMHELPER_UPDATE_NOTCH_BASELINE', 'FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX')) {
        foreach ($value in @('1', '0', '')) {
            Invoke-WithTemporaryEnvironmentVariables -Settings @{ $variable = $value } -Action {
                $actual = Invoke-FixtureCheck
                Assert-Policy ($actual.Summary.checks.updateModeVariableSet -and $actual.Summary.outcome -ceq 'failed')
            }
        }
    }
    $exampleNames = @(Get-ExampleDataTestNames)
    $viewmodelTests = @($exampleNames | Where-Object { $_ -like '*.FreeformHelperViewModelTests.*' } |
        ForEach-Object { @{ Name = $_; Outcome = 'Passed' } })
    $allExampleTests = @($exampleNames | Where-Object { $_ -notlike '*.FreeformHelperViewModelTests.*' } |
        ForEach-Object { @{ Name = $_; Outcome = 'Passed' } })
    Assert-Policy ($allExampleTests.Count -eq 11 -and $viewmodelTests.Count -eq 15)
    Write-FixtureBaseline -Shard viewmodel -ExampleFloor $viewmodelTests.Count
    Write-FixtureTrx -Directory $resultsPath -Tests $viewmodelTests
    $actual = Invoke-FixtureCheck -Shard viewmodel -Present $true
    Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and -not $actual.Summary.checks.requiredClassMissing -and
        $actual.Summary.exampleData.executed -eq 15)
    Write-FixtureBaseline -Shard viewmodel -ExampleFloor ($viewmodelTests.Count + 1)
    $actual = Invoke-FixtureCheck -Shard viewmodel -Present $true
    Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.checks.belowFloor -and
        -not $actual.Summary.checks.requiredClassMissing)
    foreach ($shardName in @('ui', 'snapshots')) {
        Write-FixtureBaseline -Shard $shardName -ExampleFloor 0
        Write-FixtureTrx -Directory $resultsPath -Tests $samples
        $actual = Invoke-FixtureCheck -Shard $shardName -Present $true
        Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and $actual.Summary.exampleData.executed -eq 0 -and
            -not $actual.Summary.checks.requiredClassMissing)
    }
    Write-FixtureBaseline -ExampleFloor $allExampleTests.Count
    Write-FixtureTrx -Directory $resultsPath -Tests $allExampleTests
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and $actual.Summary.exampleData.executed -eq $allExampleTests.Count)
    Write-FixtureBaseline -ExampleFloor ($allExampleTests.Count + 1)
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.checks.belowFloor -and $actual.Summary.outcome -ceq 'failed')
    Write-FixtureBaseline -ExampleFloor 1
    $missingGolden = @($allExampleTests | Where-Object { $_.Name -notlike '*.NotchGoldenBaselineTests.*' })
    Write-FixtureTrx -Directory $resultsPath -Tests $missingGolden
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.checks.requiredClassMissing -and
        -not $actual.Summary.checks.belowFloor)
    Write-FixtureBaseline -ExampleFloor 1
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.checks.requiredClassMissing -and $actual.Summary.checks.belowFloor)
    $badGolden = @($allExampleTests | ForEach-Object {
        @{ Name = $_.Name; Outcome = $(if ($_.Name -like '*.NotchGoldenBaselineTests.*') { 'Failed' } else { 'Passed' }) }
    })
    Write-FixtureTrx -Directory $resultsPath -Tests $badGolden
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.checks.requiredClassMissing)
    $skippedGolden = @($allExampleTests | ForEach-Object {
        @{ Name = $_.Name; Outcome = $(if ($_.Name -like '*.NotchGoldenBaselineTests.*') { 'NotExecuted' } else { 'Passed' }) }
    })
    Write-FixtureTrx -Directory $resultsPath -Tests $skippedGolden
    $actual = Invoke-FixtureCheck -Present $true
    Assert-Policy ($actual.Summary.checks.requiredClassMissing -and $actual.Summary.checks.skippedNotZero)
    Write-FixtureTrx -Directory $resultsPath -Tests $allExampleTests
    & $updateScript -Shard core -ResultsDirectory $resultsPath -BaselinePath $baselinePath `
        -MeasuredAtCommit ("b" * 40) -ExampleDataPresent 6>$null
    $raisedExamples = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    Assert-Policy ($raisedExamples.shards.core.exampleMinTests -eq $allExampleTests.Count -and
        $raisedExamples.shards.viewmodel.exampleMinTests -eq 15 -and $raisedExamples.shards.ui.minTotal -eq 113)
    Write-FixtureBaseline -Shard viewmodel -ExampleFloor 1
    Write-FixtureTrx -Directory $resultsPath -Tests $viewmodelTests
    & $updateScript -Shard viewmodel -ResultsDirectory $resultsPath -BaselinePath $baselinePath `
        -MeasuredAtCommit ("b" * 40) -ExampleDataPresent 6>$null
    $raisedViewmodel = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    Assert-Policy ($raisedViewmodel.shards.viewmodel.exampleMinTests -eq 15 -and
        $raisedViewmodel.shards.core.exampleMinTests -eq 11 -and $raisedViewmodel.shards.core.requiredClasses.Count -eq 3)
    Write-FixtureBaseline -Floor $allExampleTests.Count -ExampleFloor ($allExampleTests.Count + 1)
    Write-FixtureTrx -Directory $resultsPath -Tests $allExampleTests
    Assert-FixtureUpdateRejected -Present

    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    $json = Get-Content -LiteralPath (Join-Path $testRoot "summary.json") -Raw | ConvertFrom-Json -AsHashtable
    $json.privatePayload = 'fixture'
    Assert-Policy (-not (Test-Json -Json ($json | ConvertTo-Json -Depth 6) -SchemaFile $summarySchema -ErrorAction SilentlyContinue))
    $json.Remove('privatePayload') | Out-Null
    $json.failedTests = @('Tests.Sample.One(private)')
    Assert-Policy (-not (Test-Json -Json ($json | ConvertTo-Json -Depth 6) -SchemaFile $summarySchema -ErrorAction SilentlyContinue))

    Write-FixtureBaseline -Floor 2
    & $updateScript -Shard core -ResultsDirectory $resultsPath -BaselinePath $baselinePath -MeasuredAtCommit ("b" * 40) 6>$null
    $raised = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    Assert-Policy ($raised.shards.core.minTotal -eq 3 -and $raised.measuredAtCommit -ceq ("b" * 40))
    Write-FixtureBaseline -Floor 4
    Assert-FixtureUpdateRejected
    foreach ($case in $outcomeCases) {
        $tests = @($samples) + @(@{ Name = 'Tests.Sample.Four'; Outcome = $case.Outcome })
        Write-FixtureTrx -Directory $resultsPath -Tests $tests -RunOutcome $case.RunOutcome
        Assert-FixtureUpdateRejected
    }
    foreach ($invalidXml in @('CONFIDENTIAL_SENTINEL', '<!DOCTYPE TestRun [<!ENTITY x SYSTEM "file:///fixture">]><TestRun>&x;</TestRun>')) {
        [System.IO.File]::WriteAllText((Join-Path $resultsPath "fixture.trx"), $invalidXml)
        $actual = Invoke-FixtureCheck
        Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.total -eq 0)
    }
    Remove-Item -LiteralPath (Join-Path $resultsPath "fixture.trx")
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.outcome -ceq 'failed')
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    [System.IO.File]::WriteAllText($baselinePath, '{}')
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.outcome -ceq 'failed')
    Write-FixtureBaseline
    [xml]$inconsistent = Get-Content -LiteralPath (Join-Path $resultsPath "fixture.trx") -Raw
    $inconsistent.TestRun.ResultSummary.Counters.SetAttribute('passed', '2')
    $inconsistent.Save((Join-Path $resultsPath "fixture.trx"))
    $actual = Invoke-FixtureCheck
    Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.total -eq 0)
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    Write-FixtureTrx -Directory (Join-Path $resultsPath "second") -Tests $samples
    $read = Read-TrxResults -ResultsDirectory $resultsPath
    Assert-Policy ($read.Completed -and $read.Total -eq 6 -and $read.Passed -eq 6 -and $read.ExecutedClasses[0] -ceq 'Tests.Sample')
    Write-FixtureTrx -Directory (Join-Path $resultsPath 'second') -Tests $samples -RunOutcome Aborted
    $read = Read-TrxResults -ResultsDirectory $resultsPath
    Assert-Policy (-not $read.Completed -and $read.Total -eq 6 -and $read.Passed -eq 6)

    # A later group's corrupt TRX must never blame the valid first group.
    $groupResultsPath = Join-Path $testRoot 'group-results'
    $groupDiagnostics = @('notch-core', 'application') | ForEach-Object {
        [pscustomobject]@{ Group = $_; Phase = 'read-results'; ExitCode = 0; ExceptionType = ''; TrxFiles = 1; DirectoryPresent = $true }
    }
    Write-FixtureTrx -Directory (Join-Path $groupResultsPath 'notch-core') -Tests $samples
    Write-FixtureTrx -Directory (Join-Path $groupResultsPath 'application') -Tests $samples
    $corruptTrx = Join-Path $groupResultsPath 'application/fixture.trx'
    [System.IO.File]::WriteAllText($corruptTrx, 'PRIVATE-MARKER-DO-NOT-PRINT')
    $actual = Invoke-FixtureCheck -ResultsDirectory $groupResultsPath -RunDiagnostics $groupDiagnostics
    $diagnostics = @($actual.Output | Where-Object { [string]$_ -like '[[]check-test-shard] diagnostic *' })
    $expectedDiagnostic = '[check-test-shard] diagnostic shard=core group=application phase=read-results exit=0 trx-files=1 results-dir=present'
    Assert-Policy ($diagnostics.Count -eq 1 -and [string]$diagnostics[0] -ceq $expectedDiagnostic)
    Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.total -eq 0 -and $actual.Message -ceq 'Shard validation failed.')
    Assert-Policy (($actual.Output -join '') -notmatch 'PRIVATE-MARKER-DO-NOT-PRINT')
    # The group list names the group without results: notch-core has its own total, application reads as 0.
    Assert-Policy (@($actual.Summary.groups).Count -eq 2 -and $actual.Summary.groups[0].name -ceq 'notch-core' -and
        $actual.Summary.groups[0].total -eq 3 -and $actual.Summary.groups[1].name -ceq 'application' -and
        $actual.Summary.groups[1].total -eq 0 -and $actual.Summary.groups[1].trxFileCount -eq 1)

    Write-FixtureTrx -Directory (Join-Path $groupResultsPath 'application') -Tests $samples
    $actual = Invoke-FixtureCheck -ResultsDirectory $groupResultsPath -RunDiagnostics $groupDiagnostics
    Assert-Policy ($actual.Summary.outcome -ceq 'passed' -and $actual.Summary.total -eq 6 -and $actual.Message -ceq '')
    Assert-Policy (($actual.Output -join "`n") -ceq '[check-test-shard] total 6 passed 6 failed 0 skipped 0 notExecuted 0')
    Assert-Policy (@($actual.Summary.groups).Count -eq 2 -and $actual.Summary.groups[0].total -eq 3 -and
        $actual.Summary.groups[1].total -eq 3)

    # A corrupt file outside the known group directories has no attributable group.
    [System.IO.File]::WriteAllText((Join-Path $groupResultsPath 'fixture.trx'), 'PRIVATE-MARKER-DO-NOT-PRINT')
    $actual = Invoke-FixtureCheck -ResultsDirectory $groupResultsPath -RunDiagnostics $groupDiagnostics
    $diagnostics = @($actual.Output | Where-Object { [string]$_ -like '[[]check-test-shard] diagnostic *' })
    Assert-Policy ($diagnostics.Count -eq 1 -and [string]$diagnostics[0] -cmatch '^\[check-test-shard\] diagnostic shard=core group=unknown phase=read-results exit=exception exception-type=[A-Za-z0-9_.]+ trx-files=3 results-dir=present$')
    Assert-Policy ($actual.Summary.outcome -ceq 'failed' -and $actual.Summary.total -eq 0 -and $actual.Message -ceq 'Shard validation failed.')
    Assert-Policy (($actual.Output -join '') -notmatch 'PRIVATE-MARKER-DO-NOT-PRINT')

    # Exercise both entries with a console-local dotnet stub: no restore, network or real test data.
    $fakeRepo = Join-Path $testRoot "repo"
    foreach ($directory in @('scripts/ci', 'scripts/tests', 'scripts/build', 'tests/FreeformHelper.Tests')) {
        $null = New-Item -ItemType Directory -Path (Join-Path $fakeRepo $directory) -Force
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "../verify.ps1") -Destination (Join-Path $fakeRepo "scripts/verify.ps1")
    foreach ($file in @('check-test-shard.ps1', 'read-trx.ps1', 'test-baseline.json', 'test-baseline.schema.json', 'shard-summary.schema.json')) {
        Copy-Item -LiteralPath (Join-Path $ciRoot $file) -Destination (Join-Path $fakeRepo "scripts/ci/$file")
    }
    foreach ($file in @('run-tests.ps1', 'private-path-patterns.ps1')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $fakeRepo "scripts/tests/$file")
    }
    [System.IO.File]::WriteAllText((Join-Path $fakeRepo "scripts/tests/assert-example-data.ps1"), 'param([switch]$AllowMissing); return $false')
    [System.IO.File]::WriteAllText((Join-Path $fakeRepo "scripts/build/fetch-core-packages.ps1"), '')
    $runSource = Get-Content -LiteralPath (Join-Path $PSScriptRoot "run-tests.ps1") -Raw
    $declarations = [regex]::Matches($runSource, '"([A-Za-z0-9]+Tests)"') | ForEach-Object { "class $($_.Groups[1].Value) {}" }
    [System.IO.File]::WriteAllText((Join-Path $fakeRepo "tests/FreeformHelper.Tests/Groups.cs"), ($declarations -join "`n"))
    $fakeBaselinePath = Join-Path $fakeRepo "scripts/ci/test-baseline.json"
    $fakeBaseline = Get-Content -LiteralPath $fakeBaselinePath -Raw | ConvertFrom-Json -AsHashtable
    foreach ($shardName in $fakeBaseline.shards.Keys) { $fakeBaseline.shards[$shardName].minTotal = 1 }
    $fakeBaseline | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $fakeBaselinePath
    $entryCases = @(
        @{ Enforce = $false; Unverified = $false; Fail = $false; Outcome = 'passed' },
        @{ Enforce = $false; Unverified = $false; Fail = $true; Outcome = 'failed' },
        @{ Enforce = $true; Unverified = $false; Fail = $false; Outcome = 'passed' },
        @{ Enforce = $true; Unverified = $false; Fail = $true; Outcome = 'failed' },
        @{ Enforce = $true; Unverified = $true; Fail = $false; Outcome = 'unverified' }
    )
    foreach ($case in $entryCases) {
        Invoke-WithTemporaryEnvironmentVariables -Settings @{ FREEFORMHELPER_TEST_AREA = (Join-Path $testRoot "test-area") } -Action {
            & {
                $calls = [System.Collections.Generic.List[object]]::new()
                function git { $global:LASTEXITCODE = 0; return ("a" * 40) }
                function dotnet {
                    $calls.Add($args)
                    $index = [Array]::IndexOf($args, '--results-directory')
                    if ($index -ge 0) { Write-FixtureTrx -Directory $args[$index + 1] -Tests $samples }
                    Write-Output 'CONFIDENTIAL_SENTINEL'
                    $global:LASTEXITCODE = if ($case.Fail) { 1 } else { 0 }
                }
                $summaryFile = Join-Path $fakeRepo "summary.json"
                if (Test-Path -LiteralPath $summaryFile) { Remove-Item -LiteralPath $summaryFile }
                $message = ''
                $entryOutput = @(
                    try {
                        & (Join-Path $fakeRepo "scripts/verify.ps1") -CiLane test -Shard core -AllowMissingExampleData `
                            -EnforceFloors:$case.Enforce -Unverified:$case.Unverified -SummaryPath $summaryFile 6>&1
                    } catch { $message = $_.Exception.Message }
                )
                $expectedCalls = if (-not $case.Enforce -and $case.Fail) { 1 } else { 4 }
                Assert-Policy ($calls.Count -eq $expectedCalls)
                Assert-Policy (((($entryOutput -join '') -match 'CONFIDENTIAL_SENTINEL') -eq (-not $case.Enforce)))
                Assert-Policy (-not (Test-Path -LiteralPath (Join-Path $fakeRepo "build/ci-test-results")))
                Assert-Policy (-not (Test-Path -LiteralPath (Join-Path $fakeRepo "tests/FreeformHelper.Tests/TestResults")))
                foreach ($call in $calls) {
                    Assert-Policy (($call -contains '--logger') -eq $case.Enforce)
                    Assert-Policy (-not ($call -contains '--no-restore'))
                }
                if ($case.Enforce) {
                    $summaryText = Get-Content -LiteralPath $summaryFile -Raw
                    Assert-Policy (Test-Json -Json $summaryText -SchemaFile $summarySchema)
                    $entrySummary = $summaryText | ConvertFrom-Json
                    Assert-Policy ($entrySummary.outcome -ceq $case.Outcome -and $entrySummary.total -eq 12)
                    Assert-Policy (($message.Length -gt 0) -eq ($case.Fail -or $case.Unverified))
                    if ($case.Unverified) { Assert-Policy ($message -ceq 'unverified, cannot merge') }
                    $expectedOutput = @(
                        '[verify] NVT Core packages',
                        '[check-test-shard] total 12 passed 12 failed 0 skipped 0 notExecuted 0'
                    )
                    if (-not $case.Fail -and -not $case.Unverified) { $expectedOutput += '[verify] OK (test)' }
                    Assert-Policy (($entryOutput -join "`n") -ceq ($expectedOutput -join "`n"))
                } else {
                    Assert-Policy (-not (Test-Path -LiteralPath $summaryFile) -and ($message.Length -gt 0) -eq $case.Fail)
                    if ($case.Fail) { Assert-Policy ($message -ceq "Test group 'notch-core' failed with exit code 1.") }
                }
            }
        }
    }
    $diagnosticCases = @(
        @{ Mode = 'exit'; Shard = 'snapshots'; Phase = 'run-tests'; Exit = '1'; Trx = 0; Directory = 'missing' },
        @{ Mode = 'exception'; Shard = 'snapshots'; Phase = 'run-tests'; Exit = 'exception exception-type=System.InvalidOperationException'; Trx = 0; Directory = 'missing' },
        @{ Mode = 'missing'; Shard = 'snapshots'; Phase = 'read-results'; Exit = '0'; Trx = 0; Directory = 'missing' },
        @{ Mode = 'empty'; Shard = 'snapshots'; Phase = 'read-results'; Exit = '0'; Trx = 0; Directory = 'present' },
        @{ Mode = 'zero'; Shard = 'snapshots'; Phase = 'read-results'; Exit = '0'; Trx = 1; Directory = 'present' },
        @{ Mode = 'invalid'; Shard = 'snapshots'; Phase = 'read-results'; Exit = '0'; Trx = 1; Directory = 'present' },
        @{ Mode = 'unreadable'; Shard = 'snapshots'; Phase = 'results-directory'; Exit = '0'; Trx = 0; Directory = 'present' },
        @{ Mode = 'exit'; Shard = 'core'; Phase = 'run-tests'; Exit = '1'; Trx = 0; Directory = 'missing' },
        @{ Mode = 'mixed'; Shard = 'core'; Group = 'application'; Phase = 'run-tests'; Exit = '1'; Trx = 0; Directory = 'missing' }
    )
    foreach ($case in $diagnosticCases) {
        Invoke-WithTemporaryEnvironmentVariables -Settings @{ FREEFORMHELPER_TEST_AREA = (Join-Path $testRoot 'test-area') } -Action {
            & {
                function git { $global:LASTEXITCODE = 0; return ('a' * 40) }
                function dotnet {
                    $index = [Array]::IndexOf($args, '--results-directory')
                    $directory = $args[$index + 1]
                    Write-Output 'PRIVATE-MARKER-DO-NOT-PRINT'
                    Write-Host 'PRIVATE-MARKER-DO-NOT-PRINT'
                    Write-Warning 'PRIVATE-MARKER-DO-NOT-PRINT'
                    switch ($case.Mode) {
                        'exception' { throw [System.InvalidOperationException]::new('PRIVATE-MARKER-DO-NOT-PRINT') }
                        'empty' { $null = New-Item -ItemType Directory -Path $directory -Force }
                        'unreadable' { $null = New-Item -ItemType Directory -Path $directory -Force }
                        'zero' { Write-FixtureTrx -Directory $directory -Tests @() }
                        'invalid' {
                            $null = New-Item -ItemType Directory -Path $directory -Force
                            [System.IO.File]::WriteAllText((Join-Path $directory 'fixture.trx'), 'PRIVATE-MARKER-DO-NOT-PRINT')
                        }
                    }
                    $global:LASTEXITCODE = if ($case.Mode -eq 'exit' -or
                        ($case.Mode -eq 'mixed' -and (Split-Path -Leaf $directory) -eq 'application')) { 1 } else { 0 }
                }
                function Get-ChildItem {
                    param([string]$LiteralPath, [string]$Filter, [switch]$File, [switch]$Recurse, [switch]$Force)
                    if ($case.Mode -eq 'unreadable' -and $Filter -eq '*.trx') {
                        throw [System.UnauthorizedAccessException]::new('PRIVATE-MARKER-DO-NOT-PRINT')
                    }
                    Microsoft.PowerShell.Management\Get-ChildItem @PSBoundParameters
                }
                $summaryFile = Join-Path $fakeRepo 'summary.json'
                $message = ''
                $entryOutput = @(
                    try {
                        & (Join-Path $fakeRepo 'scripts/verify.ps1') -CiLane test -Shard $case.Shard -AllowMissingExampleData `
                            -EnforceFloors -SummaryPath $summaryFile *>&1
                    } catch { $message = $_.Exception.Message }
                )
                $group = if ($case.ContainsKey('Group')) { $case.Group } elseif ($case.Shard -eq 'core') { 'notch-core' } else { 'ui-snapshots' }
                $expectedDiagnostic = '[check-test-shard] diagnostic shard={0} group={1} phase={2} exit={3} trx-files={4} results-dir={5}' -f
                    $case.Shard, $group, $case.Phase, $case.Exit, $case.Trx, $case.Directory
                $diagnostics = @($entryOutput | Where-Object { [string]$_ -like '[[]check-test-shard] diagnostic *' })
                Assert-Policy ($diagnostics.Count -eq 1 -and [string]$diagnostics[0] -ceq $expectedDiagnostic)
                Assert-Policy (($entryOutput -join "`n") -notmatch 'PRIVATE-MARKER-DO-NOT-PRINT|StackTrace|ErrorInfo')
                Assert-Policy ($message -ceq 'Shard validation failed.')
                $summaryText = Get-Content -LiteralPath $summaryFile -Raw
                Assert-Policy (Test-Json -Json $summaryText -SchemaFile $summarySchema)
                $entrySummary = $summaryText | ConvertFrom-Json
                Assert-Policy ($entrySummary.outcome -ceq 'failed' -and $entrySummary.total -eq 0)
                Assert-Policy (-not (Test-Path -LiteralPath (Join-Path $fakeRepo 'build/ci-test-results')))
            }
        }
    }
    # Real child exits prove that setup, precheck and cleanup failures cannot publish private filenames.
    Write-FixtureTrx -Directory $resultsPath -Tests $samples
    $entryTrx = Join-Path $testRoot 'entry.trx'
    Copy-Item -LiteralPath (Join-Path $resultsPath 'fixture.trx') -Destination $entryTrx
    $failureRunner = Join-Path $testRoot 'entry-failure.ps1'
    [System.IO.File]::WriteAllText($failureRunner, @'
param([string]$Repo, [string]$Fixture, [string]$Failure)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function git { $global:LASTEXITCODE = 0; return ('a' * 40) }
function dotnet {
    $index = [Array]::IndexOf($args, '--results-directory')
    $directory = $args[$index + 1]
    Write-Output 'PRIVATE-MARKER-DO-NOT-PRINT'
    if ($Failure -eq 'zero-exception') { throw [System.InvalidOperationException]::new('PRIVATE-MARKER-DO-NOT-PRINT') }
    if ($Failure -in @('zero-exit', 'zero-unverified')) {
        $global:LASTEXITCODE = if ($Failure -eq 'zero-exit') { 1 } else { 0 }
        return
    }
    $null = New-Item -ItemType Directory -Path $directory -Force
    Copy-Item -LiteralPath $Fixture -Destination (Join-Path $directory 'fixture.trx')
    Write-Output 'CONFIDENTIAL_SENTINEL_FILE.bin'
    $global:LASTEXITCODE = 0
}
function Remove-Item {
    param([string]$LiteralPath, [switch]$Recurse, [switch]$Force)
    if ($Failure -eq 'cleanup' -and $LiteralPath -like '*ci-test-results*') {
        throw 'CONFIDENTIAL_SENTINEL_FILE.bin'
    }
    Microsoft.PowerShell.Management\Remove-Item @PSBoundParameters
}
function New-Item {
    param([string]$Path, [string]$ItemType, [switch]$Force)
    if ($Failure -eq 'creation' -and $Path -like '*ci-test-results*') {
        throw 'CONFIDENTIAL_SENTINEL_FILE.bin'
    }
    Microsoft.PowerShell.Management\New-Item @PSBoundParameters
}
$verifyPath = Join-Path $Repo 'scripts/verify.ps1'
if ($Failure -eq 'resolution') {
    # Inject only the static path-resolution call in the copied entry script.
    $source = [System.IO.File]::ReadAllText($verifyPath)
    $expression = '[System.IO.Path]::GetFullPath($SummaryPath)'
    if (-not $source.Contains($expression)) { throw 'Missing path-resolution fixture seam.' }
    [System.IO.File]::WriteAllText($verifyPath, $source.Replace($expression, '$(throw ''CONFIDENTIAL_SENTINEL_FILE.bin'')'))
}
& $verifyPath -CiLane test -Shard core -EnforceFloors `
    -SummaryPath (Join-Path $Repo 'summary.json') -AllowMissingExampleData -Unverified:($Failure -eq 'zero-unverified')
'@)
    foreach ($failure in @('precheck', 'cleanup', 'creation', 'resolution', 'zero-exit', 'zero-exception', 'zero-unverified')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../verify.ps1') -Destination (Join-Path $fakeRepo 'scripts/verify.ps1')
        $precheckSource = if ($failure -eq 'precheck') {
            @'
param([switch]$AllowMissing)
Write-Output 'CONFIDENTIAL_SENTINEL_FILE.bin'
Write-Host 'CONFIDENTIAL_SENTINEL_FILE.bin'
Write-Warning 'CONFIDENTIAL_SENTINEL_FILE.bin'
& pwsh -NoProfile -Command "[Console]::Error.WriteLine('CONFIDENTIAL_SENTINEL_FILE.bin'); exit 1"
throw 'CONFIDENTIAL_SENTINEL_FILE.bin'
'@
        } else { 'param([switch]$AllowMissing); return $false' }
        [System.IO.File]::WriteAllText((Join-Path $fakeRepo 'scripts/tests/assert-example-data.ps1'), $precheckSource)
        $summaryFile = Join-Path $fakeRepo 'summary.json'
        if (Test-Path -LiteralPath $summaryFile) { Remove-Item -LiteralPath $summaryFile }
        $output = @(& pwsh -NoProfile -File $failureRunner -Repo $fakeRepo -Fixture $entryTrx -Failure $failure 2>&1)
        Assert-Policy ($LASTEXITCODE -ne 0)
        $reason = switch ($failure) {
            'precheck' { 'example-precheck-failed core' }
            'cleanup' { 'results-cleanup-failed core' }
            'creation' { 'results-creation-failed core' }
            'resolution' { 'path-resolution-failed core' }
            'zero-exit' { 'Shard validation failed.' }
            'zero-exception' { 'Shard validation failed.' }
            'zero-unverified' { 'unverified, cannot merge' }
        }
        Assert-Policy (($output -join '') -match $reason -and ($output -join '') -notmatch 'CONFIDENTIAL_SENTINEL_FILE\.bin|PRIVATE-MARKER-DO-NOT-PRINT')
        if ($failure -like 'zero-*' -or $failure -eq 'creation') {
            Assert-Policy ($LASTEXITCODE -eq 1)
            $exit = switch ($failure) {
                'creation' { 'exception exception-type=System.Management.Automation.RuntimeException' }
                'zero-exit' { '1' }
                'zero-exception' { 'exception exception-type=System.InvalidOperationException' }
                'zero-unverified' { '0' }
            }
            $phase = if ($failure -eq 'creation') { 'results-directory' } elseif ($failure -eq 'zero-unverified') { 'read-results' } else { 'run-tests' }
            $expectedDiagnostic = '[check-test-shard] diagnostic shard=core group=notch-core phase={0} exit={1} trx-files=0 results-dir=missing' -f $phase, $exit
            $diagnostics = @($output | Where-Object { [string]$_ -like '[[]check-test-shard] diagnostic *' })
            Assert-Policy ($diagnostics.Count -eq 1 -and [string]$diagnostics[0] -ceq $expectedDiagnostic)
        }
        if ($failure -in @('precheck', 'resolution')) {
            Assert-Policy (-not (Test-Path -LiteralPath $summaryFile))
        } else {
            $summaryText = Get-Content -LiteralPath $summaryFile -Raw
            Assert-Policy (Test-Json -Json $summaryText -SchemaFile $summarySchema)
            Assert-Policy ($summaryText -notmatch 'CONFIDENTIAL_SENTINEL_FILE\.bin')
            if ($failure -eq 'creation') {
                $summary = $summaryText | ConvertFrom-Json
                Assert-Policy ($summary.outcome -ceq 'failed' -and $summary.total -eq 0)
            }
            if ($failure -like 'zero-*') {
                $summary = $summaryText | ConvertFrom-Json
                $expectedOutcome = if ($failure -eq 'zero-unverified') { 'unverified' } else { 'failed' }
                Assert-Policy ($summary.outcome -ceq $expectedOutcome -and $summary.total -eq 0)
            }
        }
    }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../verify.ps1') -Destination (Join-Path $fakeRepo 'scripts/verify.ps1')
    $pathCases = @(
        @{ Shard = 'core'; Path = (Join-Path $fakeRepo 'build/ci-test-results/shard-summary.json'); Message = 'Shard summary must be outside private results.' },
        @{ Shard = ''; Path = (Join-Path $fakeRepo 'summary.json'); Message = 'An explicit summary path requires one shard.' }
    )
    foreach ($case in $pathCases) {
        $message = ''
        try {
            & (Join-Path $fakeRepo 'scripts/verify.ps1') -CiLane test -Shard $case.Shard -EnforceFloors `
                -SummaryPath $case.Path -AllowMissingExampleData 6>$null
        } catch { $message = $_.Exception.Message }
        Assert-Policy ($message -ceq $case.Message)
    }
    if (-not $SkipCallerEnvironmentCase) {
        Invoke-WithTemporaryEnvironmentVariables -Settings @{
            FREEFORMHELPER_UPDATE_NOTCH_BASELINE = '1'
            FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX = '1'
        } -Action {
            $output = @(& $policyScript -SkipCallerEnvironmentCase *>&1)
            Assert-Policy ($output.Count -eq 1 -and [string]$output[0] -match '^\[check-test-shard-policy\] OK [0-9]+$')
            Assert-Policy ($env:FREEFORMHELPER_UPDATE_NOTCH_BASELINE -ceq '1' -and $env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX -ceq '1')
        }
    }
    Write-Host ("[check-test-shard-policy] OK {0}" -f $caseCount)
} finally {
    foreach ($name in $updateEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, ($updateEnvironment[$name] ?? [NullString]::Value))
    }
    $cleanupPath = [System.IO.Path]::GetFullPath($testRoot)
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    if (-not $cleanupPath.StartsWith($temporaryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Policy workspace cleanup refused."
    }
    if (Test-Path -LiteralPath $cleanupPath) { Remove-Item -LiteralPath $cleanupPath -Recurse -Force }
}
