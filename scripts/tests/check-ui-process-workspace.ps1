$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot "../dev/test-ui-process-in-repo.ps1")

$root = 'C:\work\FreeformHelper\worktrees\current\'
$mainRoot = 'C:\work\FreeformHelper\'
$nestedRoot = 'C:\work\FreeformHelper\.claude\worktrees\current\'
$nestedRootAlias = 'c:/WORK/FreeformHelper/.claude/worktrees/CURRENT/'
$cases = @(
  @{ name = 'under root'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet "C:/work/FreeformHelper/worktrees/current/build/FreeformHelper.UI.dll"'; expected = $true },
  @{ name = 'project path under root'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet run --project C:\work\FreeformHelper\worktrees\current\src\FreeformHelper.UI\FreeformHelper.UI.csproj'; expected = $true },
  @{ name = 'quoted path with spaces'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet "C:\work\FreeformHelper\worktrees\current\build output\FreeformHelper.UI.dll"'; expected = $true },
  @{ name = 'other worktree'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet C:\work\FreeformHelper\worktrees\other\build\FreeformHelper.UI.dll'; expected = $false },
  @{ name = 'sibling prefix'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet C:\work\FreeformHelper\worktrees\current-extra\build\FreeformHelper.UI.dll'; expected = $false },
  @{ name = 'missing command line'; path = 'C:\Program Files\dotnet\dotnet.exe'; command = $null; expected = $false },
  @{ name = 'local executable'; path = 'c:/WORK/FreeformHelper/worktrees/current/build/FreeformHelper.UI.exe'; command = $null; expected = $true },
  @{ name = 'foreign executable'; path = 'C:\work\FreeformHelper\worktrees\other\build\FreeformHelper.UI.exe'; command = 'C:\work\FreeformHelper\worktrees\other\build\FreeformHelper.UI.exe --data C:\work\FreeformHelper\worktrees\current\build\FreeformHelper.UI.dll'; expected = $false },
  @{ name = 'main root excludes nested worktree'; root = $mainRoot; otherRoots = @($nestedRootAlias); path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet C:\work\FreeformHelper\.claude\worktrees\current\build\FreeformHelper.UI.dll'; expected = $false },
  @{ name = 'main root excludes nested executable'; root = $mainRoot; otherRoots = @($nestedRootAlias); path = 'C:\work\FreeformHelper\.claude\worktrees\current\build\FreeformHelper.UI.exe'; command = $null; expected = $false },
  @{ name = 'nested worktree owns its process'; root = $nestedRoot; otherRoots = @($mainRoot); path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet C:\work\FreeformHelper\.claude\worktrees\current\build\FreeformHelper.UI.dll'; expected = $true },
  @{ name = 'main root owns its process with nested worktrees'; root = $mainRoot; otherRoots = @($nestedRoot); path = 'C:\Program Files\dotnet\dotnet.exe'; command = 'dotnet C:\work\FreeformHelper\build\FreeformHelper.UI.dll'; expected = $true }
)

foreach ($case in $cases) {
  $process = [pscustomobject]@{
    Name = if ($case.name -in @('local executable', 'foreign executable', 'main root excludes nested executable')) { 'FreeformHelper.UI.exe' } else { 'dotnet.exe' }
    ExecutablePath = $case.path
    CommandLine = $case.command
  }
  $caseRoot = if ($case.ContainsKey('root')) { $case.root } else { $root }
  $otherRoots = if ($case.ContainsKey('otherRoots')) { $case.otherRoots } else { @() }
  $actual = Test-UiProcessInRepo -RepoRoot $caseRoot -Process $process -OtherWorktreeRoots $otherRoots
  if ($actual -ne $case.expected) {
    throw "Case '$($case.name)' expected $($case.expected), got $actual."
  }
  Write-Host ("[check-ui-process-workspace] PASS {0}: {1}" -f $case.name, $actual)
}

Write-Host ("[check-ui-process-workspace] OK ({0} cases)" -f $cases.Count)
