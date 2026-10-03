function Test-UiProcessInRepo {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,
    [Parameter(Mandatory = $true)]
    [psobject]$Process,
    [string[]]$OtherWorktreeRoots = @()
  )

  $root = $RepoRoot.Replace("\", "/").TrimEnd("/")
  if ([string]::IsNullOrWhiteSpace($root)) {
    return $false
  }

  $rootPrefix = [regex]::Escape($root) + "/"
  $excludedRoots = @(
    foreach ($otherRoot in $OtherWorktreeRoots) {
      $normalized = $otherRoot.Replace("\", "/").TrimEnd("/")
      if (-not [string]::IsNullOrWhiteSpace($normalized) -and
          $normalized.StartsWith("$root/", [System.StringComparison]::OrdinalIgnoreCase)) {
        $normalized
      }
    }
  )
  $options = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
  $executablePath = [string]$Process.ExecutablePath
  if ($Process.Name -in @("FreeformHelper.UI.exe", "FreeformHelper.CadLoadSpinner.exe") -and
      -not [string]::IsNullOrWhiteSpace($executablePath)) {
    $normalizedExecutablePath = $executablePath.Replace("\", "/")
    if (-not [regex]::IsMatch($normalizedExecutablePath, "^$rootPrefix", $options)) {
      return $false
    }
    foreach ($excludedRoot in $excludedRoots) {
      if ([regex]::IsMatch($normalizedExecutablePath, "^$([regex]::Escape($excludedRoot))/", $options)) {
        return $false
      }
    }
    return $true
  }

  $commandLine = [string]$Process.CommandLine
  if ([string]::IsNullOrWhiteSpace($commandLine)) {
    return $false
  }

  $target = '(?:FreeformHelper\.UI\.(?:exe|dll|csproj)|FreeformHelper\.CadLoadSpinner(?:\.(?:exe|dll|csproj))?)'
  $unquoted = '(?<![a-z0-9_])' + $rootPrefix + '(?:[^\s"''/]+/)*' + $target + '(?=$|[\s"''])'
  $quoted = '["'']' + $rootPrefix + '(?:[^"''/:]+/)*' + $target + '["'']'
  $normalizedCommandLine = $commandLine.Replace("\", "/")
  if (-not ([regex]::IsMatch($normalizedCommandLine, $unquoted, $options) -or
            [regex]::IsMatch($normalizedCommandLine, $quoted, $options))) {
    return $false
  }

  foreach ($excludedRoot in $excludedRoots) {
    $excludedPrefix = [regex]::Escape($excludedRoot) + "/"
    $excludedUnquoted = '(?<![a-z0-9_])' + $excludedPrefix + '(?:[^\s"''/]+/)*' + $target + '(?=$|[\s"''])'
    $excludedQuoted = '["'']' + $excludedPrefix + '(?:[^"''/:]+/)*' + $target + '["'']'
    if ([regex]::IsMatch($normalizedCommandLine, $excludedUnquoted, $options) -or
        [regex]::IsMatch($normalizedCommandLine, $excludedQuoted, $options)) {
      return $false
    }
  }

  return $true
}
