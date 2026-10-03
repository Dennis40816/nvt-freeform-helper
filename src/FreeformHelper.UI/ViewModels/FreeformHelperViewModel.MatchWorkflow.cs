using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Handles the asynchronous operation of matching regular pads to CAD pads.
    /// </summary>
    private async Task MatchAsync()
    {
        var status = CreateStatusScope("Match");
        if (!TryGetOperationCadAndGrid(
                BuildFilteredCadPadSet,
                "Step 1: import DXF and build grid first.",
                out var cad,
                out var grid))
        {
            return;
        }

        await RunUiProgressOperationAsync(
            operationName: "Match task",
            setBusy: busy => IsMatchingPads = busy,
            setProgress: progress => MatchProgress = progress,
            onStart: () =>
            {
                ApplyUiToSettings(_projectFile.Settings);
                status.ReportProgress("Matching pads...");
                Logger.Info(CultureInfo.InvariantCulture, "Matching pads.");
            },
            operationAsync: async reportProgress =>
            {
                await ApplyStep1MatchResultAsync(
                    cad,
                    grid,
                    reportProgress,
                    invalidateDownstream: true,
                    moveToStep2: true,
                    completionStatus: "Matching complete.");
                Logger.Info(CultureInfo.InvariantCulture, "Matching complete.");
            },
            onError: ex =>
            {
                status.ReportError("Match failed", ex);
                Logger.Error(ex, "Match failed.");
            });
    }

    private async Task ApplyStep1MatchResultAsync(
        CadPadSet cad,
        RegularGrid grid,
        Action<double> reportProgress,
        bool invalidateDownstream,
        bool moveToStep2,
        string? completionStatus)
    {
        var result = await Task.Run(() => _padMatchService.Match(
            cad,
            grid,
            _projectFile.Settings.Matching,
            reportProgress));

        SetLatestPadMatchResult(result);
        BumpNotchExportGridFingerprint();
        RebuildVisibleDxfIndexMap(CadPads.ToList());
        MatchLinksForCanvas = result.Links;
        var telemetry = result.Telemetry;
        Logger.Debug(CultureInfo.InvariantCulture, "PERF PADMATCH: cad={0}, candidateCells={1}, boundsIntersections={2}, polygonIntersections={3}, avgCandidatePerCad={4:0.##}, p95CandidatePerCad={5}.",
            telemetry.CadPadCount,
            telemetry.CandidateCellVisits,
            telemetry.BoundsIntersections,
            telemetry.PolygonIntersections,
            telemetry.CandidateCellsPerCadAverage,
            telemetry.CandidateCellsPerCadP95);

        OnWorkflowStepCompleted(
            WorkflowStepId.Step1Match,
            invalidateDownstream,
            moveToStep2);

        // H4: keep Step3 preview live when Step1 result changes and CAD selection already exists.
        if (_selectedCadIds.Count > 0)
        {
            RefreshNotchCanvasPreview(showStatus: false);
        }

        if (!string.IsNullOrWhiteSpace(completionStatus))
        {
            SetStatus(completionStatus);
        }

        NotifySimulationWorkspaceSourceChanged();
        CanvasHost?.Invalidate();
    }

    private async Task<bool> RestoreStep1MatchAfterProjectLoadAsync()
    {
        var status = CreateStatusScope("LoadProject.Step1Replay");
        if (!TryGetOperationCadAndGrid(
                BuildFilteredCadPadSet,
                "Project loaded: no CAD output pads; Step 1 auto replay skipped.",
                out var cad,
                out var grid))
        {
            return false;
        }

        ApplyUiToSettings(_projectFile.Settings);
        status.ReportProgress("Project loaded: replaying Step 1 match...");

        await ApplyStep1MatchResultAsync(
            cad,
            grid,
            _ => { },
            invalidateDownstream: false,
            moveToStep2: false,
            completionStatus: null);

        RefreshFreeformAutoDetectStats("project load");
        status.ReportSuccess("Project loaded: Step 1 match restored.");
        return true;
    }

    private async Task<bool> RestoreStep2FreeformAfterProjectLoadAsync()
    {
        var status = CreateStatusScope("LoadProject.Step2Replay");
        if (!AutoReplayStep2AfterProjectLoad)
        {
            return false;
        }

        if (_latestPadMatchResult.CadToRegular.Count == 0)
        {
            return false;
        }

        if (!TryGetOperationCadAndGrid(
                BuildActiveCadPadSet,
                "Project loaded: Step 2 auto replay skipped (CAD/grid missing).",
                out var cad,
                out var grid))
        {
            return false;
        }

        ApplyUiToSettings(_projectFile.Settings);
        var matchingSettings = CloneMatchingSettings(_projectFile.Settings.Matching);
        var assignments = await BuildStep2FreeformAssignmentsAsync(
            cad,
            grid,
            matchingSettings);
        CommitStep2FreeformResult(
            grid,
            assignments,
            source: "project load auto-detect",
            invalidateDownstream: true,
            moveToStep3: false,
            setStatusFromSummary: false,
            reapplyManualOverrides: true);
        status.ReportSuccess("Project loaded: Step 2 freeform restored.");
        return true;
    }

    /// <summary>
    /// Handles the asynchronous operation of automatically detecting and tagging freeform pads.
    /// </summary>
    private async Task AutoDetectFreeformsAsync()
    {
        if (!TryEnsureWorkflowStep(WorkflowStepId.Step2Freeform))
        {
            return;
        }

        if (!TryGetOperationCadAndGrid(
                BuildActiveCadPadSet,
                "Step 2: import DXF and run Step 1 first.",
                out var cad,
                out var grid))
        {
            return;
        }

        try
        {
            ApplyUiToSettings(_projectFile.Settings); // Apply current UI settings.
            var matchingSettings = CloneMatchingSettings(_projectFile.Settings.Matching);
            var assignments = await BuildStep2FreeformAssignmentsAsync(
                cad,
                grid,
                matchingSettings);
            CommitStep2FreeformResult(
                grid,
                assignments,
                source: "auto-detect",
                invalidateDownstream: true,
                moveToStep3: true,
                setStatusFromSummary: true,
                reapplyManualOverrides: false);
        }
        catch (Exception ex)
        {
            SetStatusError("Auto detect failed", ex);
        }

    }

    private async Task<IReadOnlyDictionary<int, FreeformType>> BuildStep2FreeformAssignmentsAsync(
        CadPadSet cad,
        RegularGrid grid,
        MatchingSettings matchingSettings)
    {
        return await Task.Run(() => FreeformTaggingUseCase.BuildAutoDetectAssignments(
            cad,
            grid,
            matchingSettings,
            _latestPadMatchResult));
    }

    private void CommitStep2FreeformResult(
        RegularGrid grid,
        IReadOnlyDictionary<int, FreeformType> assignments,
        string source,
        bool invalidateDownstream,
        bool moveToStep3,
        bool setStatusFromSummary,
        bool reapplyManualOverrides)
    {
        if (invalidateDownstream)
        {
            OnWorkflowStepChanged(WorkflowStepId.Step2Freeform, showStatus: false);
        }

        FreeformTaggingUseCase.ApplyAutoDetectAssignments(grid, assignments);
        if (reapplyManualOverrides)
        {
            PadOverrideService.ApplyFreeformOverrides(_projectFile, grid);
        }

        BumpNotchExportGridFingerprint();

        RefreshFreeformAutoDetectStats(source);
        if (_selectedCadIds.Count > 0)
        {
            RefreshNotchCanvasPreview(showStatus: false);
        }

        OnWorkflowStepCompleted(
            WorkflowStepId.Step2Freeform,
            invalidateDownstream: false,
            moveToNextStep: moveToStep3);

        if (setStatusFromSummary)
        {
            SetStatus(FreeformAutoDetectSummary);
        }

        NotifySimulationWorkspaceSourceChanged();
        CanvasHost?.Invalidate();
    }

    private static MatchingSettings CloneMatchingSettings(MatchingSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new MatchingSettings
        {
            FreeformAxisThreshold = source.FreeformAxisThreshold,
            EnableAutoDetectXy = source.EnableAutoDetectXy,
            EnableFreeformEdgeSpecialization = source.EnableFreeformEdgeSpecialization,
        };
    }
}

