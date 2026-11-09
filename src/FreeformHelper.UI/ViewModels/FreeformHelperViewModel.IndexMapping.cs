using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task AnalyzeIndexMappingAsync()
    {
        if (!TryGetOperationCadAndGrid(
                BuildFilteredCadPadSet,
                "Step 4: import DXF and build grid first.",
                onCadUnavailable: () =>
                {
                    MappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun() with
                    {
                        Summary = "Diagnostics: no CAD output pads.",
                    };
                    SetStatus(DxfRegularMappingSummary);
                },
                out var cadVisible,
                out var grid))
        {
            return;
        }
        await RunUiProgressOperationAsync(
            operationName: "DXF/Regular mapping analysis",
            setBusy: busy => IsAnalyzingIndexMapping = busy,
            setProgress: progress => IndexMappingProgress = progress,
            onStart: () =>
            {
                ApplyUiToSettings(_projectFile.Settings);
                InvalidateDownstreamFromStep4(showStatus: false);
                MappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun() with
                {
                    Summary = "Diagnostics: running...",
                };
                SetStatus("Running mapping diagnostics...");
                _logger.Info(CultureInfo.InvariantCulture, "Analyzing DXF/Regular mapping (cadVisible={0}, regular={1}).", cadVisible.Pads.Count, grid.Pads.Count);
            },
            operationAsync: AnalyzeMappingAsync,
            onError: ex =>
            {
                SetStatusError("Mapping analyze failed", ex);
                _logger.Error(ex, "Mapping analyze failed.");
            });
        async Task AnalyzeMappingAsync(Action<double> reportProgress)
        {
            var workflowSnapshot = BuildWorkflowDataSnapshot();
            var res = await Task.Run(() => DxfRegularMappingUseCase.Analyze(
                cadVisible,
                grid,
                _projectFile.Settings,
                _projectFile.DxfRegularMappingOverrides,
                workflowSnapshot.CadOutputFwDiffIndexByCadId,
                workflowSnapshot.CadToRegularByCadId,
                workflowSnapshot.CadIcIndexByCadId,
                workflowSnapshot.ActiveRegularVisibilityMaskPadIds,
                workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId,
                reportProgress));

            ApplyMappingDiagnosticsReport(res.Report);

            if (OpenIndexMappingReportAsync is not null &&
                (res.Report.TotalIssueCount > 0 ||
                 (res.Report.MaskAuditRows?.Count ?? 0) > 0))
            {
                var reportViewModel = CreateIndexMappingReportViewModel(res.Report);
                await OpenIndexMappingReportAsync(reportViewModel);
            }

            LogMappingDiagnosticsReport(res.Report);

            OnWorkflowStepCompleted(
                WorkflowStepId.Step4IndexDiagnostics,
                invalidateDownstream: false,
                moveToNextStep: true);
        }
    }

    private void ApplyMappingDiagnosticsReport(DxfRegularMappingReport report)
    {
        var issueMessages = report.SampleIssues.Select(i => $"[{i.Kind}] {i.Message}").ToList();

        MappingDiagnosticsResult = new MappingDiagnosticsResult(
            report.HasIssues
                ? $"Diagnostics warnings: {report.Summary}"
                : $"Diagnostics OK: {report.Summary}",
            new ObservableCollection<string>(issueMessages),
            HasIssues: (report.HasIssues && report.SampleIssues.Count > 0) ||
                       report.MaskChangedCount > 0 ||
                       report.MaskRemovedCount > 0);

        SetStatus(BuildMappingStatusForWorkspaceHeader(report));
    }

    private IndexMappingReportViewModel CreateIndexMappingReportViewModel(DxfRegularMappingReport report)
    {
        var reportViewModel = new IndexMappingReportViewModel(
            DxfRegularMappingSummary,
            report,
            LocateMappingTargetFromReport,
            ApplyMappingOverrideFromReport,
            ApplyCadOutputFwDiffOverrideFromReport,
            ClearMappingOverrideFromReport,
            GetMappingOverrideRegularIndex);
        reportViewModel.Editing.AttachProject(this);
        return reportViewModel;
    }

    private static void LogMappingDiagnosticsReport(DxfRegularMappingReport report)
    {
        if (report.HasIssues)
        {
            _logger.Warn(CultureInfo.InvariantCulture, "DXF/Regular mapping warnings: {0}", report.Summary);
            foreach (var issue in report.SampleIssues.Take(10))
            {
                _logger.Warn(CultureInfo.InvariantCulture, "Mapping issue: {0}", issue.Message);
            }
        }
        else
        {
            _logger.Info(CultureInfo.InvariantCulture, "DXF/Regular mapping OK: {0}", report.Summary);
        }
    }

    private bool LocateMappingTargetFromReport(int? cadPadId, int? regularPadIndex)
    {
        var cadIds = new List<int>(1);
        var regularIndices = new List<int>(1);

        if (cadPadId is int cadId && CadPads.Any(p => p.Id == cadId))
        {
            cadIds.Add(cadId);
        }

        if (regularPadIndex is int regularIndex && RegularPads.Any(p => p.RegularPadId == regularIndex))
        {
            regularIndices.Add(regularIndex);
        }

        if (cadIds.Count == 0 && regularIndices.Count == 0)
        {
            return false;
        }

        return TryLocateSelection(
            cadIds,
            regularIndices,
            focusSelection: true,
            notVisibleStatus: "Mapping issue locate: target not visible.",
            successStatusBuilder: (visibleCadCount, visibleRegularCount) =>
                $"Mapping diagnostics focused: CAD={visibleCadCount}, Regular={visibleRegularCount}.");
    }

    private bool ApplyMappingOverrideFromReport(int cadId, int regularIndex)
    {
        if (RegularPads.All(p => p.RegularPadId != regularIndex))
        {
            SetStatus("Mapping override: target regular pad not in current grid.");
            return false;
        }

        _projectFile.DxfRegularMappingOverrides[cadId] = regularIndex;
        InvalidateDownstreamFromStep4(showStatus: false);
        MarkUnsaved();
        SetStatus($"Mapping override saved: CAD {cadId} -> regular id {regularIndex}.");
        _logger.Info(CultureInfo.InvariantCulture, "Mapping override saved: CAD {0} -> regular id {1}.", cadId, regularIndex);
        return true;
    }

    private bool ClearMappingOverrideFromReport(int cadId)
    {
        if (!_projectFile.DxfRegularMappingOverrides.Remove(cadId))
        {
            SetStatus($"Mapping override clear: CAD {cadId} has no saved override.");
            return false;
        }

        InvalidateDownstreamFromStep4(showStatus: false);
        MarkUnsaved();
        SetStatus($"Mapping override cleared: CAD {cadId}.");
        _logger.Info(CultureInfo.InvariantCulture, "Mapping override cleared for CAD {0}.", cadId);
        return true;
    }

    private bool ApplyCadOutputFwDiffOverrideFromReport(int cadId, int diffIndex)
    {
        if (CadPads.All(pad => pad.Id != cadId))
        {
            SetStatus($"Diff repair override ignored: CAD {cadId} is not visible.");
            return false;
        }

        SetCadOutputFwDiffIndexOverride(cadId, diffIndex);
        _logger.Info(CultureInfo.InvariantCulture, "Diff repair override applied: CAD {0} -> diff {1}.", cadId, diffIndex);
        return true;
    }

    private int? GetMappingOverrideRegularIndex(int cadPadId)
    {
        return _projectFile.DxfRegularMappingOverrides.TryGetValue(cadPadId, out var regularIndex)
            ? regularIndex
            : null;
    }

    private static string BuildMappingStatusForWorkspaceHeader(DxfRegularMappingReport report)
    {
        if (!report.HasIssues)
        {
            var okText = $"Diagnostics OK: map={report.MappedCount}, low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}.";
            return ClampWorkspaceHeaderStatus(okText);
        }

        var warningText =
            $"Diagnostics warn: map={report.MappedCount}, unmapCad={report.UnmappedCadCount}, unmapReg={report.UnmappedRegularCount}, " +
            $"low={report.LowConfidenceCount}, amb={report.AmbiguousCount}, dup={report.DuplicateDiffCount}, mask={report.MaskChangedCount}/{report.MaskRemovedCount}.";
        return ClampWorkspaceHeaderStatus(warningText);
    }

    private static string ClampWorkspaceHeaderStatus(string statusText)
    {
        const int maxChars = 100;
        if (statusText.Length <= maxChars)
        {
            return statusText;
        }

        return string.Concat(statusText.AsSpan(0, maxChars - 1), "…");
    }

    partial void OnMappingDiagnosticsResultChanged(MappingDiagnosticsResult? oldValue, MappingDiagnosticsResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, nameof(DxfRegularMappingSummary));
        RaiseWhenChanged(oldValue.Issues, newValue.Issues, nameof(DxfRegularMappingIssues));
        RaiseWhenChanged(oldValue.HasIssues, newValue.HasIssues, nameof(HasDxfRegularMappingIssues));
    }

    // --- DXF/Regular Mapping Properties ---

    /// <summary>
    /// Gets or sets the latest DXF/Regular mapping diagnostics result. The view model replaces the
    /// whole value. The properties that forward to it raise their change notification when their
    /// own value changed.
    /// </summary>
    [ObservableProperty] private MappingDiagnosticsResult _mappingDiagnosticsResult = MappingDiagnosticsResult.CreateNotRun();
    /// <summary>
    /// Gets the summary message for DXF/Regular mapping analysis.
    /// </summary>
    public string DxfRegularMappingSummary => MappingDiagnosticsResult.Summary;

    /// <summary>
    /// Gets or sets a value indicating whether index mapping diagnostics are currently running.
    /// </summary>
    public bool IsAnalyzingIndexMapping
    {
        get => _indexMappingState.Current.IsBusy;
        set => SetOperationBusy(_indexMappingState, value, nameof(IsAnalyzingIndexMapping));
    }

    /// <summary>
    /// Gets or sets the current progress value (0-1) for index mapping diagnostics.
    /// </summary>
    public double IndexMappingProgress
    {
        get => _indexMappingState.Current.Progress;
        set => SetOperationProgress(_indexMappingState, value, nameof(IndexMappingProgress));
    }

    /// <summary>
    /// Gets the observable collection of detailed DXF/Regular mapping issues.
    /// </summary>
    public ObservableCollection<string> DxfRegularMappingIssues => MappingDiagnosticsResult.Issues;

    /// <summary>
    /// Gets a value indicating whether any DXF/Regular mapping issues were found.
    /// </summary>
    public bool HasDxfRegularMappingIssues => MappingDiagnosticsResult.HasIssues;

    private void UpdateCadOutputFwDiffIndexing(IReadOnlyList<CadPad> visiblePads)
    {
        var ordered = DxfIndexAssigner.OrderPads(visiblePads, _grid, SelectedScanOrder);
        CadPads = new ObservableCollection<CadPad>(ordered);
        _cachedFilteredCadForBuild = new CadPadSet(ordered);
        RebuildVisibleDxfIndexMap(ordered);
        BumpNotchExportCadFingerprint();
    }

    private void RebuildVisibleDxfIndexMap(IReadOnlyList<CadPad> ordered)
    {
        _cadOutputFwDiffIndexByCadId.Clear();
        _cadDisplayIndexByCadId.Clear();
        _cadIcIndexByCadId.Clear();
        if (ordered.Count == 0)
        {
            ResetVisibleDxfIndexMapToEmpty();
            return;
        }

        var displayOrdered = DxfIndexAssigner.OrderPads(ordered, _grid, ScanOrder.LeftToRight_TopToBottom);
        for (var i = 0; i < displayOrdered.Count; i++)
        {
            _cadDisplayIndexByCadId[displayOrdered[i].Id] = i + 1;
        }

        var padsByIc = BuildPadsByIc(ordered);
        MigrateLegacyAnchorToPerIc(padsByIc);
        var autoMode = CadOutputFwDiffAutoMode;
        var strictMatchDiffByCadId = BuildCadStrictMatchDiffById(ordered);

        var assignment = CadOutputFwDiffIndexAssignmentService.Assign(
            ordered,
            _grid,
            _projectFile.Settings.Grid,
            autoMode,
            strictMatchDiffByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides,
            _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc);

        foreach (var pair in assignment.CadIcIndexByCadId)
        {
            _cadIcIndexByCadId[pair.Key] = pair.Value;
        }

        foreach (var pair in assignment.AssignedIndexByCadId)
        {
            _cadOutputFwDiffIndexByCadId[pair.Key] = pair.Value;
        }
        RefreshLatestCadOutputFwDiffAssignmentDecisions(ordered, allowCsvRepairAutoApply: true);

        if (assignment.DuplicateOverrides > 0 ||
            assignment.InvalidOverrides > 0 ||
            assignment.AutoConflictCount > 0 ||
            assignment.AutoUnmatchedCount > 0 ||
            assignment.OverrideMismatchCount > 0)
        {
            _logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff assignment issues. mode={0}, duplicateOverrides={1}, invalidOverrides={2}, autoConflicts={3}, autoUnmatched={4}, overrideMismatch={5}.",
                autoMode,
                assignment.DuplicateOverrides,
                assignment.InvalidOverrides,
                assignment.AutoConflictCount,
                assignment.AutoUnmatchedCount,
                assignment.OverrideMismatchCount);
            if (assignment.ConflictSamples.Count > 0)
            {
                _logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff conflicts sample: {0}", string.Join("; ", assignment.ConflictSamples));
            }

            if (assignment.UnmatchedSamples.Count > 0)
            {
                _logger.Warn(CultureInfo.InvariantCulture, "Auto CAD Output FW Diff unmatched sample: {0}", string.Join("; ", assignment.UnmatchedSamples));
            }
        }

        RefreshCadOutputFwDiffVisualHints(
            ordered,
            padsByIc,
            autoMode,
            assignment);
        RefreshStep4DuplicateDiffGroups(ordered);
        BumpNotchExportIndexFingerprint();
        InvalidateWorkflowDataSnapshot();
    }

    private void ResetVisibleDxfIndexMapToEmpty()
    {
        CadOutputFwDiffIndexOverrideCadIds = new ObservableCollection<int>();
        CadOutputFwDiffIndexAnchorCadIdsForCanvas = new ObservableCollection<int>();
        CadOutputFwDiffAssignmentSummary = "CAD Output FW Diff assignment: no CAD output pads.";
        SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
        RefreshStep4DuplicateDiffGroups(Array.Empty<CadPad>());
        BumpNotchExportIndexFingerprint();
        InvalidateWorkflowDataSnapshot();
    }

    private Dictionary<int, int?> BuildCadStrictMatchDiffById(IReadOnlyList<CadPad> ordered)
    {
        if (_grid is null || _workflowData.LatestPadMatch.CadToRegular.Count == 0)
        {
            return ordered.ToDictionary(static pad => pad.Id, static _ => (int?)null);
        }

        return CadBestMatchSeedService.BuildSeedByCadId(
            ordered,
            _workflowData.LatestPadMatch.CadToRegular,
            _grid.Pads.ToDictionary(pad => pad.RegularPadId),
            _cadIcIndexByCadId,
            GetActiveRegularVisibilityMaskPadIds());
    }

    private Dictionary<int, List<CadPad>> BuildPadsByIc(IReadOnlyList<CadPad> ordered)
    {
        var groups = new Dictionary<int, List<CadPad>>();
        foreach (var pad in ordered)
        {
            var ic = DxfIcResolver.Resolve(_grid, _projectFile.Settings.Grid, pad);
            _cadIcIndexByCadId[pad.Id] = ic;
            if (!groups.TryGetValue(ic, out var list))
            {
                list = new List<CadPad>();
                groups[ic] = list;
            }

            list.Add(pad);
        }

        return groups;
    }

    private void MigrateLegacyAnchorToPerIc(Dictionary<int, List<CadPad>> padsByIc)
    {
        var legacyAnchorCadId = _projectFile.CadOutputFwDiffIndexAnchorCadPadId;
        if (!legacyAnchorCadId.HasValue)
        {
            return;
        }

        var anchorsByIc = _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc;
        if (anchorsByIc.Count > 0)
        {
            _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
            return;
        }

        if (!_cadIcIndexByCadId.TryGetValue(legacyAnchorCadId.Value, out var icIndex))
        {
            return;
        }

        if (!padsByIc.TryGetValue(icIndex, out var pads) || pads.All(p => p.Id != legacyAnchorCadId.Value))
        {
            return;
        }

        anchorsByIc[icIndex] = legacyAnchorCadId.Value;
        _projectFile.CadOutputFwDiffIndexAnchorCadPadId = null;
    }

    private void RefreshCadOutputFwDiffVisualHints(
        IReadOnlyList<CadPad> ordered,
        Dictionary<int, List<CadPad>> padsByIc,
        CadOutputFwDiffAutoMode autoMode,
        CadOutputFwDiffIndexAssignmentResult assignment)
    {
        var hints = DxfIndexHintsBuilder.Build(
            ordered.Select(p => p.Id).ToHashSet(),
            _projectFile.CadOutputFwDiffIndexOverrides,
            _projectFile.CadOutputFwDiffIndexAnchorCadPadByIc,
            SelectedScanOrder,
            padsByIc.Count,
            autoMode,
            assignment);
        CadOutputFwDiffIndexOverrideCadIds = new ObservableCollection<int>(hints.OverrideCadIds);
        CadOutputFwDiffIndexAnchorCadIdsForCanvas = new ObservableCollection<int>(hints.AnchorCadIdsForCanvas);
        CadOutputFwDiffAssignmentSummary = hints.Summary;
    }

    private void RefreshLatestCadOutputFwDiffAssignmentDecisions(
        IReadOnlyList<CadPad> ordered,
        bool allowCsvRepairAutoApply = false)
    {
        if (_grid is null || _workflowData.LatestPadMatch.CadToRegular.Count == 0)
        {
            SetLatestCadOutputFwDiffAssignmentDecisions(new Dictionary<int, CadOutputFwDiffAssignmentDecision>());
            return;
        }

        var regularPadById = _grid.Pads.ToDictionary(static pad => pad.RegularPadId);
        var decisions = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _workflowData.LatestPadMatch.CadToRegular,
            regularPadById,
            _cadIcIndexByCadId,
            GetActiveRegularVisibilityMaskPadIds(),
            _cadOutputFwDiffIndexByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides);
        if (allowCsvRepairAutoApply)
        {
            decisions = TryAutoApplyCsvRepairSuggestions(ordered, regularPadById, decisions);
        }

        SetLatestCadOutputFwDiffAssignmentDecisions(decisions);
    }

    private IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> TryAutoApplyCsvRepairSuggestions(
        IReadOnlyList<CadPad> ordered,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        var activeMaskPadIds = GetActiveRegularVisibilityMaskPadIds();
        if (activeMaskPadIds is null || activeMaskPadIds.Count == 0)
        {
            return decisions;
        }

        if (decisions.Count == 0)
        {
            return decisions;
        }

        var appliedCadIds = DxfCsvRepairRule.SelectRepairs(
            decisions,
            _projectFile.CadOutputFwDiffIndexOverrides,
            _cadOutputFwDiffIndexByCadId,
            _cadIcIndexByCadId);

        if (appliedCadIds.Count == 0)
        {
            return decisions;
        }

        var refreshed = DxfRegularMaskAuditService.BuildAssignmentDecisions(
            ordered,
            _workflowData.LatestPadMatch.CadToRegular,
            regularPadById,
            _cadIcIndexByCadId,
            activeMaskPadIds,
            _cadOutputFwDiffIndexByCadId,
            _projectFile.CadOutputFwDiffIndexOverrides);
        var refreshedByCadId = refreshed.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        foreach (var cadId in appliedCadIds)
        {
            if (!refreshedByCadId.TryGetValue(cadId, out var decision))
            {
                continue;
            }

            refreshedByCadId[cadId] = decision with
            {
                DecisionSource = CadOutputFwDiffAssignmentDecisionSource.AutoRepaired,
            };
        }

        _logger.Info(
            CultureInfo.InvariantCulture,
            "Auto-applied csv local repair suggestions: count={0}, samples={1}.",
            appliedCadIds.Count,
            string.Join(", ", appliedCadIds.Take(5)));
        return refreshedByCadId;
    }

}
