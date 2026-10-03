param(
  [string]$RepoRoot = "",
  [string]$OutDir = "",
  [string]$ArchiveName = "",
  [bool]$ExcludeExamples = $true,
  [int]$MaxFileSizeMB = 10,
  [switch]$IncludeUntracked,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Normalize-PathForMatch {
  param([string]$PathValue)
  return ($PathValue -replace "\\", "/").TrimStart("./")
}

function Should-ExcludeByDirectory {
  param(
    [string]$RelativePath,
    [string[]]$ExcludedDirectoryPrefixes
  )

  foreach ($prefix in $ExcludedDirectoryPrefixes) {
    if ($RelativePath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
      return $true
    }
  }

  return $false
}

function Should-ExcludeByExtension {
  param(
    [string]$RelativePath,
    [string[]]$ExcludedExtensions
  )

  $ext = [System.IO.Path]::GetExtension($RelativePath)
  if ([string]::IsNullOrWhiteSpace($ext)) {
    return $false
  }

  return $ExcludedExtensions -contains $ext.ToLowerInvariant()
}

function Get-TrackedFiles {
  param([string]$RootPath)

  $tracked = git -C $RootPath ls-files
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to list tracked files via git ls-files."
  }

  return @($tracked | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-UntrackedFiles {
  param([string]$RootPath)

  $untracked = git -C $RootPath ls-files --others --exclude-standard
  if ($LASTEXITCODE -ne 0) {
    throw "Failed to list untracked files via git ls-files --others."
  }

  return @($untracked | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
  $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
} else {
  $RepoRoot = (Resolve-Path $RepoRoot).Path
}

if ([string]::IsNullOrWhiteSpace($OutDir)) {
  $OutDir = Join-Path $RepoRoot "build\packages"
} else {
  if ([System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = (Resolve-Path $OutDir).Path
  } else {
    $OutDir = Join-Path $RepoRoot $OutDir
  }
}

if ([string]::IsNullOrWhiteSpace($ArchiveName)) {
  $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
  $ArchiveName = "openai-upload-$timestamp.zip"
}

if (-not $ArchiveName.EndsWith(".zip", [System.StringComparison]::OrdinalIgnoreCase)) {
  $ArchiveName = "$ArchiveName.zip"
}

$archivePath = Join-Path $OutDir $ArchiveName

$excludedExtensions = @(
  ".exe", ".dll", ".pdb", ".so", ".dylib", ".a", ".lib",
  ".o", ".obj", ".class", ".jar", ".apk", ".ipa", ".msi",
  ".nupkg", ".snupkg", ".zip", ".7z", ".rar", ".tar",
  ".gz", ".bz2", ".xz", ".iso", ".dmg"
)

$excludedDirectoryPrefixes = @(
  ".git/",
  ".vs/",
  ".idea/",
  "node_modules/",
  "build/",
  "bin/",
  "obj/",
  "TestResults/"
)

if ($ExcludeExamples) {
  $excludedDirectoryPrefixes += "example/"
}

$allRelativeFiles = New-Object System.Collections.Generic.List[string]
foreach ($f in (Get-TrackedFiles -RootPath $RepoRoot)) {
  $allRelativeFiles.Add((Normalize-PathForMatch -PathValue $f))
}

if ($IncludeUntracked) {
  foreach ($f in (Get-UntrackedFiles -RootPath $RepoRoot)) {
    $normalized = Normalize-PathForMatch -PathValue $f
    if (-not $allRelativeFiles.Contains($normalized)) {
      $allRelativeFiles.Add($normalized)
    }
  }
}

$includedFiles = New-Object System.Collections.Generic.List[System.String]
$excludedByRuleCount = 0
$excludedBySizeCount = 0
$maxFileSizeBytes = [int64]$MaxFileSizeMB * 1024 * 1024

foreach ($relativePath in $allRelativeFiles) {
  if (Should-ExcludeByDirectory -RelativePath $relativePath -ExcludedDirectoryPrefixes $excludedDirectoryPrefixes) {
    $excludedByRuleCount++
    continue
  }

  if (Should-ExcludeByExtension -RelativePath $relativePath -ExcludedExtensions $excludedExtensions) {
    $excludedByRuleCount++
    continue
  }

  $fullPath = Join-Path $RepoRoot $relativePath
  if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
    continue
  }

  $fileInfo = Get-Item -LiteralPath $fullPath
  if ($fileInfo.Length -gt $maxFileSizeBytes) {
    $excludedBySizeCount++
    continue
  }

  $includedFiles.Add($relativePath)
}

$totalBytes = 0L
foreach ($relativePath in $includedFiles) {
  $fullPath = Join-Path $RepoRoot $relativePath
  $totalBytes += (Get-Item -LiteralPath $fullPath).Length
}

$totalMB = [math]::Round($totalBytes / 1MB, 2)
Write-Host "[package] repo root: $RepoRoot"
Write-Host "[package] files selected: $($includedFiles.Count)"
Write-Host "[package] excluded by rule: $excludedByRuleCount"
Write-Host "[package] excluded by size(>${MaxFileSizeMB}MB): $excludedBySizeCount"
Write-Host "[package] estimated payload: $totalMB MB"
Write-Host "[package] output: $archivePath"

if ($DryRun) {
  Write-Host "[package] dry-run mode, no archive created."
  exit 0
}

if (-not (Test-Path -LiteralPath $OutDir)) {
  New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
}

if (Test-Path -LiteralPath $archivePath) {
  Remove-Item -LiteralPath $archivePath -Force
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($archivePath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($relativePath in $includedFiles) {
    $fullPath = Join-Path $RepoRoot $relativePath
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
      $zip,
      $fullPath,
      $relativePath,
      [System.IO.Compression.CompressionLevel]::Optimal
    ) | Out-Null
  }
} finally {
  $zip.Dispose()
}

$archiveInfo = Get-Item -LiteralPath $archivePath
$archiveMB = [math]::Round($archiveInfo.Length / 1MB, 2)
Write-Host "[package] done: $archivePath ($archiveMB MB)"
