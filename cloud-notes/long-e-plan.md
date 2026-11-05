# Long-method batch E: parallel loops, timing and locks

Date: 2026-11-05. Branch: `cloud/long-e-plan`. Ledger issue tag: `health-refactor/2026-10-31` (no GitHub issue found for this batch).

## Scope and how the list was built

Batch E holds the `methodLines` entities in `eng/code-health/baseline.json` (113 long-method entities in all) whose body contains
`Parallel.*`, `Stopwatch`, `UtcNow`, `Task.Delay`, `lock`, or `Interlocked`. A scan of every long-method entity found 10 methods.
The test method `CreateSimulationWorkspaceSessionAsync_FinalProjectionChangesAtCompletion_...` is not in batch E: the
`Interlocked` calls in that file are in other test methods.

Rules that apply to every item below:

- The method keeps its name, signature and file, so it keeps its ledger entity.
- Each new helper is 80 lines or fewer, lives in the same file, and is not in a new partial file.
- Statements that carry `Stopwatch`, `UtcNow`, `Task.Delay` or a VSTHRD003 finding stay in the original method.
  This also keeps every RS0030 / VSTHRD003 finding on its current member, so no finding fingerprint moves.
- Behaviour, thread safety and lock ordering do not change. No helper holds a lock across an `await`, and no helper adds a lock.

## Batch E methods

Line counts are the baseline ceilings (Roslyn physical lines). The findings column comes from `findings` in the baseline.

| # | Method (file) | Lines | Risk markers | Findings in member | Risk |
|---|---|---|---|---|---|
| 1 | `RuntimeQueryUseCase.QueryExportNotchAsync` (`UI/Services/RuntimeQueryUseCase.Commands.Export.cs`) | 124 | 1 `Stopwatch` around the export command, try/finally swap of 3 VM delegates | none | low (**done here**) |
| 2 | `FreeformHelperViewModel.LoadProjectFromPathCoreAsync` (`UI/ViewModels/FreeformHelperViewModel.Persistence.Project.cs`) | 82 | `Stopwatch` in the operation lambda, UI operation runner | none | low |
| 3 | `FreeformHelperViewModel.ApplyLoadedProjectStateAsync` (same file) | 121 | 5 `Stopwatch`, `Task.Run`, settings-suppression scope, persistence flag restored in `finally` | none | medium |
| 4 | `FreeformHelperViewModel.RunDeferredCadInspectorSnapshotRefreshAsync` (`UI/ViewModels/FreeformHelperViewModel.PadInspectorDeferred.cs`) | 110 | `Stopwatch`, `Task.Delay`, `Interlocked` request-id checks, `CompareExchange` CTS release, 2 dispatcher hops, `Task.Run` | RS0030 ×1 | medium |
| 5 | `NotchTableGenerator.BuildV22CadCandidate` (`Application/Services/NotchTableGenerator.Generation.V22.cs`) | 149 | 2 nested `Stopwatch`, runs inside `Parallel.For` (shared `CandidateTimingAccumulator`) | none | medium |
| 6 | `NotchTableGenerator.BuildCanonicalCandidatesByDiff` (`Application/Services/NotchTableGenerator.Generation.cs`) | 160 | `Parallel.For`, `Interlocked.Increment` progress counter, 2 `Stopwatch`, AggregateException unwrap | none | medium-high |
| 7 | `NotchV22CompensationService.RunStageCEvaluateToFullAndDiagnostics` (`Application/Services/NotchV22CompensationService.Stages.cs`) | 172 | 5 `Stopwatch.GetTimestamp` step timers inside the per-regular loop, 6 accumulated elapsed counters | RS0030 ×5 | high |
| 8 | `CadLoadSpinnerProcessHost.SyncRequestedVisibilityStateAsync` (`UI/Services/CadLoadSpinnerProcessHost.cs`) | 182 | 10 `lock (_sync)` blocks, 2 `Task.Delay`, `UtcNow`, state-machine `while (true)` with `continue`/`return` | RS0030 ×3 | high |
| 9 | `FreeformHelperViewModel.GenerateCurrentNotchTableAsync` (`UI/ViewModels/FreeformHelperViewModel.NotchTableGeneration.cs`) | 340 | 2 `UtcNow`, `await batchLease.ResolutionTask!` (VSTHRD003), 2 `Task.Run`, generation-epoch / revision checks | RS0030 ×2, VSTHRD003 ×1 | high |
| 10 | `PadCanvas.Render` (`UI/Controls/PadCanvas.Rendering.cs`) | 306 | 9 `Stopwatch.GetTimestamp` phase timers, `PushTransform` scope, perf record | RS0030 ×9 | high |

