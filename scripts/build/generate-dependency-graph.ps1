param(
    [string]$SolutionPath = "FreeformHelper.sln",
    [string]$OutFile = "docs/generated/project-dependency-graph.md",
    [string]$JsonOutFile = "docs/generated/project-dependency-graph.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\\.."))
$solutionFullPath = Resolve-AbsolutePath -PathValue $SolutionPath -BasePath $repoRoot

if (-not (Test-Path -LiteralPath $solutionFullPath)) {
    throw "Solution not found: $solutionFullPath"
}

$solutionDir = Split-Path -Path $solutionFullPath -Parent

$slnListOutput = & dotnet sln $solutionFullPath list
if ($LASTEXITCODE -ne 0) {
    throw "Failed to list projects from solution: $solutionFullPath"
}

$projectRelPaths = $slnListOutput |
    Where-Object { $_.TrimEnd() -like "*.csproj" } |
    ForEach-Object { $_.Trim() } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

if ($projectRelPaths.Count -eq 0) {
    throw "No .csproj entries found in solution: $solutionFullPath"
}

$projects = @()
$pathToProject = @{}

for ($i = 0; $i -lt $projectRelPaths.Count; $i++) {
    $relative = $projectRelPaths[$i]
    $fullPath = Resolve-AbsolutePath -PathValue $relative -BasePath $solutionDir

    if (-not (Test-Path -LiteralPath $fullPath)) {
        continue
    }

    $project = [PSCustomObject]@{
        NodeId = "P$($i + 1)"
        Name = [System.IO.Path]::GetFileNameWithoutExtension($fullPath)
        RelativePath = [System.IO.Path]::GetRelativePath($repoRoot, $fullPath).Replace("\\", "/")
        FullPath = $fullPath
    }

    $projects += $project
    $pathToProject[$fullPath.ToLowerInvariant()] = $project
}

if ($projects.Count -eq 0) {
    throw "No valid projects resolved from solution."
}

$edgeSet = @{}
$edges = @()

foreach ($project in $projects) {
    [xml]$xml = Get-Content -LiteralPath $project.FullPath
    $references = Select-Xml -Xml $xml -XPath "//ProjectReference"

    foreach ($reference in $references) {
        $include = $reference.Node.Include
        if ([string]::IsNullOrWhiteSpace($include)) {
            continue
        }

        $targetPath = Resolve-AbsolutePath -PathValue $include -BasePath (Split-Path -Path $project.FullPath -Parent)
        $targetKey = $targetPath.ToLowerInvariant()

        if (-not $pathToProject.ContainsKey($targetKey)) {
            continue
        }

        $target = $pathToProject[$targetKey]
        $edgeKey = "$($project.NodeId)->$($target.NodeId)"
        if ($edgeSet.ContainsKey($edgeKey)) {
            continue
        }

        $edgeSet[$edgeKey] = $true
        $edges += [PSCustomObject]@{
            From = $project
            To = $target
        }
    }
}

$outFullPath = Resolve-AbsolutePath -PathValue $OutFile -BasePath $repoRoot
$jsonOutFullPath = Resolve-AbsolutePath -PathValue $JsonOutFile -BasePath $repoRoot
$outDir = Split-Path -Path $outFullPath -Parent
$jsonOutDir = Split-Path -Path $jsonOutFullPath -Parent

if (-not (Test-Path -LiteralPath $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $jsonOutDir)) {
    New-Item -Path $jsonOutDir -ItemType Directory -Force | Out-Null
}

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'")
$sourceSolutionRel = [System.IO.Path]::GetRelativePath($repoRoot, $solutionFullPath).Replace("\\", "/")

$mermaidLines = @("graph LR")
foreach ($project in ($projects | Sort-Object Name, RelativePath)) {
    $safeLabel = $project.Name.Replace('"', "'")
    $mermaidLines += "  $($project.NodeId)[`"$safeLabel`"]"
}

if ($edges.Count -eq 0) {
    $mermaidLines += "  %% no project-to-project references"
}
else {
    foreach ($edge in ($edges | Sort-Object { $_.From.Name }, { $_.To.Name })) {
        $mermaidLines += "  $($edge.From.NodeId) --> $($edge.To.NodeId)"
    }
}

$markdown = @(
    "# Project Dependency Graph",
    "",
    "- Generated: $timestamp",
    "- Source solution: ``$sourceSolutionRel``",
    "",
    '```mermaid'
) + $mermaidLines + @(
    '```',
    "",
    "## Project Paths",
    ""
)

foreach ($project in ($projects | Sort-Object Name, RelativePath)) {
    $markdown += "- ``$($project.Name)``: ``$($project.RelativePath)``"
}

$markdown += @(
    "",
    "## Usage",
    "",
    "- Run ``./scripts/build/generate-dependency-graph.ps1`` to refresh this graph.",
    "- Task kickoff should consult this graph first, then do targeted file search."
)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($outFullPath, ($markdown -join "`r`n") + "`r`n", $utf8NoBom)

$jsonPayload = [PSCustomObject]@{
    generatedAtUtc = $timestamp
    sourceSolution = $sourceSolutionRel
    projects = @(
        foreach ($project in ($projects | Sort-Object Name, RelativePath)) {
            [PSCustomObject]@{
                id = $project.NodeId
                name = $project.Name
                path = $project.RelativePath
            }
        }
    )
    edges = @(
        foreach ($edge in ($edges | Sort-Object { $_.From.Name }, { $_.To.Name })) {
            [PSCustomObject]@{
                from = $edge.From.Name
                to = $edge.To.Name
            }
        }
    )
}

[System.IO.File]::WriteAllText($jsonOutFullPath, ($jsonPayload | ConvertTo-Json -Depth 8), $utf8NoBom)

Write-Host "Dependency graph generated:"
Write-Host "  - $outFullPath"
Write-Host "  - $jsonOutFullPath"
