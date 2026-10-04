using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> encapsulates the logic
/// for various core operations within the application, such as DXF import, grid building,
/// pad matching, and freeform detection. These methods often involve asynchronous operations
/// and updates to the UI state.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private const int MaxBoundLayerDropdownLayers = 256;

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
    /// Builds the DXF import options based on current UI settings.
    /// </summary>
    /// <returns>A <see cref="DxfImportOptions"/> instance.</returns>
    private DxfImportOptions BuildDxfOptions()
    {
        var options = new DxfImportOptions
        {
            OnlyClosedPolylines = ImportOnlyClosedPolylines, // Set option from UI property.
            IncludeBlockPolylines = ImportBlockPolylines
        };

        return options;
    }

    private void ClearDxfLayerCatalog()
    {
        _layerCatalogStateService.Clear();
    }

    private void TryLoadDxfLayerCatalogFromPath(string path)
    {
        _layerCatalogStateService.TryLoadFromPath(path);
    }

    private Task TryLoadDxfLayerCatalogFromPathAsync(string path)
    {
        return Task.Run(() => _layerCatalogStateService.TryLoadFromPath(path));
    }

    private void TryLoadDxfLayerCatalogFromEmbedded(byte[]? data)
    {
        _layerCatalogStateService.TryLoadFromEmbedded(data);
    }

    private Task TryLoadDxfLayerCatalogFromEmbeddedAsync(byte[]? data)
    {
        return Task.Run(() => _layerCatalogStateService.TryLoadFromEmbedded(data));
    }


    /// <summary>
    /// Filters the displayed CAD pads based on the currently selected layers
    /// and triggers a grid rebuild if <see cref="RecalcBoundsOnLayerFilter"/> is true.
    /// </summary>
    private void ClearSelection()
    {
        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        _selectionCoordinator.ClearSelection(CanvasHost);
    }

    /// <summary>
    /// Applies the saved layer selections from a <see cref="UiViewSnapshot"/> to the current layer toggles.
    /// </summary>
    /// <param name="snapshot">The <see cref="UiViewSnapshot"/> containing saved layer selection states.</param>
    private void ApplyLayerSelections(UiViewSnapshot snapshot)
    {
        if (snapshot is null || snapshot.LayerSelections is null || snapshot.LayerSelections.Count == 0)
        {
            return;
        }

        var changed = false;
        _suppressLayerToggleChange = true;
        // Iterate through current layer toggles and set their selection state based on the snapshot.
        foreach (var t in LayerToggles)
        {
            var match = snapshot.LayerSelections.FirstOrDefault(s => string.Equals(s.Name, t.Name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                if (t.IsSelected != match.IsSelected)
                {
                    t.IsSelected = match.IsSelected;
                    changed = true;
                }
            }
        }
        _suppressLayerToggleChange = false;

        if (changed)
        {
            FilterCadPadsByLayer(); // Apply the restored layer filters.
        }
    }

    /// <summary>
    /// Applies persisted UI view settings from a <see cref="UiViewSnapshot"/>.
    /// </summary>
    /// <param name="snapshot">The snapshot containing view settings.</param>
    private void ApplyViewSnapshot(UiViewSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        var wasLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        _suppressUndo = true;

        ShowCad = snapshot.ShowCad;
        ShowRegular = snapshot.ShowRegular;
        HighlightUnmatched = snapshot.HighlightUnmatched;
        HighlightFreeform = snapshot.HighlightFreeform;
        ColorCadByArea = snapshot.ColorCadByArea;

        CadLineWidth = (decimal)snapshot.CadLineWidth;
        CadFillOpacity = (decimal)Math.Clamp(snapshot.CadFillOpacity, 0.0, 1.0);
        CadLineOpacity = snapshot.CadLineOpacity <= 0
            ? 1.0m
            : (decimal)Math.Clamp(snapshot.CadLineOpacity, 0.0, 1.0);
        if (!string.IsNullOrWhiteSpace(snapshot.CadLineColor))
        {
            CadLineColorHex = snapshot.CadLineColor;
        }

        RegularLineWidth = (decimal)snapshot.RegularLineWidth;
        HighlightStrokeWidthAdjust = (decimal)Math.Clamp(snapshot.HighlightStrokeWidthAdjust, -2.0, 4.0);
        RegularFillOpacity = (decimal)Math.Clamp(snapshot.RegularFillOpacity, 0.0, 1.0);
        RegularLineOpacity = snapshot.RegularLineOpacity <= 0
            ? 1.0m
            : (decimal)Math.Clamp(snapshot.RegularLineOpacity, 0.0, 1.0);
        if (!string.IsNullOrWhiteSpace(snapshot.RegularLineColor))
        {
            RegularLineColorHex = snapshot.RegularLineColor;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.RegularSelectedColor))
        {
            RegularSelectedColorHex = snapshot.RegularSelectedColor;
        }
        if (snapshot.RegularSelectedFillOpacity > 0)
        {
            RegularSelectedFillOpacity = (decimal)Math.Clamp(snapshot.RegularSelectedFillOpacity, 0.0, 1.0);
        }

        AreaBucketTolerance = (decimal)snapshot.AreaBucketTolerance;
        MaxAreaBuckets = snapshot.MaxAreaBuckets;
        ShowDiffIndexOverlay = snapshot.ShowDiffIndexOverlay;
        ShowNotchCanvasPreview = snapshot.ShowNotchCanvasPreview;
        ShowNotchToRegularLabels = snapshot.ShowNotchToRegularLabels;
        GlobalFontSizePercent = (decimal)Math.Clamp(snapshot.GlobalFontSizePercent, 80.0, 140.0);
        NotchPreviewVisualizationStep = (decimal)Math.Clamp(snapshot.NotchPreviewVisualizationStep, 1.0, 3.0);
        NotchPreviewAutoPlayIntervalMs = (decimal)Math.Clamp(snapshot.NotchPreviewAutoPlayIntervalMs, 200.0, 5000.0);
        NotchPreviewAutoPlayEnabled = snapshot.NotchPreviewAutoPlayEnabled;
        if (NotchExportFileTypeMetadata.TryParseStoredValue(snapshot.NotchExportFileType, out var exportFileType))
        {
            var exportOption = NotchExportFileTypeOptions.FirstOrDefault(o => o.Value == exportFileType);
            if (!string.IsNullOrWhiteSpace(exportOption.Display))
            {
                SelectedNotchExportFileTypeOption = exportOption;
            }
        }

        ApplyDxfLayerImageExportSnapshot(snapshot);
        CoordinatePixelWidth = (decimal)Math.Max(
            1.0,
            snapshot.CoordinatePixelWidth > 0.0
                ? snapshot.CoordinatePixelWidth
                : (double)XChannels);
        CoordinatePixelHeight = (decimal)Math.Max(
            1.0,
            snapshot.CoordinatePixelHeight > 0.0
                ? snapshot.CoordinatePixelHeight
                : (double)YChannels);
        CoordinatePreferredAaOutlineLayerName = snapshot.CoordinatePreferredAaOutlineLayerName?.Trim() ?? string.Empty;

        _suppressUndo = false;
        _isLoadingSettings = wasLoading;
    }

    private void ApplyImportSnapshot(UiImportSnapshot snapshot, ProjectFile? projectFile = null)
    {
        if (snapshot is null)
        {
            return;
        }

        var wasLoading = _isLoadingSettings;
        _isLoadingSettings = true;
        ImportOnlyClosedPolylines = snapshot.OnlyClosedPolylines;
        ImportBlockPolylines = snapshot.IncludeBlockPolylines;
        if (!string.IsNullOrWhiteSpace(snapshot.LogLevel))
        {
            var currentLevel = LoggingBootstrapper.GetCurrentMinimumLevelName();
            var normalizedLevel = snapshot.LogLevel.Trim();
            if (!string.Equals(currentLevel, normalizedLevel, StringComparison.OrdinalIgnoreCase))
            {
                var appliedLevel = LoggingBootstrapper.ApplyMinimumLevel(normalizedLevel);
                var normalized = LogLevelOptions.FirstOrDefault(option =>
                    string.Equals(option.Value, appliedLevel, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(normalized.Display))
                {
                    SelectedLogLevelOption = normalized;
                }
            }
        }

        ApplyRegularVisibilityMaskSnapshot(
            sourcePath: snapshot.RegularVisibilityMaskSourcePath,
            isEnabled: snapshot.UseRegularVisibilityMask,
            allowRuntimeRefresh: !wasLoading,
            embeddedMask: projectFile?.EmbeddedRegularVisibilityMask,
            embeddedMaskName: projectFile?.EmbeddedRegularVisibilityMaskName);

        _isLoadingSettings = wasLoading;
    }

    /// <summary>
    /// Determines if the application should prompt the user to save before exiting.
    /// This is true if there are unsaved changes or loaded CAD content has never been saved.
    /// </summary>
    /// <returns>True if a save prompt is needed, false otherwise.</returns>
    public bool ShouldPromptSaveOnExit()
    {
        return HasUnsavedChanges || (_cad is not null && string.IsNullOrWhiteSpace(_lastSavedPath));
    }

    /// <summary>
    /// Handles the asynchronous operation of opening a DXF file.
    /// Prompts the user for a file, imports the CAD pads, updates the UI, and rebuilds the grid.
    /// </summary>
    private async Task OpenDxfAsync()
    {
        // Ensure the dialog delegate is wired up by the View.
        if (PickOpenDxfPathAsync is null)
        {
            SetStatus("Open DXF: dialog handler not wired.");
            return;
        }

        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        var path = await PickOpenDxfPathAsync(); // Prompt user to pick a DXF file.
        if (string.IsNullOrWhiteSpace(path)) return; // User cancelled.

        await _cadLoadWorkflowService.OpenDxfAsync(new OpenDxfWorkflowRequest
        {
            Path = path,
            BuildDxfOptions = BuildDxfOptions,
            SetStatus = SetStatus,
            LoadLayerCatalogFromPathAsync = selectedPath =>
            {
                _layerCatalogStateService.SetFromPath(selectedPath, _dxfImportService.ImportedCatalog);
                return Task.CompletedTask;
            },
            ResetHiddenCadPads = ResetHiddenCadPads,
            ApplyCadLoadOutcomeAsync = outcome => ApplyCadLoadOutcomeAsync(outcome, null),
            SetProjectLastDxfPath = selectedPath => _projectFile.LastDxfPath = selectedPath,
            ClearLastSavedPath = () => _lastSavedPath = null,
            MarkUnsaved = MarkUnsaved,
            BuildStatus = outcome => BuildCadLoadStatus(outcome, CadLoadContext.OpenDxf, null),
            LogCadLoadOutcome = outcome => LogCadLoadOutcome(outcome, CadLoadContext.OpenDxf),
            TriggerGridRebuildAsync = () => TriggerGridRebuildAsync(requestFit: true),
            RunWithCadLoadCanvasOverlayAsync = RunWithCadLoadCanvasOverlayAsync,
            GetCadPadCountForLog = () => _cad?.Pads.Count ?? 0,
            RunUiOperationAsync = (operationName, operationAsync, onError) =>
                RunUiOperationAsync(operationName, operationAsync, onError),
            OnError = ex =>
            {
                SetStatusError("DXF import failed", ex);
                Logger.Error(ex, "DXF import failed.");
            },
        });
    }

    /// <summary>
    /// Asynchronously rebuilds the regular grid based on current settings and CAD data.
    /// </summary>
    private async Task RebuildGridAsync()
    {
        await RunUiOperationAsync(
            operationName: "Grid rebuild",
            operationAsync: async () =>
            {
                var hadRegularBefore = RegularPads.Count > 0;
                ApplyUiToSettings(_projectFile.Settings);
                SetStatus("Building regular grid...");
                Logger.Info(CultureInfo.InvariantCulture, "Building regular grid.");
                CanvasHintText = string.Empty;
                var buildRequest = CreateGridRebuildBuildRequestSnapshot();
                var buildResult = IsCadLoadCanvasOverlayVisible
                    ? await Task.Run(() => _gridRebuildOrchestrator.Build(buildRequest))
                    : _gridRebuildOrchestrator.Build(buildRequest);
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

                var grid = buildResult.Grid;
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

                var shouldClear = !SuppressSelectionClearOnRebuild;
                SuppressSelectionClearOnRebuild = false;
                if (shouldClear)
                {
                    _selectionCoordinator.ClearSelection(CanvasHost);
                }

                SetStatus(buildResult.StatusText);
                Logger.Info(CultureInfo.InvariantCulture, "Grid built. Rows={0}, Cols={1}, Pads={2}.", grid.Rows, grid.Cols, grid.Pads.Count);
                if (!_startupInitialGridBuiltMarked && _cad is null)
                {
                    StartupPerfTracker.Mark("workspace.initial-grid-built", $"pads={grid.Pads.Count}");
                    _startupInitialGridBuiltMarked = true;
                }

                CanvasHost?.Invalidate();
                if (_fitAfterNextGridBuild)
                {
                    CanvasHost?.FitToContent();
                    _fitAfterNextGridBuild = false;
                }
                else if (_cad is null && !hadRegularBefore)
                {
                    CanvasHost?.FitToContent();
                }

                UpdateSelectedRangeSummary();
                return;

                void ShowEmptyGridResult(string hint)
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
                        string.IsNullOrWhiteSpace(_regularVisibilityMaskSourcePath)
                            ? RegularVisibilityMaskResult.NotFound()
                            : RegularVisibilityMaskResult.NotFound(_regularVisibilityMaskSourcePath),
                        grid: null);
                    CanvasHintText = hint;
                    ResetFreeformAutoDetectStats();
                    if (!_isLoadingSettings)
                    {
                        ResetWorkflowFromStep(WorkflowStepId.Step1Match, showStatus: false);
                    }

                    var shouldClearSelection = !SuppressSelectionClearOnRebuild;
                    SuppressSelectionClearOnRebuild = false;
                    if (shouldClearSelection)
                    {
                        _selectionCoordinator.ClearSelection(CanvasHost);
                    }

                    CanvasHost?.Invalidate();
                    UpdateSelectedRangeSummary();
                    NotifySimulationWorkspaceSourceChanged();
                }
            },
            onError: ex =>
            {
                SetStatusError("Rebuild grid failed", ex);
                Logger.Error(ex, "Rebuild grid failed.");
            });
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

    /// <summary>
    /// Builds a <see cref="CadPadSet"/> containing only the CAD pads from currently selected layers.
    /// </summary>
    /// <returns>A new <see cref="CadPadSet"/> with filtered pads, or null if no CAD is loaded.</returns>
    private CadPadSet? BuildFilteredCadPadSet()
    {
        if (_cad is null)
        {
            return null;
        }

        if (_cachedFilteredCadForBuild is not null)
        {
            return _cachedFilteredCadForBuild;
        }

        var result = LayerFilterUseCase.Filter(_cad, LayerToggles);
        var visible = result.VisiblePads
            .Where(p => !IsCadPadEffectivelyHidden(p.Id))
            .ToList();
        _cachedFilteredCadForBuild = new CadPadSet(visible);
        return _cachedFilteredCadForBuild;
    }

    private CadPadSet? BuildCadPadSetForBounds()
    {
        if (_cad is null)
        {
            return null;
        }

        var boundLayer = SelectedBoundLayerOption?.Name;
        if (!string.IsNullOrWhiteSpace(boundLayer))
        {
            var pads = _cad.Pads
                .Where(p => !IsCadPadEffectivelyHidden(p.Id))
                .Where(p => string.Equals(p.Layer, boundLayer, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pads.Count > 0)
            {
                return new CadPadSet(pads);
            }
        }

        if (RecalcBoundsOnLayerFilter)
        {
            return BuildFilteredCadPadSet();
        }

        if (_autoHiddenDuplicateCadPadIds.Count == 0)
        {
            return _cad;
        }

        return BuildActiveCadPadSet();
    }

}


