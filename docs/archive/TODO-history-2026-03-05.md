# TODO (Unfinished List)

## New round of unfinished items (2026-03-05, S5 structural cleanup pass)
### P1 (Large file split, behavior unchanged)
- [x] **S5.1 Split `NotchV22CompensationService.Geometry` (512 lines)**
  - Goal: Split into partials by polygon clipping / reachability / rectangle merge to reduce conflicts when modifying notch geometry.
  - Scope: `src/FreeformHelper.Application/Services/NotchV22CompensationService.Geometry*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main geometry file < 260 lines, `NotchV22CompensationServiceTests` all green.
  - Completed (2026-03-05):
    - Moved the reachability/expand/grid-cell blocks into a new file `NotchV22CompensationService.Geometry.Reachability.cs`.
    - `NotchV22CompensationService.Geometry.cs` reduced to polygon clip/normalize/rectangle merge (main file lines `512 -> 198`).
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S5.2 Split `DxfOverlapAnalyzer` (576 lines)**
  - Goal: Split helpers by overlap gather / report format / guard policy to reduce algorithm change risk.
  - Scope: `src/FreeformHelper.Application/Services/DxfOverlapAnalyzer*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file < 320 lines, `DxfOverlapAnalyzerTests` (existing application test group) stays fully green.
  - Completed (2026-03-05):
    - `DxfOverlapAnalyzer` made partial and split into 3 files:
      - `DxfOverlapAnalyzer.Candidates.cs` (spatial bucket candidate pairs)
      - `DxfOverlapAnalyzer.Geometry.cs` (polygon overlap determination)
      - `DxfOverlapAnalyzer.Signatures.cs` (duplicate signature canonicalization)
    - Main file `DxfOverlapAnalyzer.cs` lines `576 -> 279`, keeping the `Analyze` main flow and report consolidation.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S5.3 Split `RuntimeQueryUseCase.NotchDetails` (510 lines)**
  - Goal: Split into partials by payload builder / polygon projection / diagnostics map, preserving the query response contract.
  - Scope: `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.NotchDetails*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
  - Definition of done: main file < 280 lines, runtime query `notch*` command output contract unchanged.
  - Completed (2026-03-05):
    - `RuntimeQueryUseCase.NotchDetails.cs` reduced to the `QueryCadNotch` main payload (main file lines `510 -> 178`).
    - Added 2 partials:
      - `RuntimeQueryUseCase.NotchDetails.MultiOwner.cs` (`QueryCadMultiOwner` + `QueryCadNotchStage`)
      - `RuntimeQueryUseCase.NotchDetails.PadQueries.cs` (`QueryCadPad` + `QueryRegularPad`)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

## New round of unfinished items (2026-03-05, S4 optimizer pass)
### P0 (Quality baseline)
- [x] **S4.1 Restore `lint -AllFiles` to green (large ENDOFLINE regression)**
  - Goal: Fix the CRLF/LF line-ending inconsistencies reported by `dotnet format` so the whole project is verifiable again.
  - Scope: `src/**/*.cs`, `tests/**/*.cs` (mainly files that lint actually reports)
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Definition of done: `lint -AllFiles` 0 errors, and no behavior changes introduced.
  - Completed (2026-03-05):
    - Batch-normalized CRLF line endings on the files lint reported (including `PadCanvas*`, `FreeformHelperViewModel.*`, `ManualSizingService`, `RuntimeQueryUseCase.Commands.WorkflowSelection`, `NotchTableGenerator*`, `SettingsPersistence*`, etc.).
    - Verification: `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`, `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`.

- [x] **S4.2 Add git EOL drift guardrail (`.gitattributes`)**
  - Goal: Prevent future refactors from reintroducing cross-file EOL drift by fixing the repo's text-file line-ending policy.
  - Scope: `.gitattributes`, necessary documentation notes
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `git diff --stat` (only expected EOL normalization/guardrail files)
  - Definition of done: after new commits on Windows, CRLF continues to be written and lint no longer regresses on ENDOFLINE.
  - Completed (2026-03-05):
    - Added `.gitattributes` to fix main text files (`*.cs`/`*.axaml`/`*.ps1`/`*.json`/`*.md`/`*.txt`) to `eol=crlf`, and marked common binary assets as `binary`.
    - Combined with S4.1 verification, `lint -AllFiles` is green again.

### P1 (Structural hotspots, behavior unchanged)
- [x] **S4.3 Split `DxfVisibleIndexAssignmentService` (607 lines)**
  - Goal: Split into partials by assignment policy / sequence / diagnostics to reduce algorithm change conflicts.
  - Scope: `src/FreeformHelper.Application/Services/DxfVisibleIndexAssignmentService*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file < 320 lines, diff idx assignment behavior and test results unchanged.
  - Completed (2026-03-05):
    - `DxfVisibleIndexAssignmentService.cs` reduced to service shell + `Assign` main flow (main file lines `607 -> 259`).
    - Added 3 partials:
      - `DxfVisibleIndexAssignmentService.Fallback.cs` (no-grid fallback assignment)
      - `DxfVisibleIndexAssignmentService.Grouping.cs` (regular diff grouping / per-IC resolve helpers)
      - `DxfVisibleIndexAssignmentService.RowSequence.cs` (row-sequence DP assignment and nested row item/group types)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S4.4 Split `FreeformHelperViewModel.Selection` (589 lines)**
  - Goal: Split by CAD/Regular selection, batch apply, and inspector sync to reduce hot-path complexity.
  - Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: main file < 320 lines, selection side-effect contract unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperViewModel.Selection.cs` reduced to selection core (main file lines `589 -> 171`).
    - Added 3 partials:
      - `FreeformHelperViewModel.Selection.NotchPreview.cs` (deferred notch preview queue/cancel/run)
      - `FreeformHelperViewModel.Selection.Summary.cs` (selection summary + manual size/range sync)
      - `FreeformHelperViewModel.Selection.Locate.cs` (quick locate / match locate / focus-highlight helpers)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S4.5 Split `NotchV22CompensationService` main file (584 lines)**
  - Goal: Split by owner decision / to-full gate / trace builder, keeping current output consistent.
  - Scope: `src/FreeformHelper.Application/Services/NotchV22CompensationService*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file < 320 lines, `NotchV22CompensationServiceTests` all green.
  - Completed (2026-03-05):
    - `NotchV22CompensationService.cs` reduced to service shell + `Compute` + public helpers (main file lines `584 -> 108`).
    - Added 3 partials:
      - `NotchV22CompensationService.Stages.cs` (Stage A/B/C/D and rule-decision adapter)
      - `NotchV22CompensationService.Boundary.cs` (boundary seed/active set helpers + stage records)
      - `NotchV22CompensationService.SpatialIndex.cs` (CAD bounds spatial index + StageB context owner cache)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

### P2 (Documentation consistency)
- [x] **S4.6 Documentation baseline calibration: behavior/settings matrix stale-check**
  - Goal: Update the "last updated" date and baseline description in `behavior-inventory` and `settings-entry-matrix` to confirm they match the current contract.
  - Scope: `docs/reference/behavior-inventory.md`, `docs/guides/settings-entry-matrix.md`
  - Verification:
    - "Last updated", "branch", and "release baseline" in the documents match the current actual state
    - Key contracts (deferred app settings, Ctrl+S, runtime query single-entry) still map to current code
  - Definition of done: documents can be used directly as the baseline for the next acceptance round, with no obvious stale descriptions.
  - Completed (2026-03-05):
    - Updated document last-updated date to `2026-03-05`, and added an `S4.6 stale-check` section.
    - Explicitly mapped 3 key contracts to code entry points:
      - deferred app settings (`MarkProjectLoadedForAppGeneralPersistence` / `FlushDeferredAppGeneralSettingsIfNeeded`)
      - `Ctrl+S -> SaveProjectAsync -> top toast`
      - runtime query IPC -> `RuntimeQueryUseCase.ExecuteAsync` single-entry
    - Files updated:
      - `docs/reference/behavior-inventory.md`
      - `docs/guides/settings-entry-matrix.md`

