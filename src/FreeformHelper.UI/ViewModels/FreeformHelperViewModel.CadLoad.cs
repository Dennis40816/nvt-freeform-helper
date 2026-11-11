using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private Task ApplyCadLoadOutcomeAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot) =>
        ApplyCadLoadOutcomeCoreAsync(outcome, snapshot, new CadEditNotificationSnapshot(this));

    private async Task ApplyCadLoadOutcomeCoreAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot, CadEditNotificationSnapshot? previous)
    {
        previous ??= new CadEditNotificationSnapshot(this);
        _cad = outcome.Cad;
        ResetRegularVisibilityMaskSelection(clearProjectEmbeddedMask: !_isLoadingSettings);
        ClearAutoHiddenDuplicateCadPadState();
        ClearDxfEditRuntimeState(resetBaseline: false, previous);
        SetAutoHiddenDuplicateCadPads(outcome.DuplicateSanitization);
        ResetDxfEditBaselineCore(outcome.Cad, previous);
        SyncHiddenCadIdsToProject(previous);
        SyncCombinedCadState(previous);
        SyncDuplicateCadState(previous);
        _cachedFilteredCadForBuild = null;

        var layerUiSnapshot = await Task.Run(() => BuildCadLayerUiSnapshot(outcome.Cad, snapshot?.LayerSelections));

        ApplyCadLayerUiSnapshot(layerUiSnapshot);
    }

    private const int MaxBoundLayerDropdownLayers = 256;

    private CadLayerUiSnapshot BuildCadLayerUiSnapshot(CadPadSet cad, IReadOnlyList<LayerSelectionSnapshot>? layerSelections)
    {
        return CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: cad,
            Catalog: _layerCatalogStateService.Catalog,
            LayerSelections: layerSelections,
            PendingBoundLayerName: _pendingLayerSelection.BoundLayerName,
            SelectedBoundLayerName: SelectedBoundLayerOption?.Name,
            PendingRegularSourceLayerName: _pendingLayerSelection.RegularSourceLayerName,
            SelectedRegularSourceLayerName: SelectedRegularSourceLayerOption?.Name,
            SelectedDxfEditTargetLayerName: DxfEditTargetLayerName,
            SelectedDxfEditRotationLayerName: DxfEditRotationLayerName,
            MaxBoundLayerDropdownLayers: MaxBoundLayerDropdownLayers));
    }

    private void ApplyCadLayerUiSnapshot(CadLayerUiSnapshot snapshot)
    {
        foreach (var toggle in LayerToggles)
        {
            toggle.PropertyChanging -= OnLayerToggleChanging;
            toggle.PropertyChanged -= OnLayerToggleChanged;
        }

        var toggles = new ObservableCollection<LayerToggle>();
        foreach (var state in snapshot.ToggleStates)
        {
            var toggle = new LayerToggle(state.Name, state.IsSelected);
            toggle.PropertyChanging += OnLayerToggleChanging;
            toggle.PropertyChanged += OnLayerToggleChanged;
            toggles.Add(toggle);
        }

        LayerToggles = toggles;
        HasCadLayers = snapshot.HasCadLayers;

        using (_settingsSuppression.EnterBoundLayerSelectionChange())
        {
            BoundLayerOptions = new ObservableCollection<BoundLayerOption>(snapshot.BoundLayerOptions);
            SelectedBoundLayerOption = BoundLayerOptions.FirstOrDefault(option =>
                string.Equals(option.Name, snapshot.SelectedBoundLayerName, StringComparison.OrdinalIgnoreCase))
                ?? BoundLayerOptions.First();
            BoundLayerOptionSummary = snapshot.BoundLayerOptionSummary;
        }

        using (_settingsSuppression.EnterRegularSourceLayerSelectionChange())
        {
            RegularSourceLayerOptions = new ObservableCollection<RegularSourceLayerOption>(snapshot.RegularSourceLayerOptions);
            SelectedRegularSourceLayerOption = RegularSourceLayerOptions.FirstOrDefault(option =>
                string.Equals(option.Name, snapshot.SelectedRegularSourceLayerName, StringComparison.OrdinalIgnoreCase))
                ?? RegularSourceLayerOptions.First();
        }

        DxfEditLayerOptions = new ObservableCollection<string>(snapshot.DxfEditLayerOptions);
        DxfEditTargetLayerName = snapshot.SelectedDxfEditTargetLayerName;
        DxfEditRotationLayerName = snapshot.SelectedDxfEditRotationLayerName;
        _pendingLayerSelection.BoundLayerName = null;
        _pendingLayerSelection.RegularSourceLayerName = null;
        NotifyDxfEditPanelActionStateChanged();
        ApplyRegularSourceLayerAutoHidePolicy(filterCadPads: false);
        RefreshDxfRegularSourceHint();
        NotifyCadLayerAggregateStateChanged();
        FilterCadPadsByLayer();
    }

    private static string BuildCadLoadStatus(CadLoadOutcome outcome, CadLoadContext context, string? projectPath)
    {
        var sourceLabel = outcome.Source == CadLoadSource.Path ? "path" : "embedded";
        var padCount = outcome.Cad.Pads.Count;
        var activePadCount = padCount - outcome.DuplicateSanitization.ExcludedPadIds.Count;
        var duplicateNote = outcome.DuplicateSanitization.HasExcludedDuplicates
            ? $" Auto-hidden same-layer duplicates: {outcome.DuplicateSanitization.ExcludedPadIds.Count}."
            : string.Empty;
        if (context == CadLoadContext.OpenDxf)
        {
            return $"DXF imported ({sourceLabel}). CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
        }

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return $"Project loaded. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
        }

        return $"Project loaded: {projectPath}. DXF ({sourceLabel}), CAD pads: {activePadCount}/{padCount}.{duplicateNote}";
    }

    private static void LogCadLoadOutcome(CadLoadOutcome outcome, CadLoadContext context)
    {
        if (outcome.DuplicateSanitization.HasExcludedDuplicates)
        {
            _logger.Warn(
                CultureInfo.InvariantCulture,
                "CAD load auto-hidden same-layer exact duplicate pads. groups={0}, duplicateIds={1}.",
                outcome.DuplicateSanitization.DuplicateSameLayerGroupCount,
                string.Join(", ", outcome.DuplicateSanitization.ExcludedPadIds));
        }

        if (outcome.Source == CadLoadSource.Path)
        {
            var label = context == CadLoadContext.OpenDxf ? "DXF imported" : "DXF reloaded";
            _logger.Info(CultureInfo.InvariantCulture, "{0}: {1} (pads={2})", label, outcome.SourcePath, outcome.Cad.Pads.Count);
            return;
        }

        _logger.Info(CultureInfo.InvariantCulture, "DXF loaded from embedded data (pads={0})", outcome.Cad.Pads.Count);
    }

    private enum CadLoadContext
    {
        OpenDxf,
        LoadProject,
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

    private Task TryLoadDxfLayerCatalogFromPathAsync(string path)
    {
        return Task.Run(() => _layerCatalogStateService.TryLoadFromPath(path));
    }

    private Task TryLoadDxfLayerCatalogFromEmbeddedAsync(byte[]? data)
    {
        return Task.Run(() => _layerCatalogStateService.TryLoadFromEmbedded(data));
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

        CadEditNotificationSnapshot? previous = null;
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
            ResetHiddenCadPads = () =>
            {
                previous = new CadEditNotificationSnapshot(this);
                ResetHiddenCadPads(previous);
            },
            ApplyCadLoadOutcomeAsync = outcome => ApplyCadLoadOutcomeCoreAsync(outcome, null, previous),
            SetProjectLastDxfPath = selectedPath => _projectFile.LastDxfPath = selectedPath,
            ClearLastSavedPath = () => _projectSession.LastSavedPath = null,
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
                _logger.Error(ex, "DXF import failed.");
            },
        });
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

        if (_cadEdit.AutoHiddenDuplicateCadPadIds.Count == 0)
        {
            return _cad;
        }

        return BuildActiveCadPadSet();
    }
}

