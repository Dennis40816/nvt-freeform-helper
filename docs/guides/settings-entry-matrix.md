# Settings Entry Matrix (M11 Phase 1~3)
Last updated: 2026-08-08

Current documentation baseline: FreeformHelper g1 current-state audit, production evidence commit `ef08945`. This document deliberately separates "editors that currently exist" from the "1.0.x normal-flow contract" to avoid mistaking the full persistence schema for a list of normal-flow UI fields.

## S11.18 stale-check (2026-03-10)
1. The `DXF edits > Hidden / Combined / Moved` summary badge on the left specifically opens `DxfEditChangeListWindow`, a modal view entry point for DXF edit project state, not a settings fork.
2. `Open DXF` / `Load Project` share the `RunWithCadLoadCanvasOverlayAsync(...) -> IsCadLoadCanvasOverlayVisible -> FreeformHelperView` inline spinner overlay; this is a fixed workflow contract, not a settings entry point.
3. The CAD load overlay must not fork into another top-level loading window or appear in Display popup / SettingsWindow / Right panel.

## S11.72 stale-check (2026-03-22)
1. The `SeeRegular.csv` regular visibility mask is not automatically searched for in adjacent directories during `Load Project`; only the path/toggle explicitly saved in the project snapshot is restored, and embedded projects prefer the embedded mask.
2. The single user entry point for the regular visibility mask is fixed at `RightWorkflowPanel > Step 1 · Geometry match`:
   - `Import Regular Visibility Mask (SeeRegular.csv)`
   - `Use Regular Visibility Mask`
   - `Clear mask`
3. This path is project-restored workflow state: `ProjectUiSnapshot.Import` saves the mask source path/toggle, and embedded projects can save the mask payload; it is outside the app-general whitelist.
4. `Step 4` diagnostics reads the current session mask directly; `Simulation` builds its active surface from `regular grid ∩ active mask`, while still using only the notch table generated through the shared path for compensation.
5. The mask therefore affects both upstream assignment and the Simulation active surface; do not describe it as taking effect only indirectly through the notch table.

## S11.147 stale-check (2026-04-23)
1. The single project source for Step5 notch version selection is `Project.Settings.Notch.EnabledVersions`.
2. `UiNotchSnapshot.EnabledVersions` is only a deserialization compatibility field for old project JSON and is no longer written by the current `BuildUiSnapshot()`.
3. Load/Save must not infer `EnableV21/EnableV22` from the UI snapshot, to prevent a second source alongside `Settings.Notch.EnabledVersions`.

## S10.10 stale-check (2026-03-08)
1. The `DXF Settings` panel on the left now provides frequent entry points for the DXF edit session, beyond layer visibility.
2. The `DXF edits > Hidden / Combined / Moved` summary badge opens the `DXF edit details` modal, supporting per-item focus and restoration.
3. `Layers > On / Off` is aggregate state + batch action:
   - `On` = all layers are enabled
   - `Off` = all layers are disabled
4. `Export Notch Rows` hide / restore is not a setting, but is a fixed workflow contract:
   - restore entry point: `Restore` at the top right of the workspace
   - shortcut: `Ctrl+Shift+E`

## S4.6 stale-check (2026-03-05)
1. This refactor iteration only splits structure (selection/notch services into partials), without changing settings entry roles or the single source.
2. The app-level settings deferred policy remains unchanged:
   - Defer after `Load Project`: `MarkProjectLoadedForAppGeneralPersistence()` (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`)
   - Flush after successful `Save Project`: `FlushDeferredAppGeneralSettingsIfNeeded()` (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`)
3. The `Ctrl+S -> SaveProjectAsync() -> top toast` contract remains unchanged: `src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`.
4. The runtime query single-entry (IPC -> `RuntimeQueryUseCase.ExecuteAsync`) remains unchanged: `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`.

## Purpose
- Inventory the UI entry points that currently allow settings changes (Display popup / SettingsWindow / Right panel).
- Explicitly mark the single source to avoid rule drift for the same setting across multiple entry points.
- Serve as the input baseline for subsequent `M11` convergence (Phase 2~4).

## Entry point roles (current state)
| Entry point | Role | Application method |
| --- | --- | --- |
| `WorkspaceHeader` Display popup | Frequent, immediate visual adjustments | Direct TwoWay binding to `FreeformHelperViewModel`, effective immediately |
| `SettingsWindow` | Required operator-facing settings (General + Step1~5); not equivalent to a full persistence schema editor | Draft model (`SettingsWindowViewModel`), applied only on `Save` |
| `RightWorkflowPanel` | Workflow actions + General shortcuts + settings navigation | The General section keeps common fields directly editable; Step sections keep shortcuts and deep links |
| `Left DXF panel` | DXF import/check/edit, layer visibility, DXF edit session maintenance | Direct binding to VM commands/state, effective immediately |