## New round of unfinished items (2026-03-05, S3 refactor pass)
### P1 (Structural refactoring without behavior change)
- [x] **S3.1 Split NotchTableGenerator hotspot (864 lines)**
  - Goal: Split the cad-allocation/v2.2 candidate/eligibility/memo sections into partials, keeping a single public entry point.
  - Scope: `src/FreeformHelper.Application/Services/NotchTableGenerator.cs`, `src/FreeformHelper.Application/Services/NotchTableGenerator.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file reduced to < 350 lines, behavior of `NotchTableGeneratorTests` / `NotchV22CompensationServiceTests` unchanged.
  - Completed (2026-03-05):
    - `NotchTableGenerator` split into 4 partials:
      - `NotchTableGenerator.cs` (public entry + constructor)
      - `NotchTableGenerator.Generation.cs` (legacy/cad-allocation/v2.2 candidate and threshold/progress helpers)
      - `NotchTableGenerator.Eligibility.cs` (single-CAD eligibility evaluation)
      - `NotchTableGenerator.Memo.cs` (cad allocation memo)
    - Main file lines `864 -> 119`.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S3.2 Split FreeformHelperView.Canvas view flow (804 lines)**
  - Goal: Extract canvas viewport/overlay/selection event subdomains to avoid a giant main view code-behind.
  - Scope: `src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.cs`, `src/FreeformHelper.UI/Views/FreeformHelperView.Canvas.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: main file < 350 lines, canvas display and interaction behavior unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperView.Canvas.cs` split into:
      - `FreeformHelperView.Canvas.cs` (canvas host/event wiring + pad info interaction entry)
      - `FreeformHelperView.Canvas.Layout.cs` (popover position calculation and geometry utilities)
      - `FreeformHelperView.Canvas.DirtyState.cs` (pending changes / hit-test block policy)
    - Main file lines `804 -> 343`.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S3.3 Split PadCanvas core shell (798 lines)**
  - Goal: Split `PadCanvas` core state/resources/initialization into partials (state/resources/lifecycle).
  - Scope: `src/FreeformHelper.UI/Controls/PadCanvas.cs`, `src/FreeformHelper.UI/Controls/PadCanvas.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: `PadCanvas.cs` main file < 320 lines, selection/render pipeline behavior unchanged.
  - Completed (2026-03-05):
    - `PadCanvas` added 3 partials:
      - `PadCanvas.Properties.cs` (StyledProperty declarations and wrappers)
      - `PadCanvas.State.cs` (core state/caches/selection fields)
      - `PadCanvas.Lifecycle.cs` (constructor, theme/resource lifecycle, cache invalidation, match-link index rebuild)
    - `PadCanvas.cs` reduced to control shell + events (main file lines `798 -> 27`).
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S3.4 Split PadCanvas.Rendering (797 lines)**
  - Goal: Split render layers (grid/cad/regular/labels/overlay) to reduce rendering regression risk.
  - Scope: `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs`, `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`
  - Definition of done: main file < 320 lines, draw order and overlay display contract remain consistent.
  - Completed (2026-03-05):
    - `PadCanvas.Rendering.cs` reduced to render entry and draw-list orchestration (main file lines `797 -> 241`).
    - Added 3 partials:
      - `PadCanvas.Rendering.Cad.cs` (CAD draw + diff-idx overlay)
      - `PadCanvas.Rendering.Regular.cs` (Regular draw + hatch + selection overlay hook)
      - `PadCanvas.Rendering.Labels.cs` (match allocation labels / ratio label placement)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`.

- [x] **S3.5 Split FreeformHelperViewModel.State (794 lines)**
  - Goal: Split state transitions / selection snapshot / derived summary into dedicated partials.
  - Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.cs`, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.State.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: main file < 350 lines, state transition and property-changed behavior unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperViewModel.State.cs` reduced to state shell (selection/inspector/status entry), main file lines `794 -> 238`.
    - Added 2 partials:
      - `FreeformHelperViewModel.State.Configuration.cs` (grid/match/index-mapping/UI options and corresponding partial callbacks)
      - `FreeformHelperViewModel.State.SizingAndPitch.cs` (manual sizing fields, pitch summary, cascade settings)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S3.6 Split FreeformHelperViewModel.Settings (726 lines)**
  - Goal: Keep settings fields / apply / side-effects under a single policy entry point, reducing main file density.
  - Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file < 320 lines, settings persistence/deferred policy unchanged.
  - Completed (2026-03-05):
    - `FreeformHelperViewModel.Settings.cs` reduced to settings shell (cascade rebuild + utility + dirty-tracking override), main file lines `726 -> 236`.
    - Added `FreeformHelperViewModel.Settings.PropertyCallbacks.cs` to hold all `partial void On*Changed(...)` callbacks and setting side-effects.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`.

- [x] **S3.7 Split ManualSizingService (713 lines)**
  - Goal: Further split parsing/apply/undo paths to reduce single-file change conflicts.
  - Scope: `src/FreeformHelper.UI/Services/ManualSizingService.cs`, `src/FreeformHelper.UI/Services/ManualSizingService.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
  - Definition of done: main file < 320 lines, `ManualSizingServiceTests` behavior consistent.
  - Completed (2026-03-05):
    - `ManualSizingService.cs` reduced to snapshot/ensure + result records (main file lines `713 -> 114`).
    - Added 2 partials:
      - `ManualSizingService.Operations.cs` (Apply/Reset/SetPadDimensions + global/local apply paths)
      - `ManualSizingService.Distribution.cs` (compose/distribute helpers, target size, resize/jagged clone)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`.

- [x] **S3.8 Split PadCanvas.View (712 lines)**
  - Goal: Extract viewport transform/fit/zoom helpers to reduce view path complexity.
  - Scope: `src/FreeformHelper.UI/Controls/PadCanvas.View.cs`, `src/FreeformHelper.UI/Controls/PadCanvas.View.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: main file < 320 lines, fit/zoom/pan behavior unchanged.
  - Completed (2026-03-05):
    - `PadCanvas.View.cs` reduced to fit/zoom/pan entry points (main file lines `712 -> 223`).
    - Added 2 partials:
      - `PadCanvas.View.AxisLabels.cs` (axis labels / IC block rendering and label layout helpers)
      - `PadCanvas.View.Bounds.cs` (world/screen transform, world/selection bounds calculation)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S3.9 Split RuntimeQueryUseCase.Commands (693 lines)**
  - Goal: Split by command domain (selection/export/notch/terminal) to prevent command handlers from growing again.
  - Scope: `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.cs`, `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.Commands.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: main file < 320 lines, runtime query JSON schema unchanged.
  - Completed (2026-03-05):
    - `RuntimeQueryUseCase.Commands.cs` reduced to a shell (main file lines `693 -> 4`).
    - Added 4 domain partials:
      - `RuntimeQueryUseCase.Commands.WorkflowSelection.cs`
      - `RuntimeQueryUseCase.Commands.Export.cs`
      - `RuntimeQueryUseCase.Commands.TerminalStatus.cs`
      - `RuntimeQueryUseCase.Commands.NotchQueries.cs`
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S3.10 Split SettingsPersistence test hotspot (678 lines)**
  - Goal: Split test files by topic: app-settings / project-settings / deferred-save.
  - Scope: `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.cs`, `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`
    - `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Definition of done: test names/behavior unchanged, all green after the file split.
  - Completed (2026-03-05):
    - `FreeformHelperViewModelTests.SettingsPersistence.cs` reduced to the settings-draft topic (main file lines `678 -> 186`).
    - Added 2 test partial files:
      - `FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs` (save/load project snapshot logic)
      - `FreeformHelperViewModelTests.SettingsPersistence.AppGeneral.cs` (app-general precedence and deferred flush)
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

## New round of unfinished items (2026-03-05, repo-optimizer-loop)
### P0 (Restore quality baseline first)
- [x] **S2.1 Fix lint -AllFiles regression (FINALNEWLINE / IMPORTS)**
  - Goal: Restore `lint -AllFiles` to fully green so later optimizations are not built on a distorted baseline.
  - Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Snapshots.cs`, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Formatting.cs`, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.Trace.cs`
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Definition of done: both commands succeed and no new formatting suggestions appear.
  - Completed (2026-03-05):
    - Fixed `FINALNEWLINE` and `IMPORTS` in `PadInspector.Snapshots/Formatting/Trace` via `dotnet format --include`.
    - Verification: `./scripts/tests/lint.ps1 -AllFiles -UseNoAppHost`, `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`.

### P1 (Maintainability / extensibility)
- [x] **S2.2 Split DevView giant XAML (1245 lines)**
  - Goal: Split the Dev page into section controls to reduce conflicts and regression risk when adjusting prototype UI.
  - Scope: `src/FreeformHelper.UI/Views/DevView.axaml`, `src/FreeformHelper.UI/Views/DevSections/*`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
  - Definition of done: `DevView.axaml` main file reduced to < 400 lines and existing prototype section functionality on the Dev page unchanged.
  - Completed (2026-03-05):
    - `DevView.axaml` became a shell + probe section, with 3 section controls split out:
      - `Views/DevSections/DevUiLabsSectionView`
      - `Views/DevSections/DevIconAndStickySectionView`
      - `Views/DevSections/DevInspectorPrototypeSectionView`
    - `DevView.axaml` lines `1245 -> 37`.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`.

- [x] **S2.3 Split NotchDetails main flow (866 lines)**
  - Goal: Move snapshot/trace/export row assembly into dedicated partials while keeping a single entry point.
  - Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`, `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application`
  - Definition of done: main file < 350 lines, notch details/runtime query output behavior stays consistent.
  - Completed (2026-03-05):
    - `FreeformHelperViewModel.NotchDetails.cs` reduced to preview/detail entry (`181` lines).
    - Added `FreeformHelperViewModel.NotchDetails.Compensation.cs` (cache + compensation/query helpers).
    - Added `FreeformHelperViewModel.NotchDetails.AutoPlay.cs` (Step3 auto-play loop and stage switching).
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S2.4 Split PadCanvas.Input (854 lines) into pointer/keyboard/selection subdomains**
  - Goal: Reduce coupling in the input event path so box-select latency and shortcut fixes can be iterated independently.
  - Scope: `src/FreeformHelper.UI/Controls/PadCanvas.Input.cs`, `src/FreeformHelper.UI/Controls/PadCanvas.Input.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`
  - Definition of done: `PadCanvas.Input.cs` main file < 300 lines, existing selection/hit-test/shortcut behavior unchanged.
  - Completed (2026-03-05):
    - `PadCanvas.Input` split into:
      - `PadCanvas.Input.cs` (pointer press/release + shared selection fields)
      - `PadCanvas.Input.PointerMove.cs` (pointer move / box-select dragging)
      - `PadCanvas.Input.Navigation.cs` (wheel + keyboard shortcuts)
      - `PadCanvas.Input.Selection.cs` (hit-test/selection helpers and selection API)
    - Main file lines `854 -> 247`.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

- [x] **S2.5 Split UI test hotspot Basics (722 lines)**
  - Goal: Split `FreeformHelperViewModelTests.Basics.cs` by topic to reduce test maintenance cost.
  - Scope: `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.Basics.cs`, `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.*.cs`
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group application`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
  - Definition of done: the giant Basics file is split, test names and covered topics remain traceable, and all tests are green.
  - Completed (2026-03-05):
    - `FreeformHelperViewModelTests.Basics.cs` split into 4 topic files:
      - `FreeformHelperViewModelTests.Basics.CoreFlags.cs`
      - `FreeformHelperViewModelTests.Basics.LocateAndMatchActions.cs`
      - `FreeformHelperViewModelTests.Basics.Step3AndOverlap.cs`
      - `FreeformHelperViewModelTests.Basics.GridPitch.cs`
    - Test names and behavior unchanged; only file splitting and formatting cleanup.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group application -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`, `./scripts/tests/run-tests.ps1 -Group smoke -UseNoAppHost`, `./scripts/tests/lint.ps1 -UseNoAppHost`.

## Scan baseline (2026-02-26)
- Current working branch: `master`
- Current release baseline: `beta-0.1` (tag, 2026-03-04)
- Scan scope (tracked files): `353` files
  - `src`: 247
  - `tests`: 54
  - `docs`: 22
  - `scripts`: 15
  - `example`: 6
- Completed static hotspot scan (`.cs/.axaml`): `286` files (including tests)

