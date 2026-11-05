# Long-method decomposition, batch D (async and settings-order methods)

Branch: `cloud/long-d-decomp` (from `main` at 5e48bec). No PR.

## Scope

Batch D = the `methodLines` entities in `eng/code-health/baseline.json` that are
async methods or settings load/save/apply-order methods. Methods in files
changed on `origin/cloud/long-a-decomp` or `origin/cloud/long-bc-decomp` were
skipped; none of the 17 methods below is touched by those branches.

Not in batch D (left for other batches): `App.OnFrameworkInitializationCompleted`,
`SettingsWindowViewModel..ctor` and the async test methods under `tests/`.

## Methods

Every method keeps its name, signature and file, so it keeps its ledger entity.
"Before" is the ledger ceiling; "after" is the declaration span (signature line
to closing brace, the same span the health script measures). All new helpers
are 80 lines or fewer and sit in the same file (no new partial files).

| # | Method (file) | Before | After | Main helpers added |
|---|---|---:|---:|---|
| 1 | `CadLoadSpinnerProcessHost.SyncRequestedVisibilityStateAsync()` (Services/CadLoadSpinnerProcessHost.cs) | 182 | 79 | TryCaptureStateSyncSnapshot, IsStateSyncHideNeeded, SendStateSyncHideAndCheckShowRequested, AcquireStateSyncWarmupTask, TrySendStateSyncShow, TryReadRequestedVisibility, ReadLastShowSentUtc |
| 2 | `RuntimeQueryUseCase.QueryExportNotchAsync` (Services/RuntimeQueryUseCase.Commands.Export.cs) | 124 | 60 | TryResolveNotchExportFormat, CaptureNotchExportTargetState, BuildNotchExportResult, record struct NotchExportTargetState |
| 3 | `RuntimeQueryUseCase.QuerySimulationAsync` (Services/RuntimeQueryUseCase.Commands.Simulation.cs) | 132 | 54 | BuildSimulationRegularPayload, BuildSimulationWorkspacePayload |
| 4 | `SettingsApplyPlanBuilder.Resolve` (Services/SettingsApplyPlanBuilder.cs) | 99 | 17 | TryResolveLayoutProperty, ResolveStepProperty |
| 5 | `FreeformHelperViewModel.ExportDxfLayerImageAsync()` (DxfEditing.Export.cs) | 103 | 66 | TryCollectDxfLayerImageExportSource, WriteDxfLayerImage |
| 6 | `FreeformHelperViewModel.AnalyzeIndexMappingAsync()` (IndexMapping.cs) | 102 | 72 | ApplyMappingDiagnosticsReport, CreateIndexMappingReportViewModel, LogMappingDiagnosticsReport |
| 7 | `FreeformHelperViewModel.GenerateCurrentNotchTableAsync(...)` (NotchTableGeneration.cs) | 340 | 76 | BeginStep5NotchGenerationProgress, PrepareNotchGenerationInputs, LogNotchGenerationStarted, AcquireNotchCadAllocationBatch, TakeCachedNotchBatchOrLogMiss, ProjectNotchCadAllocationBatchAsync, GenerateLegacyNotchTableAsync, LogNotchGenerationMetrics, LogNotchGenerationPhaseTimings, TryAcceptNotchGeneration, PromoteSelectedNotchSparseResult, CompleteNotchGeneration, record NotchGenerationInputs |
| 8 | `FreeformHelperViewModel.RunDeferredCadInspectorSnapshotRefreshAsync(...)` (PadInspectorDeferred.cs) | 110 | 72 | CaptureDeferredCadInspectorContext, ResolveDeferredCadInspectorAsync, TryApplyDeferredCadInspectorSnapshot |
| 9 | `FreeformHelperViewModel.ExportNotchTableAsync(...)` (Persistence.cs) | 108 | 77 | ReportNotchExportSavePickerMissing, ReportNotchExportStarting, ReportNotchExportCanceled, ReportNotchExportSucceeded, ReportNotchExportFailed |
| 10 | `FreeformHelperViewModel.BuildNotchTableForExportAsync(...)` (Persistence.cs) | 114 | 76 | TryPassNotchExportWorkflowGate, ReportNotchExportStage2Summary, ResolveNotchExportTableWithoutSelectionDialog, CreateNotchExportSelectionViewModel (+ shared ReportNotchExportCanceled) |
| 11 | `FreeformHelperViewModel.LoadProjectFromPathCoreAsync(string)` (Persistence.Project.cs) | 82 | 55 | TryNormalizeProjectLoadPath, CreateLoadedProjectCommandResult |
| 12 | `FreeformHelperViewModel.ApplyLoadedProjectStateAsync(...)` (Persistence.Project.cs) | 121 | 64 | ApplyLoadedProjectSettingsState, ResetCadStateForProjectLoad, ApplyProjectCadLoadOutcomeAsync, LogProjectLoadTiming |
| 13 | `FreeformHelperViewModel.LoadSettingsToUi(ProjectSettings)` (Settings.Sync.cs) | 150 | 39 | LoadGridSettingsToUi, LoadMatchingAndIndexMappingSettingsToUi, LoadNotchSettingsToUi |
| 14 | `FreeformHelperViewModel.ApplyUiToSettings(ProjectSettings,bool)` (Settings.Sync.cs) | 95 | 44 | ApplyUiToGridSettings, ApplyUiToNotchSettings |
| 15 | `FreeformHelperViewModel.ApplySettingsChangeEffects(...)` (SettingsPolicy.cs) | 86 | 25 | ApplySettingsSourceRefreshEffects, ApplySettingsDownstreamEffects |
| 16 | `FreeformHelperViewModel.ApplySettingsWindowDraft(SettingsWindowViewModel)` (SettingsWindow.cs) | 240 | 50 | ResolveSettingsWindowDraftOptions (64), CollectSettingsWindowDraftChanges (78), ApplySettingsWindowDraftValues (74), record struct SettingsWindowDraftOptions |
| 17 | `FreeformHelperViewModel.CreateSimulationWorkspaceSessionAsync(bool,Action<string>?)` (Simulation.cs) | 161 | 72 | ReportSimulationNoRowsGenerated, ReportSimulationDuplicateDiffWarning, CreateSimulationWorkspaceSession, ReportSimulationWorkspaceReady, ReportSimulationBuildFailed, record struct SimulationBuildReporter |

