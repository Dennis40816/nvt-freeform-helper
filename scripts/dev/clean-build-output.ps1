<#
.SYNOPSIS
Lists this checkout's build output and deletes selected directories with -Apply.
.PARAMETER Apply
Refuses while any listed .NET build, test or UI process exists anywhere on the machine, or the process list cannot be read.
#>
param(
  [switch]$Apply,
  [switch]$IncludeEvidence
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "build-output-selection.ps1")

function Get-OtherWorktreeRoots {
  param([Parameter(Mandatory = $true)][string]$RepoRoot)

  $lines = @(& git -C $RepoRoot worktree list --porcelain)
  if ($LASTEXITCODE -ne 0) {
    throw "Cannot identify other worktrees; no output deleted."
  }
  $roots = @($lines | Where-Object { $_.StartsWith("worktree ") } |
    ForEach-Object { $_.Substring("worktree ".Length) })
  if (@($roots | Where-Object {
      $_.Replace("\", "/").TrimEnd("/").Equals(
        $RepoRoot.Replace("\", "/").TrimEnd("/"), [System.StringComparison]::OrdinalIgnoreCase)
    }).Count -ne 1) {
    throw "Current repository is absent from the worktree list; no output deleted."
  }
  return @($roots | Where-Object {
      -not $_.Replace("\", "/").TrimEnd("/").Equals(
        $RepoRoot.Replace("\", "/").TrimEnd("/"), [System.StringComparison]::OrdinalIgnoreCase)
    })
}

function Assert-NoActiveBuildProcess {
  $guidance = "Close builds and IDE sessions, run 'dotnet build-server shutdown' (build servers linger after a build), then run again."
  try {
    $processes = @(Get-CimInstance Win32_Process -Property Name, ProcessId -ErrorAction Stop)
  } catch {
    throw "Cannot read the process list; no output deleted. $guidance Error: $($_.Exception.Message)"
  }

  $blockingNames = @(Get-BlockingBuildOutputProcessNames -ProcessNames @($processes | ForEach-Object { $_.Name }))
  $blocking = @($processes | Where-Object { $_.Name -in $blockingNames })
  if ($blocking.Count -gt 0) {
    Write-Host "[clean-build-output] blocking processes (name, id):"
    foreach ($process in $blocking) {
      Write-Host ("[clean-build-output]   {0} {1}" -f $process.Name, $process.ProcessId)
    }
    throw "Build/test/UI processes exist on this machine; no output deleted. $guidance"
  }
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "../..")).Path
$selection = @(Get-BuildOutputSelection -RepoRoot $repoRoot -IncludeEvidence:$IncludeEvidence)
$totalBytes = [long]0
Write-Host ("[clean-build-output] root: {0}" -f $repoRoot)
Write-Host ("[clean-build-output] mode: {0}; evidence: {1}" -f $(if ($Apply) { "apply" } else { "dry run" }), $(if ($IncludeEvidence) { "included" } else { "kept" }))
foreach ($entry in $selection) {
  $bytes = Get-BuildOutputBytes -Directory $entry.Path
  $totalBytes += $bytes
  Write-Host ("[clean-build-output] {0}: build/{1}/ ({2:N2} GiB, {3} bytes)" -f $entry.Kind, $entry.Name, ($bytes / 1GB), $bytes)
}
Write-Host ("[clean-build-output] planned recovery: {0:N2} GiB ({1} bytes) across {2} directories" -f ($totalBytes / 1GB), $totalBytes, $selection.Count)

if (-not $Apply) {
  Write-Host "[clean-build-output] dry run; nothing deleted. Use -Apply to delete the listed directories."
  return
}

if ($selection.Count -eq 0) {
  Write-Host "[clean-build-output] nothing to delete."
  return
}

Assert-NoActiveBuildProcess
foreach ($entry in $selection) {
  $otherWorktreeRoots = @(Get-OtherWorktreeRoots -RepoRoot $repoRoot)
  Assert-NoRegisteredWorktreeUnderDirectory -Directory $entry.Path -OtherWorktreeRoots $otherWorktreeRoots
  $null = Get-BuildOutputBytes -Directory $entry.Path
}
foreach ($entry in $selection) {
  $current = @(Get-BuildOutputSelection -RepoRoot $repoRoot -IncludeEvidence:$IncludeEvidence |
    Where-Object { $_.Name -eq $entry.Name })
  if ($current.Count -ne 1 -or $current[0].Path -ne $entry.Path) {
    throw "Build output selection changed; refusing to delete $($entry.Path)."
  }
  $otherWorktreeRoots = @(Get-OtherWorktreeRoots -RepoRoot $repoRoot)
  Assert-NoRegisteredWorktreeUnderDirectory -Directory $entry.Path -OtherWorktreeRoots $otherWorktreeRoots
  $null = Get-BuildOutputBytes -Directory $entry.Path
  Remove-Item -LiteralPath $entry.Path -Recurse -Force
  Write-Host ("[clean-build-output] deleted: build/{0}/" -f $entry.Name)
}