## Newly Added Unfinished Items (2026-02-27, Priority List)
### P0 (Behavior first, reduce usability and performance pain points first)
- [x] **U0.1 Export DXF (visible) must include the original DXF layer used as the regular source**
  - Background: If the current regular source is a DXF layer (not generated), `Export DXF (visible)` must also export that layer, so the exported content matches what is shown on screen and what the calculation uses.
  - Acceptance:
    - Load a project with `regular source = DXF layer`, then check visible export.
    - The export result must include the geometry of the layer designated as the regular source.
    - If the regular source is generated, the behavior stays as it is now (do not add the original layer by mistake).
  - Done (2026-02-27):
    - `Export DXF (visible)` appends the regular source layer pads when `RegularSourceMode=FromDxfLayer` (hidden CAD is still excluded).
    - Added test: `FreeformHelperViewModelTests.CommandsAndUndo.ExportDxfVisibleCommand_WhenDxfRegularSourceLayerSelected_IncludesRegularLayerPads`.

- [x] **U0.2 Clean up Step3 recompute entry: define `Recompute selected` and decide on `Recompute all`**
  - Background: The button semantics are unclear. We need to decide whether to keep the selected path, change it to all, or keep both.
  - Direction:
    - Define the trigger matrix for Step3 automatic and manual recompute (selected/all).
    - Make UI text and behavior consistent so users are not confused.
  - Acceptance:
    - `docs/reference/behavior-inventory.md` contains the complete Step3 trigger matrix.
    - The UI clearly shows whether it recomputes "only selection" or "global recompute".
  - Done (2026-02-27):
    - Decision: keep the "focused selection" path. Do not add `Recompute all` (to avoid full CAD recompute that blocks the UI unnecessarily).
    - UI text changed to `Recompute focused CAD (Step 3)`. Tooltip states clearly that it recomputes the focused CAD in the current selection, not a global recompute.
    - `docs/reference/behavior-inventory.md` now includes the Step3 automatic/manual trigger matrix and the actual recompute semantics.

- [x] **U0.3 Document the meaning of notch rows and the no-op row strategy + UI display strategy**
  - Background: Users currently find it hard to understand why no-op rows such as `8,100,65535,0,65535,0,0` exist.
  - Direction:
    - Document the data contract of notch rows (output compatibility, index alignment, downstream parsing needs).
    - In the UI, show no-op rows by default as a readable summary, and provide a "hide no-op" view toggle (do not change the default output).
  - Acceptance:
    - `docs/reference/behavior-inventory.md` and `docs/guides/*` include the no-op row explanation and examples.
    - In the `Select notch rows` window, no-op rows are easy to identify, and reading effort is reduced.
  - Done (2026-02-27):
    - `Select notch rows` adds `Rows shown` (default `Transfer-only`, can switch to `All rows`).
    - `docs/reference/behavior-inventory.md` documents the no-op row contract, its purpose, and the UI display strategy.

- [x] **U0.4 Incremental cache for the Inspector hot path (selection revision key)**
  - Current state: `FreeformHelperViewModel.PadInspector.cs` still repeats `OrderBy/GroupBy/ToList` for single selection.
  - Direction: Build revision-based caches for matched links/details and rule traces. Recompute only when the selection or upstream data version changes.
  - Acceptance:
    - When switching repeatedly between the same pad in `project_3635` with single selection, Inspector time drops significantly.
    - Existing `PadInfoViewModelTests` / `FreeformHelperViewModelTests` all pass.
  - Done (2026-02-27):
    - Added a revision-based inspector match cache (CAD/Regular split). Repeated single selection no longer reruns the `OrderBy/GroupBy/ToList` hot path.
    - `ApplySelection` adds `selectionRevision`. Deferred inspector refresh only applies the current selection version, so stale background results do not overwrite newer ones.
    - `BumpNotchExportCad/Grid/IndexFingerprint` also invalidates the inspector cache, so stale data is not read after upstream changes.
    - Added tests: `FreeformHelperViewModelTests.PadInspectorCache` (3 cases) cover cache reuse and invalidation.

- [x] **U0.5 Change AppLogStore to a ring buffer (limit memory growth)**
  - Current state: `AppLogStore` has no upper limit, so memory accumulates during long runs.
  - Direction: Add a configurable limit (default 5k to 20k). Automatically evict old lines when exceeded, while keeping thread safety and UI binding behavior.
  - Acceptance:
    - After stress writes, `Entries.Count` does not exceed the limit.
    - Console and runtime query behavior is consistent, with no cross-thread exceptions.
  - Done (2026-02-27):
    - `AppLogStore` adds a ring buffer limit (default `10000`). UI entries and the pending queue are trimmed automatically.
    - Added `GetTail(int)` and `GetTotalCount()`.
    - Added tests: `AppLogStoreTests.Add_WhenExceedMaxEntries_KeepsNewestTail`, `AppLogStoreTests.Clear_RemovesAllRingEntries`.

- [x] **U0.6 Change runtime query terminal to a tail API to avoid full copies**
  - Current state: `RuntimeQueryUseCase.Commands.cs` uses `Entries.ToList()` when reading the terminal.
  - Direction: Add `AppLogStore.GetTail()` (or an equivalent API) that returns a tail snapshot directly, avoiding full copies.
  - Acceptance:
    - `query terminal --tail N` and `query terminal-links --tail N` no longer create full intermediate collections.
    - The existing runtime query response format is unchanged.
  - Done (2026-02-27):
    - `RuntimeQueryUseCase.Commands` `terminal` / `terminal-links` now use `AppLogStore.GetTail()` instead of full `Entries.ToList()` copies.
    - Response schema stays the same (`totalLines`, `returnedLines/scannedLines`, `links`).

- [x] **U0.7 Virtualize large data in the Notch export window**
  - Current state: `NotchExportSelectionWindow.axaml` uses nested `ItemsControl + ScrollViewer`, which is expensive with many rows.
  - Direction: Use a virtualizing list (`ItemsRepeater` / `DataGrid` or an equivalent), keeping grouping and preview linkage.
  - Acceptance:
    - Scrolling and selection are smoother with 500+ rows in 3635.
    - Row selection, group select all/none, and preview sync do not regress.
  - Done (2026-02-27):
    - In `NotchExportSelectionWindow`, the left side changed from nested `ItemsControl + ScrollViewer` to "IC group summary row + one `ListBox` (`VirtualizingStackPanel`)".
    - Keeps the `Rows` main list, group select all/none, row preview sync, and the existing data contract.
    - Verified: `NotchExportSelectionViewModelTests`, `NotchExportSelectionWindowSmokeTests`, and `UiLayoutGuardTests` all pass.

- [x] **U0.8 Further slim the first frame at startup: keep only workspace/grid in the first frame, defer heavy objects to idle/background**
  - Current state: Although it is already under the <1s budget, some heavy synchronous initialization can still be moved later.
  - Direction: Review synchronous work in `App/ShellViewModel` on the first frame and defer non-critical initialization.
  - Acceptance:
    - `workspace.initial-grid-built` stays consistently within budget.
    - Cold start has no noticeable first-frame stutter, and `check-startup-budget.ps1` keeps passing.
  - Done (2026-02-27):
    - `App` runtime IPC startup changed to `Opened/ApplicationIdle -> DispatcherPriority.Background`, to avoid competing with the first frame.
    - `FreeformHelperViewModel.EnsureInitialGrid()` adds an in-flight guard to avoid duplicate initial grid rebuilds from the startup path.
    - `ShellViewModel` only updates the console summary at startup. The full console text build is deferred until expansion (or background ready), reducing synchronous load on the first frame.
    - The AvaloniaEdit theme loading in `FreeformHelperView` has a re-entrant guard, to avoid recursive style reloads and possible crashes when the console first expands.
    - `ShellViewModel` console rebuild and summary count now read the `AppLogStore` ring buffer, no longer depending on UI observable collection flushes. When terminal expands, it shows the existing log tail even if UI entries are not yet filled.
    - Console restore in `FreeformHelperView` keeps the last non-empty snapshot, so the editor is not overwritten with a blank string during temporary empty states.
    - The `AvaloniaEdit` theme is back to app-level static include in `App.axaml`. Workspace lazy-loading was removed to avoid a first-pass template/style race that caused a black screen in the terminal `TextEditor`.
    - The terminal display path is fixed to a single `AvaloniaEdit TextEditor` path. Non-empty snapshot restore and startup smoke are kept, to avoid falling back to `TextBox fallback` or reintroducing workspace lazy-load branching.
    - Terminal is expanded by default. Initial size keeps `ConsolePanelRowHeight=180` and `ConsolePanelMinHeight=120`, and no longer starts collapsed.
    - `HeadlessUiSmokeTests` now checks the real `MainWindow + FreeformHelperView + expanded terminal` render result. If the terminal becomes fully black or shows no text again, the gate fails directly.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~RuntimeQueryIpcTests|FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~WorkspaceInteractionStateTests|FullyQualifiedName~FreeformHelperViewModelTests.Basics"`, `./scripts/tests/check-startup-budget.ps1 -SkipBuild` (`workspace.initial-grid-built=658ms`, PASS).