## Currently fixed contracts (2026-03-04)
1. After `Load Project`, app-level general settings enter deferred mode without immediately overwriting user app settings.
2. App-level general settings flush only after the next successful `Save Project`.
3. `Ctrl+S` triggers `SaveProjectAsync()` and displays the result in a top toast in `MainWindow`.
4. The current M11 state still places most fields in `SettingsWindow`; the 1.0.x normative target keeps only normal operator decisions, excluding advanced/diagnostic and internal/compatibility fields from the numbered flow while preserving lossless project roundtrip.
5. The validation workflow is consistently named Step6 (`Step 6 - Validation Quick Trace`), replacing the old Step5 wording.

## Single Source (current state)
1. `FreeformHelperViewModel` is live UI projection/orchestration, not the durable owner of all settings.
2. `SettingsWindow` path: `CreateSettingsWindowViewModel` -> `SettingsWindowViewModel` (draft) -> `ApplySettingsWindowDraft`.
3. `RightWorkflowPanel` deep-link path: `OpenSettingsRequested` -> `FreeformHelperView.OpenSettingsWindowCore(section)` -> `SettingsWindow.NavigateToSection(section)`.
4. `Display popup` path: `src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml` binds directly to VM properties.

## 1.0.x normal-flow classification (normative)

| Classification | Allowed in numbered flow | Examples | Persistence rules |
| --- | --- | --- | --- |
| Flow-required | Yes | Grid/AA/source, Step2 detect, Step3 review, Step5 output target/profile | Applied through the normal editor, with full project roundtrip |
| Advanced/diagnostic | No; only in unnumbered Diagnostics/dedicated workspaces | Coordinate calibration, Step4 mapping analyze/report, trace/score tuning | Editable or read-only as determined by the diagnostic surface; schema/default/consumer retained |
| Internal/compatibility | No editor | legacy aliases, internal limits, retired matching fields | Lossless or explicitly defined canonicalized roundtrip; hiding UI does not delete fields |

Firm decision: `CoordinatePixelWidth/Height`, `MappingWeight*`, and candidate/confidence/ambiguous thresholds are all outside the normal operator flow. The g1 production UI has not completed all moves; the table below accurately lists legacy editors as the "current state". R13.305 only removes/separates entry points, without changing defaults, consumers, or the persistence schema.

## Settings entry matrix (current-state inventory)
| Settings group | Main properties (single source) | Display popup | SettingsWindow | Right panel / diagnostic workspace | Notes |
| --- | --- | --- | --- | --- | --- |
| Display toggles | `ShowCad`/`ShowRegular`/`HighlightUnmatched`/`HighlightFreeform`/`ColorCadByArea` | Yes | No | No | `Display` is the frequent-use entry point |
| CAD/Regular visual styles | `CadLineWidth`/`RegularLineWidth`/`CadLineColorHex`/`RegularLineColorHex`/`RegularSelectedColorHex`/`CadFillOpacity`/`CadLineOpacity`/`RegularFillOpacity`/`RegularLineOpacity`/`RegularSelectedFillOpacity` | Yes | Partial (currently only `HighlightStrokeWidthAdjust`) | No | `Display` remains the main style entry point |
| Highlight line width | `HighlightStrokeWidthAdjust` | Yes | Yes | No | The missing Settings field has been restored |
| Grid / AA / source / alignment | `CascadeNum`/`XChannels`/`YChannels`/`GridPaddingPercent`/`SelectedScanOrderOption`/`SelectedRegularSourceModeOption`/`SelectedRegularSourceLayerOption`/`ActiveAreaWidth`/`ActiveAreaHeight`/`SelectedGridAlignmentOption`/`PanelBiasX`/`PanelBiasY` | No | Yes | Yes (TwoWay) | Right panel keeps editable shortcuts; shares a single source with SettingsWindow |
| Import / behavior toggles | `ImportOnlyClosedPolylines`/`ImportBlockPolylines`/`RecalcBoundsOnLayerFilter`/`ApplyAppVisualPreferencesOnProjectLoad` | No | Yes | No | Full entry point in General |
| Coordinate calibration | `CoordinatePixelWidth`/`CoordinatePixelHeight` | No | Yes (current legacy entry) | Coordinate workspace has a separate diagnostic editor | Normal-flow = No; R13.305 removes the General editor, retaining the dedicated calibration surface, project/app-view compatibility values, and consumers |
| Step1 parameters | `MatchThreshold` | No | Yes | Summary + Open Settings | No direct editing in Right panel |
| Step1 regular visibility mask workflow | `RegularVisibilityMaskSummary` / `IsRegularVisibilityMaskEnabled` (session state) | No | No | Yes | `Import Regular Visibility Mask (SeeRegular.csv)` / `Use Regular Visibility Mask` / `Clear mask`; no search for adjacent files, but restoration from project snapshot/embedded mask is supported |
| Step2 parameters | `FreeformAxisThreshold`/`EnableAutoDetectXy`/`EnableFreeformEdgeSpecialization`/`AutoReplayStep2AfterProjectLoad` | No | Yes | Summary + Open Settings | Right panel retains execution and override actions |
| Step3 parameters | `EnableToRegular`/`EnableToFull`/`EnableToFullRuleEngine`/`EnableToFullRuleTrace`/`ToFullStrictOverlapPercent`/`EnableBoundaryVirtualAreaCap`/`BoundaryVirtualAreaCapPercent`/`EnableTargetCoverageGuard`/`TargetCoverageCapPercent`/`ShowNotchCanvasPreview`/`ShowNotchToRegularLabels`/`NotchPreviewVisualizationStep`/`NotchPreviewAutoPlayEnabled`/`NotchPreviewAutoPlayIntervalMs` | No | Yes | Partial (guard/caps, stage/autoplay shortcuts) | Shortcuts and Settings draft Save share the settings plan; boundary cap is part of Step3 computation invalidation, while target guard/cap invalidates only the final Step5 projection and preserves Step3/export batch |
| Step4 parameters | `MappingWeight*`/`MappingCandidateNumber`/`MappingLowConfidenceThreshold`/`MappingAmbiguousMargin`/`SelectedCadOutputFwDiffAutoModeOption`/`CadOutputFwDiffIndexStart`/`CadOutputFwDiffIndexAnchorCadId`/`ShowDiffIndexOverlay` | No | Yes (current legacy entry) | Summary + Open Settings | Normal-flow = No (scoring); R13.305 moves Analyze/report/necessary overrides to unnumbered Diagnostics. Draft Save currently only marks unsaved without immediately invalidating Step4; synchronization/invalidation happens only on actual Analyze (R13.302b debt) |
| Step5 parameters | `EnableV21`/`EnableV22(v2.2)`/`SelectedNotchExportFileTypeOption`/`SelectedNotchExportProfileOption`/`NullValue`/`NotchThresholdQ7`/`NotchThresholdPercent`/`LinkNotchThresholds` (`LenScale` is internal legacy, hidden from UI) | No | Yes | Summary + Open Settings | Right panel retains Export actions |
| Step6 validation | `NotchValidationRegularPadId` | No | No | Yes | A workflow action, outside the scope of settings convergence |
| DXF edit session | `HiddenCadPadIds`/`DxfCombinedCadGroups`/`DxfCadLayerOverrides` (project state) | No | No | No | `DXF edits` actions on the left; the summary badge opens `DxfEditChangeListWindow` |
| CAD load overlay workflow | `IsCadLoadCanvasOverlayVisible` (transient UI state) | No | No | No | `Open DXF` / `Load Project` share `RunWithCadLoadCanvasOverlayAsync(...)`; `FreeformHelperView` displays only an inline centered spinner |
| Layer visibility aggregate | `LayerToggles` | No | No | No | `Layers On / Off` on the left is aggregate state + batch action |

