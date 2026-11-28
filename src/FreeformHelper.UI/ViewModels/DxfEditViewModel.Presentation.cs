// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using FreeformHelper.Application.CadEditing;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditViewModel
{
    public bool HasSelectedCadPadsForDxfEdit => _interaction.SelectedCadIds().Count > 0;

    public bool HasSelectedCombinedCadPadsForDxfEdit =>
        _interaction.SelectedCadIds().Any(id => CadDocument.TryResolveCombinedGroupId(id, out _));

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
            $"{_interaction.SelectedCadIds().Count} CAD pad{(_interaction.SelectedCadIds().Count == 1 ? string.Empty : "s")} selected");

    public string HiddenDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Hidden pads: {DeletedCadPadCount}. Click to open hidden CAD edit details.");

    public string CombinedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Combined groups: {CombinedCadPadCount}. Click to open synthetic combine details.");

    public string DuplicateDxfEditBadgeTooltip =>
        FormattableString.Invariant(
            $"Duplicate groups: {DuplicateCadPadCount}. Auto-hidden exact duplicates: {_cadEdit.AutoHiddenDuplicateCadPadIds.Count}, restored: {CadDocument.GetRestoredDuplicateIds().Count}. Click to open duplicate details.");

    public string MovedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Moved pads: {RelayeredCadPadCount}. Click to open layer-move details.");

    public bool CanRotateSelectedCadPads =>
        _cad is not null &&
        _actions.GetRotationTargetCadIds().Count > 0 &&
        Math.Abs(DxfEditRules.NormalizeRotationDegrees((double)DxfEditRotationDegrees)) > 1e-9;

    public string RotatedDxfEditBadgeTooltip =>
        FormattableString.Invariant($"Rotated pads: {RotatedCadPadCount}. Click to open geometry-rotation details.");

    internal void RefreshDxfEditLayerOptions(IReadOnlyList<string> layerNames)
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

    internal void NotifyDxfEditPanelActionStateChanged()
    {
        OnPropertyChanged(nameof(HasSelectedCadPadsForDxfEdit));
        OnPropertyChanged(nameof(HasSelectedCombinedCadPadsForDxfEdit));
        OnPropertyChanged(nameof(HasDetectedDuplicateCadPads));
        OnPropertyChanged(nameof(CanMoveSelectedCadPadsToLayer));
        OnPropertyChanged(nameof(CanCreateLayerAndMoveSelectedCadPads));
        OnPropertyChanged(nameof(CanRotateSelectedCadPads));
        _interaction.NotifyMappingEligibility();
        OnPropertyChanged(nameof(IsDxfEditRotationLayerScope));
        OnPropertyChanged(nameof(DxfEditSelectionSummary));
        _interaction.NotifyMappingSelectionSummary();
    }

    partial void OnDxfEditRotationDegreesChanged(decimal value)
    {
        NotifyDxfEditPanelActionStateChanged();
    }

    public string DxfEditsHeaderText =>
        $"Hidden {DeletedCadPadCount} · Merged {CombinedCadPadCount}";

    public string DxfRotationScopeHint
    {
        get
        {
            if (!IsDxfEditRotationLayerScope || _cad is null) return "Applies to selected CAD pads.";
            var targets = _actions.GetRotationTargetCadIds();
            return targets.SetEquals(_interaction.SelectedCadIds())
                ? "Applies to the chosen layer."
                : $"Layer scope · {targets.Count} target pads; selection is unchanged.";
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(DeletedCadPadCount) or nameof(CombinedCadPadCount))
        {
            OnPropertyChanged(nameof(DxfEditsHeaderText));
        }
        if (e.PropertyName is nameof(SelectedDxfEditRotationScopeOption) or nameof(DxfEditRotationLayerName)
            or nameof(CanRotateSelectedCadPads))
        {
            OnPropertyChanged(nameof(DxfRotationScopeHint));
        }
    }
}
