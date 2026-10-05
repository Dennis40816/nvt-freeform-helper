# Behavior inventory
Last updated: 2026-08-10

Current document baseline: FreeformHelper 1.3.0 current-state audit, production evidence commit `ef08945`. Historical stale-checks preserve decision context; the R13.005 calibration sections represent the current owner/result/reader state.

## S10.10 stale-check（2026-03-08）
- DXF edit behavior has been extended to the full project state:
  - `Save Project` saves the `Hidden / Combined / Layer move` state of DXF edits.
  - `Load Project` replays the same DXF edit state, rather than restoring only hidden.
- Left-side `DXF edits` contract:
  - The `Hidden / Combined / Moved` summary badges are clickable entries that open the `DXF edit details` modal.
  - `Reset all DXF edits` resets all three types of changes: hidden, combined, and layer move, rather than restoring only hidden.
  - `Remove combined` clears only the currently selected combined group, not all groups.
- `DXF edit details` modal contract:
  - Displays currently modified CAD pads in tabs by `Hidden / Combined / Moved` type.
  - Each row can `Focus` on AA/canvas and can also restore an individual change:
    - `Hidden` → restore hidden
    - `Combined` → clear combine group
    - `Moved` → restore original layer
- `Layers On / Off` contract:
  - This is an aggregate state display + batch action, not a single layer toggle.
  - `On` is considered active only when all layers are visible.
  - `Off` is considered active only when all layers are hidden.
- `Export Notch Rows` hide / restore contract:
  - The panel can temporarily `Hide panel (inspect AA)`, while AA remains usable in the main view.
  - The restore methods are fixed:
    - `Restore` at the top right of the workspace
    - `Ctrl+Shift+E`
  - A top toast shows the restore methods when the panel is hidden.

## S11.18 stale-check（2026-03-10）
- The concrete UI host for `DXF edit details` is fixed as `DxfEditChangeListWindow`:
  - The only user entry is the left-side `DXF edits > Hidden / Combined / Moved` summary badges.
  - The window only projects the existing DXF edit project state and handles restore/focus actions; it should not contain another set of DXF edit logic.
- The `CAD load overlay` contract is fixed to a single path:
  - The only user-visible entries are `Open DXF` and `Load Project`.
  - Both workflows must be wrapped in a `RunWithCadLoadCanvasOverlayAsync(...)` scope, allowing the UI to render one frame before DXF load/apply/rebuild.
  - Display state depends only on `IsCadLoadCanvasOverlayVisible`; nested scopes must keep the overlay until the outermost scope ends.
  - The presentation container is fixed as the transparent top-level `CadLoadSpinnerWindow` launched by `CadLoadSpinnerProcessHost` (a separate helper process); the main window only sends show/hide through `IsCadLoadCanvasOverlayVisible`.
  - The spinner visibility guard must cover "overlay visible + spinner dash offset continuously changing + window hidden after the scope ends".

## S11.72 stale-check（2026-03-22）
- The `SeeRegular.csv` regular visibility mask contract now requires explicit import in `Step 1 · Geometry match`:
  - The entries are fixed:
    - `Import Regular Visibility Mask (SeeRegular.csv)`
    - `Use Regular Visibility Mask`
    - `Clear mask`
  - After `Load Project`, `SeeRegular.csv` is no longer automatically searched for or loaded from the same folder; only the mask path / embedded mask explicitly saved in the project snapshot can be restored.
- The regular visibility mask state is project-restored workflow state:
  - `Load Project` can restore the mask path / toggle in the project snapshot; an embed project prefers the embedded SeeRegular mask, which can still be restored when the path is invalid.
  - After `Open DXF` succeeds, it clears the previously manually selected mask path and loaded result.
  - `Step 4` reads the current workflow snapshot mask directly; Notch generation consumes the CAD Output FW Diff assignment after the mask has been applied.
  - `Simulation` also uses the jointly generated notch table and intersects regular grid IDs with the active mask to form the active surface; it cannot currently be described as "Simulation only reads the table".
- `RegularVisibilityMaskSummary` contract:
  - `not loaded`: not yet manually imported
  - `loaded but disabled`: imported, but the toggle is off
  - `enabled`: imported and currently constraining `Step 4 / Simulation`
  - `incompatible / failed`: CSV dimensions or parsing failed; this is diagnostic only and should not silently fall back to enabled

## S11 draw pipeline rewrite（2026-03-08）
- The per-frame data model of `PadCanvas` has converged on three layers:
  - `PadCanvasViewFrameSnapshot`
    - Single source: zoom / pan / bounds size / world viewport / world-to-screen transform.
  - `visible draw cache`
    - query cache: affected only by viewport and CAD/Regular data revision.
    - draw-list cache: affected only by query revision, selection/highlight/resource, and decimation conditions.
  - `PadCanvasRenderFrame`
    - Resolves `showRegular / showCad / lowDetail / secondaryVisualsDeferred / opacity / outline width` at the start of each frame's render.
- invalidation / refresh contract:
  - `RequestViewRefresh(...)` and `RequestVisualRefresh(...)` share a coalesced queue.
  - `ViewChanged` is raised only on paths that require it; visual-only changes should not synchronously trigger `ViewChanged`.
  - selection/highlight/resource changes invalidate only the draw-list cache and do not query viewport candidates again.
  - Only CAD/Regular data or viewport changes invalidate the query cache.
