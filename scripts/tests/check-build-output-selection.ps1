$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../dev/build-output-selection.ps1")

$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("freeformhelper-build-selection-{0}" -f [guid]::NewGuid().ToString("N"))
try {
  foreach ($name in @("bin", "obj", "perf", "test-gate", "code-size", "logs", "publish", "packages", "unknown")) {
    $null = New-Item -Path (Join-Path $testRoot "build/$name") -ItemType Directory -Force
    [System.IO.File]::WriteAllBytes((Join-Path $testRoot "build/$name/sample.bin"), [byte[]](1, 2, 3))
  }

  $default = @(Get-BuildOutputSelection -RepoRoot $testRoot)
  $defaultNames = @($default | ForEach-Object { $_.Name }) -join ","
  if ($defaultNames -ne "bin,obj") {
    throw "Default selection was '$defaultNames', expected 'bin,obj'."
  }
  Write-Host "[check-build-output-selection] PASS default selects bin and obj only"

  $withEvidence = @(Get-BuildOutputSelection -RepoRoot $testRoot -IncludeEvidence)
  $evidenceNames = @($withEvidence | ForEach-Object { $_.Name }) -join ","
  if ($evidenceNames -ne "bin,code-size,logs,obj,packages,perf,publish,test-gate") {
    throw "Evidence selection was '$evidenceNames'."
  }
  if (@($withEvidence | Where-Object { $_.Name -eq "unknown" }).Count -ne 0) {
    throw "Unknown build output was selected."
  }
  Write-Host "[check-build-output-selection] PASS evidence opt-in keeps unknown paths"

  $bytes = Get-BuildOutputBytes -Directory (Join-Path $testRoot "build/bin")
  if ($bytes -ne 3) {
    throw "Size calculation was $bytes bytes, expected 3."
  }
  Write-Host "[check-build-output-selection] PASS size calculation"

  $null = New-Item -Path (Join-Path $testRoot "build/obj/nested") -ItemType Directory
  [System.IO.File]::WriteAllBytes((Join-Path $testRoot "build/obj/nested/more.bin"), [byte[]](4, 5))
  $bytes = Get-BuildOutputBytes -Directory (Join-Path $testRoot "build/obj")
  if ($bytes -ne 5) {
    throw "Recursive size calculation was $bytes bytes, expected 5."
  }
  Write-Host "[check-build-output-selection] PASS recursive size calculation"

  foreach ($gitEntryKind in @('File', 'Directory')) {
    $gitEntry = Join-Path $testRoot "build/obj/nested/.git"
    $null = New-Item -Path $gitEntry -ItemType $gitEntryKind
    $refused = $false
    try {
      $null = Get-BuildOutputBytes -Directory (Join-Path $testRoot "build/obj")
    } catch {
      $refused = $_.Exception.Message -match '\.git'
    }
    if (-not $refused) {
      throw "Nested .git $gitEntryKind was not refused by the size walk."
    }
    Remove-Item -LiteralPath $gitEntry -Recurse -Force
    Write-Host "[check-build-output-selection] PASS nested .git $gitEntryKind refused"
  }

  $selectedPath = Join-Path $testRoot "build/bin"
  $worktreeCases = @(
    @{ name = 'registered worktree inside selected output'; others = @((Join-Path $selectedPath 'src/child')); block = $true },
    @{ name = 'registered worktree equals selected output'; others = @($selectedPath); block = $true },
    @{ name = 'registered worktree beside selected output'; others = @((Join-Path $testRoot 'build/bin-other')); block = $false },
    @{ name = 'registered worktree in different output'; others = @((Join-Path $testRoot 'build/obj/child')); block = $false }
  )
  foreach ($case in $worktreeCases) {
    $refused = $false
    try {
      Assert-NoRegisteredWorktreeUnderDirectory -Directory $selectedPath -OtherWorktreeRoots $case.others
    } catch {
      $refused = $_.Exception.Message -match 'worktree'
    }
    if ($refused -ne $case.block) {
      throw "Worktree case '$($case.name)' expected block=$($case.block), got block=$refused."
    }
    Write-Host ("[check-build-output-selection] PASS {0}: block={1}" -f $case.name, $refused)
  }

  $processCases = @(
    @{ names = @(); expected = @() },
    @{ names = @('dotnet.exe'); expected = @('dotnet.exe') },
    @{ names = @('notepad.exe'); expected = @() }
  )
  foreach ($case in $processCases) {
    $blocking = @(Get-BlockingBuildOutputProcessNames -ProcessNames $case.names)
    if (($blocking -join ',') -ne ($case.expected -join ',')) {
      throw "Process names '$($case.names -join ',') returned unexpected blockers."
    }
    Write-Host ("[check-build-output-selection] PASS process names [{0}]: blockers [{1}]" -f ($case.names -join ','), ($blocking -join ','))
  }

  Write-Host "[check-build-output-selection] OK (13 cases)"
} finally {
  if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
  }
}
