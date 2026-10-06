param(
    [string]$Configuration = "Debug",
    [string]$UiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj",
    [string]$ExePath = "build/bin/FreeformHelper.UI/Debug/net10.0/FreeformHelper.UI.exe",
    [string]$ProjectPath = "",
    [int]$StartupTimeoutMs = 60000,
    [int]$PollIntervalMs = 400,
    [int]$InitialGridBudgetMs = 1000,
    [string]$LogPath = "build/logs/app.log",
    [string]$OutPath = "build/perf/startup-markers-latest.md",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$measureScript = Join-Path $repoRoot "scripts/perf/measure-startup-load.ps1"
$analyzeScript = Join-Path $repoRoot "scripts/perf/analyze-startup-markers.ps1"

Push-Location $repoRoot
try {
    if (-not $SkipBuild) {
        dotnet build $UiProject -c $Configuration --nologo
        if ($LASTEXITCODE -ne 0) {
            throw "UI build failed with exit code $LASTEXITCODE."
        }
    }

    $measureArgs = @{
        UiProject = $UiProject
        ExePath = $ExePath
        StartupTimeoutMs = $StartupTimeoutMs
        PollIntervalMs = $PollIntervalMs
        SkipBuild = $true
    }

    if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) {
        $measureArgs.ProjectPath = $ProjectPath
    }

    & $measureScript @measureArgs

    & $analyzeScript `
        -LogPath $LogPath `
        -OutPath $OutPath `
        -InitialGridBudgetMs $InitialGridBudgetMs `
        -FailOnBudgetViolation

    Write-Host ("Startup budget gate passed: workspace.initial-grid-built <= {0}ms" -f $InitialGridBudgetMs) -ForegroundColor Green
}
finally {
    Pop-Location
}