## Extraction plans

### 1. `QueryExportNotchAsync` (124 → 69 lines) — implemented in this branch

- Moves out:
  - Argument parsing, the format switch, and the normalized-format switch → `TryParseExportNotchArguments(args, out ExportNotchArguments, out error)` (static, 45 lines).
  - `Path.GetFullPath`, `Directory.CreateDirectory`, and the pre-export file snapshot → `PrepareExportNotchTarget(outputPath)` (static, 17 lines) returning `ExportNotchTarget`.
  - The two post-export file checks → `TryVerifyExportNotchOutput(target, summaryText, out FileInfo, out error)` (static, 30 lines).
- Stays: `Stopwatch.StartNew`, the whole try/finally that swaps `SelectedNotchExportFileTypeOption`, `PickSaveNotchPathAsync` and `OpenNotchExportSelectionAsync`, `Stop()` in `finally`, and the success payload that reads `ElapsedMilliseconds`.
- Captured state that becomes parameters: `args`; `outputPath`; and `resolvedPath`, `fileExistedBefore`, `previousWriteUtc`, `previousFileSize`, now carried in the `ExportNotchTarget` record struct.
- Order of side effects is unchanged. Validation errors come first. `helper` and the option lookup come next, then the "format not available" check. After that `GetFullPath` / `CreateDirectory` run, then the export, then the file checks. The normalized-format switch now runs before the option lookup. It is pure and depends only on the mapped type.
- Locks: none before, none after. The `await` of `ExportNotchCommand` has the same synchronization context as before.

### 2. `LoadProjectFromPathCoreAsync` (82)

- Moves out: the 3 path validation returns (empty path, `TryGetFullProjectPath`, `File.Exists`) → `TryValidateProjectLoadPath(path, out normalizedPath, out ProjectLoadCommandResult failure)`.
- Stays: `CreateStatusScope`, the `RunUiOperationAsync` call with its 3 lambdas, and `loadSw` start, stop and reads inside `operationAsync`.
- Captured state: `commandResult` stays a local captured by the lambdas. It is not passed by ref.
- Locks: none. Expected size is about 62 lines.

### 3. `ApplyLoadedProjectStateAsync` (121)

- Moves out:
  - The CAD reset inside `EnterSettingsLoading()`: `_cad = null` through `LayerToggles.Clear()` → `ResetCadStateForProjectLoad()`. It is called inside the same `using` scope.
  - The `if (outcome is not null)` block → `ApplyProjectCadOutcomeAsync(file, outcome, path, status)`. It runs inside the same `RunWithCadLoadCanvasOverlayAsync` lambda.
  - The two timing log calls → `LogProjectLoadTiming(long total, long persistence, long applyState, long dxf, long rebuild, ProjectLoadReplayTiming replay, string path)`.
- Stays: every `Stopwatch.StartNew` / `Stop` / `ElapsedMilliseconds` statement, `Task.Run(() => _cadLoadUseCase.TryLoadFromProject(...))`, and the `finally` that restores `SuppressPersistence`.
- Before the log call, the elapsed values are read into locals (`var totalElapsedMs = totalSw.ElapsedMilliseconds;`). The watches are already stopped, so the values do not change.
- Captured state: `dxfElapsedMs`, `rebuildElapsedMs` and `replayTiming` stay locals of the original method, because the lambda writes them.
- Locks: none. Expected size is about 85 lines. A second step is needed to get under 80: move the apply-state block (custom values through `MarkProjectLoadedForAppGeneralPersistence`) between `applyStateSw` start and stop into `ApplyProjectFileState(file, path, status)`.

### 4. `RunDeferredCadInspectorSnapshotRefreshAsync` (110)

- Moves out:
  - The body of the second dispatcher lambda, except the debug log → `TryApplyDeferredCadInspectorSnapshot(context, resolved, diagnostics)`. It returns `bool` and runs on the UI thread inside the same `InvokeAsync`.
  - The prewarmed-or-`Task.Run` resolve → `ResolveDeferredCadInspectorAsync(context, cancellationToken)` with `ConfigureAwait(false)` kept.
