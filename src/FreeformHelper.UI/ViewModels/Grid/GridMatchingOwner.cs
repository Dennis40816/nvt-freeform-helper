// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels.Grid;

internal sealed class GridMatchingOwner
{
    private readonly GridMatchingPorts _ports;

    public GridMatchingOwner(GridMatchingPorts ports, GridRebuildOwner rebuild)
    {
        _ports = ports;
        Rebuild = rebuild;
    }

    public GridRebuildOwner Rebuild { get; }

    public async Task MatchAsync()
    {
        var status = _ports.Match.CreateStatus("Match");
        if (!_ports.Match.FilteredInputs(
                "Step 1: import DXF and build grid first.",
                out var cad,
                out var grid))
        {
            return;
        }
        await _ports.Match.RunMatchAsync(
            () =>
            {
                _ports.Effects.ApplyUiSettings();
                status.ReportProgress("Matching pads...");
                _ports.Effects.Info("Matching pads.");
            },
            MatchPadsAsync,
            ex =>
            {
                status.ReportError("Match failed", ex);
                _ports.Match.LogMatchError(ex);
            });
        async Task MatchPadsAsync(Action<double> reportProgress)
        {
            await ApplyStep1MatchResultAsync(
                cad,
                grid,
                reportProgress,
                invalidateDownstream: true,
                moveToStep2: true,
                completionStatus: "Matching complete.");
            _ports.Effects.Info("Matching complete.");
        }
    }

    private async Task ApplyStep1MatchResultAsync(
        CadPadSet cad,
        RegularGrid grid,
        Action<double> reportProgress,
        bool invalidateDownstream,
        bool moveToStep2,
        string? completionStatus)
    {
        var result = await _ports.Match.MatchAsync(cad, grid, reportProgress);

        _ports.State.SetMatch(result);
        _ports.Effects.BumpGridFingerprint();
        _ports.Match.RebuildIndexes(_ports.State.VisibleCad().ToList());
        _ports.Match.SetCanvasLinks(result.Links);
        var telemetry = result.Telemetry;
        _ports.Effects.Debug("PERF PADMATCH: cad={0}, candidateCells={1}, boundsIntersections={2}, polygonIntersections={3}, avgCandidatePerCad={4:0.##}, p95CandidatePerCad={5}.",
            telemetry.CadPadCount,
            telemetry.CandidateCellVisits,
            telemetry.BoundsIntersections,
            telemetry.PolygonIntersections,
            telemetry.CandidateCellsPerCadAverage,
            telemetry.CandidateCellsPerCadP95);

        _ports.Workflow.CompleteMatch(invalidateDownstream, moveToStep2);

        // H4: keep Step3 preview live when Step1 result changes and CAD selection already exists.
        if (_ports.State.HasSelectedCad())
        {
            _ports.Effects.RefreshPreview();
        }

        if (!string.IsNullOrWhiteSpace(completionStatus))
        {
            _ports.Effects.SetStatus(completionStatus);
        }

        _ports.Effects.SourceChanged();
        _ports.Effects.InvalidateCanvas();
    }

