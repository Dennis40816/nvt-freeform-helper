# ViewModel Hotspot Scan (Beta 0.7 / S11.151)

Date: 2026-04-23
Branch: `codex/beta0.7`
Scope: `src/FreeformHelper.UI/ViewModels/FreeformHelperViewModel*.cs`

## Conclusion

Beta 0.7 has finished several large ViewModel / XAML / test hotspot splits, but `FreeformHelperViewModel` is still the largest orchestration hub in the repo. The remaining risk is not the line count of one file. The problem is that UI state, workflow commands, projection/cache, and persistence request builders still read and write the same private fields inside one partial class family.

This slice does not change behavior. Its purpose is only to set up the candidates and order for the next split round.

## Highest Priority Files

| Rank | File | Lines | Primary responsibility | Suggested direction |
| --- | ---: | ---: | --- | --- |
| 1 | `FreeformHelperViewModel.Operations.cs` | 508 | CAD/grid/workflow operation glue, state updates, UI side effects | Split out `WorkspaceOperationUseCase`, so the VM only handles command binding and status projection |
| 2 | `FreeformHelperViewModel.Persistence.Project.cs` | 452 | Save/Load project request, deferred app settings flush, project replay | Split out `ProjectLoadOrchestrator` / `ProjectSaveOrchestrator`, and clearly separate file IO requests from UI side effects |
| 3 | `FreeformHelperViewModel.State.CanvasAndMapping.cs` | 452 | Canvas + mapping observable state | Keep only state; move derived summary / mapping projection to a projector |
| 4 | `FreeformHelperViewModel.Operations.LayerFiltering.cs` | 446 | Layer filter, visible CAD projection, bounds/selection side effects | Split out `LayerFilteringUseCase`, and unify the result model of `FilterCadPadsByLayer` |
| 5 | `FreeformHelperViewModel.Core.cs` | 427 | Constructor, service wiring, option lists, global entry fields | Move option construction and service wiring to a factory to reduce constructor width |
| 6 | `FreeformHelperViewModel.PadInspectorSummary.cs` | 424 | Inspector summary text / chips / detail aggregation | Convert to `PadInspectorSummaryProjector`, and the VM only holds the result model |
| 7 | `FreeformHelperViewModel.State.Configuration.cs` | 403 | Observable configuration state | Keep the state, but split out a Step-specific state container or Settings bridge |
| 8 | `FreeformHelperViewModel.Persistence.NotchExport.Helpers.cs` | 389 | Step5 export target/cache/fingerprint helper | Split out `NotchExportRequestBuilder` and `NotchExportCacheKeyBuilder` |
| 9 | `FreeformHelperViewModel.PadInspectorDeferred.cs` | 388 | Deferred inspector computation / cache orchestration | Split out `PadInspectorRefreshUseCase`, and merge its output contract with the Summary projector |
| 10 | `FreeformHelperViewModel.DxfEditing.ChangeList.cs` | 353 | DXF edit change list projection + apply/focus command path | Split out `DxfEditChangeListUseCase`, and keep a single apply entry |

## Single-Path Risks

Currently acceptable, but should keep being consolidated:

- `WorkflowDataSnapshot` already centralizes `CadOutputFwDiffIndexByCadId`, but the ViewModel still holds several raw fields. Later, only the snapshot builder should read these private fields.
- `RuntimeQueryUseCase` already goes through a single VM command entry, but query payloads still read many VM observable states directly. This should gradually change to a shared result model.
- Step5 export UI, runtime query, and C/CSV export already come from the same source, but export target, cache fingerprint, and file type wording are spread across the helper partial and the metadata class.
- Pad inspector summary, trace, and deferred refresh are already split into files, but they still share a lot of private state. The next step should first create `PadInspectorSnapshot` as the only result model.

## Beta 0.8 Candidate Slices

1. `S12.001 Project load/save orchestration extraction`
   - Scope: `FreeformHelperViewModel.Persistence.Project.cs`, `ProjectPersistenceUseCase`
   - Done: The VM does not build the large load/save flow directly. It only calls the orchestrator and applies an explicit side-effect result.

2. `S12.002 Layer filtering result model`
   - Scope: `FreeformHelperViewModel.Operations.LayerFiltering.cs`
   - Done: `FilterCadPadsByLayer()` returns a typed result. Selection clear, bounds rebuild, and status text are applied by one policy.

3. `S12.003 Pad inspector snapshot projector`
   - Scope: `PadInspector*.cs`
   - Done: Inspector UI, runtime query, and summary chips share the same snapshot. They no longer build text separately from partial private state.

4. `S12.004 Notch export request/cache extraction`
   - Scope: `Persistence.NotchExport.Helpers.cs`, `NotchExportFileTypeMetadata`
   - Done: Export target, format kind, cache key, and file extension are expressed in one request model.

5. `S12.005 Core constructor slimming`
   - Scope: `Core.cs`, option list / service factory
   - Done: The constructor only injects factories and services. It no longer builds many option lists and delegates inline.

## Not Recommended During Beta 0.7 Wrap-Up

- Do not split `State.*` into several nested objects now. It would require many XAML binding renames, which is high risk with limited benefit.
- Do not change Project load, Layer filtering, and Pad inspector in the same commit. Each of them triggers selection, rebuild, and cache side effects.
- Do not move `FreeformHelperViewModel` into several ViewModels at once. First extract UseCases / projectors so the binding surface stays stable.