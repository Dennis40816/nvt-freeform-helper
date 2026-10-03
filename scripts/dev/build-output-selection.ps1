function Get-BuildOutputSelection {
  param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot,
    [switch]$IncludeEvidence
  )

  $root = [System.IO.Path]::GetFullPath($RepoRoot)
  $buildRoot = Join-Path $root "build"
  if (-not (Test-Path -LiteralPath $buildRoot)) {
    return @()
  }

  $buildItem = Get-Item -LiteralPath $buildRoot -Force
  if (-not $buildItem.PSIsContainer -or
      ($buildItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
    throw "Build output path must be a regular directory: $buildRoot"
  }

  $rebuildable = @("bin", "obj")
  $evidence = @("perf", "test-gate", "code-size", "logs", "publish", "packages")
  $selection = @(
    foreach ($item in Get-ChildItem -LiteralPath $buildRoot -Force) {
      if ($item.Name -notin $rebuildable -and
          (-not $IncludeEvidence -or $item.Name -notin $evidence)) {
        continue
      }
      if (-not $item.PSIsContainer -or
          ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw "Selected build output must be a regular directory: $($item.FullName)"
      }
      [pscustomobject]@{
        Name = $item.Name
        Path = $item.FullName
        Kind = if ($item.Name -in $rebuildable) { "rebuildable" } else { "evidence" }
      }
    }
  )
  return @($selection | Sort-Object Name)
}

function Get-BuildOutputBytes {
  param([Parameter(Mandatory = $true)][string]$Directory)

  $bytes = [long]0
  foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
    if ($item.Name -eq '.git') {
      throw "Refusing to traverse a .git entry: $($item.FullName)"
    }
    if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
      throw "Refusing to traverse a reparse point: $($item.FullName)"
    }
    if ($item.PSIsContainer) {
      $bytes += Get-BuildOutputBytes -Directory $item.FullName
    } else {
      $bytes += [long]$item.Length
    }
  }
  return $bytes
}

function Assert-NoRegisteredWorktreeUnderDirectory {
  param(
    [Parameter(Mandatory = $true)][string]$Directory,
    [string[]]$OtherWorktreeRoots = @()
  )

  $selected = [System.IO.Path]::GetFullPath($Directory).Replace('\', '/').TrimEnd('/')
  foreach ($worktreeRoot in $OtherWorktreeRoots) {
    $other = [System.IO.Path]::GetFullPath($worktreeRoot).Replace('\', '/').TrimEnd('/')
    if ($other.Equals($selected, [System.StringComparison]::OrdinalIgnoreCase) -or
        $other.StartsWith("$selected/", [System.StringComparison]::OrdinalIgnoreCase)) {
      throw "Registered worktree is inside selected build output: $worktreeRoot"
    }
  }
}

function Get-BlockingBuildOutputProcessNames {
  param([Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$ProcessNames)

  return @($ProcessNames | Where-Object {
      $_ -in @('dotnet.exe', 'testhost.exe', 'testhost', 'MSBuild.exe',
        'VBCSCompiler.exe', 'FreeformHelper.UI.exe')
    })
}