- [x] **U0.9 Formally apply the CAD / Regular right-click information architecture (reuse the Dev prototype)**
  - Background: The Dev page has confirmed the new two-column rhythm, section divider lines, and the short description + tooltip information hierarchy. These need to be formally applied to the product right-click menu, while keeping existing function entry points.
  - Acceptance:
    - CAD / Regular right-click uses a fixed two-column rhythm. Long text wraps and does not overflow the container.
    - Keep existing high-frequency actions and edit entry points (match / highlight / diff idx override / anchor / size / freeform).
    - `PadInfoPopover` build and `ui-core/smoke` tests do not regress.
  - Done (2026-03-01):
    - `PadInfoPopover.axaml` now formally applies the new CAD / Regular right-click information architecture to the product page.
    - Added `padInfo*` section-divider / key-value / info-button / action-button styles, all consistently using tokens.
    - `CadPadInfoViewModel` / `RegularPadInfoViewModel` added identity, summary, and short-reason display fields.
    - The low-frequency `Geometry / Debug` blocks now expand/collapse by clicking the white divider section heading. No extra `Show detail` button is added.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core`, `./scripts/tests/run-tests.ps1 -Group smoke`.

- [x] **U0.10 Add Workspace / AA reverse lookup and row enable-disable linkage to the Notch export window**
  - Background: Originally, `Select notch rows` only had a one-way `row -> canvas preview` flow. It was not possible to select CAD / Regular in the workspace and look up the related notch rows.
  - Acceptance:
    - While the export window stays open, clicking CAD / Regular in the main workspace shows the linked rows in the window.
    - Linked rows can be enabled or disabled directly in the linked row section, without manually searching the left list.
    - After switching between `Transfer-only / All rows` mode, the linked row display matches the current row mode.
  - Done (2026-03-01):
    - `NotchExportSelectionWindow` now opens as modeless. Workspace selection events sync to `NotchExportSelectionViewModel.ApplyWorkspaceSelection(...)`.
    - The window adds a `Workspace selection / AA lookup` section that shows linked rows, with `Enable linked` / `Disable linked`.
    - Repeated `Preview` clicks on the same row now also resync to workspace selection, instead of doing nothing because `SelectedRow` did not change.
    - The first screen of the right-side preview is condensed to `Key value / Effect / Pad relation`. Long payload details moved to a collapsible `Payload / Debug` section, to reduce noise.
    - Added `NotchExportSelectionViewModelTests` covering workspace-linked rows and row-mode filtering.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `./scripts/tests/run-tests.ps1 -Group ui-core`, `./scripts/tests/run-tests.ps1 -Group smoke`.

### P1 (High-value architecture optimization)
- [x] **U1.1 Change the notch export cache to revision/fingerprint and remove the full signature on every call**
  - Current state: `NotchExportGenerationCacheService` recomputes `ComputeCadSignature/ComputeGridSignature` on every lookup.
  - Direction: Use an incrementing revision or an idempotent fingerprint (driven by upstream change events) instead of a full hash.
  - Acceptance:
    - When data has not changed, cache lookup cost drops significantly.
    - Cache hit/miss semantics and output correctness do not change.
  - Done (2026-02-27):
    - `NotchExportGenerationCacheService` adds fingerprint overloads:
      - `TryGet(ulong cadFingerprint, ulong gridFingerprint, ...)`
      - `Store(ulong cadFingerprint, ulong gridFingerprint, ...)`
    - `BuildNotchTableForExportAsync` now uses the revision fingerprint path and no longer scans `cad/grid` on every lookup to compute signatures.
    - `FreeformHelperViewModel` adds `cad/grid/index` fingerprint revisions. They increment on layer filter changes, grid rebuilds, and Step1/Step2 changes, ensuring cache keys are invalidated correctly.
    - Added test: `NotchExportGenerationCacheServiceTests.TryGet_WithFingerprintOverload_HitsWithoutCadGridSignatureScanPath`.

- [x] **U1.2 Clean up repeated expensive flow in NotchTableGenerator + make DI consistent**
  - Current state: There is a duplicate `BuildAllocations` path and unused or temporary `new` calls (inconsistent service injection).
  - Direction:
    - Unify service injection and lifetimes.
    - Extract v2.2 candidate memoization to avoid repeated calculation.
  - Acceptance:
    - `NotchTableGeneratorTests` and `NotchV22CompensationServiceTests` all pass.
    - 3635 export time drops, and output content stays the same.
  - Done (2026-02-27):
    - `NotchTableGenerator` completes v2.2 compensation service injection consistency (all constructor paths are unified).
    - Added CAD allocation memoization: under the same `RegularGrid`, the allocation for the same CAD pad can be reused, avoiding full recomputation in both `Generate` and `Evaluate`.
    - `GenerateCadAllocationCompatible` adds v2.2 candidate per-CAD memoization, avoiding repeated candidate building for the same CAD in one generation pass.
    - Verification: `NotchTableGeneratorTests` and `NotchV22CompensationServiceTests` pass.

- [x] **U1.3 Refactor shell console text buffer (avoid O(N) cumulative copies)**
  - Current state: `ShellViewModel` append still uses `new StringBuilder(ConsoleText)`, which is costly during long output.
  - Direction: Change to a line-buffer collection (tail buffer) + on-demand rendered string, without rebuilding the full text on every append.
  - Acceptance:
    - CPU and GC pressure drop during high-frequency logging.
    - Deduplication, search, filter, and summary count behavior stay consistent.
  - Done (2026-02-27):
    - `ShellViewModel` now uses a persistent `_consoleTextBuffer`. The append path removes `new StringBuilder(ConsoleText)`.
    - Reset, rebuild, and dedup flows also share the buffer. Summary counts match the original behavior.

## This Round's Performance Optimizations (2026-02-27, behavior-equivalent)
- [x] **P5.1 PadCanvas CAD indexing (Selection/HitTest)**
  - Goal: Change `TryHitCad` and CAD box selection to spatial index queries, avoiding linear scans over all CAD each time.
  - Risk and protection: Keep the linear fallback. Hit testing still uses `Bounds + Polygon.Contains + smallest area first`.
  - Verification:
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~PadCanvasCacheInvalidationTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Done (2026-02-27):
    - Added `CadSpatialIndex`, and `PadCanvas` clears the index cache when CAD data is invalidated.
    - `PadCanvasSelectionEngine.TryHitCad` and `SelectPadsInWorldRect` (CAD path) now query the index first, then fall back to linear search.
    - Strengthened `PadCanvasCacheInvalidationTests`: changing `RegularPads` keeps the CAD index, and changing `CadPads` clears the CAD index.

- [x] **P5.2 Regular candidate index for notch allocation (replace full CAD x Regular scan)**
  - Goal: `NotchAllocationService` / `NotchV22CompensationService StageA` use regular candidate queries, avoiding a full scan of `grid.Pads` for each CAD.
  - Risk and protection: Candidates are only coarsely filtered. `IntersectionAreaWithRect` exact calculation keeps the original rule. Necessary fallback is kept.
  - Verification:
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~NotchV22CompensationServiceTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~RegularGridCandidateQueryTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Done (2026-02-27):
    - Added `RegularGridCandidateQuery` (uses `grid.XEdges/YEdges` to narrow row/col candidate regions first, then refines with `Bounds.Intersects`).
    - `NotchAllocationService.BuildAllocations` now uses the candidate query instead of scanning all `grid.Pads`.
    - `NotchV22CompensationService.RunStageACollectOverlaps` now uses the candidate query, keeping `IntersectionAreaWithRect` exact calculation and the existing output semantics.
    - Added `RegularGridCandidateQueryTests` to verify candidate query equivalence with linear scan.

- [x] **P5.3 ToFull owner check indexing/caching (controlled side effects)**
  - Goal: `StageBBoundaryContext.GetStrictOwnerCadPads` avoids scanning `allCadPads` for every regular pad.
  - Risk and protection: Use deterministic sorting, a cache key (regular index + strict ratio), and fallback to keep results consistent.
  - Verification:
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~NotchV22CompensationServiceTests|FullyQualifiedName~NotchTableGeneratorTests|FullyQualifiedName~WorkflowPipelineServiceTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Done (2026-02-27):
    - Added `CadBoundsSpatialIndex`. `StageBBoundaryContext` enables index-based candidate queries when the CAD count is above a threshold.
    - `GetStrictOwnerCadPads` now works as: `candidate CAD -> IntersectionAreaWithRect exact calculation -> stable sort by pad.Id`, keeping output order and semantics.
    - Added test `Compute_ToFullEnabled_WithManyCadPads_KeepsMultiOwnerGate` to cover multi-owner gate correctness with a high CAD count.

- [x] **P5.4 Visible draw list built from viewport query instead of full CAD table iteration**
  - Goal: `PadCanvas.VisibleDrawListBuilder` uses viewport candidate queries in both low-detail and normal modes, reducing per-frame scan cost.
  - Risk and protection: Keep decimation and highlight/selection visual priority. Do not change existing display semantics.
  - Verification:
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~PadCanvasCacheInvalidationTests|FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~UiLayoutGuardTests"`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Done (2026-02-27):
    - `PadCanvas.Render` ensures `CadSpatialIndex` exists when CAD is displayed, so draw-list queries do not fall back to a full table scan.
    - `PadCanvasVisibleDrawListBuilder` first gets CAD candidates inside `worldViewport`, then applies low-detail decimation and selection/highlight classification.
    - `UiLayoutGuardTests` shortcut contract synced to `FreeformHelperView.InputAndShortcuts.cs`. Added a string contract check for the draw-list index query.

- [x] **P5.5 Step4 visible diff idx assignment changed to IC-row sequence + manual segment lock**
  - Goal: Avoid visible DXF diff idx assignment depending only on a single best match, which causes a whole row to shift after a jump in the middle of an IC row.
  - Rules:
    - By default, use `IC + row` as the sequence unit for monotonic assignment.
    - Manual override is a hard constraint. It splits that row into segments and only reorders that segment, without affecting other rows or ICs.
    - If the strict seed of a per-IC anchor is valid, it participates in the sequence as a fixed point, not just as a visual hint.
  - Acceptance:
    - When a row has a manual override, the segments before and after keep monotonicity and do not drift across segments.
    - When an anchor falls inside a valid row, it is actually used. It is only recorded as ignored when invalid.
    - `application` / `ui-core` tests all pass.
  - Done (2026-03-01):
    - Added `DxfVisibleIndexAssignmentService`, which moves Step4 visible diff idx assignment into a single service entry point.
    - `RebuildVisibleDxfIndexMap` now runs through the service with `override > IC-row sequence > best-match`.
    - `DxfIndexAssignmentSummary` and Inspector text are synced to the `IC-row sequence` semantics.
    - Added test: `DxfVisibleIndexAssignmentServiceTests` covers manual override segment splitting and anchor application.