- navigation hot path contract:
  - Brief `transient low-detail` is allowed during zoom / pan / fit / focus.
  - `diff index overlay`、`notch preview overlays`、`match/notch ratio labels`、`axis labels`、`hover debug overlay`
    - Drawing may be deferred during the active navigation window.
    - A coalesced refresh draws them after navigation ends; the final result must be consistent.
  - `selection overlay`, box-selection rectangle, and core CAD/Regular geometry remain on the main drawing hot path.

## S11.8 Notch 2.2 per-CAD result path (R13.005 calibration)
- `NotchV22CompensationResult` is the primary result of the current UI/RuntimeQuery per-CAD numeric kernel:
  - `ToRegularRatio`
  - `ToFullRatio`
  - `CombinedRatio`
  - `IsToFullEnabled`
  - `Stage3Area`
- `IsToFullEnabled` contract:
  - Depends only on `RegularDebugInfos.Any(info => info.IsToFullApplied)`.
  - `IsToFullBoundaryCandidate` must no longer be used as a substitute.
- `Stage3Area` / `ToFullRatio` contract:
  - `Stage3Area = cadArea + Σ(max(0, ReachableArea - OverlapArea))`, counting only regulars with `IsToFullApplied`.
  - `ToFullRatio = max(1.0, Stage3Area / cadArea)`。
  - `CombinedRatio = ToRegularRatio * ToFullRatio` is a CAD-level diagnostic; the v2.2 row payload must not use it directly as a source-wide gain.
- `Target allocation` contract:
  - `NotchV22TargetAllocationService` must read `compensation.Stage3Area` and must not recalculate it through a path other than `cadArea * ToFullRatio` on its own.
  - `Current (Gain)` must use per-target Stage3 coverage: `stage3EffectiveAreaOnTarget / targetRegularArea`.
  - `Conservative (No Gain)` must use per-target source overlap coverage: `overlapAreaOnTarget / targetRegularArea`; ToFull does not directly increase the target amount.
  - `ToRegularRatio = Σ(overlapArea / targetRegularArea)` is retained only as a CAD-level diagnostic; the whole-CAD `R` must no longer be aggregated first and then multiplied into each target share.
- `Per-IC CAD allocation pool` contract:
  - ordinary admission must use the Q7-positive predicate shared by `NotchAllocationService` and `BuildAllocations`; raw overlap quantized to Q7=0 does not constitute ordinary membership in that IC.
  - An IC mismatch must be excluded before geometry intersection; the membership query may short-circuit at the first positive Q7 allocation without building complete allocations/candidates.
  - A cross-IC CAD may enter every IC pool where it actually has a positive Q7 allocation and must not be excluded based only on its primary IC assignment. The existing empty-pool all-visible and selected-target inclusion fallbacks remain; the ordinary predicate must not be miswritten as an unconditional `iff`.
  - The actual pool is sorted by CAD ID; the pool component of cache identity uses ordered IDs and includes the count, and a same-count member swap must invalidate it.
  - For the A `[0,5]`, B `[5,15]` fixture, UI `ToRegular / ToFull / Combined` was deliberately corrected from `0.5 / 2 / 1` on the old primary-IC-only path to `0.5 / 1 / 0.5`, matching the generator.
- `Resolved result` contract:
  - `NotchV22ResolvedResultService` is the per-CAD projection entry for UI/query; it is not the repository-wide export source-of-truth.
  - `PadInspector`, `Notch preview`, `Notch detail`, and `RuntimeQuery query notch` read only the resolved result and must not independently rebuild `enabled / stage2 / stage3 / target allocation`. Detail's raw overlap allocation/Q7/freeform remains the existing view projection and is outside the target-allocation resolved model.
  - current boundary: normal `CadAllocation` export already has a separate Application-owned output-request-neutral candidate batch; matching export does not rebuild candidates because of versions, threshold, `NullValue`, or target guard/cap. Phases 1～3 resolution and phase 4 final projection are separated; concurrent Export/Simulation with the same CAD/grid/Step3/computation identity and generation epoch share one in-flight resolution task, while each caller still projects with the final request frozen at its own entry. A fingerprint hit still performs full computation-settings equality; a fault removes the task for retry; final-only changes retain the task/batch, while full invalidation advances the epoch, detaches the old task, and rejects its store/publish. Live UI guard/cap-only changes also retain Step3 revision, per-CAD sparse identity, and this batch, clearing only the current Step5 table/validation and notifying Simulation source revision; only a full Step5 clear evicts the batch and resets operation state. A generation request freezes cache generation epoch, Step5 final-projection revision, and Simulation source revision; completion may commit row count, last table, progress/flow, and caller continuation only when all three are still current, and the Simulation session also retains the accepted source revision from that request. The V22 C exporter and simulation also share final node normalization from `NotchV22FirmwareProjector`; a batch request can reuse the exact resolved instance for the current single selected CAD, or return at most one identity-bound result after cold resolution and promote it back to the existing per-CAD owner after validating currentness. The batch still stores only compact candidate evidence, not polygons/debug results for all CADs. R13.101e makes normal generator/UI compensation build a complete `NotchV22CompensationContext` first; allocation/boundary/active/strict-query/policy evidence is no longer assembled from partial nullable values by the caller to control stage order; the old public API acts only as an adapter, explicitly empty allocations do not fall back to geometry, and final-output state still does not enter the context. `LegacyRegularAnchor` now fixes versions/threshold/`NullValue`/`LenScale` for the same generation through an owned request and uses the same explicit V21/V22 switch for generation and eligibility; the unused strategy registry/injection seam has been removed, but the request-specific table cache remains. R13.101c-2/R13.102/102a-2 and R13.103 remain open. Public compatibility `NotchDetailUseCase.Build` can still build an unanchored result, but the normal command no longer uses it.

