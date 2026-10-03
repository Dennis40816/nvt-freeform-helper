param(
    [string]$LogPath = "build/logs/app.log",
    [string]$OutPath = "build/perf/padmatch-telemetry-latest.md"
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

    $timestamp = ""
    $split = $line.Split("|")
    if ($split.Length -gt 0)
    {
        $timestamp = $split[0].Trim()
    }

    $rows += [PSCustomObject]@{
        Time = $timestamp
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
$lines.Add("# PadMatcher Telemetry Report")
$lines.Add("")
$lines.Add("- Source log: `$LogPath`")
$lines.Add("- Generated at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
$lines.Add("")

if ($rows.Count -eq 0)
{
    $lines.Add("No `PERF PADMATCH` entries found.")
}
else
{
    $lines.Add("| Time | CAD | Candidate cells | Bounds intersections | Polygon intersections | Avg candidate/CAD | P95 candidate/CAD |")
    $lines.Add("| --- | ---: | ---: | ---: | ---: | ---: | ---: |")
    foreach ($row in $rows)
    {
        $lines.Add("| $($row.Time) | $($row.CadCount) | $($row.CandidateCellVisits) | $($row.BoundsIntersections) | $($row.PolygonIntersections) | $([math]::Round($row.AvgCandidatePerCad, 2)) | $($row.P95CandidatePerCad) |")
    }
}

Set-Content -Path $OutPath -Value $lines
Write-Host "PadMatcher telemetry report written: $OutPath"