Total for the 17 methods: 2,349 lines before, 998 after.

## Baseline entries that should drop

All 17 `kind: "member"` entries with a `methodLines` ceiling for the methods
above should be removed by `-Mode LowerBaseline` (each method is now at or
below the 80-line limit):

- `FreeformHelper.UI.Services.CadLoadSpinnerProcessHost.SyncRequestedVisibilityStateAsync()` (182)
- `FreeformHelper.UI.Services.RuntimeQueryUseCase.QueryExportNotchAsync(IReadOnlyDictionary<string, string>?)` (124)
- `FreeformHelper.UI.Services.RuntimeQueryUseCase.QuerySimulationAsync(IReadOnlyDictionary<string, string>?)` (132)
- `FreeformHelper.UI.Services.SettingsApplyPlanBuilder.Resolve(IEnumerable<string>,SettingsChangeContext)` (99)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ExportDxfLayerImageAsync()` (103)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.AnalyzeIndexMappingAsync()` (102)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.GenerateCurrentNotchTableAsync(CadPadSet,RegularGrid,string,...)` (340)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.RunDeferredCadInspectorSnapshotRefreshAsync(long,int,long,CancellationTokenSource,CancellationToken)` (110)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ExportNotchTableAsync(string,string,string,string,Func<...>,bool)` (108)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.BuildNotchTableForExportAsync(CadPadSet,RegularGrid,string,bool)` (114)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.LoadProjectFromPathCoreAsync(string)` (82)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ApplyLoadedProjectStateAsync(ProjectFile,string,long)` (121)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.LoadSettingsToUi(ProjectSettings)` (150)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ApplyUiToSettings(ProjectSettings,bool)` (95)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ApplySettingsChangeEffects(SettingsChangeEffects,bool,bool)` (86)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.ApplySettingsWindowDraft(SettingsWindowViewModel)` (240)
- `FreeformHelper.UI.ViewModels.FreeformHelperViewModel.CreateSimulationWorkspaceSessionAsync(bool,Action<string>?)` (161)

No other ledger entry should change:

- Findings that must stay put stayed in the original method with the same
  statement text: the two `Task.Delay` calls and the `DateTimeOffset.UtcNow`
  statement in `SyncRequestedVisibilityStateAsync`; both `DateTime.UtcNow`
  statements and the VSTHRD003 `await batchLease.ResolutionTask!` in
  `GenerateCurrentNotchTableAsync`; the `Task.Delay` in
  `RunDeferredCadInspectorSnapshotRefreshAsync`; the `File.WriteAllText`
  (RS0030) in `ExportNotchTableAsync`. Stopwatch statements also stayed.
- No partial files were added (FreeformHelperViewModel 94, RuntimeQueryUseCase 10).
- No file crosses 800 lines (largest touched: CadLoadSpinnerProcessHost.cs, 762 -> 755).
- New nested records (NotchGenerationInputs 9 members, NotchExportTargetState,
  SettingsWindowDraftOptions, SimulationBuildReporter) are under the 30
  state-member limit.

## Verification

- `dotnet build FreeformHelper.sln` (SDK 10.0.301, after
  `python scripts/fetch_core_packages.py`): 0 errors, no warning that is not
  also in the `main` build.
- Tests: see the commit message for the filtered test run.
- `eng/core-health/repo-health.ps1 -Mode Verify` was **not run**: `pwsh` is
  not available in this environment. Line counts above come from a script that
  measures the same declaration span; run Verify (and then `LowerBaseline`)
  on a machine with PowerShell before opening a PR.
- `prepare-ui-workspace.ps1` and `lint.ps1` were not run for the same reason;
  edited files keep CRLF line endings.

## Behaviour notes

Pure extraction; call order and side effects are unchanged. Spots worth a
reviewer's look:

- `SyncRequestedVisibilityStateAsync`: the repeated "lock, check disposed, read
  requested visibility" blocks now go through `TryReadRequestedVisibility`;
  the loop's return/continue decisions are the same.
- `GenerateCurrentNotchTableAsync`: the legacy (non CAD-allocation) cache path
  now returns `(table, lookup, tableToStore)` from `GenerateLegacyNotchTableAsync`;
  the table is stored only when it has rows, as before.
- `ApplyLoadedProjectStateAsync`: the timing record is built before the timing
  logs (same values; all stopwatches are already stopped).
