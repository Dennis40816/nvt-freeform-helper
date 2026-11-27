# Copyright (c) 2026 Dennis Liu. All rights reserved.

<#
.SYNOPSIS
Captures CadAllocation V22 row evidence or compares snapshots, including snapshots from other worktrees.
.DESCRIPTION
Build the test project first. This tool runs only the opt-in host with --no-build --no-restore.
All outputs must be under the current worktree's build directory. Inputs are never modified.
Project load uses the existing test settings isolation. Host output is not echoed because it may
contain private paths on failure. No background process or terminal window is started.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('snapshot', 'compare')]
    [string]$Action,

    [string]$ProjectPath,
    [string]$BeforePath,
    [string]$AfterPath,

    [Parameter(Mandatory)]
    [string]$OutputPath,

    [ValidateSet('CurrentGain', 'ConservativeNoGain', 'Disabled')]
    [string]$CompensationModel,

    [ValidateSet('On', 'Off')]
    [string]$BoundaryVirtualAreaCap,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildRoot = Join-Path $repoRoot 'build'
$output = [IO.Path]::GetFullPath($OutputPath, $repoRoot)
if (-not $output.StartsWith($buildRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputPath must be under this worktree''s build directory.'
}

# Resolve-Path errors quote the path, which may be private.
function Resolve-InputFile([string]$Path, [string]$Name) {
    try {
        return (Resolve-Path -LiteralPath $Path).Path
    }
    catch {
        throw "$Name does not point to a readable file."
    }
}

$request = [ordered]@{ Action = $Action.ToLowerInvariant(); OutputPath = $output }
if ($Action -eq 'snapshot') {
    if ([string]::IsNullOrWhiteSpace($ProjectPath) -or $BeforePath -or $AfterPath) {
        throw 'snapshot requires ProjectPath and does not accept BeforePath or AfterPath.'
    }
    $request.ProjectPath = Resolve-InputFile $ProjectPath 'ProjectPath'
    if ($CompensationModel) { $request.CompensationModel = $CompensationModel }
    if ($BoundaryVirtualAreaCap) { $request.BoundaryVirtualAreaCap = $BoundaryVirtualAreaCap -eq 'On' }
}
else {
    if ([string]::IsNullOrWhiteSpace($BeforePath) -or [string]::IsNullOrWhiteSpace($AfterPath) -or
        $ProjectPath -or $CompensationModel -or $BoundaryVirtualAreaCap) {
        throw 'compare requires BeforePath and AfterPath, and does not accept project settings.'
    }
    $request.BeforePath = Resolve-InputFile $BeforePath 'BeforePath'
    $request.AfterPath = Resolve-InputFile $AfterPath 'AfterPath'
}

$previousRequest = $env:NFH_NOTCH_AB_REQUEST
try {
    $env:NFH_NOTCH_AB_REQUEST = $request | ConvertTo-Json -Compress
    $testProject = Join-Path $repoRoot 'tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj'
    $hostOutput = & dotnet test $testProject -c $Configuration --no-build --no-restore --nologo `
        -p:UseAppHost=false --filter 'FullyQualifiedName=FreeformHelper.Tests.NotchAbReportHost.RunAsync' `
        --logger 'console;verbosity=quiet' 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw 'The A/B host failed. Build the selected configuration and check project inputs; host output was withheld for confidentiality.'
    }
    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
        throw 'The A/B host did not write the requested output. Check that the opt-in host is built.'
    }
    Write-Output 'A/B output written under build/.'
}
finally {
    $env:NFH_NOTCH_AB_REQUEST = $previousRequest
    $hostOutput = $null
}