## This Round's P1 Quality Landing (2026-02-27)
- [x] **Q4.5 Reduce the tests analyzer warning baseline (CA1861 / CA1869 / CA1512)**
  - Background: `build` and `ui-core/smoke` can pass, but the tests project still has many analyzer warnings, which dilute new real issues.
  - Current state (2026-03-04):
    - First fixed the `IMPORTS` / `IDE0005` lint baseline regression. `./scripts/tests/lint.ps1 -AllFiles` is passing again.
    - First batch of `CA1861` hotspots cleaned: `ui-core` warning count dropped from `CA1861=108` to `CA1861=19`.
  - Done (2026-03-04):
    - The second batch cleaned up the remaining `CA1861 / CA1859 / CA1869 / CA1512 / CA1845 / IDE0060`. The stats in `build/logs/ui-core-analyzer-batch2.log` are all `0`.
    - `./scripts/tests/run-tests.ps1 -Group ui-core` and `./scripts/tests/lint.ps1 -AllFiles` both pass.
  - Direction:
    - Start with high-frequency warnings: `CA1861` (repeated constant arrays), `CA1869` (repeated `JsonSerializerOptions`), and `CA1512`.
    - Only make behavior-equivalent fixes. Do not relax rules to hide problems.
  - Acceptance:
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - The number of tests analyzer warnings drops clearly, without affecting existing test behavior.

- [x] **Q4.6 Sync the UI visual baseline (`ConsolePanel.axaml` / `PadInfoPopover.axaml`)**
  - Background: While focusing on tests in `Q4.5`, `UiVisualSnapshotTests.CriticalUiFiles_MatchMinimalVisualBaseline` showed that the current baseline has drifted from the actual UI files.
  - Current observation (2026-03-04):
    - `src/FreeformHelper.UI/Views/ConsolePanel.axaml`
    - `src/FreeformHelper.UI/Views/PadInfoPopover.axaml`
  - Done (2026-03-04):
    - Confirmed the drift in these two files comes from the already-landed, intended UI changes:
      - `ConsolePanel.axaml`: XAML after the terminal was fixed back to the `TextEditor` path.
      - `PadInfoPopover.axaml`: the new CAD / Regular right-click information architecture was formally applied.
    - `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json` is synced to the new hash.
    - Verification: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`, `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --no-build --nologo --filter "FullyQualifiedName~UiVisualSnapshotTests"`.
  - Direction:
    - Confirm that the UI changes in these two files are the intended behavior.
    - If they are as intended, update `tests/FreeformHelper.Tests/Snapshots/ui-visual-minimal-baseline.json`.
    - If not, go back and find the real source of the UI drift and fix the product files.
  - Acceptance:
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug --no-build --nologo --filter "FullyQualifiedName~UiVisualSnapshotTests"`

- [x] **Q4.7 Stabilize headless AvaloniaEdit / font bootstrap**
  - Background: `HeadlessUiSmokeTests` in `ui-core` can currently reproduce a missing `fonts:SystemFonts`. The suspected root cause is that the headless test host removes the app-level AvaloniaEdit theme.
  - Direction:
    - Remove the headless-only `FREEFORMHELPER_HEADLESS_DISABLE_AVALONIAEDIT_THEME` special case.
    - Add a smoke / startup path test that explicitly requires the headless app to keep the `AvaloniaEdit` theme.
  - Acceptance:
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`
    - `./scripts/tests/run-refactor-gate.ps1`
  - Done (2026-03-05):
    - `AvaloniaTestApp` and `Program.BuildAvaloniaApp()` now both explicitly set `Inter` as the system font source and default family, so the headless path no longer falls back to `$Default/SystemFonts`.
    - Added `AppFontBootstrapper` to centralize the `FontManagerOptions` and `WithSystemFontSource` contract.
    - Added `HeadlessAppBootstrap`, which ensures `App.Initialize()` completes before smoke and snapshot tests, so the headless session does not create only partial app state.
    - `HeadlessUiSmokeTests`, `TerminalStartupPathTests`, `UiRenderedVisualSnapshotTests`, and `NotchExportSelectionWindowSmokeTests` now use the shared bootstrap.
    - All `AvaloniaFact` headless UI classes are consolidated into the `HeadlessUiSerial` collection, to avoid `FontManager` / headless session conflicts when `ui-core` runs in parallel.

## Next Mainline Convergence (2026-03-04)
- [x] **R7.1 Sync TODO / docs to the `master + beta-0.1` baseline**
  - Background: `TODO.md` and some docs still contain old branch context. If not synced first, later optimizations will cause document and mainline state drift.
  - Scope:
    - `TODO.md`
    - `docs/reference/behavior-inventory.md`
    - `docs/guides/settings-entry-matrix.md`
  - Done (2026-03-04):
    - `behavior-inventory` and `settings-entry-matrix` are synced to the `master / beta-0.1` baseline.
    - Added the fixed terminal contract (`TextEditor-only`, app-level theme include, expanded by default) and the app settings deferred / `Ctrl+S` top toast contract.
    - `git grep "refactor-code-reduction-pass1"` currently only shows historical/completed records in `docs/guides/repo-refactor-scan-2026-02-26.md` and `TODO.md`.
  - Acceptance:
    - Branch / baseline / terminal contract in the docs match `master` and `beta-0.1`.
    - `git grep "refactor-code-reduction-pass1"` only shows historical completion records, and no longer appears in the current execution baseline description.

- [x] **R7.2 Further split `PadInfoPopover` to reduce the maintenance cost of the formal right-click menu**
  - Background: `src/FreeformHelper.UI/Views/PadInfoPopover.axaml` is still a hotspot with `700+` lines. The new information architecture is now stable, so it should be split into section views to reduce future adjustment risk.
  - Direction:
    - Keep the existing behavior and data contract.
    - Split views by `CAD summary / actions / edit tools / geometry-debug` and `Regular summary / actions / edit tools / geometry-debug`.
  - Done (2026-03-04):
    - Added `src/FreeformHelper.UI/Views/PadInfoSections/CadPadInfoContentView.axaml`.
    - Added `src/FreeformHelper.UI/Views/PadInfoSections/RegularPadInfoContentView.axaml`.
    - The main `PadInfoPopover.axaml` file is now reduced to shell + DataTemplate routing + confirm footer (`733 -> 42` lines).
    - Kept the original binding/command and the `PadInfoPopover.axaml.cs` close-confirm behavior. No second workflow entry point was added.
  - Acceptance:
    - The main `PadInfoPopover.axaml` file is significantly shorter.
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`

- [x] **R7.3 Split `RightWorkflowPanel` into section views to prevent settings / step UI from growing back into a giant XAML file**
  - Background: `src/FreeformHelper.UI/Views/RightWorkflowPanel.axaml` is still close to `800` lines and is the next high-risk UI hotspot.
  - Direction:
    - Split by step section / inspector shell, without changing user-visible behavior.
    - Keep tokens and existing styles. Do not add hardcoded values in views.
  - Done (2026-03-04):
    - Added `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowInspectorView.axaml`.
    - Added `src/FreeformHelper.UI/Views/RightWorkflowSections/RightWorkflowSettingsTabView.axaml`.
    - The main `RightWorkflowPanel.axaml` file is now reduced to shell + shared resources (`796 -> 72` lines).
    - The original `OpenSettingsRequested` routing and the `BringIntoView()` logic after expanding a step were moved to `RightWorkflowSettingsTabView`. User-visible behavior stays the same.
  - Acceptance:
    - The main `RightWorkflowPanel.axaml` file is significantly shorter.
    - `./scripts/tests/run-tests.ps1 -Group ui-core`
    - `./scripts/tests/run-tests.ps1 -Group smoke`

- [x] **R7.4 Further slim the main `PadInspector` flow, consolidating into snapshot builder / formatter / action entry**
  - Background: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.cs` is still a hotspot with `750+` lines, and adding more rule/summary logic later could easily get out of control.
  - Direction:
    - Split snapshot build, text formatting, and action entry into separate single-responsibility partials / services.
    - Keep the `selection revision cache` and current inspector behavior unchanged.
  - Done (2026-03-05):
    - Added `FreeformHelperViewModel.PadInspector.Formatting.cs`.
    - Added `FreeformHelperViewModel.PadInspector.Snapshots.cs`.
    - Added `FreeformHelperViewModel.PadInspector.Trace.cs`.
    - `FreeformHelperViewModel.PadInspector.cs` main file is now reduced to a high-level snapshot entry (`757 -> 216` lines). Formatting, trace, and notch-row helpers are split out. Existing output and cache behavior are unchanged.
  - Acceptance:
    - The `PadInspector.cs` main file is significantly shorter.
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `./scripts/tests/run-tests.ps1 -Group ui-core`

- [x] **R7.5 Fix the `project_3635` performance regression gate (selection / inspector / export)**
  - Background: There are already startup and regression scripts, but selection / inspector / export still lack fixed gates. Later optimizations will make it hard to judge whether there has been a regression.
  - Direction:
    - Use `project_3635.json` to create fixed sampling scripts or report output for selection latency, inspector refresh, and CSV/TXT export.
    - First define measurement and thresholds. Do not mix in large-scale algorithm rewrites in the same step.
  - Done (2026-03-05):
    - `RuntimeQueryResponseBuilder.BuildSelectionPayload(...)` now adds `timings(summary/inspector/notchPreview/total)`. `query selection` / `query select-cad` can now directly sample selection pipeline time without relying on Debug logs.
    - `query export-notch` now adds `elapsedMs`, so it can be included in the 3635 export gate directly.
    - Added the fixed budget `docs/performance/regression-baseline-3635.budget.json`. `scripts/perf/run-3635-regression-baseline.ps1` outputs a `gate` result and supports `-EnforceBudget`.
    - The script adds `-SkipNotchValidation`, so the selection / inspector / export perf gate can be decoupled from Step5 validation queries.
    - Live run verification: `./scripts/perf/run-3635-regression-baseline.ps1 -SkipBuild -SelectionCycles 2 -OutDir build/perf/3635-regression-verify -SkipNotchValidation -EnforceBudget`. Summary shows `gate.pass=true`. In this sample, `selection.total.p95=6ms`, `selection.inspector.p95=0ms`, `export.csv.elapsed=9682ms`, `export.txt.elapsed=80ms`.
  - Acceptance:
    - `scripts/perf` or `docs/performance` adds the corresponding baseline.
    - It can be run repeatedly and compared before and after.

- [x] **P1.1 Make the test naming rule a policy (relax CA1707 for tests only)**
  - Goal: Keep the readability of underscore test method names, and prevent CA1707 from filling the analyzer noise.
  - Change:
    - `.editorconfig` adds `[tests/**/*.cs]` with `dotnet_diagnostic.CA1707.severity = none`.
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `scripts/tests/run-tests.ps1 -Group ui-core -UseNoAppHost`
  - Result:
    - `build` / `ui-core tests` pass.
    - `lint -AllFiles` is still blocked by existing baseline issues (not introduced by this change). Main remaining items are `ENDOFLINE` and many existing analyzer/doc warnings.

- [x] **P1.2 Enable GenerateDocumentationFile and remove IDE0005 prerequisite noise**
  - Goal: Remove the `EnableGenerateDocumentationFile` hint, so lint output stays focused.
  - Change:
    - `Directory.Build.props` enables `GenerateDocumentationFile=true`.
    - Use `NoWarn += 1591` to avoid XML comment coverage noise after the import.
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
  - Result:
    - The `EnableGenerateDocumentationFile` hint is removed.
    - `lint -AllFiles` is still not fully green for the same reason as above (existing baseline, to be cleaned later).

## Newly Added This Round (2026-02-26 Night)
- [x] **S1.9 Remove the startup screen loading overlay (startup can now go straight to workspace)**
  - Changes:
    - `MainWindow` removes the startup overlay visuals and related timer/event control logic.
    - The `App` startup flow no longer waits for the overlay. It starts runtime IPC directly after shell ready.
    - The `FreeformHelperView` deferred hook is now simply a telemetry mark `workspace.startup-ready-signal`.
    - Removed overlay-specific tokens (`StartupOverlay*`).
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj -c Debug /p:UseAppHost=false`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~PadCanvasCacheInvalidationTests|FullyQualifiedName~PadCanvasHitTestTests|FullyQualifiedName~FreeformHelperViewModelTests|FullyQualifiedName~IndexMappingReportViewModelTests|FullyQualifiedName~IndexMappingSettingsTests|FullyQualifiedName~NotchExportSelectionViewModelTests|FullyQualifiedName~PadInfoViewModelTests|FullyQualifiedName~WorkspaceInteractionStateTests"`
    - `dotnet test tests/FreeformHelper.Tests/FreeformHelper.Tests.csproj -c Debug /p:UseAppHost=false --filter "FullyQualifiedName~HeadlessUiSmokeTests|FullyQualifiedName~WorkflowPipelineServiceTests"`

