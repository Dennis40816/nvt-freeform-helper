param(
    [string]$LogPath = "build/logs/app.log",
    [string]$OutPath = "build/perf/non-notch-perf-baseline-latest.md",
    [string]$OutJsonPath = "build/perf/non-notch-perf-baseline-latest.json",
    [string]$CompareJsonPath = "",
    [double]$RegressionThresholdPercent = 20.0,
    [int]$TopOperations = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-Percentile {
    param(
        [int[]]$SortedValues,
        [double]$Percentile
    )

    if ($null -eq $SortedValues -or $SortedValues.Count -eq 0) {
        return 0
    }

    if ($SortedValues.Count -eq 1) {
        return $SortedValues[0]
    }

    $rank = [Math]::Ceiling($Percentile * $SortedValues.Count) - 1
    $index = [Math]::Min([Math]::Max([int]$rank, 0), $SortedValues.Count - 1)
    return $SortedValues[$index]
}

function Convert-OperationRowsToMap {
    param([object[]]$Rows)

    $map = @{}
    foreach ($row in $Rows) {
        if ($null -eq $row.Operation) {
            continue
        }

        $map[$row.Operation] = $row
    }

    return $map
}

if (-not (Test-Path -LiteralPath $LogPath)) {
    throw "Log file not found: $LogPath"
}

$lines = Get-Content -LiteralPath $LogPath
$lineCount = $lines.Count

$operationRegex = [regex]'\|\s+INFO\s+\|.*\|\s+(?<operation>.+?)\s+finished in\s+(?<ms>\d+)\s+ms\.?$'
$samples = New-Object System.Collections.Generic.List[object]

foreach ($line in $lines) {
    $match = $operationRegex.Match($line)
    if (-not $match.Success) {
        continue
    }

    $samples.Add([PSCustomObject]@{
        Operation = $match.Groups["operation"].Value.Trim()
        Ms = [int]$match.Groups["ms"].Value
    })
}

$groupedStats = @()
if ($samples.Count -gt 0) {
    $groupedStats = $samples |
        Group-Object -Property Operation |
        ForEach-Object {
            $values = @($_.Group | Select-Object -ExpandProperty Ms | Sort-Object)
            $avg = [Math]::Round((($values | Measure-Object -Average).Average), 2)
            [PSCustomObject]@{
                Operation = $_.Name
                Count = $values.Count
                Min = $values[0]
                P50 = Get-Percentile -SortedValues $values -Percentile 0.50
                P95 = Get-Percentile -SortedValues $values -Percentile 0.95
                Max = $values[$values.Count - 1]
                Avg = $avg
            }
        } |
        Sort-Object -Property @{ Expression = "P95"; Descending = $true }, @{ Expression = "Avg"; Descending = $true }, @{ Expression = "Count"; Descending = $true }
}

$selectionUpdatedCount = @($lines | Select-String -Pattern "Selection updated" -SimpleMatch).Count
$gridBuiltCount = @($lines | Select-String -Pattern "Grid built" -SimpleMatch).Count
$viewModelInitCount = @($lines | Select-String -Pattern "ViewModel initialized" -SimpleMatch).Count
$warningCount = @($lines | Select-String -Pattern "| WARN |" -SimpleMatch).Count
$errorCount = @($lines | Select-String -Pattern "| ERROR |" -SimpleMatch).Count

$startupRegex = [regex]'\|\s+INFO\s+\|.*\|\s+PERF STARTUP #(?<seq>\d+): stage=(?<stage>[^;]+);\s*elapsed=(?<elapsed>\d+)ms;\s*delta=(?<delta>\d+)ms(?:;\s*detail=(?<detail>.*))?\.?$'
$startupSamples = New-Object System.Collections.Generic.List[object]

$loadSummaryRegex = [regex]'\|\s+INFO\s+\|.*\|\s+PERF LOAD_PROJECT SUMMARY:\s*total=(?<total>\d+)ms\s+persistence=(?<persistence>\d+)ms\s+applyState=(?<applyState>\d+)ms\s+dxf=(?<dxf>\d+)ms\s+rebuild=(?<rebuild>\d+)ms\s+step1=(?<step1>\d+)ms\s+step2=(?<step2>\d+)ms\s+step1Replayed=(?<step1Replayed>[YN])\s+step2Replayed=(?<step2Replayed>[YN])\s+path=(?<path>.+)$'
$loadSamples = New-Object System.Collections.Generic.List[object]

foreach ($line in $lines) {
    $startupMatch = $startupRegex.Match($line)
    if ($startupMatch.Success) {
        $startupSamples.Add([PSCustomObject]@{
            Sequence = [int]$startupMatch.Groups["seq"].Value
            Stage = $startupMatch.Groups["stage"].Value.Trim()
            ElapsedMs = [int]$startupMatch.Groups["elapsed"].Value
            DeltaMs = [int]$startupMatch.Groups["delta"].Value
            Detail = $startupMatch.Groups["detail"].Value.Trim()
        })
    }

    $loadMatch = $loadSummaryRegex.Match($line)
    if ($loadMatch.Success) {
        $loadSamples.Add([PSCustomObject]@{
            TotalMs = [int]$loadMatch.Groups["total"].Value
            PersistenceMs = [int]$loadMatch.Groups["persistence"].Value
            ApplyStateMs = [int]$loadMatch.Groups["applyState"].Value
            DxfMs = [int]$loadMatch.Groups["dxf"].Value
            RebuildMs = [int]$loadMatch.Groups["rebuild"].Value
            Step1ReplayMs = [int]$loadMatch.Groups["step1"].Value
            Step2ReplayMs = [int]$loadMatch.Groups["step2"].Value
            Step1Replayed = $loadMatch.Groups["step1Replayed"].Value
            Step2Replayed = $loadMatch.Groups["step2Replayed"].Value
            Path = $loadMatch.Groups["path"].Value.Trim()
        })
    }
}

$startupStageStats = @()
if ($startupSamples.Count -gt 0) {
    $startupStageStats = $startupSamples |
        Group-Object -Property Stage |
        ForEach-Object {
            $deltaValues = @($_.Group | Select-Object -ExpandProperty DeltaMs | Sort-Object)
            $last = $_.Group | Sort-Object Sequence | Select-Object -Last 1
            [PSCustomObject]@{
                Stage = $_.Name
                Count = $deltaValues.Count
                DeltaMin = $deltaValues[0]
                DeltaP50 = Get-Percentile -SortedValues $deltaValues -Percentile 0.50
                DeltaP95 = Get-Percentile -SortedValues $deltaValues -Percentile 0.95
                DeltaMax = $deltaValues[$deltaValues.Count - 1]
                LastElapsedMs = [int]$last.ElapsedMs
            }
        } |
        Sort-Object -Property @{ Expression = "LastElapsedMs"; Descending = $false }, @{ Expression = "Stage"; Descending = $false }
}

$latestStartupSnapshot = $null
if ($startupSamples.Count -gt 0) {
    $latestSequence = ($startupSamples | Measure-Object -Property Sequence -Maximum).Maximum
    $latestStartupSnapshot = $startupSamples |
        Where-Object { $_.Sequence -eq $latestSequence } |
        Select-Object -First 1
}

$loadStats = $null
if ($loadSamples.Count -gt 0) {
    $totals = @($loadSamples | Select-Object -ExpandProperty TotalMs | Sort-Object)
    $latestLoad = $loadSamples | Select-Object -Last 1
    $loadStats = [PSCustomObject]@{
        Count = $totals.Count
        TotalMin = $totals[0]
        TotalP50 = Get-Percentile -SortedValues $totals -Percentile 0.50
        TotalP95 = Get-Percentile -SortedValues $totals -Percentile 0.95
        TotalMax = $totals[$totals.Count - 1]
        Latest = $latestLoad
    }
}

$generatedUtc = [DateTime]::UtcNow
$generatedUtcDisplay = $generatedUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
$generatedUtcIso = $generatedUtc.ToString("o")

$baseline = [PSCustomObject]@{
    generatedUtc = $generatedUtcIso
    sourceLog = $LogPath
    snapshot = [PSCustomObject]@{
        lineCount = $lineCount
        sampleCount = $samples.Count
        warningCount = $warningCount
        errorCount = $errorCount
        selectionUpdatedCount = $selectionUpdatedCount
        gridBuiltCount = $gridBuiltCount
        viewModelInitCount = $viewModelInitCount
        startupMarkerCount = $startupSamples.Count
        loadSummaryCount = $loadSamples.Count
    }
    operations = @($groupedStats)
    startupStages = @($startupStageStats)
    startupLatest = $latestStartupSnapshot
    loadProject = $loadStats
}

$regressions = @()
if (-not [string]::IsNullOrWhiteSpace($CompareJsonPath) -and (Test-Path -LiteralPath $CompareJsonPath)) {
    $previous = Get-Content -LiteralPath $CompareJsonPath -Raw | ConvertFrom-Json
    $prevOps = @($previous.operations)
    $prevMap = Convert-OperationRowsToMap -Rows $prevOps

    foreach ($current in $groupedStats) {
        if (-not $prevMap.ContainsKey($current.Operation)) {
            continue
        }

        $prev = $prevMap[$current.Operation]
        $prevP95 = [double]$prev.P95
        if ($prevP95 -le 0) {
            continue
        }

        $deltaMs = [double]$current.P95 - $prevP95
        $deltaPercent = ($deltaMs / $prevP95) * 100.0
        if ($deltaPercent -lt $RegressionThresholdPercent) {
            continue
        }

        $regressions += [PSCustomObject]@{
            Operation = $current.Operation
            PreviousP95 = [int]$prev.P95
            CurrentP95 = [int]$current.P95
            DeltaMs = [Math]::Round($deltaMs, 2)
            DeltaPercent = [Math]::Round($deltaPercent, 2)
        }
    }

    $regressions = $regressions |
        Sort-Object -Property @{ Expression = "DeltaPercent"; Descending = $true }, @{ Expression = "DeltaMs"; Descending = $true }, @{ Expression = "Operation"; Descending = $false }
}

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Non-Notch Performance Baseline (Latest)")
$markdown.Add("Generated: $generatedUtcDisplay")
$markdown.Add(("Source log: {0}" -f $LogPath))
if (-not [string]::IsNullOrWhiteSpace($CompareJsonPath)) {
    $markdown.Add(("Compare baseline: {0}" -f $CompareJsonPath))
}

$markdown.Add("")
$markdown.Add("## Snapshot")
$markdown.Add("- Log lines: $lineCount")
$markdown.Add("- Timed operation samples: $($samples.Count)")
$markdown.Add("- WARN lines: $warningCount")
$markdown.Add("- ERROR lines: $errorCount")
$markdown.Add(('- `Selection updated` lines: {0}' -f $selectionUpdatedCount))
$markdown.Add(('- `Grid built` lines: {0}' -f $gridBuiltCount))
$markdown.Add(('- `ViewModel initialized` lines: {0}' -f $viewModelInitCount))
$markdown.Add(('- `PERF STARTUP` markers: {0}' -f $startupSamples.Count))
$markdown.Add(('- `PERF LOAD_PROJECT SUMMARY` markers: {0}' -f $loadSamples.Count))
$markdown.Add("")

if ($groupedStats.Count -eq 0) {
    $markdown.Add("## Timed Operations")
    $markdown.Add('No `finished in N ms` operation lines found.')
}
else {
    $markdown.Add("## Timed Operations (top $TopOperations by p95)")
    $markdown.Add("| Operation | Count | Min (ms) | p50 (ms) | p95 (ms) | Max (ms) | Avg (ms) |")
    $markdown.Add("|---|---:|---:|---:|---:|---:|---:|")

    foreach ($row in ($groupedStats | Select-Object -First $TopOperations)) {
        $markdown.Add("| $($row.Operation) | $($row.Count) | $($row.Min) | $($row.P50) | $($row.P95) | $($row.Max) | $($row.Avg) |")
    }
}

if ($startupStageStats.Count -eq 0) {
    $markdown.Add("")
    $markdown.Add("## Startup Markers")
    $markdown.Add("No `PERF STARTUP` markers found.")
}
else {
    $markdown.Add("")
    $markdown.Add("## Startup Markers")
    if ($null -ne $latestStartupSnapshot) {
        $markdown.Add(("- Latest marker: #{0} {1} at {2} ms" -f $latestStartupSnapshot.Sequence, $latestStartupSnapshot.Stage, $latestStartupSnapshot.ElapsedMs))
    }

    $markdown.Add("| Stage | Count | Delta min (ms) | Delta p50 (ms) | Delta p95 (ms) | Delta max (ms) | Last elapsed (ms) |")
    $markdown.Add("|---|---:|---:|---:|---:|---:|---:|")
    foreach ($row in $startupStageStats) {
        $markdown.Add("| $($row.Stage) | $($row.Count) | $($row.DeltaMin) | $($row.DeltaP50) | $($row.DeltaP95) | $($row.DeltaMax) | $($row.LastElapsedMs) |")
    }
}

if ($null -eq $loadStats) {
    $markdown.Add("")
    $markdown.Add("## Load Project Markers")
    $markdown.Add("No `PERF LOAD_PROJECT SUMMARY` markers found.")
}
else {
    $markdown.Add("")
    $markdown.Add("## Load Project Markers")
    $markdown.Add(("- Samples: {0}" -f $loadStats.Count))
    $markdown.Add(("- Total min/p50/p95/max: {0}/{1}/{2}/{3} ms" -f $loadStats.TotalMin, $loadStats.TotalP50, $loadStats.TotalP95, $loadStats.TotalMax))
    $markdown.Add(("- Latest: total={0}ms, persistence={1}ms, apply={2}ms, dxf={3}ms, rebuild={4}ms, step1={5}ms, step2={6}ms, path={7}" -f
        $loadStats.Latest.TotalMs,
        $loadStats.Latest.PersistenceMs,
        $loadStats.Latest.ApplyStateMs,
        $loadStats.Latest.DxfMs,
        $loadStats.Latest.RebuildMs,
        $loadStats.Latest.Step1ReplayMs,
        $loadStats.Latest.Step2ReplayMs,
        $loadStats.Latest.Path))
}

if (-not [string]::IsNullOrWhiteSpace($CompareJsonPath)) {
    $markdown.Add("")
    $markdown.Add("## Regression Check")
    if ($regressions.Count -eq 0) {
        $markdown.Add("No operation exceeded regression threshold ($RegressionThresholdPercent%).")
    }
    else {
        $markdown.Add("| Operation | Prev p95 (ms) | Curr p95 (ms) | Delta (ms) | Delta (%) |")
        $markdown.Add("|---|---:|---:|---:|---:|")
        foreach ($row in $regressions) {
            $markdown.Add("| $($row.Operation) | $($row.PreviousP95) | $($row.CurrentP95) | $($row.DeltaMs) | $($row.DeltaPercent) |")
        }
    }
}

$outDir = Split-Path -Parent $OutPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

$jsonDir = Split-Path -Parent $OutJsonPath
if (-not [string]::IsNullOrWhiteSpace($jsonDir) -and -not (Test-Path -LiteralPath $jsonDir)) {
    New-Item -Path $jsonDir -ItemType Directory -Force | Out-Null
}

Set-Content -LiteralPath $OutPath -Value $markdown -Encoding utf8
$baseline | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutJsonPath -Encoding utf8

Write-Host "Perf baseline markdown generated: $OutPath"
Write-Host "Perf baseline json generated: $OutJsonPath"
