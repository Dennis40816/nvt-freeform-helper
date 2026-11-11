using System.Collections.ObjectModel;
using System.Globalization;
using FreeformHelper.Application.CadImport;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadImport;

namespace FreeformHelper.UI.Services.CadImport;

internal sealed partial class CadLayerWorkflow
{
    public bool CadLayersMatchDocument(CadPadSet cad)
    {
        var current = _state.LayerToggles.Select(static toggle => toggle.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = CadLayerUiSnapshotBuilder.Build(CaptureRequest(cad, null)).ToggleStates
            .Select(static state => state.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.SetEquals(expected);
    }

    public void UpdateLayerToggles(CadPadSet cad) => ApplySnapshot(CadLayerUiSnapshotBuilder.Build(CaptureRequest(cad, null)));

    public async Task LoadLayerOptionsAsync(CadPadSet cad, IReadOnlyList<LayerSelectionSnapshot>? selections)
    {
        var request = CaptureRequest(cad, selections);
        var snapshot = await Task.Run(() => CadLayerUiSnapshotBuilder.Build(request));
        // Deliberately accept older completions, as pinned by the characterization tests.
        ApplySnapshot(snapshot);
    }

    private CadLayerUiSnapshotRequest CaptureRequest(CadPadSet cad, IReadOnlyList<LayerSelectionSnapshot>? selections) => new(
        cad, _inputs.Catalog(), selections, _state.Pending.BoundLayerName, _state.SelectedBoundLayerOption?.Name,
        _state.Pending.RegularSourceLayerName, _state.SelectedRegularSourceLayerOption?.Name,
        _inputs.EditTarget(), _inputs.EditRotation(), MaxBoundLayerDropdownLayers: 256);

    private void ApplySnapshot(CadLayerUiSnapshot snapshot)
    {
        _state.ReplaceToggles(snapshot.ToggleStates.Select(state => (state.Name, state.IsSelected)));
        _state.HasCadLayers = snapshot.HasCadLayers;
        PublishBoundOptions(snapshot);
        PublishSourceOptions(snapshot);
        _effects.PublishEditOptions(snapshot.DxfEditLayerOptions, snapshot.SelectedDxfEditTargetLayerName,
            snapshot.SelectedDxfEditRotationLayerName);
        _state.Pending.BoundLayerName = null;
        _state.Pending.RegularSourceLayerName = null;
        _effects.NotifyEditActions();
        ApplyRegularSourceLayerAutoHidePolicy(filterCadPads: false);
        RefreshDxfRegularSourceHint();
        _state.NotifyCadLayerAggregateStateChanged();
        FilterCadPadsByLayer();
    }

    private void PublishBoundOptions(CadLayerUiSnapshot snapshot)
    {
        using (_guards.EnterBoundChange())
        {
            _state.BoundLayerOptions = new ObservableCollection<BoundLayerOption>(snapshot.BoundLayerOptions);
            _state.SelectedBoundLayerOption = _state.BoundLayerOptions.FirstOrDefault(option =>
                string.Equals(option.Name, snapshot.SelectedBoundLayerName, StringComparison.OrdinalIgnoreCase))
                ?? _state.BoundLayerOptions.First();
            _state.BoundLayerOptionSummary = snapshot.BoundLayerOptionSummary;
        }
    }

    private void PublishSourceOptions(CadLayerUiSnapshot snapshot)
    {
        using (_guards.EnterSourceChange())
        {
            _state.RegularSourceLayerOptions = new ObservableCollection<RegularSourceLayerOption>(snapshot.RegularSourceLayerOptions);
            _state.SelectedRegularSourceLayerOption = _state.RegularSourceLayerOptions.FirstOrDefault(option =>
                string.Equals(option.Name, snapshot.SelectedRegularSourceLayerName, StringComparison.OrdinalIgnoreCase))
                ?? _state.RegularSourceLayerOptions.First();
        }
    }

    public void InitializeLayerSelectionOptions()
    {
        using (_guards.EnterBoundChange())
        {
            _state.BoundLayerOptions.Add(new BoundLayerOption(null, "Auto (visible layers)"));
            _state.SelectedBoundLayerOption = _state.BoundLayerOptions[0];
        }
        using (_guards.EnterSourceChange())
        {
            _state.RegularSourceLayerOptions.Add(new RegularSourceLayerOption(null, "Select layer"));
            _state.SelectedRegularSourceLayerOption = _state.RegularSourceLayerOptions[0];
        }
    }

    public void OnBoundSelectionChanged()
    {
        if (_guards.IsBoundChangeActive()) return;
        if (_inputs.Alignment() == GridAlignmentMode.FromCadBounds) _ = _effects.Rebuild(true);
    }

    public void OnSourceSelectionChanged()
    {
        if (_guards.IsSourceChangeActive() || _guards.IsLoading()) return;
        _effects.ApplySettings(nameof(ICadLayerPresentation.SelectedRegularSourceLayerOption));
    }

    public void OnRecalcBoundsChanged()
    {
        if (!_guards.IsLoading()) _effects.ApplySettings(nameof(ICadLayerPresentation.RecalcBoundsOnLayerFilter));
    }

    public bool ApplyRegularSourceLayerAutoHidePolicy(bool filterCadPads = true)
    {
        if (_inputs.SourceMode() != RegularSourceMode.FromDxfLayer) return false;
        var regularLayer = _state.SelectedRegularSourceLayerOption?.Name;
        if (string.IsNullOrWhiteSpace(regularLayer)) return false;
        var target = _state.LayerToggles.FirstOrDefault(toggle =>
            string.Equals(toggle.Name, regularLayer, StringComparison.OrdinalIgnoreCase));
        if (target is null || !target.IsSelected) return false;
        using (_guards.EnterToggleChange()) target.IsSelected = false;
        if (!_guards.IsLoading()) _effects.MarkUnsaved();
        if (filterCadPads) FilterCadPadsByLayer();
        _logger.Info(CultureInfo.InvariantCulture, "Auto-hidden regular source layer from CAD view: {0}", regularLayer);
        return true;
    }

    public void RefreshDxfRegularSourceHint()
    {
        var mode = _inputs.SourceMode();
        var name = _state.SelectedRegularSourceLayerOption?.Name;
        var count = mode == RegularSourceMode.FromDxfLayer && !string.IsNullOrWhiteSpace(name)
            ? _document.Cad?.Pads.Count(pad => !_document.IsEffectivelyHidden(pad.Id)
                && string.Equals(pad.Layer, name, StringComparison.OrdinalIgnoreCase)) ?? 0
            : 0;
        _state.DxfRegularSourceHint = CadRegularSourceRules.BuildHint(mode, _document.Cad is not null,
            name, count, _inputs.XChannels(), _inputs.YChannels(), _inputs.CascadeNum());
    }
}
