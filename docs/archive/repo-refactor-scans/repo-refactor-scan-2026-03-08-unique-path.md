# Repo Refactor Scan

Generated: 2026-03-08 23:16:38
Branch: feature/draw-pipeline-rewrite

## File counts
- tracked files: 562
- src: 421
- tests: 74
- docs: 36
- scripts: 19

## Line count hotspots
- docs\archive\TODO-history-2026-03-05.md: 1131
- src\FreeformHelper.UI\Styles\Controls.Core.axaml: 1075
- src\FreeformHelper.UI\Views\NotchExportSelectionWindow.axaml: 716
- tests\FreeformHelper.Tests\UI\ViewModels\NotchExportSelectionViewModelTests.cs: 702
- src\FreeformHelper.UI\ViewModels\NotchExportSelectionViewModel.cs: 646
- src\FreeformHelper.UI\Styles\Tokens.axaml: 613
- tests\FreeformHelper.Tests\Application\Notch\NotchTableGeneratorTests.cs: 602
- src\FreeformHelper.UI\Styles\Controls.Form.axaml: 596
- src\FreeformHelper.UI\ViewModels\NotchExportSelectionViewModel.Selection.cs: 560
- tests\FreeformHelper.Tests\UI\ViewModels\FreeformHelperViewModelTests.CommandsAndUndo.cs: 542
- src\FreeformHelper.UI\Views\DevSections\DevInspectorPrototypeSectionView.axaml: 541
- src\FreeformHelper.UI\Controls\WorkspaceHeader.axaml: 512
- src\FreeformHelper.UI\Controls\PadCanvas.Spatial.cs: 503
- src\FreeformHelper.UI\Controls\PadCanvas.Input.Selection.cs: 479
- docs\reference\behavior-inventory.md: 476
- tests\FreeformHelper.Tests\UI\Snapshots\UiLayoutGuardTests.cs: 473
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.PadInspectorSummary.cs: 473
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Operations.cs: 469
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Settings.PropertyCallbacks.cs: 466
- src\FreeformHelper.UI\ViewModels\SettingsWindowViewModel.cs: 454
- src\FreeformHelper.UI\Controls\PadCanvas.Properties.cs: 443
- src\FreeformHelper.UI\ViewModels\FreeformHelperViewModel.Persistence.Project.cs: 439
- src\FreeformHelper.UI\Views\DevSections\DevUiLabsSectionView.axaml: 435
- src\FreeformHelper.UI\Views\FreeformHelperView.Canvas.cs: 428
- tests\FreeformHelper.Tests\Application\Project\ProjectPersistenceUseCaseTests.cs: 426

## Analyzer / gate summary
- `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj /p:UseAppHost=false`: pass (`0 warning / 0 error`)
- `./scripts/tests/lint.ps1 -UseNoAppHost`: pass (`0 warning / 0 error`)
- `repo_scan.ps1`: completed on `2026-03-08`; first attempt failed because a running `FreeformHelper.UI` process locked build outputs, second attempt succeeded after closing the process.

## Unique-path audit summary
| Feature | Status | Notes |
| --- | --- | --- |
| Notch 2.2 final result (`ToFull enabled` / `Stage3Area` / `ToFullRatio` / `CombinedRatio` / overlay / target allocation) | P0 multi-path risk | Same result is re-derived in Application, UI ViewModel, RuntimeQuery, and detail overlay paths. |
| Notch display projection (`PadInspector` / `PadInfo` / `NotchDetailViewModel`) | P1 multi-path risk | Same business conclusion is formatted in multiple ViewModels/helpers, likely to drift. |
| Export notch rows filter visibility | OK single-path | Multiple UI controls converge to `IsRowVisible(...)`; this is multi-entry, not multi-path. |
| Project save/load DXF edit state | OK single-path | Save/load now converges through `CaptureDxfEditProjectState(...)` / `ApplyCapturedDxfEditProjectState(...)` with regression coverage. |

## Detailed findings

