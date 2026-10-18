$env:DOTNET_CLI_UI_LANGUAGE = 'en'
$env:VSLANG = '1033'
$env:PreferredUILang = 'en-US'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:UseSharedCompilation = 'false'
$wt = 'C:/Dennis/dev/work/novatek/nfh-worktrees/console-adopt'
Set-Location $wt
[Environment]::CurrentDirectory = (Get-Location).Path
Copy-Item C:/Dennis/dev/work/novatek/nfh-a12-work/h5/TempConsoleShotsAfter.cs tests/FreeformHelper.Tests/UI/Snapshots/TempConsoleShots.cs -Force
dotnet build tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --no-restore -m:4 -nodeReuse:false 2>&1 | Select-String -Pattern 'error |Error\(s\)|Build succeeded' | Select-Object -Last 6
$env:FH_SHOT_DIR = 'C:/Dennis/dev/work/novatek/nfh-a12-work/h5/shots-after-094'
dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj --no-build -nodeReuse:false --logger "console;verbosity=detailed" --filter "FullyQualifiedName~TempConsoleShots" 2>&1 | Select-String -Pattern 'PANEL|Passed|Failed|error|Assert' | Select-Object -Last 20
Get-ChildItem $env:FH_SHOT_DIR -ErrorAction SilentlyContinue | Select-Object Name, Length
Remove-Item tests/FreeformHelper.Tests/UI/Snapshots/TempConsoleShots.cs -Force
"done"