- [x] **R6.1 Giant file third split pass (P0)**
  - Hotspots (>1000 lines): `FreeformHelperViewModel.Operations.cs`, `FreeformHelperViewModel.State.cs`, `FreeformHelperViewModel.Settings.cs`, `PadCanvas.Rendering.cs`, `NotchV22CompensationService.cs`.
  - Direction: continue single-entry, split into flow/state/formatting submodules, keep each file < 800 lines.
  - Done (2026-02-27):
    - `FreeformHelperViewModel.Operations` adds `FreeformHelperViewModel.Operations.LayerSelection.cs`; main file reduced to `462` lines.
    - `FreeformHelperViewModel.State` adds `FreeformHelperViewModel.State.CanvasAndMapping.cs`; main file reduced to `794` lines.
    - `FreeformHelperViewModel.Settings` adds `FreeformHelperViewModel.Settings.DirtyTracking.cs`; main file reduced to `727` lines.
    - `PadCanvas.Rendering` adds `PadCanvas.Rendering.NotchPreview.cs`; main file reduced to `792` lines.
    - Existing `NotchV22CompensationService.Geometry.cs` kept; `NotchV22CompensationService.cs` remains `470` lines.

- [x] **R6.2 View code-behind slimming (P1)**
  - Hotspots: `FreeformHelperView.axaml.cs` (966 lines), `FreeformHelperView.Console.cs` (915 lines).
  - Direction: extract shortcut/router, console attach/sync, and window/shell hooks into independent services or partial subdomains.
  - Done (2026-02-27):
    - Added `FreeformHelperView.InputAndShortcuts.cs` and `FreeformHelperView.ConsoleLinks.cs`.
    - Line counts: `FreeformHelperView.axaml.cs` `631` lines, `FreeformHelperView.Console.cs` `597` lines.

- [x] **R6.3 ManualSizingService responsibility split (P1)**
  - Hotspot: `ManualSizingService.cs` (901 lines).
  - Direction: split into a calculation core (pure algorithms) and UI interaction wrapper (commands/state), and add unit tests as guards.
  - Done (2026-02-27):
    - Added `ManualSizingService.Parsing.cs`; main file reduced to `713` lines, keeping existing entry points and behavior.

- [x] **R6.4 NotchDetails synchronous blocking cleanup (P1)**
  - Hotspot: `FreeformHelperViewModel.NotchDetails.cs` still uses `Task.Result` (lines 84/232/244).
  - Direction: move to an async pipeline or cached result snapshot to avoid potential UI thread blocking.
  - Done (2026-02-26):
    - Renamed the `Notch compensation cache` tuple field from `Result` to `Compensation`, removing the `entry.Result` naming ambiguity so the cache field is not mistaken for `Task.Result` synchronous blocking.
    - Updated `FreeformHelperViewModel.Core.cs` and `FreeformHelperViewModel.NotchDetails.cs` accordingly.

- [x] **R6.5 Project-wide lint baseline reset (P0)**
  - Current state: `lint.ps1 -AllFiles` produces many errors from line endings/using order (`ENDOFLINE`/`IMPORTS`), and is sensitive to apphost file locks.
  - Direction:
    - Unify project CRLF/using order (clean `src` first, then `tests`).
    - Add a `UseAppHost=false` option to lint/build scripts to avoid external process locking `FreeformHelper.UI.exe`.
  - Done (2026-02-27):
    - Added the `UseNoAppHost` switch:
      - `scripts/tests/lint.ps1`
      - `scripts/tests/run-tests.ps1`
      - `scripts/tests/run-refactor-gate.ps1`
    - `lint.ps1` now checks `dotnet` subcommand exit codes to avoid false success.
    - Full `CRLF/IMPORTS` cleanup completed; `./scripts/tests/run-refactor-gate.ps1 -Configuration Debug -UseNoAppHost -LintAllFiles` passes.

## Requirement sync for this round (2026-02-26)
- [x] **N0.1 Upper-left Settings becomes real tabs**
  - Only adjust the upper-left `SettingsWindow` (General/Step1..Step5 single-page switching); the right workflow panel is unchanged.
  - Switching tabs automatically returns to the top of the content, avoiding the reading burden of the old collapsible long page.

- [x] **N0.2 Select notch rows to export: reorder information hierarchy**
  - Export selection window changed to "high-frequency key points on top, details below": `Values` and the result summary are placed at the top, long descriptions go later.
  - Added an in-window `Export type` switch, so choosing the wrong type doesn't require starting over.

- [x] **N0.3 Export image resolution fix**
  - Final output size changed to `A + 2B` (`width/height + padding*2`), and the UI shows a live output resolution text.

- [x] **N0.4 Documenting when Recompute / compensation is triggered**
  - Need a section that matches current behavior: when it auto-computes, when to press Recompute, and which toggles only affect the overlay.
  - Done:
    - Added a "Step3 Recompute / Compensation trigger timing" section to `docs/reference/behavior-inventory.md`, clearly distinguishing:
      - Auto recompute triggers
      - Display toggles (overlay only)
      - Actual purpose of the Recompute button
      - Corresponding code entry files

- [x] **N0.5 App settings write timing adjustment (deferred after loading project)**
  - Rule: after loading a project, settings changes are staged and not written to app settings immediately; they are flushed once on the next successful `Save Project`.
  - Implemented deferred persistence to avoid tests or workflows overwriting app-level defaults midway.

- [x] **N0.6 Save project shortcut + top prompt**
  - Added a global `Ctrl+S` shortcut (when not focused on text input) that triggers `SaveProjectAsync()`.
  - After saving completes, a top toast is shown (success/failure/cancel notice).

