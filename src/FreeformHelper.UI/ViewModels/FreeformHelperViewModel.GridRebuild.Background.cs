using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private GridRebuildBuildRequest CreateGridRebuildBuildRequestSnapshot()
    {
        var fullCadSnapshot = _cad;
        var boundsCadSnapshot = GridAlignmentMode == GridAlignmentMode.FromCadBounds
            ? BuildCadPadSetForBounds()
            : fullCadSnapshot;

        return new GridRebuildBuildRequest
        {
            ProjectFile = CreateGridRebuildProjectSnapshot(),
            Settings = CloneGridSettings(_projectFile.Settings.Grid),
            RegularSourceMode = RegularSourceMode,
            GridAlignmentMode = GridAlignmentMode,
            Cad = fullCadSnapshot,
            RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name,
            HiddenCadPadIds = BuildEffectiveHiddenCadPadIds(),
            BuildCadPadSetForBounds = () => boundsCadSnapshot,
        };
    }

    private ProjectFile CreateGridRebuildProjectSnapshot()
    {
        return new ProjectFile
        {
            FreeformOverrides = new Dictionary<int, FreeformType>(_projectFile.FreeformOverrides),
        };
    }

    private static GridSettings CloneGridSettings(GridSettings source)
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

    /// <summary>
    /// Triggers an asynchronous grid rebuild operation.
    /// This method manages the state of grid rebuilding to prevent concurrent rebuilds
    /// and handles pending rebuild requests.
    /// </summary>
    /// <param name="requestFit">If true, the canvas will attempt to fit content after the rebuild.</param>
    private async Task TriggerGridRebuildAsync(bool requestFit = false)
    {
        await _gridRebuildUseCase.RequestAsync(requestFit); // Single entry for rebuild requests.
    }

    /// <summary>
    /// Asynchronously rebuilds the regular grid based on current settings and CAD data.
    /// </summary>
    private async Task RebuildGridAsync()
    {
        await RunUiOperationAsync(
            operationName: "Grid rebuild",
            operationAsync: RebuildGridCoreAsync,
            onError: ex =>
            {
                SetStatusError("Rebuild grid failed", ex);
                _logger.Error(ex, "Rebuild grid failed.");
            });
    }

    private async Task RebuildGridCoreAsync()
    {
        var hadRegularBefore = RegularPads.Count > 0;
        ApplyUiToSettings(_projectFile.Settings);
        SetStatus("Building regular grid...");
        _logger.Info(CultureInfo.InvariantCulture, "Building regular grid.");
        CanvasHintText = string.Empty;
        var buildRequest = CreateGridRebuildBuildRequestSnapshot();
        var buildResult = IsCadLoadCanvasOverlayVisible
            ? await Task.Run(() => GridRebuildOrchestrator.Build(buildRequest))
            : GridRebuildOrchestrator.Build(buildRequest);
        if (!string.IsNullOrWhiteSpace(buildResult.DxfRegularSourceHint))
        {
            DxfRegularSourceHint = buildResult.DxfRegularSourceHint;
        }

        if (!buildResult.Success || buildResult.Grid is null)
        {
            SetStatus(buildResult.StatusText);
            ShowEmptyGridResult(buildResult.CanvasHint ?? string.Empty);
            return;
        }

        ApplyBuiltGrid(buildResult.Grid, buildResult.StatusText, hadRegularBefore);
    }

    private void ApplyBuiltGrid(RegularGrid grid, string statusText, bool hadRegularBefore)
    {
        _grid = grid;
        BumpNotchExportGridFingerprint();
        InvalidateNotchCompensationCache();
        SetLatestPadMatchResult(PadMatchResult.Empty);
        MatchLinksForCanvas = Array.Empty<PadMatchLink>();
        OnPropertyChanged(nameof(PitchSizeX));
        OnPropertyChanged(nameof(PitchSizeY));
        OnPropertyChanged(nameof(PitchSizeXSummary));
        OnPropertyChanged(nameof(PitchSizeYSummary));

        RegularPads = new ObservableCollection<RegularPad>(grid.Pads);
        RefreshRegularVisibilityMask(grid);
        ResetFreeformAutoDetectStats("Freeform stats: run Step 2 auto-detect.");
        if (!_isLoadingSettings)
        {
            ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
        }

        if (CadPads.Count > 0)
        {
            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        }
        RefreshDxfRegularSourceHint();
        NotifySimulationWorkspaceSourceChanged();

        var shouldClear = !_selection.SuppressClearOnRebuild;
        _selection.SuppressClearOnRebuild = false;
        if (shouldClear)
        {
            _selectionCoordinator.ClearSelection(CanvasHost);
        }

        SetStatus(statusText);
        _logger.Info(CultureInfo.InvariantCulture, "Grid built. Rows={0}, Cols={1}, Pads={2}.", grid.Rows, grid.Cols, grid.Pads.Count);
        if (!_gridStartup.InitialGridBuiltMarked && _cad is null)
        {
            StartupPerfTracker.Mark("workspace.initial-grid-built", $"pads={grid.Pads.Count}");
            _gridStartup.InitialGridBuiltMarked = true;
        }

        CanvasHost?.Invalidate();
        if (_gridStartup.FitAfterNextBuild)
        {
            CanvasHost?.FitToContent();
            _gridStartup.FitAfterNextBuild = false;
        }
        else if (_cad is null && !hadRegularBefore)
        {
            CanvasHost?.FitToContent();
        }

        UpdateSelectedRangeSummary();
    }

    private void ShowEmptyGridResult(string hint)
    {
        SetLatestPadMatchResult(PadMatchResult.Empty);
        MatchLinksForCanvas = Array.Empty<PadMatchLink>();
        _grid = null;
        BumpNotchExportGridFingerprint();
        InvalidateNotchCompensationCache();
        OnPropertyChanged(nameof(PitchSizeX));
        OnPropertyChanged(nameof(PitchSizeY));
        OnPropertyChanged(nameof(PitchSizeXSummary));
        OnPropertyChanged(nameof(PitchSizeYSummary));
        RegularPads = new ObservableCollection<RegularPad>();
        ApplyRegularVisibilityMaskResult(
            string.IsNullOrWhiteSpace(_regularVisibilityMask.SourcePath)
                ? RegularVisibilityMaskResult.NotFound()
                : RegularVisibilityMaskResult.NotFound(_regularVisibilityMask.SourcePath),
            grid: null);
        CanvasHintText = hint;
        ResetFreeformAutoDetectStats();
        if (!_isLoadingSettings)
        {
            ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
        }

        var shouldClearSelection = !_selection.SuppressClearOnRebuild;
        _selection.SuppressClearOnRebuild = false;
        if (shouldClearSelection)
        {
            _selectionCoordinator.ClearSelection(CanvasHost);
        }

        CanvasHost?.Invalidate();
        UpdateSelectedRangeSummary();
        NotifySimulationWorkspaceSourceChanged();
    }

    private void ClearPadMatchResult(bool clearRegularAssignments)
    {
        SetLatestPadMatchResult(PadMatchResult.Empty);
        MatchLinksForCanvas = Array.Empty<PadMatchLink>();
        BumpNotchExportGridFingerprint();

        if (!clearRegularAssignments || _grid is null)
        {
            return;
        }

        foreach (var regularPad in _grid.Pads)
        {
            regularPad.MatchedCadPadId = null;
            regularPad.MatchScore = 0.0;
        }

        BumpNotchExportIndexFingerprint();
    }
}
