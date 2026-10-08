# TODO (Active Backlog)

> Archived history: `docs/archive/TODO-history-2026-03-05.md`

## Execution Rules (Fixed)
- [ ] For each completed task: `build + matching tests + lint`.
- [ ] For each milestone: `commit + push`.
- [ ] When a task is completed or its scope changes, update this file immediately.

## S6 Structural Cleanup Pass (2026-03-05)

### P1 (Structural Refactor, No Behavior Change)
- [x] **S6.1 Split `FreeformHelperViewModel.DxfEditing` (550 lines)**
  - Goal: Split into partials by import/edit/apply workflow to reduce coupling in the ViewModel hotspot.
  - Scope:
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.cs`
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - Definition of done: Main file < 320 lines, DXF edit path behavior unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperViewModel.DxfEditing.cs` reduced to snapshot/core helpers (main file lines `550 -> 92`).
    - Added 3 new partials:
      - `FreeformHelperViewModel.DxfEditing.Edits.cs` (hide/restore/combine/clear)
      - `FreeformHelperViewModel.DxfEditing.Export.cs` (DXF/image export)
      - `FreeformHelperViewModel.DxfEditing.State.cs` (hidden/combined state sync)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S6.2 Split `FreeformHelperView` code-behind hotspot**
  - Goal: Split `FreeformHelperView.axaml.cs` / `FreeformHelperView.Console.cs` by UI subdomain (workspace/overlay/console binding).
  - Scope:
    - `src/FreeformHelper.UI/Views/FreeformHelperView.axaml.cs`
    - `src/FreeformHelper.UI/Views/FreeformHelperView.Console.cs`
    - New `src/FreeformHelper.UI/Views/FreeformHelperView.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - Definition of done: Main file line count reduced; Terminal and shortcut behavior unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperView.axaml.cs` main file `539 -> 58` lines, split into:
      - `FreeformHelperView.Lifecycle.cs` (attach/detach/initial-fit/deferred hooks)
      - `FreeformHelperView.SettingsWindow.cs` (settings window and root menu)
      - `FreeformHelperView.ConsoleHost.cs` (console shell binding/theme/sync)
    - `FreeformHelperView.Console.cs` main file `535 -> 27` lines, split into:
      - `FreeformHelperView.Console.Events.cs` (auto-scroll, events and interaction)
      - `FreeformHelperView.Console.Rendering.cs` (render/colorizer/search/filter)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S6.3 Split `NotchExportSelectionViewModel` (516 lines)**
  - Goal: Separate row list, preview, and commands into domains to reduce export window maintenance cost.
  - Scope:
    - `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs`
    - New `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - Definition of done: Main file < 300 lines; Export row selection and preview flow unchanged.
  - Completed (2026-03-05):
    - `NotchExportSelectionViewModel.cs` main file reduced to constructor/properties/observable callbacks (`516 -> 158` lines).
    - Added 3 new partials:
      - `NotchExportSelectionViewModel.Selection.cs` (visible rows/group select/counts)
      - `NotchExportSelectionViewModel.Workspace.cs` (workspace linked rows/index/preview callback)
      - `NotchExportSelectionViewModel.Models.cs` (row mode and group/row item models)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

### P2 (Contract Test Hardening)
- [x] **S6.4 Contract tests: Terminal / RuntimeQuery / Ctrl+S**
  - Goal: Add automated guards to prevent regression of fixed contracts.
  - Scope:
    - `tests/FreeformHelper.Tests/UI/**`
    - `tests/FreeformHelper.Tests/Runtime/**` (create if missing)
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
    - `./scripts/tests/lint.ps1 -UseNoAppHost`
  - Definition of done:
    - Terminal TextEditor path is guarded by tests.
    - Runtime query notch payload shape is guarded by tests.
    - `Ctrl+S -> SaveProjectAsync -> toast` contract is guarded by tests.
  - Completed (2026-03-05):
    - Added Runtime query payload shape test: `RuntimeQueryUseCaseTests.ExecuteAsync_QueryNotchValidation_ProducesStableRowPayloadShape`.
    - Extended shortcut contract test: `UiLayoutGuardTests.ShortcutContract_GlobalAndCanvasScopesRemainConsistent` now asserts `Ctrl+S -> SaveProjectFromShortcutAsync -> ShowTopToast`.
    - Terminal TextEditor path uses the existing guard: `TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_UsesTextEditorOnly`.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`, `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --nologo /p:UseAppHost=false --filter "FullyQualifiedName~RuntimeQueryUseCaseTests.ExecuteAsync_QueryNotchValidation_ProducesStableRowPayloadShape|FullyQualifiedName~UiLayoutGuardTests.ShortcutContract_GlobalAndCanvasScopesRemainConsistent|FullyQualifiedName~TerminalStartupPathTests.ConsoleExpanded_AfterStartupLogs_UsesTextEditorOnly"`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

### P2 (Measurement Baseline)
- [x] **S6.5 3635 baseline measurement (measure first, no algorithm changes)**
  - Goal: Quantify selection lag and export time to set a baseline for before/after comparison in later optimizations.
  - Scope:
    - New measurement record under `docs/performance/`
  - Verification:
    - Document is reproducible (includes commands, test data, and measurement fields).
  - Definition of done: Has a fixed template and first baseline results.
  - Completed (2026-03-05):
    - Added `docs/performance/3635-selection-export-baseline-2026-03-05.md`, including:
      - Fixed command, test data, and measurement field template
      - First-pass selection/export baseline results (source: `build/perf/3635-regression-verify`)
    - Added a link to the fixed measurement record in `docs/performance/regression-baseline-3635.md`.
    - Verification: Commands, input paths, and output file fields in the document map directly to existing scripts and artifacts.

## Completed (Recent)
- [x] S4.1~S4.6 (optimizer pass)
- [x] S5.1~S5.3 (structural cleanup pass)