- Stays: `Stopwatch.StartNew`, `Task.Delay(..., cancellationToken).ConfigureAwait(false)`, both `Interlocked.Read` request-id checks, and the `_logger.Debug` that reads `sw.ElapsedMilliseconds` (now `if (TryApply…) { _logger.Debug(…) }` in the lambda). The `catch` clauses and the `finally` with `Interlocked.CompareExchange(ref _padInspectorDeferredRefreshCts, null, cts)` also stay.
- Captured state: `context` (nullable local written by lambda 1) stays in the original method. The helper takes it as a non-null parameter after the null check.
- Thread/lock: no locks. Each helper runs on the same thread as the code it replaces: UI-thread code stays inside `InvokeAsync`, and pool-thread code stays after `ConfigureAwait(false)`. The cancellation and request-id checks run in the same order.

### 5. `BuildV22CadCandidate` (149)

- Moves out:
  - Selected-result identity and reusable-result validation (lines 279–308) → `ResolveV22ReusableResult(anchor, cadPad, context, out isSelectedResult, out selectedIdentity)`. It is pure and reads only `context`.
  - The allocation `if / else if / else` (334–361) → `ResolveV22TargetAllocation(...)`.
  - Coverage projection, the combined percent and the comment text (362–389) → `BuildV22CandidateProjection(...)`, returning a record struct.
- Stays: both `Stopwatch.StartNew` / `Stop`, the `NotchV22CompensationService.Compute` call inside the compensation timer, the `timingAccumulator?.Add(...)` (shared across `Parallel.For` workers), and `return candidate`.
- Thread safety: the helpers are `static`, take read-only `context` / `profile` inputs, and write no shared state. `CandidateTimingAccumulator` is only touched from the original method, as before. The timed regions cover the same calls because the helpers run inline and synchronously.

### 6. `BuildCanonicalCandidatesByDiff` (160)

- Moves out:
  - The empty-profile double progress report (575–597) → `ReportEmptyCanonicalCandidatePhases(progress, candidatePhaseStep, mergePhaseStep, phaseStepCount)`.
  - The sequential merge loop (650–698) → `MergeCanonicalCandidatesByDiff(candidatesByProfile, cadProfiles, candidatesByDiff, progress, mergePhaseStep, phaseStepCount)`. It runs after `Parallel.For` has returned, inside the `mergeStopwatch` region.
  - The selected sparse result (700–707) → `BuildSelectedSparseResult(context, selectedResolvedByProfile)`.
- Stays: the `Parallel.For` lambda unchanged, `Interlocked.Increment(ref processedInParallel)` (the local must stay in this method, so it can be passed by `ref` to Interlocked), the `AggregateException` unwrap, both stopwatches, and the final progress report and result construction.
- Thread safety: the parallel region does not change. The merge helper is single-threaded, as before. It has no locks.

### 7. `RunStageCEvaluateToFullAndDiagnostics` (172)

- Moves out (per-iteration bodies between timer statements):
  - The to-full region collection (161–185) → `CollectToFullRegions(...)`, adding to the 3 polygon lists that are passed in.
  - The `NotchV22RegularDebugInfo` construction (197–223) → `BuildRegularDebugInfo(in StageCRegularEvaluation)`, where the new record struct holds the ~20 per-regular locals.
  - The owner/blocker lookup and occupied-area expressions stay. Each one sits between a `GetTimestamp` and an accumulator line.
- Stays: all 5 `stepStartedAt = Stopwatch.GetTimestamp()` statements, so the RS0030 count stays 5 on this member. All `+= GetElapsedMilliseconds(stepStartedAt)` lines and the final `StageCEvaluationResult` also stay.
- Each timer still measures the same calls. Risk is high because the record struct must capture every local exactly. Guard it with the existing Stage C / v2.2 golden comparison before merging.

### 8. `SyncRequestedVisibilityStateAsync` (182)

- Moves out:
  - Each `lock (_sync) { if (_disposed) return; x = ...; }` read → a small helper that takes the lock itself and returns `bool` (not disposed) plus `out` value: `TryReadSyncStartState(out showRequestedAtStart, out showRequest, out warmupTask)`, `TryReadShouldHide(out bool)`, `TryReadShowStillRequested(out bool)`, `TryReadHideRequested(out bool)`.
  - The not-visible branch (276–311) → `SyncHiddenState()` returning `SyncStep { Return, Continue }`.
  - The final "hide after show" tail → `HideAfterShow()` returning `SyncStep`.
