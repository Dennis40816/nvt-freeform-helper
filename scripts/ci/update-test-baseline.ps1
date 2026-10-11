param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("core", "ui", "viewmodel", "snapshots")]
    [string]$Shard,
    [Parameter(Mandatory = $true)][string]$ResultsDirectory,
    [string]$BaselinePath = (Join-Path $PSScriptRoot "test-baseline.json"),
    [string]$MeasuredAtCommit = "",
    [switch]$ExampleDataPresent
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "read-trx.ps1")

try {
    $text = Get-Content -LiteralPath $BaselinePath -Raw
    $schema = Join-Path $PSScriptRoot "test-baseline.schema.json"
    $null = Test-Json -Json $text -SchemaFile $schema -ErrorAction Stop
    $baseline = $text | ConvertFrom-Json -AsHashtable
    if (-not $baseline.shards.ContainsKey($Shard)) { throw "Missing shard floor." }
    $counts = Read-TrxResults -ResultsDirectory $ResultsDirectory
    if (-not $counts.Completed -or $counts.Failed -ne 0 -or $counts.Skipped -ne 0 -or $counts.NotExecuted -ne 0) { throw "Unsuccessful tests." }
    if ($counts.Total -lt $baseline.shards[$Shard].minTotal) { throw "Lower total refused." }
    $floor = $counts.Total
    $exampleFloor = $baseline.shards[$Shard].exampleMinTests
    if ($ExampleDataPresent) {
        $names = @(Get-ExampleDataTestNames)
        $executed = @($counts.Results | Where-Object {
            $_.Outcome -eq "Passed" -and $names -ccontains $_.MethodName
        } | ForEach-Object MethodName | Sort-Object -Unique).Count
        if ($executed -lt $exampleFloor) { throw "Lower example count refused." }
        $exampleFloor = $executed
    }
    if ($floor -eq $baseline.shards[$Shard].minTotal -and $exampleFloor -eq $baseline.shards[$Shard].exampleMinTests) {
        Write-Host "[update-test-baseline] unchanged"
        return
    }
    if ([string]::IsNullOrWhiteSpace($MeasuredAtCommit)) {
        $MeasuredAtCommit = [string](git -C (Join-Path $PSScriptRoot "../..") rev-parse HEAD 2>$null)
    }
    $baseline.shards[$Shard].minTotal = $floor
    $baseline.shards[$Shard].exampleMinTests = $exampleFloor
    $baseline.measuredAtCommit = $MeasuredAtCommit
    $json = $baseline | ConvertTo-Json -Depth 6
    $null = Test-Json -Json $json -SchemaFile $schema -ErrorAction Stop
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($BaselinePath),
        ($json -replace '\r?\n', "`r`n") + "`r`n", [System.Text.UTF8Encoding]::new($false))
    Write-Host ("[update-test-baseline] minTotal {0} exampleMinTests {1}" -f $floor, $exampleFloor)
} catch {
    throw "Test baseline update failed."
}
