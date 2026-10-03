using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task ApplyCadLoadOutcomeAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot)
    {
        _cad = outcome.Cad;
        ResetRegularVisibilityMaskSelection(clearProjectEmbeddedMask: !_isLoadingSettings);
        ClearAutoHiddenDuplicateCadPadState();
        ClearDxfEditRuntimeState(resetBaseline: false);
        SetAutoHiddenDuplicateCadPads(outcome.DuplicateSanitization);
        ResetDxfEditBaseline(outcome.Cad);
        SyncHiddenCadIdsToProject();
        SyncCombinedCadState();
        SyncDuplicateCadState();
        _cachedFilteredCadForBuild = null;

        var layerUiSnapshot = await Task.Run(() => BuildCadLayerUiSnapshot(outcome.Cad, snapshot?.LayerSelections));

        ApplyCadLayerUiSnapshot(layerUiSnapshot);
    }

    private CadLayerUiSnapshot BuildCadLayerUiSnapshot(CadPadSet cad, IReadOnlyList<LayerSelectionSnapshot>? layerSelections)
    {
        return CadLayerUiSnapshotBuilder.Build(new CadLayerUiSnapshotRequest(
            Cad: cad,
            Catalog: _layerCatalogStateService.Catalog,
            LayerSelections: layerSelections,
            PendingBoundLayerName: _pendingBoundLayerName,
            SelectedBoundLayerName: SelectedBoundLayerOption?.Name,
            PendingRegularSourceLayerName: _pendingRegularSourceLayerName,
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

        _suppressBoundLayerSelectionChange = true;
        BoundLayerOptions = new ObservableCollection<BoundLayerOption>(snapshot.BoundLayerOptions);
        SelectedBoundLayerOption = BoundLayerOptions.FirstOrDefault(option =>
            string.Equals(option.Name, snapshot.SelectedBoundLayerName, StringComparison.OrdinalIgnoreCase))
            ?? BoundLayerOptions.First();
        BoundLayerOptionSummary = snapshot.BoundLayerOptionSummary;
        _suppressBoundLayerSelectionChange = false;

        _suppressRegularSourceLayerSelectionChange = true;
        RegularSourceLayerOptions = new ObservableCollection<RegularSourceLayerOption>(snapshot.RegularSourceLayerOptions);
        SelectedRegularSourceLayerOption = RegularSourceLayerOptions.FirstOrDefault(option =>
            string.Equals(option.Name, snapshot.SelectedRegularSourceLayerName, StringComparison.OrdinalIgnoreCase))
            ?? RegularSourceLayerOptions.First();
        _suppressRegularSourceLayerSelectionChange = false;

        DxfEditLayerOptions = new ObservableCollection<string>(snapshot.DxfEditLayerOptions);
        DxfEditTargetLayerName = snapshot.SelectedDxfEditTargetLayerName;
        DxfEditRotationLayerName = snapshot.SelectedDxfEditRotationLayerName;
        _pendingBoundLayerName = null;
        _pendingRegularSourceLayerName = null;
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
            Logger.Warn(
                CultureInfo.InvariantCulture,
                "CAD load auto-hidden same-layer exact duplicate pads. groups={0}, duplicateIds={1}.",
                outcome.DuplicateSanitization.DuplicateSameLayerGroupCount,
                string.Join(", ", outcome.DuplicateSanitization.ExcludedPadIds));
        }

        if (outcome.Source == CadLoadSource.Path)
        {
            var label = context == CadLoadContext.OpenDxf ? "DXF imported" : "DXF reloaded";
            Logger.Info(CultureInfo.InvariantCulture, "{0}: {1} (pads={2})", label, outcome.SourcePath, outcome.Cad.Pads.Count);
            return;
        }

        Logger.Info(CultureInfo.InvariantCulture, "DXF loaded from embedded data (pads={0})", outcome.Cad.Pads.Count);
    }

    private enum CadLoadContext
    {
        OpenDxf,
        LoadProject,
    }
}

