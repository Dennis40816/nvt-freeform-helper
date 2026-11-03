#Requires -Version 7.0
# Warns when another open PR edits a shared file that your change also edits.
# Shared files are ledgers and roadmaps that many PRs touch. Two PRs on the same shared file conflict.
# Read-only: it only runs `git diff` and `gh pr list`. Run it before you open a PR.
param(
  [string]$BaseRef = "origin/main",
  [string[]]$Files = @(),
  [int]$ExcludePr = 0,
  [switch]$Strict
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$sharedFiles = @(
  "ROADMAP.md",
  "eng/code-health/baseline.json",
  "eng/code-health/test-debt.json"
)

if ($Files.Count -eq 0) {
  $Files = @(git diff --name-only "$BaseRef...HEAD")
  if ($LASTEXITCODE -ne 0) {
    throw "git diff against '$BaseRef' failed."
  }
}

$mine = @($Files | ForEach-Object { $_ -replace "\\", "/" } | Where-Object { $sharedFiles -contains $_ })
if ($mine.Count -eq 0) {
  Write-Host "[shared-files] This change touches no shared file."
  exit 0
}

Write-Host ("[shared-files] This change touches: {0}" -f ($mine -join ", "))

$branch = (git branch --show-current).Trim()
$json = gh pr list --state open --limit 100 --json number,title,headRefName,files
if ($LASTEXITCODE -ne 0) {
  throw "gh pr list failed. Check that gh is installed and signed in."
}

$conflicts = @()
foreach ($pr in @($json | ConvertFrom-Json)) {
  if ($pr.number -eq $ExcludePr -or ($branch -and $pr.headRefName -ceq $branch)) {
    continue
  }
  $shared = @($pr.files | ForEach-Object { $_.path } | Where-Object { $mine -contains $_ })
  if ($shared.Count -gt 0) {
    $conflicts += [pscustomobject]@{ Number = $pr.number; Title = $pr.title; Files = $shared }
  }
}

if ($conflicts.Count -eq 0) {
  Write-Host "[shared-files] No other open PR edits these files."
  exit 0
}

foreach ($c in $conflicts) {
  Write-Warning ("PR #{0} also edits {1}: {2}" -f $c.Number, ($c.Files -join ", "), $c.Title)
}
Write-Host "[shared-files] Put shared-file edits in the last PR of the batch, or wait for the other PR to merge."
if ($Strict) {
  exit 1
}
