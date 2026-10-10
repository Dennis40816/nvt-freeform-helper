param(
  [string]$Configuration = "Debug",
  [switch]$Fix,
  [switch]$AllFiles,
  [switch]$SkipNormalizeLineEndings,
  [switch]$NoThrow,
  [string]$ResultOutputPath = "",
  [switch]$UseNoAppHost,
  [switch]$WarningsAsErrors,
  [ValidateSet("UiProject", "Solution")]
  [string]$AnalyzerScope = "UiProject"
)

$ErrorActionPreference = "Stop"

Set-StrictMode -Version Latest

function Get-FailureKind {
  param(
    [string]$Message
  )

  if ([string]::IsNullOrWhiteSpace($Message)) {
    return "unknown"
  }

  $normalized = $Message.ToLowerInvariant()
  $environmentMarkers = @(
    "being used by another process",
    "program '",
    "failed to run",
    "resourceunavailable",
    "access is denied",
    "cannot find path",
    "could not find",
    "no executable found matching command",
    "standardoutputencoding is only supported"
  )

  foreach ($marker in $environmentMarkers) {
    if ($normalized.Contains($marker)) {
      return "environment"
    }
  }

  return "code"
}

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

$lintResult = [ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString("o")
  configuration = $Configuration
  fix = [bool]$Fix
  allFiles = [bool]$AllFiles
  skipNormalizeLineEndings = [bool]$SkipNormalizeLineEndings
  useNoAppHost = [bool]$UseNoAppHost
  analyzerScope = $AnalyzerScope
  normalizeLineEndings = [ordered]@{
    status = "pending"
    command = ""
    scannedFiles = 0
    normalizedFiles = 0
    skippedBinaryFiles = 0
    skippedMissingFiles = 0
  }
  xamlActionRoles = [ordered]@{
    status = "pending"
    command = ""
    scannedFiles = 0
    checkedElements = 0
    issueCount = 0
  }
  formatter = [ordered]@{
    status = "pending"
    command = ""
  }
  analyzerBuild = [ordered]@{
    status = "pending"
    command = ""
  }
  overallStatus = "pending"
  failureKind = ""
  failureMessage = ""
}