## S11.9 Notch 2.2 shared display projection（2026-03-09）
- `NotchDisplayProjector` is the single path for Notch 2.2 display strings:
  - normal anchored target-coverage combined ratio/overflow only formats `NotchV22TargetCoverageProjection`; target summary/compact line/card role consume only the `(IcIndex, DiffIndex)` membership of `EmittedTargets`; target admission, per-group rounding, and risk must not be recalculated in the UI.
  - `NotchV22TargetAllocationPolicy` owns the old all-target ratio fallback for unanchored compatibility and non-target-coverage modes; when `RawCombinedPercent == null`, target summary/line/role retain the existing strict-only display, while diagnostic target rows still display all targets.
  - `ToRegularRatioText`
  - `ToFullRatioText`
  - `CombinedRatioText`
  - `ToRegularValueText`
  - `ToFullValueText`
  - `CombinedValueText`
  - `To Full reason`（short / full）
  - `Stage3AreaText`
  - `TargetAllocationSummaryText`
  - `TargetAllocationLines`
  - `OwnerSummaryText`
  - `formatted diagnostics`
- `PadInspector` contract:
  - `Reason / To Regular / Combined` must read the shared display projection and must not independently parse `diagnostics` or reconstruct ratio text.
- `PadInfo` contract:
  - The notch section of `CadPadInfoViewModel` (ratio / reason / stage3 / target summary / owner summary) must read the shared display projection.
- `Notch detail` contract:
  - `NotchDetailUseCase` must build the shared display projection first, then pass it to `NotchDetailViewModel` for presentation.
  - `NotchDetailViewModel` should not recalculate ratio text on its own.
- `RuntimeQuery` contract:
  - `query pad` / `query notch` may retain raw numeric payloads.
  - Display-oriented text output must be produced through the shared display projection rather than assembling strings independently.

## R13.005 result / reader matrix

There are currently two source-of-truth result scopes and a Simulation review projection derived from the generated table; they have not yet been combined into one repository-wide model:

| Result scope | Current owner / entry | Readers | Current second-pass debt |
| --- | --- | --- | --- |
| Per-CAD diagnostics | `NotchV22ResolvedResultService`; root VM revision/cache; combined-overflow and normal target effective membership are projected by `NotchV22TargetAllocationPolicy` | Step3 preview/Canvas, deferred Inspector, CAD PadInfo, Notch detail, RuntimeQuery `pad/notch/notch-stage/multi-owner` | normal UI readers and `query multi-owner` already consume the revisioned per-CAD result; public compatibility `NotchDetailUseCase.Build` still retains unanchored standalone behavior; `GetCrossIcOwnerSharesForRegularPad` still derives from geometry, and the display projector still parses diagnostics (R13.102/104) |
| Generated firmware artifact | `GenerateCurrentNotchTableAsync` -> `NotchExportGenerationCacheService` -> `NotchTableGenerator`; `NotchV22FirmwareProjector` owns final V22 node normalization | UI Step5 export, Runtime export adapter, Simulation workspace, Step6/Runtime validation | The normal `CadAllocation` cache stores a compact output-request-neutral candidate batch; phases 1～3 are resolved once by an identity/epoch-bound task, and concurrent full-table consumers share the task before each performs phase 4 final projection. final-only changes do not detach the task; full invalidation detaches it and rejects old stores; faults can be retried, and fingerprint collisions still reject incorrect joins through full settings identity. Generation completion also checks currentness using the captured cache epoch, final-projection revision, and source revision. The V22 exporter and simulation now share the `SourceRows`/`NodesByIc` projection; the selected sparse bridge lets the batch reuse a warm result or return at most one CAD + anchor-keyed resolved result when cold, promoting it back to the same per-CAD owner after validating currentness, without retaining complete diagnostics for the other candidates. R13.101e has consolidated the complete evidence and policy for normal generator/UI compensation into a single context/compute path; the compatibility adapter and explicit-empty correction do not change the final request, row projection, or schema. V21/V22 final projectors and Step6/Runtime validation share an Application-owned `0..65535` null-sentinel guard; an invalid sentinel fails fast in the VM/direct UseCase, actual IPC returns the exact message through the existing `IPC_ERROR` envelope, and default-sentinel C is unchanged. Legacy now shares an owned request within the same generation, and callback mutation affects only the next generation; generation/eligibility share fixed explicit V21/V22 dispatch, and the registry/injection seam has been removed. `LegacyRegularAnchor` still retains the request-specific `NotchTable` and compatibility boundary. R13.101c-2/R13.102/R13.102a-2/R13.103 remain open |
| Simulation review | `NotchApplySimulationReviewUseCase` -> `NotchApplySimulationResult` / `SimulationSafetyAuditResult`; the EMS after-cap decision is owned by `SimulationSafetyAuditService.IsEmsAfterCapViolation`, and workspace/overview/export base severity, no-cell cap text, Copper replay severity, and Notch cap/target-cap guidance are owned respectively by `SimulationSafetyTextProjector.BuildStatusText`/`FormatEmsAfterCap`/`BuildReplayStatusText`/guidance builders; overview and physical-only export summary share `BuildPhysicalAuditSummaryText` evidence | `SimulationWorkspaceUseCase`, Simulation VM/overlay/validation, `CopperPillarPathReplayArtifactService`, export selection safety badge/summary, Step 3/5 Notch guidance, Runtime Query simulation regular snapshot, cell/overview/high-risk projections | This scope is a valid projection of the generated table; the active-surface mask is an additional explicit input, not a recalculation of Notch geometry. R13.104a-1 has consolidated the EMS predicate, a-4 through a-8 have consolidated workspace/overview/replay/export status and physical evidence, a-9 makes active Step 3/5 guidance read the current overview cap together, and a-10 further makes active Step 3 target-cap help use current target/EMS caps and Settings use the current draft target/default EMS cap; both share the same template. The complete EMS-blocked dialog text retains the existing contract, the artifact row synchronizes the JSON-ignored global-flow fact, and the serialized schema is unchanged. Overview-only stale/unavailable remains at the boundary, `HasRisk` means only EMS danger, `NeedsAttention` also includes physical/stale, and `MarginToCap` serves only as numeric evidence; other hard-coded cap and cell/high-risk/export/replay display/text debt still awaits the R13.104 parent. |