## Persistence hierarchy (comparison)
- Project (full snapshot): `ProjectSettings + ProjectUiSnapshot` (save/load round-trip).
- Project (DXF edit session): `HiddenCadPadIds + DxfCombinedCadGroups + DxfCadLayerOverrides`.
- App General (whitelist): `UiViewSnapshot + UiImportSnapshot + Behavior.ApplyVisualPreferencesOnProjectLoad`.
- Coordinate pixel X/Y currently exists in both project `UiViewSnapshot` and the app-general view clone; when the app visual preference overlay is enabled, app-general values can override the project view. Whether calibration remains a visual preference is decided by R13.301/R13.305; this document does not silently change the current precedence.
- Step4 scoring is stored only in `ProjectSettings.IndexMapping`; legacy `CandidatePaddingCells` is canonicalized to `CandidateNumber`, and the analyzer still consumes the remaining weights/thresholds/internal limits.
- App General write timing: deferred after `Load Project`; flushed to app settings only after the next successful `Save Project`.
- Default: program defaults (when neither of the first two levels applies).

## Current flow and target flow

- The g1 current UI still presents `Step1 -> Step2 -> Step3 -> Step4 -> Step5`, and Step6 validation also appears in a numbered settings tab; this is registered presentation debt, not an indication that Step4/6 is an export prerequisite.
- The R13.305 target is `Step1 -> Step2 -> Step3 -> Step5`; Step4 mapping diagnostics and Step6 validation move to unnumbered Diagnostics/Inspector, where they can still be run manually and reports can be read.
- Before moving entry points, R13.301/R13.302 must establish draft apply, dirty-field handling, and typed invalidation to avoid changing settings side effects while hiding editors.

## M11 Phase 2 convergence results
1. `RightWorkflowPanel > General Settings` keeps editable shortcut fields (Grid / AA / source / alignment).
2. `Display popup` is for frequent, immediate visual items; M11 treated `SettingsWindow` as the full entry point at the time, but that conclusion has been superseded by the R13.305 operator-facing target.
3. Step3 keeps workflow shortcuts (stage/autoplay) in the right panel; fields that are not normal manual decisions move to advanced/diagnostic or remain only in persistence, with "the entire schema is editable" no longer the goal.
4. Phase 3 added UI guard + ViewModel draft/apply tests to lock in entry contracts (including the main Right panel General TwoWay fields).
