using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private int _notchOperationBusyScopeCount;

    private sealed record NotchTableGenerationResult(
        NotchTable Table,
        RegularGrid EffectiveGrid,
        NotchTable RawTable,
        RegularGrid RawGrid,
        NotchTable CadOutputFwDiffTable,
        RegularGrid CadOutputFwDiffGrid,
        ProjectSettings ExportSettings,
        CrossIcMismatchSnapshot CrossIcMismatch,
        int SourceRevision);

    private async Task<NotchTableGenerationResult?> GenerateCurrentNotchTableAsync(
        CadPadSet cad,
        RegularGrid grid,
        string operationLabel,
        IProgress<NotchGenerationProgress>? progress = null,
        WorkflowDataSnapshot? workflowSnapshot = null,
        bool reportStep5Progress = false,
        bool projectToCadOutputFwDiff = true,
        Action<string>? progressTextReporter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationLabel);

        var generationEpoch = _notchExportGenerationCacheService.CaptureGenerationEpoch();
        var finalProjectionRevision = Volatile.Read(ref _notchFinalProjectionRevision);
        var sourceRevision = SimulationWorkspaceSourceRevision;
        IProgress<NotchGenerationProgress>? effectiveProgress = progress;
        if (reportStep5Progress)
        {
            IProgress<NotchGenerationProgress> step5Reporter = CreateStep5GenerationProgressReporter(
                operationLabel,
                finalProjectionRevision,
                sourceRevision,
                progressTextReporter);
            effectiveProgress = progress is null
                ? step5Reporter
                : new Progress<NotchGenerationProgress>(snapshot =>
                {
                    step5Reporter.Report(snapshot);
                    progress.Report(snapshot);
                });
            BeginNotchOperationBusyScope();

            NotchExportProgress = 0.0;
            var preparingMessage = $"{operationLabel}: preparing generation...";
            NotchExportProgressText = preparingMessage;
            progressTextReporter?.Invoke(preparingMessage);
        }

        try
        {
            ApplyUiToSettings(_projectFile.Settings);
            workflowSnapshot ??= BuildWorkflowDataSnapshot();
            var exportSettings = CreateExportSettingsSnapshot(_projectFile.Settings);
            var activeRegularPadIds = GetActiveRegularPadIdsForNotchComputation();
            var activeRegularHash = ComputeActiveRegularHash(activeRegularPadIds);
            var cadOutputFwDiffHash = ComputeCadOutputFwDiffHash(workflowSnapshot.CadOutputFwDiffIndexByCadId);
            var step3Revision = GetNotchStep3Revision();
            var selectedSparseResultRequest = exportSettings.Notch.ComputationMode == NotchComputationMode.CadAllocation
                ? NotchSparseResultUseCase.CreateRequest(
                    _selectedCadIds,
                    cad,
                    _grid,
                    grid,
                    workflowSnapshot,
                    selectedCadPad => GetOrBuildCadV22ResolvedResultCached(
                        selectedCadPad,
                        GetCadPadsForNotchComputation(selectedCadPad.Id),
                        activeRegularPadIds,
                        workflowSnapshot,
                        buildIfMissing: false))
                : null;
            var generationSettingsFingerprint = exportSettings.Notch.ComputationMode == NotchComputationMode.CadAllocation
                ? NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(exportSettings)
                : ComputeNotchExportSettingsFingerprint(exportSettings);
            var settingsFingerprint = CombineNotchExportSettingsFingerprint(
                generationSettingsFingerprint,
                activeRegularHash,
                cadOutputFwDiffHash);
            var cadFingerprint = BuildNotchExportCadFingerprint(cad);
            var gridFingerprint = BuildNotchExportGridFingerprint(grid);
            NotchTable table;
            NotchTableGenerator.CadAllocationResolvedBatch? resolvedBatch = null;
            NotchTable? legacyTableToStore = null;
            var stage2StartedAt = DateTime.UtcNow;

            Logger.Info(
                CultureInfo.InvariantCulture,
                "{0} generation started: cad={1}, regular={2}, mode={3}, compModel={4}, versions={5}, thV21Q7={6}, thV22Pct={7}, thLink={8}, profile={9}, activeRegular={10}, activeHash={11}.",
                operationLabel,
                cad.Pads.Count,
                grid.Pads.Count,
                exportSettings.Notch.ComputationMode,
                exportSettings.Notch.CompensationModel,
                string.Join(",", exportSettings.Notch.EnabledVersions
                    .OrderBy(static version => (int)version)
                    .Select(static version => version.ToDisplayLabel())),
                exportSettings.Notch.ThresholdQ7,
                exportSettings.Notch.ThresholdPercentV22,
                exportSettings.Notch.LinkVersionThresholds ? "Y" : "N",
                exportSettings.Notch.ExportProfile,
                activeRegularPadIds?.Count ?? 0,
                activeRegularHash);

            NotchExportGenerationCacheLookup cacheLookup;
            if (exportSettings.Notch.ComputationMode == NotchComputationMode.CadAllocation)
            {
                var batchLease = _notchExportGenerationCacheService.AcquireCadAllocationResolvedBatch(
                    generationEpoch,
                    cadFingerprint,
                    gridFingerprint,
                    step3Revision,
                    settingsFingerprint,
                    exportSettings,
                    () => Task.Run(() => _notchExportService.ResolveCadAllocationBatch(
                        cad,
                        grid,
                        exportSettings,
                        effectiveProgress,
                        activeRegularPadIds,
                        workflowSnapshot.CadOutputFwDiffIndexByCadId,
                        selectedSparseResultRequest)));
                cacheLookup = batchLease.Lookup;
                var isBatchCacheHit = batchLease.CachedBatch is not null;
                if (batchLease.CachedBatch is { } cachedBatch)
                {
                    resolvedBatch = cachedBatch;
                }
                else
                {
                    if (batchLease.IsTaskOwner)
                    {
                        LogNotchGenerationCacheMiss(operationLabel, cacheLookup);
                    }

                    resolvedBatch = await batchLease.ResolutionTask!;
                }

                table = await Task.Run(() => _notchExportService.ProjectCadAllocationResolvedBatch(
                    resolvedBatch,
                    exportSettings,
                    effectiveProgress,
                    includeResolutionTimings: !isBatchCacheHit));
                if (isBatchCacheHit)
                {
                    Logger.Info(
                        CultureInfo.InvariantCulture,
                        "{0} reused cached CAD-allocation batch: projectedRows={1}, step3Revision={2}, settingsFingerprint={3}.",
                        operationLabel,
                        table.Rows.Count,
                        step3Revision,
                        settingsFingerprint);
                }
            }
            else if (_notchExportGenerationCacheService.TryGet(
                         cadFingerprint,
                         gridFingerprint,
                         step3Revision,
                         settingsFingerprint,
                         out var cachedTable,
                         out cacheLookup))
            {
                table = cachedTable;
                Logger.Info(
                    CultureInfo.InvariantCulture,
                    "{0} reused cached notch table: rows={1}, step3Revision={2}, settingsFingerprint={3}.",
                    operationLabel,
                    table.Rows.Count,
                    step3Revision,
                    settingsFingerprint);
            }
            else
            {
                LogNotchGenerationCacheMiss(operationLabel, cacheLookup);
                table = await Task.Run(() => _notchExportService.Generate(
                    cad,
                    grid,
                    exportSettings,
                    effectiveProgress,
                    activeRegularPadIds,
                    workflowSnapshot.CadOutputFwDiffIndexByCadId));

                if (table.Rows.Count > 0)
                {
                    legacyTableToStore = table;
                }
            }

            var projection = NotchCadOutputFwDiffProjectionService.Project(
                grid,
                table,
                workflowSnapshot.CadOutputFwDiffIndexByCadId);
            var projectedTable = projection.Table;
            var projectedGrid = projection.Grid;
            var effectiveTable = projectToCadOutputFwDiff ? projectedTable : table;
            var effectiveGrid = projectToCadOutputFwDiff ? projectedGrid : grid;

            var stage2ElapsedMs = (long)(DateTime.UtcNow - stage2StartedAt).TotalMilliseconds;
            var crossIcMismatch = BuildCrossIcMismatchSnapshot(grid);
            Logger.Info(
                CultureInfo.InvariantCulture,
                "{0} metrics: elapsed={1} ms, cache={2}, crossIcCad={3}, crossIcRegular={4}, shiftedRows={5}, shiftedAnchors={6}, conflictedRawDiffKeys={7}, projectionMode={8}.",
                operationLabel,
                stage2ElapsedMs,
                cacheLookup.Status,
                crossIcMismatch.CadCount,
                crossIcMismatch.RegularCount,
                projection.ChangedRowCount,
                projection.ChangedAnchorCount,
                projection.ConflictedRawDiffKeyCount,
                projectToCadOutputFwDiff ? "visible" : "raw");
            if (effectiveTable.GenerationPhaseTimings.HasData)
            {
                var phaseTimings = effectiveTable.GenerationPhaseTimings;
                Logger.Info(
                    CultureInfo.InvariantCulture,
                    "{0} phase timings: profiles={1} ms, candidates={2} ms, merge={3} ms, legacy={4} ms, finalize={5} ms, total={6} ms.",
                    operationLabel,
                    phaseTimings.BuildProfilesElapsedMs,
                    phaseTimings.BuildCanonicalCandidatesElapsedMs,
                    phaseTimings.MergeCanonicalCandidatesElapsedMs,
                    phaseTimings.BuildLegacyRowsElapsedMs,
                    phaseTimings.FinalizeCanonicalExportsElapsedMs,
                    phaseTimings.TotalElapsedMs);
                if (phaseTimings.CandidateBreakdown?.HasData == true)
                {
                    var candidateBreakdown = phaseTimings.CandidateBreakdown;
                    var compensationStages = candidateBreakdown.CompensationStageTimings;
                    var stageCBreakdown = compensationStages.StageCBreakdown;
                    Logger.Info(
                        CultureInfo.InvariantCulture,
                        "{0} candidate breakdown: count={1}, buildCadCandidate={2} ms, compensation={3} ms, postCompute={4} ms, stageA={5} ms, stageB={6} ms, stageC={7} ms, stageD={8} ms.",
                        operationLabel,
                        candidateBreakdown.CandidateCount,
                        candidateBreakdown.BuildV22CadCandidateElapsedMs,
                        candidateBreakdown.CompensationComputeElapsedMs,
                        candidateBreakdown.CandidatePostComputeElapsedMs,
                        compensationStages.StageAElapsedMs,
                        compensationStages.StageBElapsedMs,
                        compensationStages.StageCElapsedMs,
                        compensationStages.StageDElapsedMs);
                    if (stageCBreakdown?.HasData == true)
                    {
                        Logger.Info(
                            CultureInfo.InvariantCulture,
                            "{0} stageC breakdown: ownerLookup={1} ms, occupiedArea={2} ms, sourceCoverage={3} ms, reachability={4} ms, ruleDecision={5} ms, previewDebug={6} ms.",
                            operationLabel,
                            stageCBreakdown.OwnerAndBlockerLookupElapsedMs,
                            stageCBreakdown.ExactOccupiedAreaElapsedMs,
                            stageCBreakdown.SourceCoverageElapsedMs,
                            stageCBreakdown.ReachabilityElapsedMs,
                            stageCBreakdown.RuleDecisionElapsedMs,
                            stageCBreakdown.PreviewAndDebugElapsedMs);
                    }
                }
            }
            if (crossIcMismatch.CadCount > 0)
            {
                Logger.Warn(
                    CultureInfo.InvariantCulture,
                    "{0} cross-IC mismatch detected: cadCount={1}, regularCount={2}, sample={3}.",
                    operationLabel,
                    crossIcMismatch.CadCount,
                    crossIcMismatch.RegularCount,
                    string.IsNullOrWhiteSpace(crossIcMismatch.SampleCadText) ? "-" : crossIcMismatch.SampleCadText);
            }

            if (!_notchExportGenerationCacheService.IsGenerationEpochCurrent(generationEpoch) ||
                !IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
            {
                LogSupersededNotchGeneration(operationLabel);
                return null;
            }

            var cacheAccepted = resolvedBatch is not null
                ? _notchExportGenerationCacheService.TryUpdateProjectedRowCount(
                    generationEpoch,
                    resolvedBatch,
                    table.Rows.Count)
                : legacyTableToStore is not null
                    ? _notchExportGenerationCacheService.TryStore(
                        generationEpoch,
                        cadFingerprint,
                        gridFingerprint,
                        step3Revision,
                        settingsFingerprint,
                        legacyTableToStore)
                    : _notchExportGenerationCacheService.IsGenerationEpochCurrent(generationEpoch);
            if (!cacheAccepted ||
                !IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
            {
                LogSupersededNotchGeneration(operationLabel);
                return null;
            }

            if (resolvedBatch is not null && selectedSparseResultRequest is not null)
            {
                var promotionWorkflowSnapshot = BuildWorkflowDataSnapshot();
                var selectedResolved = NotchSparseResultUseCase.GetCurrentResult(
                    resolvedBatch,
                    selectedSparseResultRequest,
                    _selectedCadIds,
                    _grid,
                    grid,
                    promotionWorkflowSnapshot);
                if (selectedResolved is not null && TryResolveNotchComputationInputs(
                        selectedSparseResultRequest.CadPadId,
                        out var currentCadPad,
                        out var currentCadPads,
                        out var currentActiveRegularPadIds))
                {
                    _ = GetOrBuildCadV22ResolvedResultCached(
                        currentCadPad,
                        currentCadPads,
                        currentActiveRegularPadIds,
                        promotionWorkflowSnapshot,
                        precomputedResolved: selectedResolved);
                }
            }

            _lastGeneratedNotchTable = effectiveTable;
            Logger.Info(
                CultureInfo.InvariantCulture,
                "{0} generated {1} row(s).",
                operationLabel,
                effectiveTable.Rows.Count);
            if (reportStep5Progress)
            {
                NotchExportProgress = 1.0;
                NotchExportProgressText = $"{operationLabel}: generated {effectiveTable.Rows.Count} row(s).";
            }

            return new NotchTableGenerationResult(
                effectiveTable,
                effectiveGrid,
                table,
                grid,
                projectedTable,
                projectedGrid,
                exportSettings,
                crossIcMismatch,
                sourceRevision);
        }
        finally
        {
            if (reportStep5Progress)
            {
                EndNotchOperationBusyScope();
            }
        }
    }

    private void BeginNotchOperationBusyScope()
    {
        Interlocked.Increment(ref _notchOperationBusyScopeCount);
        IsNotchExporting = true;
    }

    private void EndNotchOperationBusyScope()
    {
        IsNotchExporting = Interlocked.Decrement(ref _notchOperationBusyScopeCount) > 0;
    }

    private bool IsNotchFinalProjectionCurrent(int finalProjectionRevision, int sourceRevision)
    {
        return Volatile.Read(ref _notchFinalProjectionRevision) == finalProjectionRevision &&
               SimulationWorkspaceSourceRevision == sourceRevision;
    }

    private static void LogSupersededNotchGeneration(string operationLabel)
    {
        Logger.Info(
            CultureInfo.InvariantCulture,
            "{0} generation superseded because its request identity changed.",
            operationLabel);
    }

    private static void LogNotchGenerationCacheMiss(
        string operationLabel,
        NotchExportGenerationCacheLookup cacheLookup)
    {
        Logger.Debug(
            CultureInfo.InvariantCulture,
            "{0} cache miss: status={1}, requested(cad=0x{2:X},grid=0x{3:X},rev={4},settings={5}), " +
            "cached(cad=0x{6:X},grid=0x{7:X},rev={8},settings={9}).",
            operationLabel,
            cacheLookup.Status,
            cacheLookup.RequestedCadSignature,
            cacheLookup.RequestedGridSignature,
            cacheLookup.RequestedStep3Revision,
            cacheLookup.RequestedSettingsFingerprint,
            cacheLookup.CachedCadSignature,
            cacheLookup.CachedGridSignature,
            cacheLookup.CachedStep3Revision,
            cacheLookup.CachedSettingsFingerprint);
    }
}

