param(
  [switch]$AllFiles,
  [switch]$NoThrow,
  [string]$ResultOutputPath = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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

function Get-CandidatePaths {
  param(
    [switch]$UseAllFiles
  )

  if ($UseAllFiles) {
    $tracked = git ls-files
    $untracked = git ls-files --others --exclude-standard
    return @($tracked + $untracked | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
  }

  $trackedChanged = @(git diff --name-only --diff-filter=ACMRTUXB HEAD)
  $untrackedChanged = @(git ls-files --others --exclude-standard)
  return @($trackedChanged + $untrackedChanged | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique)
}

function Test-BinaryFile {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [Parameter(Mandatory = $true)]
    [System.Collections.Generic.HashSet[string]]$BinaryExtensions
  )

  $ext = [System.IO.Path]::GetExtension($Path)
  if ($BinaryExtensions.Contains($ext)) {
    return $true
  }

  $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
  try {
    $buffer = New-Object byte[] 4096
    $read = $stream.Read($buffer, 0, $buffer.Length)
    for ($i = 0; $i -lt $read; $i++) {
      if ($buffer[$i] -eq 0) {
        return $true
      }
    }
  } finally {
    $stream.Dispose()
  }

  return $false
}

function Get-FileTextWithEncoding {
  param(
    [Parameter(Mandatory = $true)]
    [string]$Path
  )

  $bytes = [System.IO.File]::ReadAllBytes($Path)
  $hasUtf8Bom = $bytes.Length -ge 3 -and `
    $bytes[0] -eq 0xEF -and `
    $bytes[1] -eq 0xBB -and `
    $bytes[2] -eq 0xBF

  $reader = [System.IO.StreamReader]::new($Path, $true)
  try {
    $text = $reader.ReadToEnd()
    $currentEncoding = $reader.CurrentEncoding
    $targetEncoding = $currentEncoding
    if ($currentEncoding.WebName -eq "utf-8") {
      $targetEncoding = [System.Text.UTF8Encoding]::new($hasUtf8Bom)
    }

    return @{
      text = $text
      encoding = $targetEncoding
    }
  } finally {
    $reader.Dispose()
  }
}

$normalizeResult = [ordered]@{
  generatedUtc = [DateTime]::UtcNow.ToString("o")
  allFiles = [bool]$AllFiles
  scannedFiles = 0
  normalizedFiles = 0
  skippedBinaryFiles = 0
  skippedMissingFiles = 0
  normalizedPaths = @()
  overallStatus = "pending"
  failureMessage = ""
}

$binaryExtensions = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
@(
  ".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".7z", ".dll", ".exe", ".bmp", ".tif", ".tiff", ".bin"
) | ForEach-Object {
  [void]$binaryExtensions.Add($_)
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Push-Location $repoRoot
try {
  $candidates = Get-CandidatePaths -UseAllFiles:$AllFiles
  foreach ($relativePath in $candidates) {
    # Preserve canonical Core bytes and LF-stable health artifacts.
    if ($relativePath -ceq "src/FreeformHelper.CoreSource/UiEventRunner.cs" -or
        $relativePath -ceq "src/FreeformHelper.CoreSource/manifest.json" -or
        $relativePath.StartsWith("eng/core-health/", [System.StringComparison]::Ordinal) -or
        $relativePath -cin @(".editorconfig", "eng/core-health.lock.json", "eng/code-health/baseline.json", "eng/code-health/test-debt.json")) {
      continue
    }

    $fullPath = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
      $normalizeResult.skippedMissingFiles++
      continue
    }

    if (Test-BinaryFile -Path $fullPath -BinaryExtensions $binaryExtensions) {
      $normalizeResult.skippedBinaryFiles++
      continue
    }

    $normalizeResult.scannedFiles++
    $fileData = Get-FileTextWithEncoding -Path $fullPath
    $originalText = $fileData.text
    $normalizedText = $originalText.Replace("`r`n", "`n").Replace("`r", "`n").Replace("`n", "`r`n")
    if ($normalizedText -ceq $originalText) {
      continue
    }

    $writer = [System.IO.StreamWriter]::new($fullPath, $false, $fileData.encoding)
    try {
      $writer.Write($normalizedText)
    } finally {
      $writer.Dispose()
    }

    $normalizeResult.normalizedFiles++
    $normalizeResult.normalizedPaths += $relativePath
  }

  $normalizeResult.overallStatus = "ok"
  Write-Host ("[normalize-crlf] scanned={0}, normalized={1}, skippedBinary={2}, skippedMissing={3}" -f `
      $normalizeResult.scannedFiles, `
      $normalizeResult.normalizedFiles, `
      $normalizeResult.skippedBinaryFiles, `
      $normalizeResult.skippedMissingFiles)
}
catch {
  $normalizeResult.overallStatus = "failed"
  $normalizeResult.failureMessage = $_.Exception.Message
  if (-not $NoThrow) {
    Write-ResultFile -Result $normalizeResult
    throw
  }
}
finally {
  Write-ResultFile -Result $normalizeResult
  Pop-Location
}
