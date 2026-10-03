param(
    [string]$LogPath = "build/logs/app.log",
    [string]$OutPath = "build/perf/padmatch-decision-latest.md",
    [double]$AvgCandidatePerCadThreshold = 120,
    [int]$P95CandidatePerCadThreshold = 400,
    [long]$PolygonIntersectionsThreshold = 2000000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
if (-not [System.IO.Path]::IsPathRooted($LogPath))
{
    $LogPath = Join-Path $repoRoot $LogPath
}
if (-not [System.IO.Path]::IsPathRooted($OutPath))
{
    $OutPath = Join-Path $repoRoot $OutPath
}

if (-not (Test-Path $LogPath))
{
    throw "Log file not found: $LogPath"
}

$pattern = [regex]'PERF PADMATCH: cad=(?<cad>\d+), candidateCells=(?<candidate>\d+), boundsIntersections=(?<bounds>\d+), polygonIntersections=(?<poly>\d+), avgCandidatePerCad=(?<avg>[\d\.]+), p95CandidatePerCad=(?<p95>\d+)'
$rows = @()
foreach ($line in Get-Content -Path $LogPath)
{
    $match = $pattern.Match($line)
    if (-not $match.Success)
    {
        continue
    }

    $rows += [PSCustomObject]@{
        CadCount = [int]$match.Groups["cad"].Value
        CandidateCellVisits = [long]$match.Groups["candidate"].Value
        BoundsIntersections = [long]$match.Groups["bounds"].Value
        PolygonIntersections = [long]$match.Groups["poly"].Value
        AvgCandidatePerCad = [double]$match.Groups["avg"].Value
        P95CandidatePerCad = [int]$match.Groups["p95"].Value
    }
}

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir))
{
    New-Item -ItemType Directory -Path $dir | Out-Null
}

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add("# PadMatcher Spatial Index Decision")
$lines.Add("")
$lines.Add("- Source log: $LogPath")
$lines.Add("- Generated at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("- Thresholds: avgCandidate/CAD <= $AvgCandidatePerCadThreshold, p95Candidate/CAD <= $P95CandidatePerCadThreshold, polygonIntersections <= $PolygonIntersectionsThreshold")
$lines.Add("")

if ($rows.Count -eq 0)
{
    $lines.Add("No PERF PADMATCH entries found. Decision: **Insufficient data**.")
}
else
{
    $sampleCount = $rows.Count
    $maxAvg = ($rows | Measure-Object -Property AvgCandidatePerCad -Maximum).Maximum
    $maxP95 = ($rows | Measure-Object -Property P95CandidatePerCad -Maximum).Maximum
    $maxPoly = ($rows | Measure-Object -Property PolygonIntersections -Maximum).Maximum
    $maxCandidateCells = ($rows | Measure-Object -Property CandidateCellVisits -Maximum).Maximum

    $needUpgrade = $false
    $reasons = New-Object System.Collections.Generic.List[string]
    if ($maxAvg -gt $AvgCandidatePerCadThreshold)
    {
        $needUpgrade = $true
        $reasons.Add("max avgCandidate/CAD=$([math]::Round($maxAvg, 2)) > $AvgCandidatePerCadThreshold")
    }
    if ($maxP95 -gt $P95CandidatePerCadThreshold)
    {
        $needUpgrade = $true
        $reasons.Add("max p95Candidate/CAD=$maxP95 > $P95CandidatePerCadThreshold")
    }
    if ($maxPoly -gt $PolygonIntersectionsThreshold)
    {
        $needUpgrade = $true
        $reasons.Add("max polygonIntersections=$maxPoly > $PolygonIntersectionsThreshold")
    }

    $decision = if ($needUpgrade) { "Consider R-tree / spatial hash (Phase 2)" } else { "Keep current XEdges/YEdges candidate-range index" }

    $lines.Add("## Summary")
    $lines.Add("- Samples: $sampleCount")
    $lines.Add("- Max candidate cell visits: $maxCandidateCells")
    $lines.Add("- Max avg candidate/CAD: $([math]::Round($maxAvg, 2))")
    $lines.Add("- Max p95 candidate/CAD: $maxP95")
    $lines.Add("- Max polygon intersections: $maxPoly")
    $lines.Add("- Decision: **$decision**")
    $lines.Add("")
    if ($needUpgrade)
    {
        $lines.Add("### Triggered conditions")
        foreach ($reason in $reasons)
        {
            $lines.Add("- $reason")
        }
        $lines.Add("")
    }
}

Set-Content -Path $OutPath -Value $lines
Write-Host "PadMatcher decision report written: $OutPath"
