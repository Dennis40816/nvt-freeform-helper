param(
    [switch]$StructureOnly,
    [switch]$All,
    [ValidateSet("", "structure", "build", "test")]
    [string]$CiLane = "",
    [ValidateSet("", "core", "ui", "viewmodel", "snapshots")]
    [string]$Shard = "",
    [string]$Configuration = "Debug",
    [switch]$AllowMissingExampleData
)

# Single verification entry for local runs and CI. The workflow file names lanes and shards only;
# which checks, projects and test groups they contain is decided here.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$lintScript = Join-Path $repoRoot "scripts/tests/lint.ps1"
$runTestsScript = Join-Path $repoRoot "scripts/tests/run-tests.ps1"
$normalizeScript = Join-Path $repoRoot "scripts/tests/normalize-crlf.ps1"
$xamlRoleScript = Join-Path $repoRoot "scripts/tests/check-xaml-action-roles.ps1"
$assertExampleDataScript = Join-Path $repoRoot "scripts/tests/assert-example-data.ps1"
$uiProcessWorkspaceScript = Join-Path $repoRoot "scripts/tests/check-ui-process-workspace.ps1"
$privatePathCheckScript = Join-Path $repoRoot "scripts/tests/check-private-path-patterns.ps1"
$temporaryEnvironmentCheckScript = Join-Path $repoRoot "scripts/tests/check-temporary-environment.ps1"
. (Join-Path $repoRoot "scripts/tests/private-path-patterns.ps1")
$buildOutputSelectionScript = Join-Path $repoRoot "scripts/tests/check-build-output-selection.ps1"

# Every test class belongs to at least one group here; 'uncategorized' catches the classes no list names.
$shardGroups = [ordered]@{
    core = @("notch-core", "application", "infrastructure", "uncategorized")
    ui = @("ui-stable")
    viewmodel = @("ui-viewmodel")
    snapshots = @("ui-snapshots")
}

$requiredFiles = @(
    ".editorconfig",
    ".gitattributes",
    ".gitmodules",
    "AGENTS.md",
    "Directory.Build.props",
    "Directory.Packages.props",
    "FreeformHelper.sln",
    "LICENSE",
    "README.md",
    "TODO.md",
    "VERSION",
    "global.json"
)

# Workstation paths must not be published: a Windows user profile with either slash style, the owner's
# development root, and Unix or Git Bash home folders. The boundaries are ASCII on purpose: .NET's \b and
# \w treat CJK characters as word characters, so a path right after Chinese prose would not match. The
# user name starts with a letter, digit or underscore of any script and continues with whatever a folder
# name allows, so placeholders and route templates (%USERNAME%, {user}, /users/${id}) and a bare
# 'C:\Users\' are not reported. An allowed name counts only when the name ends right after it; punctuation
# that follows it in prose or Markdown ends it too.
$privatePathPatterns = @(Get-PrivatePathPatterns)

$binaryExtensions = @(
    ".dll", ".dwg", ".exe", ".gif", ".ico", ".jpeg", ".jpg", ".pdf", ".png", ".ttf", ".xlsx", ".zip", ".7z"
)

function Write-Lane {
    param([string]$Message)
    Write-Host ("[verify] {0}" -f $Message) -ForegroundColor Cyan
}

function Add-StructureFailures {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$failures
    )

    foreach ($file in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $file) -PathType Leaf)) {
            $failures.Add("Required file is missing: $file")
        }
    }

    $versionPath = Join-Path $repoRoot "VERSION"
    if (Test-Path -LiteralPath $versionPath -PathType Leaf) {
        $versionText = [System.IO.File]::ReadAllText($versionPath)
        if ($versionText -notmatch '\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\r\n\z') {
            $failures.Add("VERSION must contain one MAJOR.MINOR.PATCH line ending in CRLF, without leading zeros.")
        }
    }

    $buildPropsPath = Join-Path $repoRoot "Directory.Build.props"
    if (Test-Path -LiteralPath $buildPropsPath -PathType Leaf) {
        [xml]$buildProps = Get-Content -LiteralPath $buildPropsPath -Raw
        $versionNodes = @($buildProps.SelectNodes("/Project/PropertyGroup/Version"))
        $versionReadExpression = '$([System.IO.File]::ReadAllText(''$(MSBuildThisFileDirectory)VERSION'').Trim())'
        if ($versionNodes.Count -ne 1 -or $versionNodes[0].InnerText -cne $versionReadExpression) {
            $failures.Add("Directory.Build.props must set Version by reading the root VERSION file with the expected MSBuild property function.")
        }
    }

    # example/ is confidential data in a private repository; only the submodule link may be tracked here.
    $exampleEntries = @(git -C $repoRoot ls-files -s -- example)
    if ($exampleEntries.Count -ne 1 -or -not $exampleEntries[0].StartsWith("160000 ")) {
        $failures.Add("'example' must be tracked only as a submodule link; found $($exampleEntries.Count) tracked entries.")
    }

    # Every tracked file except known binary types; quotePath off so non-ASCII names resolve on disk.
    $trackedTextFiles = @(git -C $repoRoot -c core.quotePath=false ls-files |
        Where-Object { $binaryExtensions -notcontains [System.IO.Path]::GetExtension($_).ToLowerInvariant() })
    foreach ($relativePath in $trackedTextFiles) {
        $fullPath = Join-Path $repoRoot $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            continue
        }

        $hits = @(Select-String -LiteralPath $fullPath -Pattern $privatePathPatterns)
        foreach ($hit in $hits) {
            $failures.Add("Private workstation path in ${relativePath}:$($hit.LineNumber)")
        }
    }

    $globalJson = Get-Content -LiteralPath (Join-Path $repoRoot "global.json") -Raw | ConvertFrom-Json
    $sdkVersion = [string]$globalJson.sdk.version
    if ($sdkVersion -notmatch '^\d+\.\d+\.\d+$') {
        $failures.Add("global.json must pin a stable SDK version; found '$sdkVersion'.")
    }

    # The workflow names the shards and this script defines them; a shard missing on either side would
    # silently leave its tests out of CI.
    $ciWorkflow = Join-Path $repoRoot ".github/workflows/ci.yml"
    if (Test-Path -LiteralPath $ciWorkflow) {
        $matrixLine = Select-String -LiteralPath $ciWorkflow -Pattern '^\s*shard:\s*\[(.+)\]\s*$' | Select-Object -First 1
        if ($null -eq $matrixLine) {
            $failures.Add("ci.yml has no 'shard: [...]' matrix line to compare with the shards of verify.ps1.")
        } else {
            $workflowShards = @($matrixLine.Matches[0].Groups[1].Value -split ',' | ForEach-Object { $_.Trim() } | Sort-Object)
            $definedShards = @($shardGroups.Keys | Sort-Object)
            if (($workflowShards -join ',') -cne ($definedShards -join ',')) {
                $failures.Add("ci.yml runs shards [$($workflowShards -join ', ')] but verify.ps1 defines [$($definedShards -join ', ')].")
            }
        }
    }

    $workflowRoot = Join-Path $repoRoot ".github/workflows"
    if (Test-Path -LiteralPath $workflowRoot) {
        $workflowFiles = @(Get-ChildItem -LiteralPath $workflowRoot -File | Where-Object { $_.Extension -in @(".yml", ".yaml") })
        foreach ($workflow in $workflowFiles) {
            $uses = @(Select-String -LiteralPath $workflow.FullName -Pattern '^\s*(?:-\s*)?uses:\s*(\S+)')
            foreach ($use in $uses) {
                $reference = $use.Matches[0].Groups[1].Value
                if ($reference.StartsWith("./")) {
                    continue
                }

                if ($reference -notmatch '@[0-9a-f]{40}$') {
                    $failures.Add("Action is not pinned to a full commit SHA in $($workflow.Name):$($use.LineNumber): $reference")
                }
            }
        }
    }
}

