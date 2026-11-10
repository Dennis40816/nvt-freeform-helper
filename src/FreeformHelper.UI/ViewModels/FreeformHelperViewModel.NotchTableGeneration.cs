using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // The notch snapshot is owned and accessed only on the UI thread.
    private readonly UiNotchOperationState _notchOperationState = new();

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
        var finalProjectionRevision = _notchFinalProjection.Read();
        var sourceRevision = SimulationWorkspaceSourceRevision;
        var effectiveProgress = reportStep5Progress
            ? BeginStep5NotchGenerationProgress(operationLabel, progress, finalProjectionRevision, sourceRevision, progressTextReporter)
            : progress;

        try
        {
            var inputs = PrepareNotchGenerationInputs(cad, grid, workflowSnapshot);
            NotchTable table;
            NotchTableGenerator.CadAllocationResolvedBatch? resolvedBatch = null;
            NotchTable? legacyTableToStore = null;
            var stage2StartedAt = TimeProvider.System.GetTimestamp();

            LogNotchGenerationStarted(operationLabel, cad, grid, inputs);

            NotchExportGenerationCacheLookup cacheLookup;
            if (inputs.ExportSettings.Notch.ComputationMode == NotchComputationMode.CadAllocation)
            {
                var batchLease = AcquireNotchCadAllocationBatch(generationEpoch, cad, grid, inputs, effectiveProgress);
                cacheLookup = batchLease.Lookup;
                var isBatchCacheHit = batchLease.CachedBatch is not null;
                resolvedBatch = TakeCachedNotchBatchOrLogMiss(operationLabel, batchLease);
                if (resolvedBatch is null)
                {
                    resolvedBatch = await batchLease.ResolutionTask!.WaitAsync(CancellationToken.None);
                }

                table = await ProjectNotchCadAllocationBatchAsync(operationLabel, resolvedBatch, inputs, effectiveProgress, isBatchCacheHit);
            }
            else
            {
                (table, cacheLookup, legacyTableToStore) = await GenerateLegacyNotchTableAsync(
                    operationLabel, cad, grid, inputs, effectiveProgress);
            }

            var projection = NotchCadOutputFwDiffProjectionService.Project(grid, table, inputs.WorkflowSnapshot.CadOutputFwDiffIndexByCadId);
            var effectiveTable = projectToCadOutputFwDiff ? projection.Table : table;
            var effectiveGrid = projectToCadOutputFwDiff ? projection.Grid : grid;

            var stage2ElapsedMs = (long)TimeProvider.System.GetElapsedTime(stage2StartedAt).TotalMilliseconds;
            var crossIcMismatch = NotchCrossIcMismatch.BuildCrossIcMismatchSnapshot(grid);
            LogNotchGenerationMetrics(operationLabel, stage2ElapsedMs, cacheLookup, crossIcMismatch, projection, projectToCadOutputFwDiff, effectiveTable);

            if (!TryAcceptNotchGeneration(operationLabel, generationEpoch, finalProjectionRevision, sourceRevision, inputs, table, resolvedBatch, legacyTableToStore))
            {
                return null;
            }

            PromoteSelectedNotchSparseResult(resolvedBatch, inputs.SelectedSparseResultRequest, grid);
            CompleteNotchGeneration(operationLabel, effectiveTable, reportStep5Progress);
            return new NotchTableGenerationResult(
                effectiveTable, effectiveGrid, table, grid, projection.Table, projection.Grid,
                inputs.ExportSettings, crossIcMismatch, sourceRevision);
        }
        finally
        {
            if (reportStep5Progress)
            {
                EndNotchOperationBusyScope();
            }
        }
    }

    private IProgress<NotchGenerationProgress> BeginStep5NotchGenerationProgress(
        string operationLabel,
        IProgress<NotchGenerationProgress>? progress,
        long finalProjectionRevision,
        int sourceRevision,
        Action<string>? progressTextReporter)
    {
        IProgress<NotchGenerationProgress> step5Reporter = CreateStep5GenerationProgressReporter(
            operationLabel,
            finalProjectionRevision,
            sourceRevision,
            progressTextReporter);
        IProgress<NotchGenerationProgress> effectiveProgress = progress is null
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
        return effectiveProgress;
    }

    private NotchGenerationInputs PrepareNotchGenerationInputs(CadPadSet cad, RegularGrid grid, WorkflowDataSnapshot? workflowSnapshot)
    {
        ApplyUiToSettings(_projectFile.Settings);
        workflowSnapshot ??= BuildWorkflowDataSnapshot();
        var exportSettings = CreateExportSettingsSnapshot(_projectFile.Settings);
        var activeRegularPadIds = GetActiveRegularPadIdsForNotchComputation();
        var activeRegularHash = ComputeActiveRegularHash(activeRegularPadIds);
        var cadOutputFwDiffHash = NotchExportFingerprints.ComputeCadOutputFwDiffHash(workflowSnapshot.CadOutputFwDiffIndexByCadId);
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
            : NotchExportFingerprints.ComputeNotchExportSettingsFingerprint(exportSettings);
        var settingsFingerprint = NotchExportFingerprints.CombineNotchExportSettingsFingerprint(
            generationSettingsFingerprint,
            activeRegularHash,
            cadOutputFwDiffHash);
        var cadFingerprint = BuildNotchExportCadFingerprint(cad);
        var gridFingerprint = BuildNotchExportGridFingerprint(grid);
        return new NotchGenerationInputs(
            exportSettings,
            activeRegularPadIds,
            activeRegularHash,
            step3Revision,
            selectedSparseResultRequest,
            settingsFingerprint,
            cadFingerprint,
            gridFingerprint,
            workflowSnapshot);
    }

    private static void LogNotchGenerationStarted(string operationLabel, CadPadSet cad, RegularGrid grid, NotchGenerationInputs inputs)
    {
        _logger.Info(
            CultureInfo.InvariantCulture,
            "{0} generation started: cad={1}, regular={2}, mode={3}, compModel={4}, versions={5}, thV21Q7={6}, thV22Pct={7}, thLink={8}, profile={9}, activeRegular={10}, activeHash={11}.",
            operationLabel,
            cad.Pads.Count,
            grid.Pads.Count,
            inputs.ExportSettings.Notch.ComputationMode,
            inputs.ExportSettings.Notch.CompensationModel,
            string.Join(",", inputs.ExportSettings.Notch.EnabledVersions
                .OrderBy(static version => (int)version)
                .Select(static version => version.ToDisplayLabel())),
            inputs.ExportSettings.Notch.ThresholdQ7,
            inputs.ExportSettings.Notch.ThresholdPercentV22,
            inputs.ExportSettings.Notch.LinkVersionThresholds ? "Y" : "N",
            inputs.ExportSettings.Notch.ExportProfile,
            inputs.ActiveRegularPadIds?.Count ?? 0,
            inputs.ActiveRegularHash);
    }

    private NotchCadAllocationBatchLease AcquireNotchCadAllocationBatch(
        long generationEpoch,
        CadPadSet cad,
        RegularGrid grid,
        NotchGenerationInputs inputs,
        IProgress<NotchGenerationProgress>? effectiveProgress)
    {
        return _notchExportGenerationCacheService.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            inputs.CadFingerprint,
            inputs.GridFingerprint,
            inputs.Step3Revision,
            inputs.SettingsFingerprint,
            inputs.ExportSettings,
            () => Task.Run(() => _notchExportService.ResolveCadAllocationBatch(
                cad,
                grid,
                inputs.ExportSettings,
                effectiveProgress,
                inputs.ActiveRegularPadIds,
                inputs.WorkflowSnapshot.CadOutputFwDiffIndexByCadId,
                inputs.SelectedSparseResultRequest)));
    }

    private static NotchTableGenerator.CadAllocationResolvedBatch? TakeCachedNotchBatchOrLogMiss(
        string operationLabel,
        NotchCadAllocationBatchLease batchLease)
    {
        if (batchLease.CachedBatch is { } cachedBatch)
        {
            return cachedBatch;
        }

        if (batchLease.IsTaskOwner)
        {
            LogNotchGenerationCacheMiss(operationLabel, batchLease.Lookup);
        }

        return null;
    }

    private static async Task<NotchTable> ProjectNotchCadAllocationBatchAsync(
        string operationLabel,
        NotchTableGenerator.CadAllocationResolvedBatch resolvedBatch,
        NotchGenerationInputs inputs,
        IProgress<NotchGenerationProgress>? effectiveProgress,
        bool isBatchCacheHit)
    {
        var table = await Task.Run(() => NotchExportService.ProjectCadAllocationResolvedBatch(
            resolvedBatch,
            inputs.ExportSettings,
            effectiveProgress,
            includeResolutionTimings: !isBatchCacheHit));
        if (isBatchCacheHit)
        {
            _logger.Info(
                CultureInfo.InvariantCulture,
                "{0} reused cached CAD-allocation batch: projectedRows={1}, step3Revision={2}, settingsFingerprint={3}.",
                operationLabel,
                table.Rows.Count,
                inputs.Step3Revision,
                inputs.SettingsFingerprint);
        }

        return table;
    }

    private async Task<(NotchTable Table, NotchExportGenerationCacheLookup CacheLookup, NotchTable? LegacyTableToStore)> GenerateLegacyNotchTableAsync(
        string operationLabel,
        CadPadSet cad,
        RegularGrid grid,
        NotchGenerationInputs inputs,
        IProgress<NotchGenerationProgress>? effectiveProgress)
    {
        if (_notchExportGenerationCacheService.TryGet(
                inputs.CadFingerprint,
                inputs.GridFingerprint,
                inputs.Step3Revision,
                inputs.SettingsFingerprint,
                out var cachedTable,
                out var cacheLookup))
        {
            _logger.Info(
                CultureInfo.InvariantCulture,
                "{0} reused cached notch table: rows={1}, step3Revision={2}, settingsFingerprint={3}.",
                operationLabel,
                cachedTable.Rows.Count,
                inputs.Step3Revision,
                inputs.SettingsFingerprint);
            return (cachedTable, cacheLookup, null);
        }

        LogNotchGenerationCacheMiss(operationLabel, cacheLookup);
        var table = await Task.Run(() => _notchExportService.Generate(
            cad,
            grid,
            inputs.ExportSettings,
            effectiveProgress,
            inputs.ActiveRegularPadIds,
            inputs.WorkflowSnapshot.CadOutputFwDiffIndexByCadId));

        return (table, cacheLookup, table.Rows.Count > 0 ? table : null);
    }

    private static void LogNotchGenerationMetrics(
        string operationLabel,
        long stage2ElapsedMs,
        NotchExportGenerationCacheLookup cacheLookup,
        CrossIcMismatchSnapshot crossIcMismatch,
        NotchCadOutputFwDiffProjectionResult projection,
        bool projectToCadOutputFwDiff,
        NotchTable effectiveTable)
    {
        _logger.Info(
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
            LogNotchGenerationPhaseTimings(operationLabel, effectiveTable.GenerationPhaseTimings);
        }

        if (crossIcMismatch.CadCount > 0)
        {
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "{0} cross-IC mismatch detected: cadCount={1}, regularCount={2}, sample={3}.",
                operationLabel,
                crossIcMismatch.CadCount,
                crossIcMismatch.RegularCount,
                string.IsNullOrWhiteSpace(crossIcMismatch.SampleCadText) ? "-" : crossIcMismatch.SampleCadText);
        }
    }

    private static void LogNotchGenerationPhaseTimings(string operationLabel, NotchGenerationPhaseTimings phaseTimings)
    {
        _logger.Info(
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
            _logger.Info(
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
                _logger.Info(
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

    private bool TryAcceptNotchGeneration(
        string operationLabel,
        long generationEpoch,
        long finalProjectionRevision,
        int sourceRevision,
        NotchGenerationInputs inputs,
        NotchTable table,
        NotchTableGenerator.CadAllocationResolvedBatch? resolvedBatch,
        NotchTable? legacyTableToStore)
    {
        if (!_notchExportGenerationCacheService.IsGenerationEpochCurrent(generationEpoch) ||
            !IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
        {
            LogSupersededNotchGeneration(operationLabel);
            return false;
        }

        var cacheAccepted = resolvedBatch is not null
            ? _notchExportGenerationCacheService.TryUpdateProjectedRowCount(
                generationEpoch,
                resolvedBatch,
                table.Rows.Count)
            : legacyTableToStore is not null
                ? _notchExportGenerationCacheService.TryStore(
                    generationEpoch,
                    inputs.CadFingerprint,
                    inputs.GridFingerprint,
                    inputs.Step3Revision,
                    inputs.SettingsFingerprint,
                    legacyTableToStore)
                : _notchExportGenerationCacheService.IsGenerationEpochCurrent(generationEpoch);
        if (!cacheAccepted ||
            !IsNotchFinalProjectionCurrent(finalProjectionRevision, sourceRevision))
        {
            LogSupersededNotchGeneration(operationLabel);
            return false;
        }

        return true;
    }

    private void PromoteSelectedNotchSparseResult(
        NotchTableGenerator.CadAllocationResolvedBatch? resolvedBatch,
        NotchTableGenerator.CadAllocationSparseResultRequest? selectedSparseResultRequest,
        RegularGrid grid)
    {
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
    }

    private void CompleteNotchGeneration(string operationLabel, NotchTable effectiveTable, bool reportStep5Progress)
    {
        _notchCache.LastGeneratedTable = effectiveTable;
        _logger.Info(
            CultureInfo.InvariantCulture,
            "{0} generated {1} row(s).",
            operationLabel,
            effectiveTable.Rows.Count);
        if (reportStep5Progress)
        {
            NotchExportProgress = 1.0;
            NotchExportProgressText = $"{operationLabel}: generated {effectiveTable.Rows.Count} row(s).";
        }
    }

    private sealed record NotchGenerationInputs(
        ProjectSettings ExportSettings,
        HashSet<int>? ActiveRegularPadIds,
        int ActiveRegularHash,
        int Step3Revision,
        NotchTableGenerator.CadAllocationSparseResultRequest? SelectedSparseResultRequest,
        int SettingsFingerprint,
        ulong CadFingerprint,
        ulong GridFingerprint,
        WorkflowDataSnapshot WorkflowSnapshot);

    private void BeginNotchOperationBusyScope()
    {
        _notchOperationState.BeginBusyScope(value => IsNotchExporting = value);
    }

    private void EndNotchOperationBusyScope()
    {
        _notchOperationState.EndBusyScope(value => IsNotchExporting = value);
    }

    private bool IsNotchFinalProjectionCurrent(long finalProjectionRevision, int sourceRevision)
    {
        return _notchFinalProjection.Read() == finalProjectionRevision &&
               SimulationWorkspaceSourceRevision == sourceRevision;
    }

    private static void LogSupersededNotchGeneration(string operationLabel)
    {
        _logger.Info(
            CultureInfo.InvariantCulture,
            "{0} generation superseded because its request identity changed.",
            operationLabel);
    }

    private static void LogNotchGenerationCacheMiss(
        string operationLabel,
        NotchExportGenerationCacheLookup cacheLookup)
    {
        _logger.Debug(
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

