using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool HasSelectedCadPadsForDxfEdit => _selectedCadIds.Count > 0;

    public bool HasSelectedCombinedCadPadsForDxfEdit => _selectedCadIds.Any(id => TryResolveCombinedCadGroupId(id, out _));

    public bool HasManualHiddenCadPads => _cadEdit.HiddenCadPadIds.Any(id => !_combinedCad.GroupIdBySourceCadId.ContainsKey(id));

    public bool HasDetectedDuplicateCadPads => _cadEdit.DuplicateCadPadIdToCanonicalId.Count > 0;

    public bool CanMoveSelectedCadPadsToLayer =>
        _cad is not null &&
        HasSelectedCadPadsForDxfEdit &&
        !string.IsNullOrWhiteSpace(DxfEditTargetLayerName);

    public bool CanCreateLayerAndMoveSelectedCadPads =>
        _cad is not null &&
        HasSelectedCadPadsForDxfEdit &&
        !string.IsNullOrWhiteSpace(DxfEditRules.NormalizeLayerName(DxfEditNewLayerName));

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
            $"Duplicate groups: {DuplicateCadPadCount}. Auto-hidden exact duplicates: {_cadEdit.AutoHiddenDuplicateCadPadIds.Count}, restored: {CadDocument.GetRestoredDuplicateIds().Count}. Click to open duplicate details.");

    public string MovedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Moved pads: {RelayeredCadPadCount}. Click to open layer-move details.");

    private void PreserveDxfDependentLayerSelections()
    {
        _pendingLayerSelection.BoundLayerName = SelectedBoundLayerOption?.Name;
        _pendingLayerSelection.RegularSourceLayerName = SelectedRegularSourceLayerOption?.Name;
    }

    private void MoveSelectedCadPadsToLayer()
    {
        var targetLayerName = DxfEditRules.NormalizeLayerName(DxfEditTargetLayerName);
        if (string.IsNullOrWhiteSpace(targetLayerName))
        {
            SetStatus("DXF edit: select target layer first.");
            return;
        }

        MoveSelectedCadPadsToLayerCore(targetLayerName, operationName: "move to existing layer");
    }

    private void CreateLayerAndMoveSelectedCadPads()
    {
        var newLayerName = DxfEditRules.NormalizeLayerName(DxfEditNewLayerName);
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
            _logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no DXF loaded.");
            return;
        }

        var selected = _selectedCadIds
            .Where(id => !CadDocument.IsEffectivelyHidden(id))
            .Distinct()
            .ToList();
        if (selected.Count == 0)
        {
            SetStatus("DXF edit: select CAD pads first.");
            _logger.Debug(CultureInfo.InvariantCulture, "DXF layer move skipped: no CAD pad selected.");
            return;
        }

        _cadEditWorkflow.MoveToLayer(cad, selected.ToHashSet(), targetLayerName, operationName);
    }

    private void RestoreLayerSelectionAfterCadLayerEdit(
        Dictionary<string, bool> previousSelection,
        string ensureVisibleLayerName)
    {
        if (LayerToggles.Count == 0)
        {
            return;
        }

        using (_settingsSuppression.EnterLayerToggleChange())
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

                toggle.IsSelected = isSelected;
            }
        }

        // The moved pads keep their old layer in the visible pad list until it is filtered again, so filter on every move.
        FilterCadPadsByLayer();

        NotifyCadLayerAggregateStateChanged();
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