- Stays: the `while (true)` loop with its `continue`/`return` decisions, `await warmupTask`, both `Task.Delay`, `DateTimeOffset.UtcNow - showSentUtc`, and the `lock` that reads `_lastShowSentUtc`. `SendShowRequestCoreLocked` also stays under its lock in the original method.
- Lock scope before and after: one `lock (_sync)` per helper, with the same statements inside. No lock is nested, no lock is held across an `await`, and `SendHideRequest` / `HasRunningProcess()` stay outside the lock in the same order. Lock acquisition count and order per iteration do not change.
- The 3 RS0030 findings (`Task.Delay` ×2, `UtcNow`) stay on this member.
- Guard: the existing `CadLoadSpinnerProcessHost` tests plus a new show→hide→show race test before the refactor.

### 9. `GenerateCurrentNotchTableAsync` (340)

- Moves out:
  - Input fingerprinting (66–95) → `CaptureNotchGenerationInputs(cad, grid, workflowSnapshot)`, returning a record with the snapshot, export settings, hashes, sparse request and fingerprints.
  - The phase-timing log (226–270) → `LogNotchGenerationPhaseTimings(effectiveTable, operationLabel)`.
  - The cross-IC log (271–280) → `LogCrossIcMismatch(...)`.
  - Synchronous cache acceptance and commit after the last `await` (282–345) → `CommitNotchGenerationResult(...)`. It runs on the same thread, after the last await.
  - In the CadAllocation branch (121–168), only the lease setup before the `await` moves.
- Stays: both `DateTime.UtcNow` statements (RS0030 ×2), `await batchLease.ResolutionTask!` (VSTHRD003), both `Task.Run` awaits, the outer try/finally, and the epoch/revision check placement relative to the awaits.
- Captured state: `generationEpoch`, `finalProjectionRevision`, `sourceRevision` (read before the first await) become parameters of the commit helper.
- Locks: none, but the `Volatile.Read` of `_notchFinalProjectionRevision` stays first.

### 10. `PadCanvas.Render` (306)

- Moves out:
  - The render-frame unpacking and cache ensure (56–94) → `EnsureRenderCaches(renderFrame)`.
  - The regular and CAD brush/pen setup (96–190) → `ResolveRegularPadPaint(renderFrame)` and `ResolveCadPadPaint(renderFrame)`, returning record structs.
  - The geometry draws (201–238) → `DrawPadGeometry(context, paint, ...)`.
  - The post-transform overlays (272–303) → `DrawPostTransformOverlays(context, secondaryVisualsDeferred)`.
- Stays: all 9 `Stopwatch.GetTimestamp` statements (RS0030 ×9) with their `ElapsedMilliseconds(...)` accumulators, `base.Render`, the `using (context.PushTransform(worldToScreen))` scope (helpers are called inside it), and `RecordRenderPerf`.
- Thread: UI render thread only, no locks. Draw order is preserved because the helpers are called in the current sequence. Guard: a headless render smoke test or a Dev page check before and after.

## Recommended order

1 → 2 → 3 → 4 → 5 → 6 → 9 → 10 → 7 → 8. Do one method per commit, and put all of them in one PR with the ledger update in its last commit.

## Proof on method 1 (this branch)

- `QueryExportNotchAsync` is now 69 lines (method declaration to closing brace) and is under the 80-line limit. The new helpers are 45, 17 and 30 lines.
- Build: .NET SDK 10.0.301 after `python scripts/fetch_core_packages.py`, `dotnet build src/FreeformHelper.UI/FreeformHelper.UI.csproj`: 0 errors. The UI project has 76 warnings before and 76 after, with an identical set (diffed with line numbers removed).
- Tests:
  - `RuntimeQueryUseCaseTests` and `RuntimeQueryCharacterization*`: 179 passed, 16 skipped, 0 failed.
  - `Architecture` ratchet tests: 102 passed.
  - Two new behaviour tests: `ExecuteAsync_QueryExportNotch_RejectsMissingPath` and `ExecuteAsync_QueryExportNotch_CreatesOutputFolderBeforeExport`. Both also pass against the original method, so they characterize existing behaviour.
- Gap: the success path (a real export file) has no automated test. A full workflow needs the private `example/` submodule, which is not available in this cloud session. Before merge, run `query export-notch --format csv --path <tmp>` on a real project once.
- Not run: `eng/core-health/repo-health.ps1 -Mode Verify` and `scripts/tests/lint.ps1`. `pwsh` is not installed in this environment.
- Ledger follow-up for the owner: the entity for `QueryExportNotchAsync` (ceiling 124) is now below the limit. Do not edit the ledger by hand. Run `repo-health.ps1 -Mode LowerBaseline` (rule 3 arguments), then `-Mode Verify`, on a machine with pwsh.