function Invoke-StructureLane {
    Write-Lane "structure: required files, project version, submodule link, private paths, SDK pin, action pins"
    $failures = New-Object System.Collections.Generic.List[string]
    & $privatePathCheckScript
    & $temporaryEnvironmentCheckScript
    Add-StructureFailures -failures $failures

    Write-Lane "structure: test group lists"
    & $runTestsScript -ValidateOnly

    Write-Lane "structure: UI process workspace ownership"
    & $uiProcessWorkspaceScript

    Write-Lane "structure: build output cleanup selection"
    & $buildOutputSelectionScript

    Write-Lane "structure: line endings"
    $normalizeResultPath = Join-Path $repoRoot "build/test-gate/verify-normalize-crlf-result.json"
    & $normalizeScript -AllFiles -ResultOutputPath $normalizeResultPath
    $normalizeResult = Get-Content -LiteralPath $normalizeResultPath -Raw | ConvertFrom-Json
    if ([int]$normalizeResult.normalizedFiles -gt 0) {
        $failures.Add("$($normalizeResult.normalizedFiles) file(s) did not use CRLF; they were normalized in the working tree and must be committed that way.")
    }

    Write-Lane "structure: XAML action roles"
    & $xamlRoleScript

    if ($failures.Count -gt 0) {
        foreach ($failure in $failures) {
            Write-Host ("[verify] FAIL {0}" -f $failure) -ForegroundColor Red
        }

        throw "Structure verification failed with $($failures.Count) finding(s)."
    }
}

function Invoke-BuildLane {
    Write-Lane "build: format, analyzers and UI build with warnings as errors"
    & $lintScript -Configuration $Configuration -AllFiles -UseNoAppHost -WarningsAsErrors
}

function Invoke-TestShard {
    param([string]$Name)

    foreach ($group in $shardGroups[$Name]) {
        Write-Lane "test shard '$Name': group '$group'"
        & $runTestsScript `
            -Group $group `
            -Configuration $Configuration `
            -UseNoAppHost `
            -AllowMissingExampleData:$AllowMissingExampleData
    }
}

$lanes = @()
if ($All) {
    $lanes = @("structure", "build", "test")
} elseif ($StructureOnly) {
    $lanes = @("structure")
} elseif (-not [string]::IsNullOrWhiteSpace($CiLane)) {
    $lanes = @($CiLane)
} else {
    throw "Choose one of -StructureOnly, -All, or -CiLane <structure|build|test>."
}

$shards = if ([string]::IsNullOrWhiteSpace($Shard)) { @($shardGroups.Keys) } else { @($Shard) }

Push-Location $repoRoot
try {
    # Without the private data the golden tests skip; a run that includes tests must not pass that way by
    # accident, so check the data before the slow lanes.
    if ($lanes -contains "test") {
        $null = & $assertExampleDataScript -AllowMissing:$AllowMissingExampleData
    }

    foreach ($lane in $lanes) {
        switch ($lane) {
            "structure" { Invoke-StructureLane }
            "build" { Invoke-BuildLane }
            "test" {
                foreach ($shardName in $shards) {
                    Invoke-TestShard -Name $shardName
                }
            }
        }
    }

    Write-Host ("[verify] OK ({0})" -f ($lanes -join ", ")) -ForegroundColor Green
}
finally {
    Pop-Location
}
