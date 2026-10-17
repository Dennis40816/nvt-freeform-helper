#requires -Version 7.0
# Re-signs the two signed BOE36.35 Notch C examples after an output change.
#
# Run NotchExampleCExportDriftTests first. A failing run writes the current export to
# build/notch-example-drift-v2.1.actual.c and build/notch-example-drift-v2.2.actual.c. A version whose
# export did not change has no drift file and is left as it is. This script copies the drift files over
# the signed examples in the example/ checkout and updates nodes, bytes, sha256 and signedAt in
# notch_export_golden_manifest.json. It prints old and new values. It never prints file content,
# because the example data is confidential.
#
# It changes the local example/ checkout only. It does not commit or push. After it runs, commit inside
# example/ on a branch, open a pull request in the data repository, and move the example pointer here.
# Run it on Windows: the signed files use CRLF and the drift test writes the platform line ending.
param(
  [string]$RepoRoot = "",
  [string]$ExampleDirectory = "example/BOE36.35",
  [string]$BuildDirectory = "build",
  [string]$SignedAt = "",
  [switch]$WhatIf
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = if ([string]::IsNullOrWhiteSpace($RepoRoot)) { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) } else { $RepoRoot }
$exampleRoot = Join-Path $root $ExampleDirectory
$buildRoot = Join-Path $root $BuildDirectory
$manifestPath = Join-Path $exampleRoot "notch_export_golden_manifest.json"
if ([string]::IsNullOrWhiteSpace($SignedAt)) {
  $SignedAt = (Get-Date).ToString("yyyy-MM-dd", [System.Globalization.CultureInfo]::InvariantCulture)
}
if ($SignedAt -notmatch '^\d{4}-\d{2}-\d{2}$') {
  throw "SignedAt must look like 2026-10-14."
}

$outputs = @(
  @{ Key = "v21"; Name = "notch_export_v21_current.c"; Drift = "notch-example-drift-v2.1.actual.c" },
  @{ Key = "v22"; Name = "notch_export_v22_current.c"; Drift = "notch-example-drift-v2.2.actual.c" }
)

if (-not (Test-Path -LiteralPath $manifestPath)) {
  throw "The manifest was not found. Run 'git submodule update --init example' first."
}

function Get-FileFacts {
  param([string]$Path)
  $bytes = [System.IO.File]::ReadAllBytes($Path)
  $hash = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::HashData($bytes)).Replace("-", "")
  return @{ Bytes = $bytes.Length; Sha256 = $hash }
}

$manifestText = [System.IO.File]::ReadAllText($manifestPath)
$plan = @()
foreach ($output in $outputs) {
  $signedPath = Join-Path $exampleRoot $output.Name
  $driftPath = Join-Path $buildRoot $output.Drift
  if (-not (Test-Path -LiteralPath $signedPath)) {
    throw "Signed example not found: $($output.Name)."
  }
  if (-not (Test-Path -LiteralPath $driftPath)) {
    "$($output.Key): no drift file, left as it is."
    continue
  }

  $driftText = [System.IO.File]::ReadAllText($driftPath)
  if ($driftText -notmatch '#define\s+USER_NHC_NODE_NUM\s+\((\d+)u\)') {
    throw "The drift file for $($output.Key) has no USER_NHC_NODE_NUM define. It is not a Notch C export."
  }
  $newNodes = [int]$Matches[1]
  if ($driftText.Contains("`n") -and -not $driftText.Contains("`r`n")) {
    throw "The drift file for $($output.Key) has LF line endings. Run the drift test on Windows, so the signed file keeps CRLF."
  }

  $old = Get-FileFacts -Path $signedPath
  $new = Get-FileFacts -Path $driftPath
  if ($old.Sha256 -eq $new.Sha256) {
    "$($output.Key): the drift file equals the signed example (stale build output?). Left as it is."
    continue
  }

  $pattern = '("' + $output.Key + '":\s*\{\s*"path":\s*"[^"]*",\s*"nodes":\s*)(?<nodes>\d+)(,\s*"bytes":\s*)' + $old.Bytes +
    '(,\s*"sha256":\s*")' + $old.Sha256 + '"'
  $match = [System.Text.RegularExpressions.Regex]::Match($manifestText, $pattern)
  if (-not $match.Success) {
    throw "The manifest entry for $($output.Key) does not match the signed example on disk. Refusing to re-sign an already inconsistent set."
  }

  $plan += @{ Output = $output; SignedPath = $signedPath; DriftPath = $driftPath; Old = $old; New = $new; OldNodes = [int]$match.Groups["nodes"].Value; NewNodes = $newNodes; Pattern = $pattern }
}

if ($plan.Count -eq 0) {
  throw "Nothing to re-sign. Run NotchExampleCExportDriftTests first: it writes $BuildDirectory/notch-example-drift-v2.x.actual.c when the export differs."
}

$updated = $manifestText
foreach ($item in $plan) {
  $newNodes = $item.NewNodes
  $newBytes = $item.New.Bytes
  $newSha = $item.New.Sha256
  $updated = [System.Text.RegularExpressions.Regex]::Replace(
    $updated,
    $item.Pattern,
    { param($m) $m.Groups[1].Value + $newNodes + $m.Groups[2].Value + $newBytes + $m.Groups[3].Value + $newSha + '"' },
    [System.Text.RegularExpressions.RegexOptions]::None)
}
$updated = [System.Text.RegularExpressions.Regex]::Replace($updated, '("signedAt":\s*")[^"]*"', { param($m) $m.Groups[1].Value + $SignedAt + '"' })

foreach ($item in $plan) {
  "{0}: nodes {1} -> {2}" -f $item.Output.Key, $item.OldNodes, $item.NewNodes
  "{0}: bytes {1} -> {2}" -f $item.Output.Key, $item.Old.Bytes, $item.New.Bytes
  "{0}: sha256 {1} -> {2}" -f $item.Output.Key, $item.Old.Sha256, $item.New.Sha256
}
"signedAt -> $SignedAt"

if ($WhatIf) {
  "WhatIf: nothing was written."
  return
}

try {
  $newManifestPath = $manifestPath + ".new"
  [System.IO.File]::WriteAllText($newManifestPath, $updated, [System.Text.UTF8Encoding]::new($false))
  foreach ($item in $plan) {
    [System.IO.File]::WriteAllBytes($item.SignedPath, [System.IO.File]::ReadAllBytes($item.DriftPath))
  }
  Move-Item -LiteralPath $newManifestPath -Destination $manifestPath -Force
}
catch {
  "Writing failed. Restore the examples with: git -C example checkout -- BOE36.35"
  throw
}

"Re-signed in the local example/ checkout. Next: commit inside example/ on a branch, open a pull request in the data repository, then move the example pointer in this repository. Also update the values in docs/performance/regression-baseline-3635.md if that file lists them."