    public async Task<bool> RestoreStep1MatchAfterProjectLoadAsync()
    {
        var status = _ports.Match.CreateStatus("LoadProject.Step1Replay");
        if (!_ports.Match.FilteredInputs(
                "Project loaded: no CAD output pads; Step 1 auto replay skipped.",
                out var cad,
                out var grid))
        {
            return false;
        }

        _ports.Effects.ApplyUiSettings();
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

    public async Task<bool> RestoreStep2FreeformAfterProjectLoadAsync()
    {
        var status = _ports.Match.CreateStatus("LoadProject.Step2Replay");
        if (!_ports.Match.AutoReplayStep2())
        {
            return false;
        }

        if (_ports.State.Match().CadToRegular.Count == 0)
        {
            return false;
        }

        if (!_ports.Match.ActiveInputs(
                "Project loaded: Step 2 auto replay skipped (CAD/grid missing).",
                out var cad,
                out var grid))
        {
            return false;
        }

        _ports.Effects.ApplyUiSettings();
        var matchingSettings = CloneMatchingSettings(_ports.Match.MatchingSettings());
        var assignments = await _ports.Match.BuildAssignmentsAsync(
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

    public async Task AutoDetectFreeformsAsync()
    {
        if (!_ports.Workflow.EnsureStep2())
        {
            return;
        }

        if (!_ports.Match.ActiveInputs(
                "Step 2: import DXF and run Step 1 first.",
                out var cad,
                out var grid))
        {
            return;
        }

        await _ports.Match.RunAutoDetectAsync(async () =>
        {
            _ports.Effects.ApplyUiSettings(); // Apply current UI settings.
            var matchingSettings = CloneMatchingSettings(_ports.Match.MatchingSettings());
            var assignments = await _ports.Match.BuildAssignmentsAsync(
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
        });
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
            _ports.Workflow.ChangeFreeform();
        }

        _ports.Match.ApplyAssignments(grid, assignments);
        if (reapplyManualOverrides)
        {
            _ports.Match.ApplyOverrides(grid);
        }

        _ports.Effects.BumpGridFingerprint();

        RefreshFreeformAutoDetectStats(source);
        if (_ports.State.HasSelectedCad())
        {
            _ports.Effects.RefreshPreview();
        }

        _ports.Workflow.CompleteFreeform(moveToStep3);

        if (setStatusFromSummary)
        {
            _ports.Effects.SetStatus(_ports.Match.StatsSummary());
        }

        _ports.Effects.SourceChanged();
        _ports.Effects.InvalidateCanvas();
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

    public void SetFreeformForSelection(FreeformType type)
    {
        if (_ports.State.Grid() is null)
        {
            _ports.Effects.SetStatus("Freeform tagging: build grid first.");
            return;
        }

        if (_ports.State.SelectedRegularIndices().Count == 0)
        {
            _ports.Effects.SetStatus("Freeform tagging: select regular pads first.");
            return;
        }

        SetFreeformForPadIndices(_ports.State.SelectedRegularIndices(), type);
    }

    public void SetFreeformForPadIndices(IReadOnlyCollection<int> padIndices, FreeformType type)
    {
        if (_ports.State.Grid() is null)
        {
            _ports.Effects.SetStatus("Freeform tagging: build grid first.");
            return;
        }

        if (padIndices.Count == 0)
        {
            _ports.Effects.SetStatus("Freeform tagging: no regular pads selected.");
            return;
        }

        var targetPads = _ports.State.Grid()!.Pads
            .Where(pad => padIndices.Contains(pad.Index))
            .ToList();
        var touched = _ports.Match.ApplyManual(_ports.State.Grid()!, padIndices, type);
        _ports.Match.SyncOverrides(targetPads, type);
        _ports.Effects.BumpGridFingerprint();
        _ports.Workflow.ChangeFreeform();
        RefreshFreeformAutoDetectStats("manual override");
        if (_ports.State.HasSelectedCad())
        {
            _ports.Effects.RefreshPreview();
        }
        _ports.Workflow.CompleteFreeform(true);
        _ports.Effects.SetStatus($"Freeform set to {type} for {touched} regular pads.");
        _ports.Effects.SourceChanged();
        _ports.Effects.InvalidateCanvas();
        _ports.Effects.MarkUnsaved();
    }

    public void ResetFreeformAutoDetectStats(string summary = FreeformAutoDetectResult.NotRunSummary)
    {
        _ports.Match.SetStats(new FreeformAutoDetectResult(
            summary,
            new ObservableCollection<FreeformAutoDetectStatRow>()));
    }

    public void OnFreeformAutoDetectResultChanged(FreeformAutoDetectResult? oldValue, FreeformAutoDetectResult newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.Summary, newValue.Summary, "FreeformAutoDetectSummary");
        RaiseWhenChanged(oldValue.Rows, newValue.Rows, "FreeformAutoDetectRows");
        RaiseWhenChanged(oldValue.HasRows, newValue.HasRows, "HasFreeformAutoDetectRows");
    }

    public void RefreshFreeformAutoDetectStats(string source)
    {
        if (_ports.State.Grid() is null || _ports.State.Grid()!.Pads.Count == 0)
        {
            ResetFreeformAutoDetectStats("Freeform stats: no regular pads.");
            return;
        }

        FreeformStatistics stats = FreeformStatisticsBuilder.Build(_ports.State.Grid()!.Pads);
        var rows = new ObservableCollection<FreeformAutoDetectStatRow>
        {
            new("None", stats.NoneCount, stats.TotalCount),
            new("XWay", stats.XWayCount, stats.TotalCount),
            new("YWay", stats.YWayCount, stats.TotalCount),
            new("XYWay", stats.XyWayCount, stats.TotalCount),
        };

        _ports.Match.SetStats(new FreeformAutoDetectResult(
            $"Freeform stats ({source}): total={stats.TotalCount}, tagged={stats.TaggedCount}, none={stats.NoneCount}.",
            rows));
    }

    private void RaiseWhenChanged<T>(T oldValue, T newValue, string propertyName)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            _ports.Effects.Changed(propertyName);
        }
    }
}
