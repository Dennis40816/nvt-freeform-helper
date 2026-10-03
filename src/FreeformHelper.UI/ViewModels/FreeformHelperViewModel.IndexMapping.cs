using System.Globalization;
using FreeformHelper.Application.Services;
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
                    DxfRegularMappingSummary = "Diagnostics: no CAD output pads.";
                    DxfRegularMappingIssues.Clear();
                    HasDxfRegularMappingIssues = false;
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
                DxfRegularMappingSummary = "Diagnostics: running...";
                HasDxfRegularMappingIssues = false;
                DxfRegularMappingIssues.Clear();
                SetStatus("Running mapping diagnostics...");
                Logger.Info(CultureInfo.InvariantCulture, "Analyzing DXF/Regular mapping (cadVisible={0}, regular={1}).", cadVisible.Pads.Count, grid.Pads.Count);
            },
            operationAsync: async reportProgress =>
            {
                var workflowSnapshot = BuildWorkflowDataSnapshot();
                var res = await Task.Run(() => _dxfRegularMappingUseCase.Analyze(
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

                var issueMessages = res.Report.SampleIssues.Select(i => $"[{i.Kind}] {i.Message}").ToList();

                DxfRegularMappingIssues.Clear();
                foreach (var message in issueMessages)
                {
                    DxfRegularMappingIssues.Add(message);
                }

                HasDxfRegularMappingIssues = (res.Report.HasIssues && res.Report.SampleIssues.Count > 0) ||
                                            res.Report.MaskChangedCount > 0 ||
                                            res.Report.MaskRemovedCount > 0;
                DxfRegularMappingSummary = res.Report.HasIssues
                    ? $"Diagnostics warnings: {res.Report.Summary}"
                    : $"Diagnostics OK: {res.Report.Summary}";

                SetStatus(BuildMappingStatusForWorkspaceHeader(res.Report));

                if (OpenIndexMappingReportAsync is not null &&
                    (res.Report.TotalIssueCount > 0 ||
                     (res.Report.MaskAuditRows?.Count ?? 0) > 0))
                {
                    await OpenIndexMappingReportAsync(new IndexMappingReportViewModel(
                        DxfRegularMappingSummary,
                        res.Report,
                        LocateMappingTargetFromReport,
                        ApplyMappingOverrideFromReport,
                        ApplyCadOutputFwDiffOverrideFromReport,
                        ClearMappingOverrideFromReport,
                        GetMappingOverrideRegularIndex));
                }

                if (res.Report.HasIssues)
                {
                    Logger.Warn(CultureInfo.InvariantCulture, "DXF/Regular mapping warnings: {0}", res.Report.Summary);
                    foreach (var issue in res.Report.SampleIssues.Take(10))
                    {
                        Logger.Warn(CultureInfo.InvariantCulture, "Mapping issue: {0}", issue.Message);
                    }
                }
                else
                {
                    Logger.Info(CultureInfo.InvariantCulture, "DXF/Regular mapping OK: {0}", res.Report.Summary);
                }

                OnWorkflowStepCompleted(
                    WorkflowStepId.Step4IndexDiagnostics,
                    invalidateDownstream: false,
                    moveToNextStep: true);
            },
            onError: ex =>
            {
                SetStatusError("Mapping analyze failed", ex);
                Logger.Error(ex, "Mapping analyze failed.");
            });
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
        Logger.Info(CultureInfo.InvariantCulture, "Mapping override saved: CAD {0} -> regular id {1}.", cadId, regularIndex);
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
        Logger.Info(CultureInfo.InvariantCulture, "Mapping override cleared for CAD {0}.", cadId);
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
        Logger.Info(CultureInfo.InvariantCulture, "Diff repair override applied: CAD {0} -> diff {1}.", cadId, diffIndex);
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
}

