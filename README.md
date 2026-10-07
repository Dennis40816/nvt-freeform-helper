English | [繁體中文](README.zh-TW.md)

# FreeformHelper

See [LICENSE](LICENSE) for licensing and usage restrictions.

Since 2026-10-04, new work (branches, PRs, and new issues) takes place in the public repository [Dennis40816/nvt-freeform-helper](https://github.com/Dennis40816/nvt-freeform-helper), with PRs targeting `1.0.x`.

FreeformHelper is a DXF analysis tool with this main workflow:
`DXF import -> Regular Grid creation -> CAD/Regular mapping -> Freeform/Notch output`

## 0. Start here (90 seconds)

- To launch the UI directly, see "1) Quick start: run the app".
- To package it for others without requiring a .NET installation, see "2) Quick start: single-file EXE".
- The README includes a complete, copy-and-paste procedure for building a single-file EXE.

## 1. Quick start: run the app (copy and paste the entire block)

> Requirements: Windows + PowerShell + Python 3.10 or later + .NET SDK 10.0.3xx (pinned by `global.json`). The projects target `net10.0`.

```powershell
# 1) Go to the repository root (replace the placeholder with your path)
Set-Location <FreeformHelper repo 路徑>

# 2) Download the NVT Core packages (needs Python 3.10+), then restore and build.
#    Run the download again before an IDE build or dotnet run whenever core-packages.json changes.
./scripts/build/fetch-core-packages.ps1
dotnet restore
dotnet build FreeformHelper.sln

# 3) Launch the UI
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj
```

## 2. Quick start: package a single-file EXE (copy and paste the entire block)

> This self-contained package requires no installation and is best suited for handing over for testing.

```powershell
# 1) Go to the repository root (replace the placeholder with your path)
Set-Location <FreeformHelper repo 路徑>

# 2) Package a single-file EXE (win-x64 and Release by default)
./scripts/build/publish-exe-single-file.ps1

# 3) Check the output
Get-Item .\build\publish\win-x64\single-file\FreeformHelper.UI.exe

# 4) Run it directly
.\build\publish\win-x64\single-file\FreeformHelper.UI.exe
```

Common variant (ARM64):

```powershell
./scripts/build/publish-exe-single-file.ps1 -Runtime win-arm64
```

## 3. Quick start: package a folder-based EXE (copy and paste the entire block)

> Startup is usually more reliable and debugging is easier, at the cost of more files.

```powershell
Set-Location <FreeformHelper repo 路徑>
./scripts/build/publish-exe-folder.ps1
Get-Item .\build\publish\win-x64\folder\FreeformHelper.UI.exe
.\build\publish\win-x64\folder\FreeformHelper.UI.exe
```

## 4. Recommended workflow for first use (UI)

1. `File -> Open DXF`
2. Check layer visibility and overlap on the left.
3. Adjust Grid / Scan order / Alignment on the right.
4. Run `Match` and inspect the mapping summary and report.
5. Mark freeform pads as needed, then export Notch / DXF.
6. Use `Save Project` to save JSON for a later `Load Project`.

## 5. Runtime CLI (advanced; optional)

After launching the UI, query its runtime state from another terminal.

```powershell
# List commands
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query help

# Query status
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query status

# Load a project
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query load-project --path example/BOE36.35/project_3635.json

# Run Steps 1–3
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 1 --timeout-ms 120000
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 2 --timeout-ms 120000
dotnet run --project src/FreeformHelper.UI/FreeformHelper.UI.csproj -- query run-step --step 3
```

For the complete CLI reference, see `docs/reference/runtime-cli-plan.md`.

## 6. Frequently asked questions

- `MSB3027` / `file is being used by another process`
  - Close the running `FreeformHelper.UI.exe` before building or publishing.
- `INSTANCE_NOT_RUNNING`
  - No UI instance is running; launch the UI first.
- Notch export fails
  - Confirm that a DXF is loaded, the grid is created, and matching has run.
- Questions about the `To Regular` algorithm
  - Read `docs/reference/notch-system-reference.md` first. Since beta0.9, `ToRegularRatio = Σ(overlapArea / targetRegularArea)` is a CAD-level diagnostic value. The Gain / No Gain v2.2 row payload uses per-target regular coverage rather than applying the whole CAD's `R` as a source-wide gain to every target share.

## 6.1 Example data (private submodule)

`example/` is a Git submodule pointing to the private repository `FreeformHelper-testdata` (customer-panel CAD, IC mapping, and the project JSON and firmware C exports generated from them). You can build and run the UI without access to that repository.

```powershell
# With access: fetch the data after cloning or creating a worktree
git submodule update --init example
```

- When the data is absent, tests that read `example/` are marked Skipped rather than failing.
- Direct `dotnet test` skips those tests. Repository scripts (`run-tests.ps1`, `run-refactor-gate.ps1`, `run-pre-push-gate.ps1`, the test lane of `verify.ps1`, and `build.ps1`) require the data by default; add `-AllowMissingExampleData` when you do not have access. Even with that option, data is still checked whenever it is present.
- Commit data changes inside `example/` and push them to the data repository, then return to this repository and commit the new submodule pointer. The gate checks that `example/` is exactly at the pinned commit and has no uncommitted changes.
- The two Notch golden snapshots are in `example/golden-snapshots/`. Setting `FREEFORMHELPER_UPDATE_NOTCH_BASELINE=1` or `FREEFORMHELPER_UPDATE_TM81_NOTCH_MATRIX=1` directly updates the corresponding file in the data-repository checkout. Maintainers must commit and push in `FreeformHelper-testdata` before updating this repository's submodule pointer; otherwise, `assert-example-data.ps1` rejects uncommitted `example/` changes.
- Before switching to a historical branch that predates the `example/` submodule and still stores it as an ordinary directory, run `git submodule deinit -f example` to avoid Git refusing the switch because files already exist in that directory.

## 7. Main paths

- Project files: `*.json` (default: `freeform_helper_project.json`)
- Logs: `logs/app.log`
- Build files and outputs: `build/`

## 8. Project structure

- `src/FreeformHelper.Domain`: geometry and pad domain models
- `src/FreeformHelper.Application`: grid/match/notch algorithms and settings models
- `src/FreeformHelper.Infrastructure`: DXF import, project JSON store, and migration
- `src/FreeformHelper.UI`: Avalonia UI (View/ViewModel/Controls/Styles)
- `tests/FreeformHelper.Tests`: unit tests and headless UI tests
- `docs/`: specifications and behavior inventories

## 9. Further documentation

- Documentation index: `docs/README.md`
- Notch canonical reference: `docs/reference/notch-system-reference.md`
- Main algorithm document: `docs/core/freeform-helper-algorithms.md`
- Notch V21: `docs/core/notch-v21-algorithm.md`
- Notch V22: `docs/core/notch-v22-algorithm.md`
- Notch flowchart: `docs/core/notch-v21-v22-flow.md`
- Current C export examples: `example/BOE36.35/notch_export_v21_current.c` / `example/BOE36.35/notch_export_v22_current.c`
- User manual: `docs/guides/app-user-manual.md`
- Development backlog: `ROADMAP.md`
- Script overview: `scripts/README.md`

## 10. Refactor gate (developers)

```powershell
# Standard gate (lint + build + all test groups: notch-core/application/infrastructure/ui-core/ui-snapshots/uncategorized)
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost

# Without access to example/ data
./scripts/tests/run-refactor-gate.ps1 -UseNoAppHost -AllowMissingExampleData
```