### F1. Notch 2.2 final result is split across multiple derivation paths
Evidence:
- `src/FreeformHelper.Application/Services/NotchV22CompensationService.Stages.cs`
- `src/FreeformHelper.Application/Services/NotchV22TargetAllocationService.cs`
- `src/FreeformHelper.UI/Services/NotchDetailUseCase.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.Compensation.Diagnostics.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspector.cs`
- `src/FreeformHelper.UI/Services/RuntimeQueryUseCase.NotchDetails.cs`

Symptoms:
- `CAD 4826` reproduces a direct contradiction:
  - `query notch --cad-id 4826` reports `toFullAppliedCount=1`
  - Stage2/Stage3 polygons exist
  - target allocation includes boundary expand
  - but `ToFullRatio=1.0` / `Stage3Area=cadArea`
- `ResolveCadToFullEnabled(...)` is currently re-derived in UI instead of being read from one final result model.
- Stage2 candidate polygons are re-built in UI from `RegularDebugInfos`, instead of being read from a final stage result.

Risk:
- Overlay, inspector, runtime query, export, and ratio text can disagree.
- Fixing one path will not automatically fix the others.

Required direction:
- Application layer must expose one final Notch 2.2 result model.
- UI / RuntimeQuery / export / inspector may only read/project that model.

### F2. Notch presentation/projection is duplicated across UI surfaces
Evidence:
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.PadInspectorSummary.cs`
- `src/FreeformHelper.UI/ViewModels/PadInfo/CadPadInfoViewModel.Formatting.cs`
- `src/FreeformHelper.UI/ViewModels/PadInfo/CadPadInfoViewModel.Construction.cs`
- `src/FreeformHelper.UI/ViewModels/NotchDetailViewModel.cs`

Current duplication:
- `Combined ratio` text is formatted in at least:
  - `BuildCombinedRatioSummary(...)`
  - `BuildCombinedRatioText(...)`
  - direct interpolation in `CadPadInfoViewModel.Construction`
  - `NotchDetailViewModel`
- `To Full reason` / diagnostics are also projected differently between inspector and pad info.

Risk:
- UI wording and warning thresholds can drift.
- One panel may show a stale/rounded/shortened interpretation that no longer matches another panel.

Required direction:
- Add a shared projection/formatter layer for Notch display text and chip summary.
- Panels should read the same projected object, not rebuild strings separately.

## Healthy single-path examples (do not "fix" these)

### H1. Export notch rows filter visibility is already single-path
Evidence:
- `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.Selection.cs:168`
- `src/FreeformHelper.UI/ViewModels/NotchExportSelectionViewModel.cs:183`
- `tests/FreeformHelper.Tests/UI/ViewModels/NotchExportSelectionViewModelTests.cs:177`
- `tests/FreeformHelper.Tests/UI/ViewModels/NotchExportSelectionViewModelTests.cs:200`

Reason:
- Search box, selected-only chip, rows-shown mode, and header column filters are multiple inputs.
- But final visibility still converges to one predicate path: `IsRowVisible(...)`.
- This is acceptable multi-entry / single-path design.

### H2. Project DXF edit save/load is already single-path
Evidence:
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Persistence.cs:14`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfEditing.Persistence.cs:116`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs:34`
- `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs:245`
- `tests/FreeformHelper.Tests/UI/ViewModels/FreeformHelperViewModelTests.SettingsPersistence.ProjectSaveLoad.cs:346`

Reason:
- DXF edit state now persists through `CaptureDxfEditProjectState(...)` and replays through `ApplyCapturedDxfEditProjectState(...)`.
- Multiple UI operations may mutate the edit state, but project save/load itself is no longer split across unrelated paths.

## Clear execution plan
1. `S11.8`
   - Fix `CAD 4826`
   - Extract one Application-layer final result model for Notch 2.2
   - Remove UI-side re-derivation of `ToFull enabled`, Stage2 candidate polygons, and Stage3 area semantics
2. `S11.9`
   - Move `PadInspector` / `PadInfo` / `Notch detail` / `RuntimeQuery` to shared projection objects
   - Keep multiple readers, eliminate multiple business-formatting paths
3. `S11.10`
   - Add regression guard:
     - same input -> same `ToFull enabled`
     - same `Stage3Area`
     - same `ToFullRatio`
     - same `CombinedRatio`
     - same target allocation / overlay semantics across readers
