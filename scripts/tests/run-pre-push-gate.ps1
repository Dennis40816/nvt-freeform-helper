param(
    [string]$Configuration = "Debug",
    [switch]$UseNoAppHost,
    [string[]]$TestGroups = @("smoke"),
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipLint,
    [switch]$Milestone,
    [switch]$AllowMissingExampleData,
    [string]$SummaryPath = "build/test-gate/pre-push-gate-summary.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# The group names live in run-tests.ps1 only; read them from its parameter so the two cannot drift.
$runTestsCommand = Get-Command (Join-Path $PSScriptRoot "run-tests.ps1")
$validGroups = @($runTestsCommand.Parameters["Group"].Attributes |
    Where-Object { $_ -is [System.Management.Automation.ValidateSetAttribute] } |
    ForEach-Object { $_.ValidValues })

foreach ($group in $TestGroups) {
    if ($validGroups -notcontains $group) {
        throw "Invalid test group '$group'. Valid groups: $($validGroups -join ', ')"
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$buildScript = "scripts/build/build.ps1"
$runTestsScript = "scripts/tests/run-tests.ps1"
$lintScript = "scripts/tests/lint.ps1"
$assertExampleDataScript = "scripts/tests/assert-example-data.ps1"

$stages = New-Object System.Collections.Generic.List[object]

function Invoke-Stage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [ScriptBlock]$Action
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Write-Host ("[pre-push-gate] START {0}" -f $Name) -ForegroundColor Cyan
    & $Action
    $stopwatch.Stop()
    Write-Host ("[pre-push-gate] DONE  {0} ({1} ms)" -f $Name, $stopwatch.ElapsedMilliseconds) -ForegroundColor Green

    $script:stages.Add([PSCustomObject]@{
        stage = $Name
        elapsedMs = $stopwatch.ElapsedMilliseconds
    }) | Out-Null
}

Push-Location $repoRoot
try {
    # Fail before the build when the private example data is missing, not at the commit being pushed, or
    # not yet published to the data repository. This is about what gets pushed, so it runs even when the
    # tests are skipped.
    $null = & $assertExampleDataScript -AllowMissing:$AllowMissingExampleData -ForPush

    if (-not $SkipBuild) {
        Invoke-Stage -Name "build+dependency-graph" -Action {
            & $buildScript -NoTest
        }
    }

    if (-not $SkipTests) {
        foreach ($group in $TestGroups) {
            Invoke-Stage -Name ("tests-{0}" -f $group) -Action {
                & $runTestsScript `
                    -Group $group `
                    -Configuration $Configuration `
                    -UseNoAppHost:$UseNoAppHost `
                    -AllowMissingExampleData:$AllowMissingExampleData
            }
        }
    }

    if (-not $SkipLint) {
        Invoke-Stage -Name "lint-changed" -Action {
            & $lintScript -Configuration $Configuration -UseNoAppHost:$UseNoAppHost
        }
    }

    if ($Milestone) {
        Invoke-Stage -Name "lint-all-files" -Action {
            & $lintScript -Configuration $Configuration -AllFiles -UseNoAppHost:$UseNoAppHost
        }
    }

    $summaryDir = Split-Path -Parent $SummaryPath
    if (-not [string]::IsNullOrWhiteSpace($summaryDir) -and -not (Test-Path -LiteralPath $summaryDir)) {
        New-Item -Path $summaryDir -ItemType Directory -Force | Out-Null
    }

    $summary = [PSCustomObject]@{
        generatedUtc = [DateTime]::UtcNow.ToString("o")
        configuration = $Configuration
        useNoAppHost = $UseNoAppHost
        testGroups = $TestGroups
        skipBuild = $SkipBuild
        skipTests = $SkipTests
        skipLint = $SkipLint
        milestone = $Milestone
        allowMissingExampleData = $AllowMissingExampleData
        stages = $stages
    }

    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $SummaryPath -Encoding utf8
    Write-Host ("[pre-push-gate] summary: {0}" -f $SummaryPath) -ForegroundColor Green
}
finally {
    Pop-Location
}
