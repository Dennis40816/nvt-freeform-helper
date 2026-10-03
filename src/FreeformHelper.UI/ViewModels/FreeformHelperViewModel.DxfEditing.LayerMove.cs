using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool HasSelectedCadPadsForDxfEdit => _selectedCadIds.Count > 0;

    public bool HasSelectedCombinedCadPadsForDxfEdit => _selectedCadIds.Any(id => TryResolveCombinedCadGroupId(id, out _));

    public bool HasManualHiddenCadPads => _hiddenCadPadIds.Any(id => !_combinedCadGroupIdBySourceCadId.ContainsKey(id));

    public bool HasDetectedDuplicateCadPads => _duplicateCadPadIdToCanonicalId.Count > 0;

    public bool CanMoveSelectedCadPadsToLayer =>
        _cad is not null &&
        HasSelectedCadPadsForDxfEdit &&
        !string.IsNullOrWhiteSpace(DxfEditTargetLayerName);

    public bool CanCreateLayerAndMoveSelectedCadPads =>
        _cad is not null &&
        HasSelectedCadPadsForDxfEdit &&
        !string.IsNullOrWhiteSpace(NormalizeDxfLayerName(DxfEditNewLayerName));

    public bool IsDxfEditRotationLayerScope => SelectedDxfEditRotationScopeOption.Value == DxfEditRotationScope.TargetLayer;

    public string DxfEditSelectionSummary =>
        FormattableString.Invariant(
            $"{_selectedCadIds.Count} CAD pad{(_selectedCadIds.Count == 1 ? string.Empty : "s")} selected");

    public string HiddenDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Hidden pads: {DeletedCadPadCount}. Click to open hidden CAD edit details.");

    public string CombinedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Combined groups: {CombinedCadPadCount}. Click to open synthetic combine details.");

    public string DuplicateDxfEditBadgeTooltip =>
        FormattableString.Invariant(
            $"Duplicate groups: {DuplicateCadPadCount}. Auto-hidden exact duplicates: {_autoHiddenDuplicateCadPadIds.Count}, restored: {GetRestoredDuplicateCadPadIds().Count}. Click to open duplicate details.");

    public string MovedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Moved pads: {RelayeredCadPadCount}. Click to open layer-move details.");

    private void PreserveDxfDependentLayerSelections()
    {
        _pendingBoundLayerName = SelectedBoundLayerOption?.Name;
        _pendingRegularSourceLayerName = SelectedRegularSourceLayerOption?.Name;
    }

    private void MoveSelectedCadPadsToLayer()
    {
        var targetLayerName = NormalizeDxfLayerName(DxfEditTargetLayerName);
        if (string.IsNullOrWhiteSpace(targetLayerName))
        {
            SetStatus("DXF edit: select target layer first.");
            return;
        }

        MoveSelectedCadPadsToLayerCore(targetLayerName, operationName: "move to existing layer");
    }

    private void CreateLayerAndMoveSelectedCadPads()
    {
        var newLayerName = NormalizeDxfLayerName(DxfEditNewLayerName);
        if (string.IsNullOrWhiteSpace(newLayerName))
        {
            SetStatus("DXF edit: input new layer name first.");
            return;
        }

        MoveSelectedCadPadsToLayerCore(newLayerName, operationName: "create layer and move");
        DxfEditNewLayerName = string.Empty;
    }

    private void MoveSelectedCadPadsToLayerCore(string targetLayerName, string operationName)
    {
        if (!TryGetLoadedCad(() => _cad, "DXF edit: import DXF first.", out var cad))
        {
            Logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no DXF loaded.");
            return;
        }

        var selected = _selectedCadIds
            .Where(id => !IsCadPadEffectivelyHidden(id))
            .Distinct()
            .ToList();
        if (selected.Count == 0)
        {
            SetStatus("DXF edit: select CAD pads first.");
            Logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no CAD pad selected.");
            return;
        }

        var selectedSet = selected.ToHashSet();
        var changedCount = 0;
        var updatedPads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            if (!selectedSet.Contains(pad.Id))
            {
                updatedPads.Add(pad);
                continue;
            }

            if (string.Equals(pad.Layer, targetLayerName, StringComparison.OrdinalIgnoreCase))
            {
                updatedPads.Add(pad);
                continue;
            }

            updatedPads.Add(new CadPad(pad.Id, pad.Name, targetLayerName, pad.Polygon));
            changedCount++;
        }

        if (changedCount == 0)
        {
            SetStatus($"DXF edit: selected pads are already in layer '{targetLayerName}'.");
            return;
        }

        var undoSnapshot = CaptureCadEditUndoSnapshot();
        var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
        _cad = new CadPadSet(updatedPads);
        _cachedFilteredCadForBuild = null;
        PreserveDxfDependentLayerSelections();
        UpdateLayerToggles(_cad);
        RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, targetLayerName);
        DxfEditTargetLayerName = targetLayerName;

        SyncRelayeredCadState();
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        SetStatus($"DXF edit: moved {changedCount} CAD pad(s) to layer '{targetLayerName}'.");
        Logger.Info(
            CultureInfo.InvariantCulture,
            "DXF layer move applied. op={0}, targetLayer={1}, changedPads={2}.",
            operationName,
            targetLayerName,
            changedCount);
        MarkUnsaved();
        PushUndo(() => RestoreCadEditUndoSnapshot(undoSnapshot), "DXF move CAD pads to layer");
    }

    private void RestoreLayerSelectionAfterCadLayerEdit(
        Dictionary<string, bool> previousSelection,
        string ensureVisibleLayerName)
    {
        if (LayerToggles.Count == 0)
        {
            return;
        }

        _suppressLayerToggleChange = true;
        var changed = false;
        try
        {
            foreach (var toggle in LayerToggles)
            {
                var isSelected = previousSelection.TryGetValue(toggle.Name, out var previous)
                    ? previous
                    : toggle.IsSelected;
                if (string.Equals(toggle.Name, ensureVisibleLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    isSelected = true;
                }

                if (toggle.IsSelected == isSelected)
                {
                    continue;
                }

                toggle.IsSelected = isSelected;
                changed = true;
            }
        }
        finally
        {
            _suppressLayerToggleChange = false;
        }

        if (changed)
        {
            FilterCadPadsByLayer();
        }

        NotifyCadLayerAggregateStateChanged();
    }

    private static string NormalizeDxfLayerName(string? layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return string.Empty;
        }

        return layerName.Trim();
    }

    private void RefreshDxfEditLayerOptions(IReadOnlyList<string> layerNames)
    {
        var ordered = layerNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        DxfEditLayerOptions = new ObservableCollection<string>(ordered);

        if (ordered.Count == 0)
        {
            DxfEditTargetLayerName = string.Empty;
            DxfEditRotationLayerName = string.Empty;
            NotifyDxfEditPanelActionStateChanged();
            return;
        }

        var selected = ordered.FirstOrDefault(name =>
            string.Equals(name, DxfEditTargetLayerName, StringComparison.OrdinalIgnoreCase))
            ?? ordered[0];
        DxfEditTargetLayerName = selected;
        var rotationSelected = ordered.FirstOrDefault(name =>
            string.Equals(name, DxfEditRotationLayerName, StringComparison.OrdinalIgnoreCase))
            ?? ordered[0];
        DxfEditRotationLayerName = rotationSelected;
        NotifyDxfEditPanelActionStateChanged();
    }

    partial void OnDxfEditTargetLayerNameChanged(string value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }

    partial void OnDxfEditRotationLayerNameChanged(string value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }

    partial void OnSelectedDxfEditRotationScopeOptionChanged(DxfEditRotationScopeOption value)
    {
        OnPropertyChanged(nameof(IsDxfEditRotationLayerScope));
        NotifyDxfEditPanelActionStateChanged();
    }

    partial void OnDxfEditNewLayerNameChanged(string value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }

    private void NotifyDxfEditPanelActionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedCadPadsForDxfEdit));
        OnPropertyChanged(nameof(HasSelectedCombinedCadPadsForDxfEdit));
        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(CanMoveSelectedCadPadsToLayer));
        OnPropertyChanged(nameof(CanCreateLayerAndMoveSelectedCadPads));
        OnPropertyChanged(nameof(CanRotateSelectedCadPads));
        OnPropertyChanged(nameof(CanOffsetSelectedCadOutputFwDiffIndices));
        OnPropertyChanged(nameof(IsDxfEditRotationLayerScope));
        OnPropertyChanged(nameof(DxfEditSelectionSummary));
        OnPropertyChanged(nameof(Step4DiffShiftSelectionSummary));
    }
}
