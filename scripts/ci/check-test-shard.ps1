param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("core", "ui", "viewmodel", "snapshots")]
    [string]$Shard,
    [Parameter(Mandatory = $true)][string]$ResultsDirectory,
    [string]$BaselinePath = (Join-Path $PSScriptRoot "test-baseline.json"),
    [Parameter(Mandatory = $true)][bool]$ExampleDataPresent,
    [Parameter(Mandatory = $true)][string]$SummaryPath,
    [string]$Commit = "",
    [switch]$TestRunFailed,
    [switch]$Unverified,
    # Facts collected by the shard runner; they do not participate in verdicts or summaries.
    [object[]]$RunDiagnostics = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "read-trx.ps1")

$invalid = $false
$counts = [pscustomobject]@{ Completed = $false; Total = 0; Passed = 0; Failed = 0; Skipped = 0; NotExecuted = 0; FailedTests = @(); Results = @() }
$checks = [ordered]@{ belowFloor = $false; skippedNotZero = $false; requiredClassMissing = $false; updateModeVariableSet = $false }
$exampleExecuted = 0
$readDiagnostics = @{}
try {
    $counts = Read-TrxResults -ResultsDirectory $ResultsDirectory -ReadDiagnostics $readDiagnostics
    $baselineText = Get-Content -LiteralPath $BaselinePath -Raw
    if (-not (Test-Json -Json $baselineText -SchemaFile (Join-Path $PSScriptRoot "test-baseline.schema.json") -ErrorAction Stop)) {
        throw "Invalid baseline."
    }
    $baseline = $baselineText | ConvertFrom-Json -AsHashtable
    if (-not $baseline.shards.ContainsKey($Shard)) { throw "Missing shard floor." }
    $shardFloor = $baseline.shards[$Shard]
    $checks.belowFloor = $counts.Total -lt $shardFloor.minTotal
    $checks.skippedNotZero = $counts.Skipped -ne 0
    if ($ExampleDataPresent -and ($shardFloor.exampleMinTests -gt 0 -or $shardFloor.requiredClasses.Count -gt 0)) {
        foreach ($class in $shardFloor.requiredClasses) {
            $classResults = @($counts.Results | Where-Object ClassName -ceq $class)
            if ($classResults.Count -eq 0 -or @($classResults | Where-Object Outcome -ne "Passed").Count -gt 0) {
                $checks.requiredClassMissing = $true
            }
        }
        $exampleNames = @(Get-ExampleDataTestNames)
        $exampleExecuted = @($counts.Results | Where-Object {
            $_.Outcome -in @("Passed", "Failed") -and $exampleNames -ccontains $_.MethodName
        } | ForEach-Object MethodName | Sort-Object -Unique).Count
        # Format 2 has no separate example-data floor flag; belowFloor covers both minimums.
        $checks.belowFloor = $checks.belowFloor -or $exampleExecuted -lt $shardFloor.exampleMinTests
    }
} catch {
    $invalid = $true
    $checks.belowFloor = $true
}
# The update-mode check is last even when earlier checks failed. A value of 0 or empty is still set.
$checks.updateModeVariableSet = (Test-Path -LiteralPath "Env:FREEFORMHELPER_UPDATE_NOTCH_BASELINE") -or
    (Test-Path -LiteralPath "Env:FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX")

if ([string]::IsNullOrWhiteSpace($Commit)) {
    $Commit = [string](git -C (Join-Path $PSScriptRoot "../..") rev-parse HEAD 2>$null)
}
if ($Commit -cnotmatch '^[0-9a-f]{40}$') {
    $Commit = "0" * 40
    $invalid = $true
}
# Overall run failure uses outcome only; counters still describe the actual test rows.
$failed = $invalid -or $TestRunFailed -or -not $counts.Completed -or $counts.Failed -gt 0 -or $counts.NotExecuted -gt 0 -or
    @($checks.Values | Where-Object { $_ }).Count -gt 0
$outcome = if ($Unverified) { "unverified" } elseif ($failed) { "failed" } else { "passed" }
$summary = [ordered]@{
    schemaVersion = 1
    shard = $Shard
    commit = $Commit
    outcome = $outcome
    total = $counts.Total
    passed = $counts.Passed
    failed = $counts.Failed
    skipped = $counts.Skipped
    notExecuted = $counts.NotExecuted
    failedTests = @($counts.FailedTests)
    exampleData = [ordered]@{ present = $ExampleDataPresent; executed = $exampleExecuted }
    checks = $checks
}
try {
    $json = $summary | ConvertTo-Json -Depth 6
    $null = Test-Json -Json $json -SchemaFile (Join-Path $PSScriptRoot "shard-summary.schema.json") -ErrorAction Stop
    $fullSummaryPath = [System.IO.Path]::GetFullPath($SummaryPath)
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $fullSummaryPath) -Force
    [System.IO.File]::WriteAllText($fullSummaryPath, ($json -replace '\r?\n', "`r`n") + "`r`n", [System.Text.UTF8Encoding]::new($false))
} catch {
    throw "Shard summary could not be written."
}
Write-Host ("[check-test-shard] total {0} passed {1} failed {2} skipped {3} notExecuted {4}" -f
    $counts.Total, $counts.Passed, $counts.Failed, $counts.Skipped, $counts.NotExecuted)
if ($counts.Total -eq 0 -and $RunDiagnostics.Count -gt 0) {
    # The existing parse path identifies the failed group; never print an unrecognized directory name.
    $diagnostic = $RunDiagnostics | Where-Object { $_.Group -ceq $readDiagnostics.FailedGroup } | Select-Object -First 1
    $unknownDiagnostic = [pscustomobject]@{
        Group = 'unknown'
        Phase = 'read-results'
        ExitCode = $null
        ExceptionType = $readDiagnostics.ExceptionType
        TrxFiles = $readDiagnostics.TrxFiles
        DirectoryPresent = $readDiagnostics.DirectoryPresent -or @($RunDiagnostics | Where-Object DirectoryPresent).Count -gt 0
    }
    if ($null -eq $diagnostic -and $readDiagnostics.ExceptionType -ne '' -and $readDiagnostics.TrxFiles -gt 0) {
        $diagnostic = $unknownDiagnostic
    }
    # Without a per-file failure, prefer an observed unfinished phase, then missing TRX.
    if ($null -eq $diagnostic) {
        $diagnostic = $RunDiagnostics | Where-Object { $_.Phase -ne 'read-results' } | Select-Object -First 1
    }
    if ($null -eq $diagnostic) {
        $diagnostic = $RunDiagnostics | Where-Object { $_.TrxFiles -eq 0 } | Select-Object -First 1
    }
    if ($null -eq $diagnostic) {
        $diagnostic = if ($readDiagnostics.ExceptionType -ne '') { $unknownDiagnostic } else { $RunDiagnostics[0] }
    }
    $exit = if ($null -ne $diagnostic.ExitCode) { [string][int]$diagnostic.ExitCode } else {
        'exception exception-type={0}' -f $diagnostic.ExceptionType
    }
    $directory = if ($diagnostic.DirectoryPresent) { 'present' } else { 'missing' }
    Write-Host ('[check-test-shard] diagnostic shard={0} group={1} phase={2} exit={3} trx-files={4} results-dir={5}' -f
        $Shard, $diagnostic.Group, $diagnostic.Phase, $exit, $diagnostic.TrxFiles, $directory)
}
if ($Unverified) { throw "unverified, cannot merge" }
if ($failed) { throw "Shard validation failed." }