function Invoke-DotnetChecked {
  param(
    [Parameter(Mandatory = $true)]
    [string[]]$Args,
    [Parameter(Mandatory = $true)]
    [string]$Label
  )

  dotnet @Args
  if ($LASTEXITCODE -ne 0) {
    throw "[lint] $Label failed with exit code $LASTEXITCODE."
  }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Push-Location $repoRoot
try {
  $solutionPath = Join-Path $repoRoot "FreeformHelper.sln"
  if (-not (Test-Path $solutionPath)) {
    throw "Solution file not found: $solutionPath"
  }

  $uiProjectPath = Join-Path $repoRoot "src/FreeformHelper.UI/FreeformHelper.UI.csproj"
  if (-not (Test-Path $uiProjectPath)) {
    throw "UI project file not found: $uiProjectPath"
  }

  if (-not $SkipNormalizeLineEndings) {
    $normalizeScriptPath = Join-Path $repoRoot "scripts/tests/normalize-crlf.ps1"
    if (-not (Test-Path -LiteralPath $normalizeScriptPath)) {
      throw "CRLF normalize script not found: $normalizeScriptPath"
    }

    $normalizeResultPath = Join-Path $repoRoot "build/test-gate/lint-normalize-crlf-result.json"
    $normalizeSplat = @{
      ResultOutputPath = $normalizeResultPath
    }
    if ($AllFiles) {
      $normalizeSplat["AllFiles"] = $true
    }

    Write-Host "[lint] normalize CRLF line endings"
    $normalizeCommandText = "& $normalizeScriptPath -ResultOutputPath `"$normalizeResultPath`""
    if ($AllFiles) {
      $normalizeCommandText += " -AllFiles"
    }

    $lintResult.normalizeLineEndings.command = $normalizeCommandText
    & $normalizeScriptPath @normalizeSplat

    if (Test-Path -LiteralPath $normalizeResultPath) {
      $normalizeResult = Get-Content -LiteralPath $normalizeResultPath -Raw | ConvertFrom-Json
      $lintResult.normalizeLineEndings.status = if ([string]::IsNullOrWhiteSpace($normalizeResult.overallStatus)) { "ok" } else { $normalizeResult.overallStatus }
      $lintResult.normalizeLineEndings.scannedFiles = [int]$normalizeResult.scannedFiles
      $lintResult.normalizeLineEndings.normalizedFiles = [int]$normalizeResult.normalizedFiles
      $lintResult.normalizeLineEndings.skippedBinaryFiles = [int]$normalizeResult.skippedBinaryFiles
      $lintResult.normalizeLineEndings.skippedMissingFiles = [int]$normalizeResult.skippedMissingFiles
    } else {
      $lintResult.normalizeLineEndings.status = "ok"
    }
  } else {
    $lintResult.normalizeLineEndings.status = "skipped"
  }

  $xamlActionRoleScriptPath = Join-Path $repoRoot "scripts/tests/check-xaml-action-roles.ps1"
  if (-not (Test-Path -LiteralPath $xamlActionRoleScriptPath)) {
    throw "XAML action role guard script not found: $xamlActionRoleScriptPath"
  }

  $xamlActionRoleResultPath = Join-Path $repoRoot "build/test-gate/lint-xaml-action-roles-result.json"
  Write-Host "[lint] xaml action role guard"
  $lintResult.xamlActionRoles.command = "& $xamlActionRoleScriptPath -ResultOutputPath `"$xamlActionRoleResultPath`""
  & $xamlActionRoleScriptPath -ResultOutputPath $xamlActionRoleResultPath

  if (Test-Path -LiteralPath $xamlActionRoleResultPath) {
    $xamlActionRoleResult = Get-Content -LiteralPath $xamlActionRoleResultPath -Raw | ConvertFrom-Json
    $lintResult.xamlActionRoles.status = if ([string]::IsNullOrWhiteSpace($xamlActionRoleResult.overallStatus)) { "ok" } else { $xamlActionRoleResult.overallStatus }
    $lintResult.xamlActionRoles.scannedFiles = [int]$xamlActionRoleResult.scannedFiles
    $lintResult.xamlActionRoles.checkedElements = [int]$xamlActionRoleResult.checkedElements
    $lintResult.xamlActionRoles.issueCount = [int]$xamlActionRoleResult.issueCount
  } else {
    $lintResult.xamlActionRoles.status = "ok"
  }

  $changedCsharpFiles = @()
  if (-not $AllFiles) {
    $trackedChanged = @(git diff --name-only --diff-filter=ACMRTUXB HEAD | Where-Object { $_ -match '\.(cs|csx)$' })
    $untrackedChanged = @(git ls-files --others --exclude-standard | Where-Object { $_ -match '\.(cs|csx)$' })
    $changedCsharpFiles = @($trackedChanged + $untrackedChanged | Sort-Object -Unique)
  }

  Write-Host "[lint] dotnet format ($(if ($Fix) { 'apply' } else { 'verify' }))"
  $formatArgs = @("format", $solutionPath, "--verbosity", "minimal", "--severity", "warn")
  # This canonical source is verified by hash and must never be reformatted.
  $formatArgs += @("--exclude", "src/FreeformHelper.CoreSource/UiEventRunner.cs", "eng/core-health")
  #
  # Keep format gate focused on code-style/formatting drift.
  # A few existing analyzer diagnostics have no deterministic auto-fix in dotnet format.
  $formatExcludedDiagnostics = @("IDE0059", "IDE0060")
  if ($formatExcludedDiagnostics.Count -gt 0) {
    $formatArgs += "--exclude-diagnostics"
    $formatArgs += $formatExcludedDiagnostics
  }
  if (-not $Fix) {
    $formatArgs += "--verify-no-changes"
  }
  if (-not $AllFiles) {
    if ($changedCsharpFiles.Count -eq 0) {
      Write-Host "[lint] no changed C# files detected; skipping formatter scope."
    } else {
      Write-Host "[lint] changed C# files: $($changedCsharpFiles.Count)"
      $formatArgs += "--include"
      $formatArgs += $changedCsharpFiles
    }
  }

  $lintResult.formatter.command = "dotnet " + ($formatArgs -join " ")
  if ($AllFiles -or $changedCsharpFiles.Count -gt 0) {
    Invoke-DotnetChecked -Args $formatArgs -Label "dotnet format"
    $lintResult.formatter.status = "ok"
  } else {
    $lintResult.formatter.status = "skipped"
  }

  Write-Host "[lint] analyzer build ($Configuration)"
  $analyzerTargetPath = if ($AnalyzerScope -eq "Solution") { $solutionPath } else { $uiProjectPath }
  Write-Host "[lint] analyzer target: $AnalyzerScope ($analyzerTargetPath)"
  $buildArgs = @("build", $analyzerTargetPath, "-c", $Configuration, "--nologo")
  if ($UseNoAppHost) {
    $buildArgs += "/p:UseAppHost=false"
  }

  if ($WarningsAsErrors) {
    # An up-to-date project is not recompiled and would not report its warnings again.
    $buildArgs += @("-warnaserror", "--no-incremental")
  }

  $lintResult.analyzerBuild.command = "dotnet " + ($buildArgs -join " ")
  Invoke-DotnetChecked -Args $buildArgs -Label "analyzer build"
  $lintResult.analyzerBuild.status = "ok"
  $lintResult.overallStatus = "ok"
  Write-Host "[lint] done"
}
catch {
  $message = $_.Exception.Message
  if ($lintResult.normalizeLineEndings.status -eq "pending") {
    $lintResult.normalizeLineEndings.status = "failed"
  } elseif ($lintResult.xamlActionRoles.status -eq "pending") {
    $lintResult.xamlActionRoles.status = "failed"
  } elseif ($lintResult.formatter.status -eq "pending") {
    $lintResult.formatter.status = "failed"
  } elseif ($lintResult.analyzerBuild.status -eq "pending") {
    $lintResult.analyzerBuild.status = "failed"
  }

  $lintResult.overallStatus = "failed"
  $lintResult.failureKind = Get-FailureKind -Message $message
  $lintResult.failureMessage = $message
  if (-not $NoThrow) {
    Write-ResultFile -Result $lintResult
    throw
  }
}
finally {
  Write-ResultFile -Result $lintResult
  Pop-Location
}