## New findings from this round's scan (2026-02-26)
- [x] **S1.1 Second-round optimization of Selection/Inspector latency (P0)**
  - Goal: reduce latency at the start of box selection; tier selection summary and inspector/notch calculations, and avoid recalculation during dragging.
  - Entry: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Selection.cs`
  - Verification: box select on `project_3635.json` + log `Selection updated` (inspector/total ms reduced).
  - Done:
    - CAD/REG single selection changed to "fast snapshot + background completion": the synchronous path only keeps the summary, and full rule trace/notch details are refreshed later.
    - Added a REG deferred inspector refresh pipeline, sharing the pending state flag with the CAD deferred refresh.
    - Match details now have a line cap, preventing very long strings from causing UI stutter when many matches occur.

- [x] **S1.2 RuntimeQuery IPC shutdown flow de-blocking (P0)**
  - Goal: remove the synchronous wait `_runLoopTask?.Wait(...)` inside `RuntimeQueryIpcServer.Dispose()` and switch to async stop.
  - Entry: `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`
  - Verification: starting/closing the App produces no close hang, and IPC stop logs are normal.
  - Done:
    - `RuntimeQueryIpcHost` added `StopAsync()`; `Stop()` changed to fire-and-forget async stop.
    - `RuntimeQueryIpcServer` changed to `IAsyncDisposable`, removed synchronous `_runLoopTask?.Wait(...)`, and switched to a non-blocking shutdown flow that uses `await run loop`.
    - The `App` exit hook now calls `RuntimeQueryIpcHost.StopAsync()`, avoiding synchronous blocking on the UI thread.
    - Added `RuntimeQueryIpcTests` covering host start/stop and the stop timeout contract.

- [x] **S1.3 CA1822/CA1865 debt cleanup (P0)**
  - Goal: clear the current `lint -AllFiles` warnings for `CA1822` (72) and `CA1865` (2).
  - Entry: `src/FreeformHelper.UI/Services/*.cs`, `src/FreeformHelper.Application/**/*.cs`
  - Verification: `./scripts/tests/lint.ps1 -AllFiles` warning count drops and behavior is unchanged.
  - Done:
    - Applied analyzer code fixes for `CA1822/CA1865`, plus manual fixes for the remainder.
    - `CA1865` changed to `StartsWith(char)`.
    - `CA1822`: pure helper functions changed to static; for service classes that must keep instance APIs, added `SuppressMessage` with rationale.
    - Latest analyzer build stats: `CA1822=0`, `CA1865=0` (verified via `build/tmp/analyzer-build-after-s13d.log`).

- [x] **S1.4 Expand lint gate to the solution (P1)**
  - Goal: add an optional "full solution analyzer gate" to avoid quality debt drift when only the UI project is checked.
  - Entry: `scripts/tests/lint.ps1`, `scripts/tests/run-refactor-gate.ps1`
  - Verification: the new command runs the full solution analyzer and can be adopted locally and in CI.
  - Done:
    - `lint.ps1` adds `-AnalyzerScope UiProject|Solution` (default `UiProject`, optional `Solution`).
    - `run-refactor-gate.ps1` adds `-LintAllFiles` and `-LintAnalyzerScope`, allowing direct switching to a full-solution analyzer gate.
    - `scripts/README.md` and `tests/README.md` now include full solution lint/gate command examples for local and CI use.

- [x] **S1.5 Re-split of oversized files (P1)**
  - Goal: keep reducing the risk of >1000-line files (`RuntimeQueryUseCase`, `FreeformHelperViewModel.Settings/State/Operations`, `PadCanvas.Rendering`).
  - Principles: single-entry unchanged, behavior unchanged, split one subdomain per round and add regression tests.
  - Done:
    - `RuntimeQueryUseCase` completed its second split: entry/routing stays in `RuntimeQueryUseCase.cs`, command implementations moved to `RuntimeQueryUseCase.Commands.cs` and `RuntimeQueryUseCase.NotchDetails.cs`.
    - The class is now `partial`, keeping a single public entry (`ExecuteAsync`) and unchanged command router behavior.
    - Line counts after split: `RuntimeQueryUseCase.cs` 261 lines, `RuntimeQueryUseCase.Commands.cs` 639 lines, `RuntimeQueryUseCase.NotchDetails.cs` 512 lines (all < 1000).

- [x] **S1.6 UI text overflow guard tests (P1)**
  - Goal: text in key panels (Settings/Inspector/Notch export) must wrap and not exceed its container.
  - Verification: add/extend UI layout guard tests (headless snapshot or contract tests).
  - Done:
    - `UiLayoutGuardTests` adds `CriticalDynamicTextBindings_UseWrapOrTrimmingContract`, locking that critical dynamic text bindings in Settings/Inspector/Notch export must have wrap/trim/stepNote contracts.
    - Fixed high-risk fields: `Description/Summary` in `SettingsGeneralSectionView` and `SummaryText` in `NotchExportSelectionWindow` now have `TextWrapping`, preventing overflow.
    - Continuously guarded by the UI snapshot/layout guard test group, preventing future layout changes from reintroducing text overflow regressions.

- [x] **S1.7 Startup path optimization (P0)**
  - Goal: prioritize showing workspace/grid, defer non-critical initialization, and make loading progress smoother and more predictable.
  - Done:
    - `ShellViewModel` startup runs via `Opened + immediate bootstrap`, removing the extra background dispatch delay.
    - `workspace startup overlay` now issues a hide request first, then delays the initial console sync.
    - Added a workspace data ready check to the overlay hide condition, avoiding premature hiding that caused transparent/blank appearance.
    - Loading progress changed to smooth incremental progress (deterministic), and pushed to 100% before hiding.
    - `StartupOverlayMinVisibleMs` lowered from `1200` to `450`, reducing cold-start wait time.
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
    - `scripts/tests/run-tests.ps1 -Group smoke`

- [x] **S1.8 Startup <1s (P0) final mile**
  - Goal: grid visible within 1 second on cold start (verified by the `workspace.initial-grid-built` marker).
  - Done:
    - `RuntimeQueryIpcHost.Start(...)` now starts only after `MainWindow` emits the `StartupOverlayHidden` event, avoiding first-screen contention.
    - `AvaloniaEdit` theme loading now initializes only when the console is expanded; the default console changed from expanded to collapsed, so the theme is no longer loaded synchronously on first screen.
    - Added a startup budget gate:
      - `scripts/perf/analyze-startup-markers.ps1` supports `-InitialGridBudgetMs` + `-FailOnBudgetViolation`
      - `scripts/tests/check-startup-budget.ps1` runs measurement + budget validation in one step
      - `run-refactor-gate.ps1` supports `-IncludeStartupBudget`
    - Latest measurement: `workspace.initial-grid-built=692ms` (PASS, budget=1000ms).

## P0 (do first, keep behavior unchanged)
- [x] **Q0.1 Introduce linter gate (required)**
  - Goal: the repo must have a runnable lint, to prevent style and quality from continuously drifting.
  - Current state: no root `.editorconfig`; `Directory.Build.props` does not enable analyzer/lint gate.
  - Tasks:
    - Add root `.editorconfig` (basic C# + XAML conventions, line width, naming, whitespace, using ordering).
    - Enable `EnableNETAnalyzers`, `AnalysisLevel`, `EnforceCodeStyleInBuild` in `Directory.Build.props`.
    - Add `scripts/tests/lint.ps1` (`dotnet format --verify-no-changes` + analyzer build).
    - Update lint instructions in `scripts/README.md` and `tests/README.md`.
  - Verification:
    - `./scripts/tests/lint.ps1`
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
  - Done:
    - Added root `.editorconfig`, the `Directory.Build.props` analyzer gate, and `scripts/tests/lint.ps1`.
    - `lint.ps1` checks the changed files by default; use `-AllFiles` for a full scan.

- [x] **Q0.2 Remove the obsolete Home module (subtraction)**
  - Basis: `HomeView`/`HomeViewModel` exist, but the main view no longer has a Home tab path.
  - Affected files:
    - `src/FreeformHelper.UI/Views/HomeView.axaml`
    - `src/FreeformHelper.UI/Views/HomeView.axaml.cs`
    - `src/FreeformHelper.UI/ViewModels/HomeViewModel.cs`
    - `src/FreeformHelper.UI/Views/HowToUseView.axaml` (still mentions Home; copy needs syncing)
  - Verification:
    - `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
  - Done:
    - Removed leftover `HomeView` / `HomeViewModel` files.
    - `HowToUseView.axaml` synced to the current tabs: `Freeform Helper / How To Use / Dev`.

- [x] **Q0.3 Complete `M1` acceptance (Regular layer popup)**
  - Basis: the TODO still had the only unaccepted item.
  - Verification case:
    - `example/BOE36.35/project_3635.json`
    - Switch `Regular source` / `Regular layer`, check there are no abnormal popup jumps, no overflow, and fixed width.
  - Verification output:
    - `build/logs/app.log` must be able to trace `Open settings window source/section`.
  - Done:
    - The `regularLayerSelector` Popup now has fixed width and `PlacementConstraintAdjustment=All`, preventing dropdown overflow.
    - Added `UiLayoutGuardTests` to lock the regular layer selector contract and the settings source/section log format.
    - The existing `build/logs/app.log` can already trace `Open settings window requested: source=..., section=...`.

## P1 (architecture refactor: flow and style)
- [x] **Q1.1 Split `RuntimeQueryUseCase` (oversized file)**
  - Hotspot: `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.cs` (~1962 lines).
  - Problem: command parsing, parameter validation, response assembly, and cache policy are all coupled.
  - Refactor direction:
    - `RuntimeQueryCommandRouter`
    - `RuntimeQueryArgumentParser`
    - `RuntimeQueryResponseBuilder`
    - `RuntimeQueryNotchCacheService`
  - Requirement: CLI behavior and output JSON shape remain unchanged.
  - Done:
    - `RuntimeQueryUseCase` now delegates to `RuntimeQueryCommandRouter`, keeping a single public entry.
    - Parameter parsing extracted to `RuntimeQueryArgumentParser`.
    - Response payload assembly extracted to `RuntimeQueryResponseBuilder`.
    - Notch query cache and metrics extracted to `RuntimeQueryNotchCacheService`.

