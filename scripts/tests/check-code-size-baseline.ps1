param(
  [switch]$NoThrow,
  [string]$ResultOutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Add-Issue {
  param(
    [string]$Message
  )

  $script:issues.Add($Message)
}

function Assert-Equal {
  param(
    [object]$Actual,
    [object]$Expected,
    [string]$Label
  )

  if ($Actual -ne $Expected) {
    Add-Issue "$Label expected '$Expected', actual '$Actual'."
  }
}

function Assert-Metric {
  param(
    [object]$Metric,
    [int]$Files,
    [int]$Physical,
    [int]$Nonblank,
    [string]$Label
  )

  Assert-Equal $Metric.files $Files "$Label.files"
  Assert-Equal $Metric.physical $Physical "$Label.physical"
  Assert-Equal $Metric.nonblank $Nonblank "$Label.nonblank"
}

function Add-Metrics {
  param(
    [object[]]$Metrics
  )

  $sum = [ordered]@{ files = 0; physical = 0; nonblank = 0 }
  foreach ($metric in $Metrics) {
    $sum.files += [int]$metric.files
    $sum.physical += [int]$metric.physical
    $sum.nonblank += [int]$metric.nonblank
  }

  return [PSCustomObject]$sum
}

function Assert-MetricEquals {
  param(
    [object]$Actual,
    [object]$Expected,
    [string]$Label
  )

  Assert-Equal $Actual.files $Expected.files "$Label.files"
  Assert-Equal $Actual.physical $Expected.physical "$Label.physical"
  Assert-Equal $Actual.nonblank $Expected.nonblank "$Label.nonblank"
}

function Assert-Delta {
  param(
    [object]$Actual,
    [object]$Current,
    [object]$Reference,
    [string]$Label
  )

  Assert-Equal $Actual.files ([int]$Current.files - [int]$Reference.files) "$Label.files"
  Assert-Equal $Actual.physical ([int]$Current.physical - [int]$Reference.physical) "$Label.physical"
  Assert-Equal $Actual.nonblank ([int]$Current.nonblank - [int]$Reference.nonblank) "$Label.nonblank"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$measurementScript = Join-Path $repoRoot "scripts/perf/measure-code-size.ps1"
$measurementPath = Join-Path $repoRoot "build/test-gate/code-size-source-contract.json"
$cutdownMeasurementPath = Join-Path $repoRoot "build/test-gate/code-size-cutdown-anchor.json"
$issues = [System.Collections.Generic.List[string]]::new()
$result = [ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString("o")
  measurementPath = [System.IO.Path]::GetRelativePath($repoRoot, $measurementPath).Replace("\", "/")
  cutdownMeasurementPath = [System.IO.Path]::GetRelativePath($repoRoot, $cutdownMeasurementPath).Replace("\", "/")
  issueCount = 0
  issues = @()
  overallStatus = "pending"
}

Push-Location $repoRoot
try {
  if (-not (Test-Path -LiteralPath $measurementScript -PathType Leaf)) {
    throw "Code-size measurement script not found: $measurementScript"
  }

  & $measurementScript -SkipReleaseBuild -OutJsonPath $measurementPath

  $manifest = Get-Content -LiteralPath $measurementPath -Raw | ConvertFrom-Json
  Assert-Equal $manifest.schemaVersion 1 "schemaVersion"
  Assert-Equal $manifest.overallStatus "ok" "overallStatus"
  Assert-Equal $manifest.contractSelfTest "ok" "contractSelfTest"
  Assert-Equal $manifest.releaseArtifacts.secondary $true "releaseArtifacts.secondary"
  Assert-Equal $manifest.releaseArtifacts.skipped $true "releaseArtifacts.skipped"

  $reference = $manifest.source.reference
  Assert-Equal $reference.commit "3032121156dec2329e38e971327d55c7887db99f" "reference.commit"
  Assert-Equal $reference.sourceTree "c26d2616bfa48e0917c60bbd5aed1af43489e489" "reference.sourceTree"
  Assert-Equal $reference.content "commitArchive" "reference.content"
  Assert-Metric $reference.metrics.total 586 98950 88315 "reference.total"
  Assert-Metric $reference.metrics.logicFirst 381 64709 57370 "reference.logicFirst"
  Assert-Metric $reference.metrics.groups.domain 18 1087 994 "reference.domain"
  Assert-Metric $reference.metrics.groups.application 92 17606 15632 "reference.application"
  Assert-Metric $reference.metrics.groups.infrastructure 14 2130 1873 "reference.infrastructure"
  Assert-Metric $reference.metrics.groups.uiCSharp 393 61930 54514 "reference.uiCSharp"
  Assert-Metric $reference.metrics.groups.uiAxaml 69 16197 15302 "reference.uiAxaml"
  Assert-Metric $reference.metrics.subsets.uiViewModels 163 31076 27493 "reference.uiViewModels"
  Assert-Metric $reference.metrics.subsets.uiServices 94 12810 11378 "reference.uiServices"

  $signedObservation = [PSCustomObject]@{ files = 586; physical = 98946; nonblank = 88311 }
  Assert-Equal ([int]$reference.metrics.total.files - $signedObservation.files) 0 "reference-observation-calibration.files"
  Assert-Equal ([int]$reference.metrics.total.physical - $signedObservation.physical) 4 "reference-observation-calibration.physical"
  Assert-Equal ([int]$reference.metrics.total.nonblank - $signedObservation.nonblank) 4 "reference-observation-calibration.nonblank"

  $referenceTotal = Add-Metrics @(
    $reference.metrics.groups.domain,
    $reference.metrics.groups.application,
    $reference.metrics.groups.infrastructure,
    $reference.metrics.groups.uiCSharp,
    $reference.metrics.groups.uiAxaml)
  Assert-MetricEquals $reference.metrics.total $referenceTotal "reference.total-group-sum"

  $referenceLogic = Add-Metrics @(
    $reference.metrics.groups.domain,
    $reference.metrics.groups.application,
    $reference.metrics.groups.infrastructure,
    $reference.metrics.subsets.uiViewModels,
    $reference.metrics.subsets.uiServices)
  Assert-MetricEquals $reference.metrics.logicFirst $referenceLogic "reference.logic-first-sum"

  $current = $manifest.source.current.metrics
  Assert-Equal $manifest.source.current.content "trackedWorkingTree" "current.content"
  $currentTotal = Add-Metrics @(
    $current.groups.domain,
    $current.groups.application,
    $current.groups.infrastructure,
    $current.groups.uiCSharp,
    $current.groups.uiAxaml)
  Assert-MetricEquals $current.total $currentTotal "current.total-group-sum"

  $currentLogic = Add-Metrics @(
    $current.groups.domain,
    $current.groups.application,
    $current.groups.infrastructure,
    $current.subsets.uiViewModels,
    $current.subsets.uiServices)
  Assert-MetricEquals $current.logicFirst $currentLogic "current.logic-first-sum"

  Assert-Delta $manifest.source.deltaFromReference.total $current.total $reference.metrics.total "delta.total"
  Assert-Delta $manifest.source.deltaFromReference.logicFirst $current.logicFirst $reference.metrics.logicFirst "delta.logicFirst"
  foreach ($name in @("domain", "application", "infrastructure", "uiCSharp", "uiAxaml")) {
    Assert-Delta $manifest.source.deltaFromReference.groups.$name $current.groups.$name $reference.metrics.groups.$name "delta.groups.$name"
  }
  foreach ($name in @("uiViewModels", "uiServices")) {
    Assert-Delta $manifest.source.deltaFromReference.subsets.$name $current.subsets.$name $reference.metrics.subsets.$name "delta.subsets.$name"
  }

  if ($current.total.files -le 0) {
    Add-Issue "Current production source file count must be positive."
  }

  & $measurementScript `
    -ReferenceCommit "207e29d5115c3d3318ee3249ed3af197f2d18180" `
    -SkipReleaseBuild `
    -OutJsonPath $cutdownMeasurementPath
  $cutdown = (Get-Content -LiteralPath $cutdownMeasurementPath -Raw | ConvertFrom-Json).source.reference
  Assert-Equal $cutdown.commit "207e29d5115c3d3318ee3249ed3af197f2d18180" "cutdown-reference.commit"
  Assert-Equal $cutdown.sourceTree "c5048b4ecfd0c8a89a8a4719a48afbf11b980f68" "cutdown-reference.sourceTree"
  Assert-Metric $cutdown.metrics.total 589 98617 88021 "cutdown-reference.total"
  Assert-Metric $cutdown.metrics.logicFirst 384 64377 57077 "cutdown-reference.logicFirst"
  Assert-Metric $cutdown.metrics.groups.domain 18 1087 994 "cutdown-reference.domain"
  Assert-Metric $cutdown.metrics.groups.application 95 17475 15517 "cutdown-reference.application"
  Assert-Metric $cutdown.metrics.groups.infrastructure 14 2130 1873 "cutdown-reference.infrastructure"
  Assert-Metric $cutdown.metrics.groups.uiCSharp 393 61730 54337 "cutdown-reference.uiCSharp"
  Assert-Metric $cutdown.metrics.groups.uiAxaml 69 16195 15300 "cutdown-reference.uiAxaml"
  Assert-Metric $cutdown.metrics.subsets.uiViewModels 163 30889 27324 "cutdown-reference.uiViewModels"
  Assert-Metric $cutdown.metrics.subsets.uiServices 94 12796 11369 "cutdown-reference.uiServices"

  $result.issueCount = $issues.Count
  $result.issues = @($issues)
  if ($issues.Count -gt 0) {
    $result.overallStatus = "failed"
    foreach ($issue in $issues) {
      Write-Host "[code-size-contract] ERROR $issue"
    }

    if (-not $NoThrow) {
      throw "Code-size baseline contract failed with $($issues.Count) issue(s)."
    }
  } else {
    $result.overallStatus = "ok"
    Write-Host "[code-size-contract] source manifest and immutable reference-commit baseline are consistent."
  }
}
catch {
  if ($result.overallStatus -ne "failed") {
    $result.overallStatus = "failed"
    $issues.Add($_.Exception.Message)
    $result.issueCount = $issues.Count
    $result.issues = @($issues)
  }

  if (-not $NoThrow) {
    throw
  }
}
finally {
  if (-not [string]::IsNullOrWhiteSpace($ResultOutputPath)) {
    $resultPath = if ([System.IO.Path]::IsPathRooted($ResultOutputPath)) {
      $ResultOutputPath
    } else {
      Join-Path $repoRoot $ResultOutputPath
    }
    $resultDir = Split-Path -Parent $resultPath
    if (-not [string]::IsNullOrWhiteSpace($resultDir)) {
      New-Item -Path $resultDir -ItemType Directory -Force | Out-Null
    }

    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
  }

  Pop-Location
}