Reader assessment:

- `Validation` only projects the generated `NotchTable` into DIRECT/IN/OUT trace and does not recalculate compensation, making it an acceptable single-result reader.
- `Runtime export` delegates to the same UI `ExportNotchCommand` through `RuntimeQueryUseCase`, making it an acceptable multi-entry/single-export-path.
- R13.102b-2 consolidated `query multi-owner` so that each request obtains only one revisioned `NotchV22ResolvedResult` built from the current setting or override; the visible-CAD guard prevents fallback calculation, the metadata snapshot does not build a default-threshold result, and rows/summary/rule trace are all projected from `resolved.Compensation.RegularDebugInfos`. R13.102a-2b-2 further connects the current selected result to the full batch session through a bounded bridge; the Runtime payload schema and query entry are unchanged.
- `GenerateCurrentNotchTableAsync` completion may publish only results whose epoch/projection/source identity captured at entry is still current; only current completion updates cache row count, last generated table, and the outer workflow, and the accepted Simulation session uses the captured source revision from that request rather than rereading the current revision at completion.
- CAD-level diagnostic `CombinedRatio`, firmware `CombinePercent`, and target-coverage projection remain distinct fields: normal anchored display reads the shared final-eligibility projection; unanchored/non-target compatibility display retains the Application-owned all-target fallback. Raw values must not be interchanged because of similar names.

