param(
    [string]$Configuration = "Debug",
    [switch]$SkipLint,
    [switch]$SkipBuild,
    [switch]$UseNoAppHost,
    [switch]$IncludeNotchCore,
    [switch]$IncludeInfrastructure,
    [switch]$IncludeUiSnapshots,
    [switch]$LintAllFiles,
    [switch]$IncludeStartupBudget,
    [switch]$AllowMissingExampleData,
    [int]$InitialGridBudgetMs = 1000,
    [string]$StartupBudgetProjectPath = "",
    [ValidateSet("UiProject", "Solution")]
    [string]$LintAnalyzerScope = "UiProject"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$uiProject = "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
$runTestsScript = "scripts/tests/run-tests.ps1"
$lintScript = "scripts/tests/lint.ps1"
$startupBudgetScript = "scripts/tests/check-startup-budget.ps1"
$assertExampleDataScript = "scripts/tests/assert-example-data.ps1"

# The gate runs every test group. -IncludeNotchCore, -IncludeInfrastructure and -IncludeUiSnapshots are
# still accepted so existing command lines keep working, but those groups now always run.
$testGroups = @("notch-core", "application", "infrastructure", "ui-core", "ui-snapshots", "uncategorized")

$stages = New-Object System.Collections.Generic.List[object]

function Invoke-Stage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$Action
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host ("[gate] START {0}" -f $Name) -ForegroundColor Cyan
    & $Action
    $stopwatch.Stop()
    Write-Host ("[gate] DONE  {0} ({1} ms)" -f $Name, $stopwatch.ElapsedMilliseconds) -ForegroundColor Green

    $script:stages.Add([PSCustomObject]@{
        stage = $Name
        elapsedMs = $stopwatch.ElapsedMilliseconds
    }) | Out-Null
}

Push-Location $repoRoot
try {
    # Check the private example data before the slow stages, so a missing or drifted checkout fails here
    # with one clear message instead of as file-not-found errors after lint and build.
    $null = & $assertExampleDataScript -AllowMissing:$AllowMissingExampleData

    if (-not $SkipLint) {
        Invoke-Stage -Name "lint" -Action {
            & $lintScript `
                -Configuration $Configuration `
                -AllFiles:$LintAllFiles `
                -AnalyzerScope $LintAnalyzerScope `
                -UseNoAppHost:$UseNoAppHost
        }
    }

    if (-not $SkipBuild) {
        Invoke-Stage -Name "build-ui" -Action {
            $buildArgs = @("build", $uiProject, "-c", $Configuration, "--nologo")
            if ($UseNoAppHost) {
                $buildArgs += "/p:UseAppHost=false"
            }

            dotnet @buildArgs
            if ($LASTEXITCODE -ne 0) {
                throw "UI build failed with exit code $LASTEXITCODE."
            }
        }
    }

    foreach ($testGroup in $testGroups) {
        Invoke-Stage -Name ("tests-{0}" -f $testGroup) -Action {
            & $runTestsScript `
                -Group $testGroup `
                -Configuration $Configuration `
                -UseNoAppHost:$UseNoAppHost `
                -AllowMissingExampleData:$AllowMissingExampleData
        }
    }

    if ($IncludeStartupBudget) {
        Invoke-Stage -Name "startup-budget" -Action {
            & $startupBudgetScript `
                -Configuration $Configuration `
                -SkipBuild `
                -InitialGridBudgetMs $InitialGridBudgetMs `
                -ProjectPath $StartupBudgetProjectPath
        }
    }

    $summaryPath = "build/test-gate/refactor-gate-summary.json"
    $summaryDir = Split-Path -Parent $summaryPath
    if (-not (Test-Path -LiteralPath $summaryDir)) {
        New-Item -Path $summaryDir -ItemType Directory -Force | Out-Null
    }

    $summary = [PSCustomObject]@{
        generatedUtc = [DateTime]::UtcNow.ToString("o")
        configuration = $Configuration
        skipLint = $SkipLint
        skipBuild = $SkipBuild
        testGroups = $testGroups
        includeStartupBudget = $IncludeStartupBudget
        initialGridBudgetMs = $InitialGridBudgetMs
        startupBudgetProjectPath = $StartupBudgetProjectPath
        lintAllFiles = $LintAllFiles
        lintAnalyzerScope = $LintAnalyzerScope
        useNoAppHost = $UseNoAppHost
        allowMissingExampleData = $AllowMissingExampleData
        stages = $stages
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding utf8

    Write-Host ("[gate] summary: {0}" -f $summaryPath) -ForegroundColor Green
}
finally {
    Pop-Location
}
