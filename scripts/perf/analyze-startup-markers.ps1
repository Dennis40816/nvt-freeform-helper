param(
    [string]$LogPath = "build/logs/app.log",
    [string]$OutPath = "build/perf/startup-markers-latest.md",
    [int]$InitialGridBudgetMs = 1000,
    [switch]$FailOnBudgetViolation
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\\..")
$fullLogPath = if ([System.IO.Path]::IsPathRooted($LogPath)) {
    [System.IO.Path]::GetFullPath($LogPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $LogPath))
}

if (-not (Test-Path -LiteralPath $fullLogPath)) {
    throw "Log file not found: $fullLogPath"
}

$lineRegex = [System.Text.RegularExpressions.Regex]::new(
    '^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+) \| .*? \| PERF STARTUP #(?<idx>\d+): stage=(?<stage>[^;]+); elapsed=(?<elapsed>\d+)ms; delta=(?<delta>\d+)ms(?:; detail=(?<detail>.*))?\.$',
    [System.Text.RegularExpressions.RegexOptions]::Compiled)
$appBootRegex = [System.Text.RegularExpressions.Regex]::new(
    'Logging initialized\.',
    [System.Text.RegularExpressions.RegexOptions]::Compiled)

$markers = New-Object System.Collections.Generic.List[object]
$lineNo = 0
$latestAppBootLine = 0
Get-Content -LiteralPath $fullLogPath | ForEach-Object {
    $lineNo++
    $line = $_
    if ($appBootRegex.IsMatch($line)) {
        $latestAppBootLine = $lineNo
    }

    $match = $lineRegex.Match($line)
    if (-not $match.Success) {
        return
    }

    $markers.Add([PSCustomObject]@{
            line = $lineNo
            timestamp = $match.Groups["ts"].Value
            index = [int]$match.Groups["idx"].Value
            stage = $match.Groups["stage"].Value
            elapsedMs = [int]$match.Groups["elapsed"].Value
            deltaMs = [int]$match.Groups["delta"].Value
            detail = $match.Groups["detail"].Value
        }) | Out-Null
}

if ($markers.Count -eq 0) {
    throw "No PERF STARTUP markers found in log."
}

$runs = New-Object System.Collections.Generic.List[object]
$currentRun = New-Object System.Collections.Generic.List[object]

foreach ($marker in $markers) {
    if ($marker.index -eq 1 -and $currentRun.Count -gt 0) {
        $runs.Add([PSCustomObject]@{
                markers = $currentRun.ToArray()
            }) | Out-Null
        $currentRun = New-Object System.Collections.Generic.List[object]
    }

    $currentRun.Add($marker) | Out-Null
}

if ($currentRun.Count -gt 0) {
    $runs.Add([PSCustomObject]@{
            markers = $currentRun.ToArray()
        }) | Out-Null
}

$appRuns = @($runs | Where-Object {
        $stages = @($_.markers | ForEach-Object { $_.stage })
        $stages -contains "app.framework-init-enter"
    })

if ($appRuns.Count -eq 0) {
    throw "No app startup run found (only query runs detected)."
}

$latestCandidates = @(
    if ($latestAppBootLine -gt 0) {
        $appRuns | Where-Object {
            $firstLine = ($_.markers | Select-Object -First 1).line
            $firstLine -ge $latestAppBootLine
        }
    }
)

$usedFallbackRun = $false
if ($latestCandidates.Count -eq 0 -and $latestAppBootLine -gt 0) {
    # If latest run has no markers (e.g. non-Debug level without FREEFORM_PERF_MARKERS_INFO),
    # keep reporting the newest run that still has markers.
    $usedFallbackRun = $true
}

$latest = if ($latestCandidates.Count -gt 0) { $latestCandidates[$latestCandidates.Count - 1] } else { $appRuns[$appRuns.Count - 1] }
$latestMarkers = @($latest.markers)
$maxElapsed = ($latestMarkers | Measure-Object -Property elapsedMs -Maximum).Maximum
$topStages = @($latestMarkers | Sort-Object -Property deltaMs -Descending | Select-Object -First 8)
$initialGridMarker = @($latestMarkers | Where-Object { $_.stage -eq "workspace.initial-grid-built" } | Select-Object -Last 1)
$initialGridElapsedMs = if ($initialGridMarker.Count -gt 0) { [int]$initialGridMarker[0].elapsedMs } else { -1 }
$initialGridBudgetPass = $initialGridElapsedMs -ge 0 -and $initialGridElapsedMs -le $InitialGridBudgetMs

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add("# Startup Marker Analysis (Latest App Run)")
$markdown.Add(("Generated: {0}" -f ([DateTime]::UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"))))
$markdown.Add(("Log: {0}" -f $fullLogPath))
$markdown.Add(("Run markers: {0}" -f $latestMarkers.Count))
$markdown.Add(("Run total elapsed: {0} ms" -f $maxElapsed))
if ($usedFallbackRun) {
    $markdown.Add("> Note: latest app run had no PERF markers (likely non-Debug level and FREEFORM_PERF_MARKERS_INFO not enabled). Showing most recent run with markers.")
}
$markdown.Add("")
$markdown.Add("## Stage Timeline")
$markdown.Add("| # | Stage | Elapsed (ms) | Delta (ms) | Detail |")
$markdown.Add("| ---: | --- | ---: | ---: | --- |")
foreach ($m in $latestMarkers) {
    $detail = if ([string]::IsNullOrWhiteSpace($m.detail)) { "-" } else { ($m.detail -replace '\|', '/') }
    $markdown.Add(("| {0} | {1} | {2} | {3} | {4} |" -f
            $m.index,
            $m.stage,
            $m.elapsedMs,
            $m.deltaMs,
            $detail))
}

$markdown.Add("")
$markdown.Add("## Top Delta Stages")
$markdown.Add("| Stage | Delta (ms) | Elapsed (ms) |")
$markdown.Add("| --- | ---: | ---: |")
foreach ($m in $topStages) {
    $markdown.Add(("| {0} | {1} | {2} |" -f $m.stage, $m.deltaMs, $m.elapsedMs))
}

$markdown.Add("")
$markdown.Add("## Startup Budget Gate")
$markdown.Add(("- Rule: workspace.initial-grid-built <= {0}ms" -f $InitialGridBudgetMs))
if ($initialGridElapsedMs -lt 0) {
    $markdown.Add("- Result: FAIL (marker missing: workspace.initial-grid-built)")
}
elseif ($initialGridBudgetPass) {
    $markdown.Add(("- Result: PASS (workspace.initial-grid-built={0}ms)" -f $initialGridElapsedMs))
}
else {
    $markdown.Add(("- Result: FAIL (workspace.initial-grid-built={0}ms)" -f $initialGridElapsedMs))
}

$outFullPath = if ([System.IO.Path]::IsPathRooted($OutPath)) {
    [System.IO.Path]::GetFullPath($OutPath)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutPath))
}

$outDir = Split-Path -Parent $outFullPath
if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

Set-Content -LiteralPath $outFullPath -Value $markdown -Encoding utf8
Write-Host "Startup marker analysis generated: $outFullPath"

if ($FailOnBudgetViolation) {
    if ($initialGridElapsedMs -lt 0) {
        throw "Startup budget gate failed: marker 'workspace.initial-grid-built' not found."
    }

    if (-not $initialGridBudgetPass) {
        throw ("Startup budget gate failed: workspace.initial-grid-built={0}ms exceeds budget {1}ms." -f
            $initialGridElapsedMs,
            $InitialGridBudgetMs)
    }
}