## S4.6 stale-check（2026-03-05）
- This round was confirmed as "structural reorganization with unchanged behavior": `FreeformHelperViewModel.Selection*` and `NotchV22CompensationService*` have been split into partials, but their external contracts are unchanged.
- The deferred app settings contract still holds:
  - Enter deferred mode after `Load Project`: `MarkProjectLoadedForAppGeneralPersistence()` (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`)
  - Flush only after `Save Project` succeeds: `FlushDeferredAppGeneralSettingsIfNeeded()` (`src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs` / `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.AppSettings.cs`)
- The `Ctrl+S` contract still holds:
  - shortcut handler：`src/FreeformHelper.UI/Views/FreeformHelperView.InputAndShortcuts.cs`
  - Calls `SaveProjectAsync()` and displays a top toast: `SaveProjectFromShortcutAsync(...)` (same file)
- The runtime query single-entry contract still holds:
  - The IPC server entry still goes through `RuntimeQueryUseCase.ExecuteAsync(...)`: `src/FreeformHelper.UI/Services/RuntimeQueryIpc.cs`

Marker descriptions:
- [Rebuild] Triggers TriggerGridRebuildAsync / RebuildGridAsync
- [View] Triggers FitToContent / ZoomAt / pan changes
- [Clear selection] Clears the current selection
- [Set selection] Sets selection programmatically
- [Undo] Pushes or performs undo
- [Status] Updates StatusText or console/log display

Notes:
- RebuildGridAsync clears selection unless SuppressSelectionClearOnRebuild is true.
- RebuildGridAsync updates StatusText and may call FitToContent depending on requestFit/initial load.

## Completed feature baseline (2026-02)

### Import, save, and project consistency
- Save/Load has converged on the ProjectDocument/Snapshot path; UI settings roundtrip does not require manual mapping page by page.
- Build output and log directories have been centralized under `build/`, reducing pollution in `src/`.
- Schema migration is append-only (retaining old fields without breaking old project files).
- After `Load Project`, app-level settings switch to deferred persistence (app settings are not written immediately).
- Deferred app settings are flushed together after the next successful `Save Project`.
- DXF edit state is also within the project roundtrip scope: `hidden / combined / layer move` is restored through project save/load.
- `Open DXF` / `Load Project` share the CAD load overlay scope; the visual presentation is a separate spinner helper window (transparent top-level + centered spinner).

### DXF and Grid
- Regular grid now supports creation from a specified bound layer, independently of layer visibility.
- Layer filtering, DXF edit (hide/restore), and rebuild are connected; visible/all DXF can be exported.
- The left-side `Layers On / Off` has converged on aggregate state + batch action; the active state means "all visible" or "all hidden".
- The left-side `DXF edits` summary badges open `DxfEditChangeListWindow` (the `DXF edit details` modal), supporting individual focus / restore / clear group / restore layer.
- Grid channel limits have been removed (X<=255 / IC<=16 are no longer imposed).

### Mapping, diagnostics, and manual overrides
- CAD↔Regular mapping now has a separate diagnostics window (the summary remains on the right, while details move to the report window).
- Diagnostics now support best/second/margin and top candidates, with locate and apply/clear override.
- Overlap check is now non-blocking, with progress, highlight, selection, and a detailed report.

### Freeform and Notch
- Freeform auto-detection now supports XWay/YWay, with the option to enable XYWay auto-detection.
- The Legacy notch flow is retained; Notch mode can switch between the old and new flows (as a refactoring transition).
- AFEIndex has been retired; core keys have converged on `(IcIndex, DiffIndex)` and geometry allocation.
- Step3 overlay has converged on layer policy (`ToRegularLabel / ToFullSeed / ToFullCandidate / ToFullFinal`).
- The Step3 stage display contract is mutually exclusive:
  - `Stage 1` displays only `ToFullSeed`
  - `Stage 2` displays only `ToFullCandidate`
  - `Stage 3` displays only `ToFullFinal`
  - Previous stage overlays are no longer displayed cumulatively.
- Step3 display toggles are separated into `Show To Regular labels` and `Show To Full overlay` (affecting only AA display, not computation).
- If Step1 match results are updated and a CAD is already selected, Step3 preview refreshes immediately (without manually pressing Step3 again).
- The Step3 layer legend shows `Active/Hidden` state, and stage/autoplay controls are enabled only when the To Full overlay is visible.
- Step6 Validation quick trace is now a panel grouped by `DIRECT / IN / OUT`, using the same source data from `NotchValidationTraceService`.
- The Step3 To Full gate has been split into a rule engine (with a legacy switch), `rule trace` can be toggled, and `query notch` / `query multi-owner` can return per-regular trace.

### Step3 Recompute / Compensation trigger timing (N0.4)
- Definitions:
  - `Compensation computation`: the `NotchV22CompensationService.Compute(...)` path (the source of To Regular / To Full ratios and stage overlays).
  - `Recompute selected (Step 3)`: `ExecuteStep3PreviewWorkflow()`, which first clears Step3 downstream and then recalculates the preview for the currently selected CAD.
  - `Display toggle`: affects only canvas overlay visibility, without changing compensation algorithm results.
- Automatic triggers (without pressing Recompute):
  - Selection changes (CAD): `ApplySelection(...)` refreshes notch preview (immediate for single selection, deferred for multiple selection).
  - Step1 Match completes while a CAD is currently selected: refreshes Step3 preview immediately.
  - Step3 computation toggle/parameter changes (`EnableToRegular`, `EnableToFull`, `EnableToFullRuleEngine`, `EnableToFullRuleTrace`, `ToFullStrictOverlapPercent`):
    - invalidation cache + downstream invalidate + refresh preview (if prerequisites are met).
- Display-only effects (no recomputation):
  - `Show To Regular labels`
  - `Show To Full overlay`
  - `NotchPreviewVisualizationStep`（Stage 1/2/3）
  - `NotchPreviewAutoPlayEnabled` / interval
  - The above only trigger overlay visibility notifications and `CanvasHost.Invalidate()`.
- `RefreshNotchCanvasPreviewCommand` currently has no production XAML binding; it is used only for CLI/internal/test entry. The normal user flow has no other clickable manual Recompute button; ordinary parameter/selection changes are handled by automatic refresh.
- Main code entries:
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.WorkflowSteps.cs`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Settings.cs`
  - `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.NotchDetails.cs`

### Step5 Notch row contract and no-op row (U0.3)
- no-op row definition (v2.2):
  - `anchor_diff = D`
  - `combine = 100`
  - `target1 = nullDiff and ratio1 = 0`
  - `target2 = nullDiff and ratio2 = 0`
  - The effect is "keep the anchor diff without switching targets".
- Why it must exist:
  - Exported rows align with diff/IC: downstream receives complete anchor mapping without filling gaps itself.
  - Compatibility with existing parsers: preserves the fixed data contract, preventing downstream from misinterpreting "missing rows" as lost data.
- UI display strategy (Select notch rows):
  - The default is `Rows shown = Transfer-only`, hiding no-op rows to focus on rows with actual transfers.
  - Users can switch to `All rows` to inspect the complete output contract (including no-op rows).
  - Export content is based on the currently visible and checked rows; the default behavior reduces reading noise while retaining an entry to the complete view.

### UI structure and consistency
- The 1.3.0 current settings area still presents numbered Step1~5 and Step6 validation; Step4/6 are diagnostics but have not yet been moved out of the normal-flow surface (R13.305a debt).
- The right-side Panel is now split into `Settings / Inspector` tabs; Inspector and Step settings no longer share the same scrolling content.
- `RightWorkflowPanel > General Settings` retains editable shortcut fields (Grid/AA/source/alignment) and shares a single source with `SettingsWindow`.
- The `WorkspaceHeader` Display popup retains frequent real-time visual items; `SettingsWindow` should handle only necessary operator-facing settings, without aiming to make the entire persistence schema editable.
- Important controls now use tokens (colors, spacing, borders, shadows), with layout guard tests added.
- ScrollViewer/Scrollbar now use a unified style, reducing inconsistent interaction across pages.
- The Terminal contract is fixed:
  - `ConsolePanel` uses the single `AvaloniaEdit TextEditor` path; `TextBox fallback` is no longer allowed.
  - The AvaloniaEdit theme remains an app-level include and must not be moved back to workspace lazy-load.
  - The terminal is expanded by default, with the existing initial dimensions `ConsolePanelRowHeight=180` and `ConsolePanelMinHeight=120`.

## Commands

### FreeformHelperViewModel (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Commands.cs)
- OpenDxfCommand → OpenDxfAsync (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Operations.cs)
  - [Status] [Rebuild] [View] [Clear selection]
  - Also updates layer toggles, CadPads, RegularPads, and MarkUnsaved.
  - `CadLoadWorkflowService` internally wraps import / apply / rebuild in `RunWithCadLoadCanvasOverlayAsync(...)`.
- RebuildGridCommand → TriggerGridRebuildAsync(requestFit: true)
  - [Rebuild] [View] [Clear selection] [Status]
- MatchCommand → MatchAsync
  - [Status] (CanvasHost.Invalidate)
- AutoDetectFreeformsCommand → AutoDetectFreeformsAsync
  - [Status] [Statistics] (updates Freeform stats table, CanvasHost.Invalidate)
- SaveProjectCommand → SaveProjectAsync (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Persistence.cs)
  - [Status] (writes the project and clears HasUnsavedChanges)
- LoadProjectCommand → LoadProjectAsync
  - [Status] [Rebuild] [View] [Clear selection]
  - Reloads DXF, settings, and layers, and applies the UI snapshot.
  - If the project includes DXF reload, DXF load/apply/rebuild must also use the same canvas overlay scope.
- ExportNotchCommand → ExportNotchAsync
  - [Status]
- FitCommand → CanvasHost.FitToContent
  - [View]
- ClearSelectionCommand → CanvasHost.ClearSelection
  - [Clear selection]
- UndoCommand → Undo (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Undo.cs)
  - [Undo] [Status]
- SetFreeformNone/X/Y/XY → SetFreeformForSelection (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.Overrides.cs)
  - [Status] (CanvasHost.Invalidate, MarkUnsaved)
- UseSelectionForManualRangeCommand → ApplySelectionToManualRange (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.ManualSizing.cs)
  - [Set selection], and may trigger [Rebuild] [View] [Status] through ApplySizingToCells
- SelectAllLayersCommand / DeselectAllLayersCommand → SelectAllLayers / DeselectAllLayers
  - If RecalcBoundsOnLayerFilter=true, triggers [Rebuild] [View]
  - Calls MarkUnsaved when LayerToggle changes
- CheckDxfQualityCommand → CheckDxfQuality (src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel.DxfQuality.cs)
  - [Status]

### ShellViewModel (src/FreeformHelper.UI/ViewModels/ShellViewModel.cs)
- ShowWorkspaceCommand / ShowHowToUseCommand / ShowDevCommand
  - Switches CurrentViewModel and page visibility
- ClearConsoleCommand
  - [Status] (clears AppLogStore)
  - The terminal is expanded by default at startup; the full console text is built from the ring buffer tail without depending on UI collection flush.

### Pad Info ViewModels (src/FreeformHelper.UI/ViewModels/PadInfoViewModels.cs)
- CadPadInfoViewModel.ApplyChangesCommand → SetCadPadCustomValues
  - [Undo] [Status] (MarkUnsaved)
- CadPadInfoViewModel.DiscardChangesCommand
  - No external side effects
- RegularPadInfoViewModel.ResetPadSizeCommand → ResetSizingForCells
  - [Rebuild] [View] [Undo] [Status]
- RegularPadInfoViewModel.ApplyChangesCommand → ApplySizingToCells
  - [Rebuild] [View] [Undo] [Status]
- RegularPadInfoViewModel.DiscardChangesCommand
  - No external side effects

### Dialog ViewModels (src/FreeformHelper.UI/Views/*.axaml.cs)
- CadPadDialogViewModel Save/Cancel
  - Returns only the dialog result
- RegularPadDialogViewModel Save/Cancel
  - Returns only the dialog result

## Settings and property-change triggers

### FreeformHelperViewModel.Settings.cs
- OnActiveAreaWidthChanged / OnActiveAreaHeightChanged
  - [Rebuild] [View] [Status]
- OnPanelBiasXChanged / OnPanelBiasYChanged (when IsPanelAlignment)
  - [Rebuild] [View] [Status]
- OnCadLineOpacityChanged / OnRegularLineOpacityChanged
  - [Rebuild] [Status]
- OnSelectedScanOrderOptionChanged / OnSelectedScanOrderChanged
  - [Rebuild] [Status]
- OnSelectedGridAlignmentOptionChanged / OnGridAlignmentModeChanged
  - [Rebuild] [View] [Status]
- OnXChannelsChanged / OnYChannelsChanged / OnCascadeNumChanged / OnGridPaddingPercentChanged
  - [Rebuild] [View] [Status]
- MatchThreshold
  - [Visual] [Status] (only redraws unmatched highlight; does not rebuild the grid or change links)
- `Mode` / centroid fallback / `NearestK`
  - [Compatibility] Not read by the current overlap matcher; has no normal-flow editor or live ViewModel owner, and is only round-tripped unchanged by the project persistence adapter
- OnRecalcBoundsOnLayerFilterChanged
  - [Rebuild] [View] [Status] (MarkUnsaved)
- OnShowCadChanged / OnShowRegularChanged / OnHighlightUnmatchedChanged / OnHighlightFreeformChanged / OnColorCadByAreaChanged
  - SyncWorkspaceToggle（UI）+ [Undo]（UndoHooks）
- OnShowNotchCanvasPreviewChanged / OnShowNotchToRegularLabelsChanged / OnNotchPreviewVisualizationStepChanged
  - Updates overlay visibility + triggers CanvasHost.Invalidate (immediate AA overlay refresh)
- OnEnableToFullChanged
  - Updates `CanShowToFullPreviewToggle` + overlay visibility + Step3 downstream invalidation + immediate preview recomputation
- OnEnableToFullRuleEngineChanged / OnEnableToFullRuleTraceChanged
  - Step3 compensation cache invalidation + downstream invalidation + immediate preview recomputation
- OnNotchPreviewAutoPlayEnabledChanged / OnNotchPreviewAutoPlayIntervalMsChanged
  - Synchronizes autoplay summary text; restarts the autoplay loop when the interval changes
- OnCadLineColorHexChanged / OnRegularLineColorHexChanged
  - Updates CadLineColor / RegularLineColor + [Undo]
- OnRegularSelectedColorHexChanged / OnRegularSelectedColorChanged / OnRegularSelectedFillOpacityChanged
  - Updates RegularSelectedColor / RegularSelectedFillOpacity (selection colors are adjustable in the UI)
- OnCascadeSettingChanged (per-IC X/Y changes)
  - [Rebuild] [View] [Status]
- OnCascadeSettingChanging
  - [Undo]
- OnPropertyChanged（DirtySettingNames）
  - MarkUnsaved (persisted settings)

### FreeformHelperViewModel.State.cs
- OnUseLocalSizingChanged / OnIsWidthRowLocalChanged / OnIsHeightColumnLocalChanged
  - [Rebuild] [Status]
- OnStatusTextChanged
  - [Status] (updates StatusDisplayText/HasStatusDisplay)

### FreeformHelperViewModel.ManualSizing.cs
- OnManualRowsRangeChanged / OnManualColsRangeChanged / OnPendingColumnWidthChanged / OnPendingRowHeightChanged
  - TryApplyManualSizingAsync
  - [Clear selection] when ranges are cleared
  - May synchronize selection [Set selection]
  - [Rebuild] [View] [Status] during ApplySizingToCells

### FreeformHelperViewModel.UndoHooks.cs
- Various *Changing hooks (toggles, styles, grid, match, notch, manual sizing)
  - [Undo]（TrackUndo）

## View / Control events

### FreeformHelperView (src/FreeformHelper.UI/Views/FreeformHelperView*.cs)
- OnAttachedToVisualTree（Pickers.cs）
  - Sets CanvasHost, subscribes to InteractionState, ConfigureFilePickers
  - EnsureInitialGrid → [Rebuild]
  - EnsureInitialFit → [View]
- OnTopLevelKeyDown（FreeformHelperView.axaml.cs）
  - Ctrl+Z → UndoCommand [Undo] [Status]
  - Ctrl+S → SaveProjectAsync (save result displayed in a top toast) [Status]
  - Delete → DeleteSelectedCadPadsCommand (only when focus is outside text input) [Status]
- F / Ctrl+A → globally forwarded to Fit / SelectAll (only when focus is outside text input and the source is not the canvas) [View]/[Set selection]
  - Ctrl +/- (console focus) → ConsoleFontSize changes
  - Global shortcuts are not handled when text input (TextBox/ComboBox/NumberScrubber) has focus
- OnRootPointerPressed
  - An outside click closes pad info / otherwise focuses the canvas
- WireCanvasEvents（Canvas.cs）
  - SelectionChanged → InteractionState.SetSelection [Set selection]
  - ViewChanged → UpdatePadInfoLayout
  - CadPadContextRequested / RegularPadContextRequested → opens pad info
- PadInfoPopover wiring
  - ApplyCloseRequested / DiscardCloseRequested → ClosePadInfoViaState
  - CloseConfirmDismissed → re-enable hit testing
- Console controls（Console.cs）
  - Log changes → auto-scroll
- Panel buttons（Panels.cs）
  - Left/right panel show/hide (layout only)
- File picker delegates（Pickers.cs）
  - Provides Save/Load/Open dialogs
- ConfirmEmbedDxfAsync（Pickers.cs）
  - Displays ConfirmDialog

### PadInfoPopover (src/FreeformHelper.UI/Views/PadInfoPopover.axaml.cs)
- ApplyAndClose_Click
  - Executes ApplyChangesCommand (corresponding CAD/Regular side effects)
- DiscardAndClose_Click
  - Executes DiscardChangesCommand
- OnPopoverPointerPressed
  - Dismisses close confirm

### WorkspaceHeader (src/FreeformHelper.UI/Controls/WorkspaceHeader.axaml.cs)
- Display menu hover/pin handlers
  - Toggles the popup with no model side effects

### NumberScrubber (src/FreeformHelper.UI/Controls/NumberScrubber.axaml.cs)
- Wheel / scrub / input
  - Triggers Value changes (downstream property changes and side effects)

### MainWindow (src/FreeformHelper.UI/MainWindow.axaml.cs)
- OnClosingAsync
  - Displays ConfirmDialog and calls SaveProjectAsync after confirmation

### ConfirmDialog (src/FreeformHelper.UI/Views/ConfirmDialog.axaml.cs)
- ConfirmButton_Click / CancelButton_Click
  - Closes the dialog with result

## PadCanvas interaction

### Input and selection (src/FreeformHelper.UI/Controls/PadCanvas.Input.cs)
- Left click: select / Ctrl/Shift additive / toggle
  - Click blank space without a modifier → [Clear selection]
- Box selection (drag)
  - ApplyBoxSelection → [Set selection]
- Right click
  - Adjusts selection based on the hit result + triggers the CAD/Regular context menu
- Double click
  - Selects first if needed, then triggers the context menu
- Row/Col/IC label click
  - Directly selects regular pads in that row/col/IC (additive supported) [Set selection]
- SelectionChanged event
  - Emits SelectionChanged (selected IDs/indices)

### View control (src/FreeformHelper.UI/Controls/PadCanvas.View.cs)
- FitToContent
  - [View]
- ZoomAt（mouse wheel）
  - [View]
- Pan (middle mouse button or spacebar)
  - [View] + ViewChanged event

### Keyboard shortcuts (src/FreeformHelper.UI/Controls/PadCanvas.Input.cs)
- F → FitToContent [View]
- Ctrl+A → SelectAllPads [Set selection]
- Shift+Arrow → pan nudge [View]
- Note: The same F/Ctrl+A also have TopLevel global entries to maintain consistent behavior when the canvas is not focused.

### Programmatic selection (PadCanvas.Input.cs)
- ClearSelection() → [Clear selection]
- SetSelection() → [Set selection]

### Resource / Theme initialization (src/FreeformHelper.UI/Controls/PadCanvas.cs)
- ResourcesChanged / ActualThemeVariantChanged
  - Clears cache + ApplyThemeDefaults + InvalidateVisual
  - Prevents color/font failures caused by resource initialization timing

### ViewChanged event (PadCanvas.cs)
- Triggered when pan/zoom changes; FreeformHelperView uses it to reposition pad info popovers

---

## Current status of the seven UseCases (R13.005 calibration)

The seven UseCases listed in the original draft are not currently "all complete"; the accurate status is 6 partially implemented and 1 not implemented. `SelectionCoordinator` and status/undo are additional coordination components and are not counted among these seven UseCases.

| UseCase | 1.3.0 status | Converged responsibilities | Responsibilities / owners not yet converged |
| --- | --- | --- | --- |
| `GridRebuildUseCase` | Partial | All rebuild requests go through `TriggerGridRebuildAsync -> RequestAsync -> RebuildGridAsync` and coalesce pending requests | `preserveSelection/reason` is not yet a typed request; selection clear, fit, and status are still owned by the VM low-level rebuild (R13.302/R13.403) |
| `ManualSizingUseCase` | Partial | range parse, validation, and action plan | Mutation is performed by VM/`PadEditUseCase`; mixed state, Undo, rebuild, and fit have not formed a single result/side-effect owner (R13.402) |
| `LayerFilterUseCase` | Partial | pure allowed-layer / visible-CAD result | toggle, workflow reset, FW diff assignment, and rebuild/fit are still owned by VM orchestration (R13.403) |
| `CadLoadUseCase` | Partial | raw DXF/project import; Open DXF shares the outcome through `CadLoadWorkflowService` | Project load replay, layers, selection, and rebuild side effects are still orchestrated by persistence/VM (R13.403) |
| `FreeformTaggingUseCase` | Partial | manual/auto assignment mutation helpers | manual/auto each handle override, Undo, dirty, preview, and status (R13.403) |
| `PadEditUseCase` | Partial | regular sizing apply/reset/set-dimensions | CAD custom edit still bypasses the use case; Undo/status/rebuild/dirty are still owned by the VM (R13.402) |
| `CanvasViewUseCase` | Not implemented | None | Fit/Reset/Zoom remain distributed across commands, rebuild, and `PadCanvas` input/view; R13.302 must first define the fit side-effect policy before deciding whether a new type is needed |

## Existing coordination components and side-effect owners

| Behavior | Current convergence | Explicit side effects / debt |
| --- | --- | --- |
| Selection | `SelectionCoordinator`: canvas, manual range, programmatic locate, clear | Synchronizes selection state/Canvas; more converged than the draft, but not one of the seven UseCases above |
| Workflow policy | `WorkflowPipelineService`: next step, invalidation, expansion, project replay plan | VM `WorkflowSteps` applies clear/invalidation; completion is still inferred from result content/summary, and valid zero results may be misclassified (R13.302a) |
| Workflow execution | UI commands call Step handlers directly; Runtime CLI goes through the `RunWorkflowStepAsync` switch | Both share underlying handlers/pipeline policy but do not yet share the same execution entry; clear actions already share `ClearWorkflowStep` |
| Status / Undo | `UiOperationStatusReporter` / `UndoService` already exist | Calls and text assembly remain scattered across multiple VM paths; subsequent workspace slices will converge them gradually, without creating a speculative facade |
| Settings apply | `SettingsWindowViewModel` draft -> `ApplySettingsWindowDraft` -> orchestration plan | Still needs a dirty-field change set, revisioned stale state, and Step2/4/5 typed invalidation (R13.301/302) |

## Current workflow and target

- Current navigation is still Step1 -> Step2 -> Step3 -> Step4 -> Step5; the Right panel also presents numbered Step6. Step4 diagnostics are optional, and Step5 in `WorkflowStepGateService` does not depend on Step4.
- `RunWorkflowStepAsync` still delegates to underlying handlers through a switch; the documentation may only claim that UI/CLI share handlers/policy, not that the switch has been eliminated or that there is already a unique execution entry.
- R13.305a target: successful Step3 proceeds directly to Step5, with Step4 mapping and Step6 validation moved to unnumbered Diagnostics/Inspector; R13.302a first replaces readiness guesses based on row count, preview count, freeform value, or summary string with revisioned `Completed/Stale`.
- Regular Visibility Mask is a required explicit input of Step1 and may remain in the normal flow; Step4 Analyze is the optional diagnostics.
