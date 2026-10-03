param(
  [switch]$SkipStopApp,
  [switch]$SkipNormalizeLineEndings,
  [switch]$DryRun,
  [switch]$AllFiles,
  [switch]$NoThrow,
  [string]$ResultOutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "test-ui-process-in-repo.ps1")

function Write-ResultFile {
  param(
    [hashtable]$Result
  )

  if ([string]::IsNullOrWhiteSpace($ResultOutputPath)) {
    return
  }

  $dir = Split-Path -Parent $ResultOutputPath
  if (-not [string]::IsNullOrWhiteSpace($dir) -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -Path $dir -ItemType Directory -Force | Out-Null
  }

  $Result |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $ResultOutputPath -Encoding utf8
}

function Get-OtherWorktreeRoots {
  param([string]$RepoRoot)

  try {
    $worktreeLines = @(& git -C $RepoRoot worktree list --porcelain 2>&1)
    if ($LASTEXITCODE -ne 0) {
      throw "exit code $LASTEXITCODE`: $($worktreeLines -join ' | ')"
    }

    $normalizedRepoRoot = $RepoRoot.Replace("\", "/").TrimEnd("/")
    $foundRepoRoot = $false
    $otherRoots = @(
      foreach ($line in $worktreeLines) {
        $text = [string]$line
        if (-not $text.StartsWith("worktree ", [System.StringComparison]::Ordinal)) {
          continue
        }

        $worktreeRoot = $text.Substring("worktree ".Length)
        $normalizedWorktreeRoot = $worktreeRoot.Replace("\", "/").TrimEnd("/")
        if ($normalizedWorktreeRoot.Equals($normalizedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
          $foundRepoRoot = $true
          continue
        }
        $worktreeRoot
      }
    )
    if (-not $foundRepoRoot) {
      throw "current workspace root was absent from Git worktree output: $RepoRoot"
    }
    return $otherRoots
  } catch {
    throw "Cannot run 'git -C $RepoRoot worktree list --porcelain'; no UI processes stopped: $($_.Exception.Message)"
  }
}

function Stop-FreeformHelperUiProcess {
  param(
    [string]$RepoRoot,
    [string[]]$OtherWorktreeRoots,
    [switch]$DryRun
  )

  $processes = @(
    Get-CimInstance Win32_Process |
      Where-Object {
        $_.Name -in @("FreeformHelper.UI.exe", "FreeformHelper.CadLoadSpinner.exe", "dotnet.exe") -and (
          $_.Name -in @("FreeformHelper.UI.exe", "FreeformHelper.CadLoadSpinner.exe") -or
          $_.CommandLine -like "*FreeformHelper.UI.exe*" -or
          $_.CommandLine -like "*FreeformHelper.UI.dll*" -or
          $_.CommandLine -like "*FreeformHelper.UI.csproj*" -or
          $_.CommandLine -like "*freeformhelper.cadloadspinner*"
        )
      }
  )

  $stopped = @()
  $wouldStop = @()
  $skippedForeign = @()
  foreach ($process in $processes) {
    $entry = [ordered]@{
      processId = [int]$process.ProcessId
      name = [string]$process.Name
      executablePath = [string]$process.ExecutablePath
      commandLine = [string]$process.CommandLine
    }
    if (-not (Test-UiProcessInRepo -RepoRoot $RepoRoot -Process $process -OtherWorktreeRoots $OtherWorktreeRoots)) {
      $skippedForeign += $entry
      if ($DryRun) {
        Write-Host ("[prepare-ui-workspace] skipped foreign: pid={0} name={1} path={2} command={3}" -f `
            $entry.processId, $entry.name, $entry.executablePath, $entry.commandLine)
      }
      continue
    }

    if ($DryRun) {
      $wouldStop += $entry
      Write-Host ("[prepare-ui-workspace] would stop: pid={0} name={1} path={2} command={3}" -f `
          $entry.processId, $entry.name, $entry.executablePath, $entry.commandLine)
      continue
    }

    try {
      Stop-Process -Id $process.ProcessId -Force
      $stopped += $entry
    } catch {
      throw "Failed to stop process $($process.ProcessId): $($_.Exception.Message)"
    }
  }

  return @{
    stopped = $stopped
    wouldStop = $wouldStop
    skippedForeign = $skippedForeign
  }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$result = [ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString("o")
  skipStopApp = [bool]$SkipStopApp
  skipNormalizeLineEndings = [bool]$SkipNormalizeLineEndings
  dryRun = [bool]$DryRun
  allFiles = [bool]$AllFiles
  stoppedProcesses = @()
  wouldStopProcesses = @()
  skippedForeignProcesses = @()
  normalizeLineEndings = [ordered]@{
    status = "skipped"
    command = ""
  }
  overallStatus = "pending"
  failureMessage = ""
}

Push-Location $repoRoot
try {
  if (-not $SkipStopApp) {
    $otherWorktreeRoots = @(Get-OtherWorktreeRoots -RepoRoot $repoRoot)
    $processResult = Stop-FreeformHelperUiProcess -RepoRoot $repoRoot -OtherWorktreeRoots $otherWorktreeRoots -DryRun:$DryRun
    $result.stoppedProcesses = @($processResult.stopped)
    $result.wouldStopProcesses = @($processResult.wouldStop)
    $result.skippedForeignProcesses = @($processResult.skippedForeign)
    Write-Host ("[prepare-ui-workspace] stopped FreeformHelper UI processes: {0}" -f $result.stoppedProcesses.Count)
    if ($DryRun) {
      Write-Host ("[prepare-ui-workspace] dry run: would stop {0}, skipped foreign {1}; no processes stopped" -f `
          $result.wouldStopProcesses.Count, $result.skippedForeignProcesses.Count)
    }
  }

  if ($DryRun) {
    $result.normalizeLineEndings.status = "skipped-dry-run"
  }
  if (-not $SkipNormalizeLineEndings -and -not $DryRun) {
    $normalizeScriptPath = Join-Path $repoRoot "scripts/tests/normalize-crlf.ps1"
    if (-not (Test-Path -LiteralPath $normalizeScriptPath)) {
      throw "CRLF normalize script not found: $normalizeScriptPath"
    }

    $normalizeResultPath = Join-Path $repoRoot "build/test-gate/prepare-ui-normalize-crlf-result.json"
    $normalizeSplat = @{
      ResultOutputPath = $normalizeResultPath
    }
    if ($AllFiles) {
      $normalizeSplat["AllFiles"] = $true
    }

    $result.normalizeLineEndings.command = "& $normalizeScriptPath -ResultOutputPath `"$normalizeResultPath`""
    if ($AllFiles) {
      $result.normalizeLineEndings.command += " -AllFiles"
    }

    & $normalizeScriptPath @normalizeSplat
    $result.normalizeLineEndings.status = "ok"
  }

  $result.overallStatus = "ok"
  Write-Host "[prepare-ui-workspace] done"
}
catch {
  $result.overallStatus = "failed"
  $result.failureMessage = $_.Exception.Message
  if (-not $NoThrow) {
    Write-ResultFile -Result $result
    throw
  }
}
finally {
  Write-ResultFile -Result $result
  Pop-Location
}
