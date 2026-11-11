// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.ViewModels.Grid;

internal sealed class GridRebuildOwner
{
    private readonly GridRebuildPorts _ports;

    public GridRebuildOwner(GridRebuildPorts ports) => _ports = ports;

    public async Task RebuildGridAsync() => await _ports.Build.RunAsync(RebuildGridCoreAsync);

    public async Task RebuildGridCoreAsync()
    {
        var hadRegularBefore = _ports.State.RegularPadCount() > 0;
        _ports.Effects.ApplyUiSettings();
        _ports.Effects.SetStatus("Building regular grid...");
        _ports.Effects.Info("Building regular grid.");
        _ports.Build.SetCanvasHint(string.Empty);
        var buildResult = await _ports.Build.BuildAsync();
        if (!string.IsNullOrWhiteSpace(buildResult.DxfRegularSourceHint))
        {
            _ports.Build.SetSourceHint(buildResult.DxfRegularSourceHint);
        }

        if (!buildResult.Success || buildResult.Grid is null)
        {
            _ports.Effects.SetStatus(buildResult.StatusText);
            ShowEmptyGridResult(buildResult.CanvasHint ?? string.Empty);
            return;
        }

        ApplyBuiltGrid(buildResult.Grid, buildResult.StatusText, hadRegularBefore);
    }

    public void ApplyBuiltGrid(RegularGrid grid, string statusText, bool hadRegularBefore)
    {
        _ports.State.SetGrid(grid);
        _ports.Effects.BumpGridFingerprint();
        _ports.Effects.InvalidateCompensation();
        _ports.State.SetMatch(PadMatchResult.Empty);
        _ports.Build.SetCanvasLinks(Array.Empty<PadMatchLink>());
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeX));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeY));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeXSummary));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeYSummary));

        _ports.Build.SetRegularPads(new ObservableCollection<RegularPad>(grid.Pads));
        _ports.Build.RefreshMask(grid);
        _ports.Build.ResetStats("Freeform stats: run Step 2 auto-detect.");
        if (!_ports.State.IsLoading())
        {
            _ports.Workflow.ResetFromMatch();
        }

        if (_ports.State.VisibleCad().Count > 0)
        {
            _ports.Build.UpdateIndexes(_ports.State.VisibleCad().ToList());
        }
        _ports.Build.RefreshSourceHint();
        _ports.Effects.SourceChanged();

        var shouldClear = !_ports.Selection.PreserveSelection();
        _ports.Selection.ConsumePreservation();
        if (shouldClear)
        {
            _ports.Selection.ClearSelection();
        }

        _ports.Effects.SetStatus(statusText);
        _ports.Effects.Info("Grid built. Rows={0}, Cols={1}, Pads={2}.", grid.Rows, grid.Cols, grid.Pads.Count);
        if (!_ports.Startup.InitialGridBuiltMarked() && !_ports.State.HasCad())
        {
            _ports.Startup.MarkPerformance("workspace.initial-grid-built", $"pads={grid.Pads.Count}");
            _ports.Startup.MarkInitialGridBuilt();
        }

        _ports.Effects.InvalidateCanvas();
        if (_ports.Startup.FitAfterNextBuild())
        {
            _ports.Effects.FitCanvas();
            _ports.Startup.ConsumeFit();
        }
        else if (!_ports.State.HasCad() && !hadRegularBefore)
        {
            _ports.Effects.FitCanvas();
        }

        _ports.Selection.RefreshSummary();
    }

    public void ShowEmptyGridResult(string hint)
    {
        _ports.State.SetMatch(PadMatchResult.Empty);
        _ports.Build.SetCanvasLinks(Array.Empty<PadMatchLink>());
        _ports.State.SetGrid(null);
        _ports.Effects.BumpGridFingerprint();
        _ports.Effects.InvalidateCompensation();
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeX));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeY));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeXSummary));
        _ports.Effects.Changed(nameof(Sizing.ManualSizingOwner.PitchSizeYSummary));
        _ports.Build.SetRegularPads(new ObservableCollection<RegularPad>());
        _ports.Build.ClearMask();
        _ports.Build.SetCanvasHint(hint);
        _ports.Build.ResetStats(FreeformAutoDetectResult.NotRunSummary);
        if (!_ports.State.IsLoading())
        {
            _ports.Workflow.ResetFromMatch();
        }

        var shouldClearSelection = !_ports.Selection.PreserveSelection();
        _ports.Selection.ConsumePreservation();
        if (shouldClearSelection)
        {
            _ports.Selection.ClearSelection();
        }

        _ports.Effects.InvalidateCanvas();
        _ports.Selection.RefreshSummary();
        _ports.Effects.SourceChanged();
    }

    public void ClearPadMatchResult(bool clearRegularAssignments)
    {
        _ports.State.SetMatch(PadMatchResult.Empty);
        _ports.Build.SetCanvasLinks(Array.Empty<PadMatchLink>());
        _ports.Effects.BumpGridFingerprint();

        if (!clearRegularAssignments || _ports.State.Grid() is null)
        {
            return;
        }

        foreach (var regularPad in _ports.State.Grid()!.Pads)
        {
            regularPad.MatchedCadPadId = null;
            regularPad.MatchScore = 0.0;
        }

        _ports.Effects.BumpIndexFingerprint();
    }

    public static ProjectFile CreateProjectSnapshot(ProjectFile project)
    {
        return new ProjectFile
        {
            FreeformOverrides = new Dictionary<int, FreeformType>(project.FreeformOverrides),
        };
    }

    public static GridSettings CloneGridSettings(GridSettings source)
    {
        return new GridSettings
        {
            XChannels = source.XChannels,
            YChannels = source.YChannels,
            WidthScope = source.WidthScope,
            HeightScope = source.HeightScope,
            CascadeNum = source.CascadeNum,
            PerIcXChannels = new List<int>(source.PerIcXChannels),
            PerIcYChannels = new List<int>(source.PerIcYChannels),
            AlignmentMode = source.AlignmentMode,
            ScanOrder = source.ScanOrder,
            AfeOverlap = source.AfeOverlap,
            RegularSourceMode = source.RegularSourceMode,
            RegularSourceLayerName = source.RegularSourceLayerName,
            ColumnWidths = new List<double>(source.ColumnWidths),
            ColumnOverrides = new List<bool>(source.ColumnOverrides),
            RowHeights = new List<double>(source.RowHeights),
            RowOverrides = new List<bool>(source.RowOverrides),
            RowWidthOverrides = source.RowWidthOverrides.Select(static row => new List<double>(row)).ToList(),
            RowWidthOverrideFlags = source.RowWidthOverrideFlags.Select(static row => new List<bool>(row)).ToList(),
            ColumnHeightOverrides = source.ColumnHeightOverrides.Select(static column => new List<double>(column)).ToList(),
            ColumnHeightOverrideFlags = source.ColumnHeightOverrideFlags.Select(static column => new List<bool>(column)).ToList(),
            BoundsPaddingRatio = source.BoundsPaddingRatio,
            MaxChannelsX = source.MaxChannelsX,
            MaxChannelsY = source.MaxChannelsY,
            ActiveAreaWidth = source.ActiveAreaWidth,
            ActiveAreaHeight = source.ActiveAreaHeight,
            PanelBiasX = source.PanelBiasX,
            PanelBiasY = source.PanelBiasY,
            BoundLayerName = source.BoundLayerName,
            RecalcBoundsOnLayerFilter = source.RecalcBoundsOnLayerFilter,
        };
    }
}