- [x] **Q1.2 Split `FreeformHelperViewModel.Operations` (oversized file)**
  - Hotspot: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs` (~1804 lines).
  - Problem: DXF loading, layer catalog, grid/build, and workflow side-effects are mixed in one file.
  - Refactor direction (single-entry):
    - `CadLoadWorkflowService`
    - `GridRebuildOrchestrator`
    - `LayerCatalogStateService`
  - Requirement: the public entry `TriggerGridRebuildAsync` stays single.
  - Done:
    - Added `CadLoadWorkflowService`, extracting the Open DXF workflow from the ViewModel operations file.
    - Added `GridRebuildOrchestrator`, centralizing build decisions for regular source/grid alignment.
    - Added `LayerCatalogStateService`, centralizing the DXF layer catalog cache and loading strategy.
    - `FreeformHelperViewModel.Operations.cs` now delegates to services and is reduced to about 1296 lines.

- [x] **Q1.3 Centralize settings change side-effects**
  - Hotspots:
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`
    - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.SettingsWindow.cs`
  - Problem: `MarkUnsaved` / `InvalidateDownstreamFromStepX` / `TriggerGridRebuildAsync` have multiple scattered entry points.
  - Refactor direction:
    - Build `SettingsChangePolicy` (input: change kind; output: unified side-effects).
  - Requirement: behavior unchanged, reduce duplicated if/flow branches.
  - Done:
    - Added `SettingsChangePolicy` with `SettingsChangeKind`/`SettingsChangeEffects`.
    - Added the `FreeformHelperViewModel.SettingsPolicy` shared apply entry, unifying invalidate/rebuild/dirty/persist execution.
    - Duplicate paths in `Settings.cs` (Step2/Step3/Step4, grid rebuild, recalc bounds, etc.) now use the policy entry.
    - `SettingsWindow.cs` applies the same policy, avoiding a separate side-effects flow.

- [x] **Q1.4 Split `PadInfoViewModels`**
  - Hotspot: `src/FreeformHelper.UI/ViewModels/PadInfoViewModels.cs` (~1697 lines).
  - Refactor direction: separate CAD popover / REG popover / Notch diagnostics / command handlers.
  - Target: each file < 600 lines, reducing cross-area modification risk.
  - Done:
    - `PadInfoViewModels.cs` split into multiple partials under `src/FreeformHelper.UI/ViewModels/PadInfo/*`.
    - Separated `CadPadInfoViewModel` into core / commands / formatting / notch diagnostics.
    - Separated `RegularPadInfoViewModel` into core / commands / formatting.
    - Shared types moved to `PadInfoCommon.cs`; each file is < 600 lines.

## P2 (algorithms and performance)
- [x] **Q2.1 Split `NotchV22CompensationService` computation stages**
  - Hotspot: `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs` (~982 lines).
  - Refactor direction:
    - Stage A: candidate overlap collection
    - Stage B: boundary/owner/blocker judgment
    - Stage C: to-full reachability
    - Stage D: ratio/polygon composition
  - Requirement: `NotchV22CompensationServiceTests` all green; output results bitwise equivalent or consistent within error tolerance.
  - Done:
    - `Compute()` now uses the Stage A/B/C/D pipeline: `RunStageACollectOverlaps`, `RunStageBBuildBoundaryAndOwnership`, `RunStageCEvaluateToFullAndDiagnostics`, `RunStageDBuildResult`.
    - Boundary and owner judgment extracted to `StageBBoundaryContext`, centralizing strict owner cache and boundary index calculation.
    - Intermediate stage data explicitly typed (`OverlappedRegularInfo`, `StageAOverlapResult`, `StageCEvaluationResult`), reducing single-method coupling.
    - Gate verification passed: build, lint, and `application/ui-core/smoke` tests all green.

- [x] **Q2.2 Re-split `PadCanvas` rendering and input responsibilities**
  - Hotspots:
    - `src/FreeformHelper.UI/Controls/PadCanvas.Rendering.cs` (~1199 lines)
    - `src/FreeformHelper.UI/Controls/PadCanvas.Input.cs` (~991 lines)
  - Direction: render layers (grid/cad/notch/labels) + interaction handlers (hit test/select/drag) as independent classes.
  - Goal: improve selection start latency and reduce overlay update blocking.
  - Done:
    - Added `PadCanvasSelectionEngine` (`src/FreeformHelper.UI/Controls/PadCanvas.SelectionEngine.cs`), centralizing hit-test, box-select, and pad center lookup interaction logic.
    - Added `PadCanvasVisibleDrawListBuilder` (`src/FreeformHelper.UI/Controls/PadCanvas.VisibleDrawListBuilder.cs`), centralizing visible list and low-detail decimation planning.
    - `PadCanvas.Input.cs` / `PadCanvas.Rendering.cs` now forward through a single entry, preserving existing behavior and reducing single-file complexity.
    - Gate verification passed: build, lint, and `application/ui-core/smoke` tests all green.

- [x] **Q2.3 Maintainability refactor of `DxfPadImporter` / `DxfRegularMappingAnalyzer`**
  - Hotspots:
    - `src/FreeformHelper.Infrastructure/Dxf/DxfPadImporter.cs` (~729 lines)
    - `src/FreeformHelper.Application/Services/DxfRegularMappingAnalyzer.cs` (~614 lines)
  - Direction: split parser/normalize/filter/mapping score builder, preserving original algorithm results.
  - Done:
    - `DxfPadImporter` became partial modules: entry, `EntityParsing`, `InsertExpansion`, `Tokenization`, `PolylineCommit`, separating parser/normalize/filter responsibilities with the public API unchanged.
    - `DxfRegularMappingAnalyzer` became partial, extracting `CandidateRangeResolver`, `ScoreBuilder`, `ManualOverrideApplier`, `CandidateDiagnosticsBuilder`, splitting mapping score and candidate/override logic.
    - Existing `DxfPadImporterTests` / `DxfRegularMappingAnalyzerTests` behavior maintained; regression verified by the existing application gate after refactor.

- [x] **Q2.4 To Full rule engine and legacy path tech debt cleanup**
  - Involved:
    - `src/FreeformHelper.Application/Services/NotchToFullRuleEngine.cs`
    - `src/FreeformHelper.Application/Services/NotchV22CompensationService.cs`
  - Direction: wrap legacy fallback in an adapter, avoiding scattered branches in the main flow.
  - Requirement: trace output and rule code external format unchanged.
  - Done:
    - Added `src/FreeformHelper.Application/Services/NotchToFullRuleDecisionAdapter.cs`, unifying the rule-engine and legacy fallback branches.
    - `NotchV22CompensationService` now gets `NotchToFullRuleDecision` through the adapter, removing the in-service legacy rule-code branch and `ResolveToFullRuleCode`.
    - Legacy trace spec still uses `legacy.inlineGate`, and rule code constants still come from `NotchToFullRuleEngine`; external output format unchanged.

## P3 (UI layout and maintainability)
- [x] **Q3.1 Modularize `Controls.axaml`**
  - Hotspot: `src/FreeformHelper.UI/Styles/Controls.axaml` (~1549 lines).
  - Direction: split by domain into `Controls.Form.axaml`, `Controls.Panel.axaml`, `Controls.Tab.axaml`, etc., reducing single-file modification conflicts.
  - Done:
    - `Controls.axaml` became an aggregation entry, loading submodules via `StyleInclude` and preserving the original load order.
    - Added `Controls.Core.axaml`, `Controls.Tab.axaml`, `Controls.Form.axaml`, `Controls.Panel.axaml`, `Controls.Scroll.axaml`, `Controls.PadInfo.axaml`.
    - All style resource keys unchanged, avoiding UI behavior and token contract drift.

- [x] **Q3.2 Componentize `RightWorkflowPanel.axaml`**
  - Hotspot: `src/FreeformHelper.UI/Views/RightWorkflowPanel.axaml` (~1239 lines).
  - Direction: split Step1~Step5 blocks into user controls (move UI only, behavior unchanged).
  - Done:
    - Added `src/FreeformHelper.UI/Views/WorkflowSteps/RightWorkflowStep{1..5}View.*`, splitting Step1~Step5 UI blocks into independent UserControls.
    - `RightWorkflowPanel.axaml` became the container assembly layer, keeping `Step2Block`~`Step5Block` names to preserve existing BringIntoView/navigation paths.
    - `RightWorkflowPanel.axaml.cs` unified event forwarding via `OnStepOpenSettingsRequested` from child components, removing duplicate handlers and section parsing.
    - Headless resource contract completed (Step3 `NotchLayerStateGlyph`); build/lint/application/ui-core/smoke gates all green.

- [x] **Q3.3 Componentize `SettingsWindow.axaml`**
  - Hotspot: `src/FreeformHelper.UI/Views/SettingsWindow.axaml` (~697 lines).
  - Direction: split General/Step1..Step5 form sections into controls to avoid an oversized single file.
  - Done:
    - Added `src/FreeformHelper.UI/Views/SettingsSections/Settings{General,Step1,Step2,Step3,Step4,Step5}SectionView.*`, splitting settings content into independent section controls.
    - `SettingsWindow.axaml` became the navigation + assembly layer, keeping `GeneralSection`~`Step5Section` names and preserving existing `NavigateToSection`/`BringIntoView` behavior.
    - `SettingsWindow.axaml.cs` navigation logic reused without changes; after completion, the gates (build/lint/application/ui-core/smoke) are all green.

- [x] **Q3.4 Fix HowTo copy inconsistent with current state**
  - File: `src/FreeformHelper.UI/Views/HowToUseView.axaml`
  - Problem: still has Home tab descriptions, inconsistent with the current UI.
  - Done:
    - Home-related descriptions removed and updated to current tab copy.

## P4 (test restructuring and workflow optimization)
- [x] **Q4.1 Split oversized UI test file**
  - Hotspot: `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.cs` (~1654 lines).
  - Direction: split files by topic (selection / notch preview / settings persistence / export / workflow).
  - Done:
    - Split into `FreeformHelperViewModelTests.Basics.cs`, `FreeformHelperViewModelTests.SettingsPersistence.cs`, `FreeformHelperViewModelTests.CommandsAndUndo.cs`, `FreeformHelperViewModelTests.Helpers.cs`.
    - Kept the same `partial` test class and original test names, avoiding changes to test filtering and report paths.
    - Original `FreeformHelperViewModelTests.cs` oversized file removed; build/lint/application/ui-core/smoke gates all green.

- [x] **Q4.2 Establish a "refactor without behavior change" regression baseline**
  - Content:
    - Fixed regression script for the 3635 project (load, Step1~Step5, CSV/TXT export, selection latency sampling).
    - Runtime query baseline (`status`/`selection`/`notch`/`notch-validation`).
  - Artifacts: update baseline documents in `docs/performance` + `scripts/perf`.
  - Done:
    - Added `scripts/perf/run-3635-regression-baseline.ps1`, which runs the Step1~Step5 regression for `project_3635.json` deterministically and outputs CSV/TXT, runtime query snapshots, and selection latency stats.
    - Runtime query adds `query export-notch --format csv|txt --path <output>`, supporting non-interactive Step5 export automation.
    - Documentation completed: `docs/performance/regression-baseline-3635.md`, `docs/performance/perf-baseline-howto.md`, `docs/reference/runtime-cli-plan.md`, `scripts/README.md`.

- [x] **Q4.3 Document the test layering execution strategy**
  - Goal: fix the execution order and minimal set for each refactor, reducing the chance of regressions slipping through.
  - Aligned with:
    - `scripts/tests/run-tests.ps1 -Group application`
    - `scripts/tests/run-tests.ps1 -Group ui-core`
    - `scripts/tests/run-tests.ps1 -Group smoke`
  - Done:
    - Added `scripts/tests/run-refactor-gate.ps1`, fixing lint/build/application/ui-core/smoke as a one-command gate.
    - Added `docs/guides/refactor-test-execution-strategy.md`, clearly defining the minimal regression order and conditions for additional runs.
    - Updated the fixed test order and commands in `tests/README.md` and `scripts/README.md`.

- [x] **Q4.4 Full-project lint pass (AllFiles)**
  - Goal: `./scripts/tests/lint.ps1 -AllFiles` passes reliably on the current branch.
  - Verification:
    - `./scripts/tests/lint.ps1 -AllFiles`
  - Done:
    - Ran `./scripts/tests/lint.ps1 -AllFiles` on the `master` / `beta-0.1` baseline, exit code=0.

## Fixed Gates (each milestone)
- [x] Build gate: `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`
- [x] Lint gate: `./scripts/tests/lint.ps1`
- [x] Test gate: `scripts/tests/run-tests.ps1 -Group application`
- [x] Test gate: `scripts/tests/run-tests.ps1 -Group ui-core`
- [x] Test gate: `scripts/tests/run-tests.ps1 -Group smoke`
  - Latest verification: `./scripts/tests/run-refactor-gate.ps1` (2026-02-26).
