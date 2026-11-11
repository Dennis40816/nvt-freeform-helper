using FreeformHelper.Application.CadImport;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.CadImport;
using NLog;

namespace FreeformHelper.UI.Services.CadImport;

/// <summary>Coordinates layer transactions through neutral workspace ports. UI thread only.</summary>
internal sealed partial class CadLayerWorkflow : ICadLayerWorkflow
{
    private readonly CadDocumentSession _document;
    private readonly ICadLayerPresentation _state;
    private readonly CadLayerGuards _guards;
    private readonly CadLayerInputs _inputs;
    private readonly CadLayerEffects _effects;
    private readonly Logger _logger;

    internal CadLayerWorkflow(CadDocumentSession document, ICadLayerPresentation state,
        CadLayerGuards guards, CadLayerInputs inputs, CadLayerEffects effects, Logger logger)
    {
        _document = document;
        _state = state;
        _guards = guards;
        _inputs = inputs;
        _effects = effects;
        _logger = logger;
    }

    public Dictionary<string, bool> CaptureLayerSelectionSnapshot() => _state.LayerToggles.ToDictionary(
        toggle => toggle.Name, toggle => toggle.IsSelected, StringComparer.Ordinal);

    private void RestoreLayerSelectionSnapshot(Dictionary<string, bool> snapshot)
    {
        if (snapshot is null || snapshot.Count == 0) return;
        using (_guards.EnterToggleChange())
        {
            foreach (var toggle in _state.LayerToggles)
                if (snapshot.TryGetValue(toggle.Name, out var selected)) toggle.IsSelected = selected;
        }
        _effects.MarkUnsaved();
        FilterCadPadsByLayer();
        _state.NotifyCadLayerAggregateStateChanged();
    }

    public void OnLayerToggleChanging()
    {
        if (_guards.IsToggleChangeActive() || _guards.IsUndoSuppressed() || _guards.IsLoading() || _state.LayerToggles.Count == 0)
            return;
        _state.Pending.UndoSnapshot ??= CaptureLayerSelectionSnapshot();
    }

    public void OnLayerToggleChanged(string? propertyName)
    {
        if (_guards.IsToggleChangeActive()) return;
        if (propertyName == nameof(LayerToggle.IsSelected))
        {
            if (ApplyRegularSourceLayerAutoHidePolicy())
            {
                _state.Pending.UndoSnapshot = null;
                return;
            }
            _effects.MarkUnsaved();
            FilterCadPadsByLayer();
            if (_state.Pending.UndoSnapshot is { Count: > 0 } snapshot)
                _effects.PushUndo(() => RestoreLayerSelectionSnapshot(snapshot), "DXF layer selection");
            _state.Pending.UndoSnapshot = null;
        }
        _state.NotifyCadLayerAggregateStateChanged();
    }

    public void SelectAllLayers() => SetAllLayers(true, "Select all DXF layers");
    public void DeselectAllLayers() => SetAllLayers(false, "Deselect all DXF layers");

    private void SetAllLayers(bool selected, string undoLabel)
    {
        if (!_guards.CanProceed()) return;
        var snapshot = CaptureLayerSelectionSnapshot();
        if (SetSelected(_state.LayerToggles, _ => selected)) CommitSelection(snapshot, undoLabel);
        _state.NotifyCadLayerAggregateStateChanged();
    }

    public void ApplyLayerSelection(LayerToggle toggle, bool soloSelect)
    {
        if (toggle is null || !_guards.CanProceed()) return;
        var snapshot = CaptureLayerSelectionSnapshot();
        var changed = soloSelect
            ? SetSelected(_state.LayerToggles, item => ReferenceEquals(item, toggle))
            : SetSelected(new[] { toggle }, item => !item.IsSelected);
        if (changed) CommitSelection(snapshot, "DXF layer quick select");
        _state.NotifyCadLayerAggregateStateChanged();
    }

    private bool SetSelected(IEnumerable<LayerToggle> toggles, Func<LayerToggle, bool> selection)
    {
        var changed = false;
        using (_guards.EnterToggleChange())
        {
            foreach (var toggle in toggles)
            {
                var selected = selection(toggle);
                if (toggle.IsSelected == selected) continue;
                toggle.IsSelected = selected;
                changed = true;
            }
        }
        return changed;
    }

    private void CommitSelection(Dictionary<string, bool> snapshot, string undoLabel)
    {
        _effects.MarkUnsaved();
        FilterCadPadsByLayer();
        _effects.PushUndo(() => RestoreLayerSelectionSnapshot(snapshot), undoLabel);
    }

    public IReadOnlyList<DxfLayerCategoryState> GetDxfLayerCategoryStates()
    {
        if (_state.LayerToggles.Count == 0) return Array.Empty<DxfLayerCategoryState>();
        var states = new List<DxfLayerCategoryState>();
        foreach (var category in new[] { DxfLayerCategory.Polyline, DxfLayerCategory.Block, DxfLayerCategory.Text })
        {
            var members = GetLayerTogglesForCategory(category);
            if (members.Count == 0) continue;
            var (display, description) = CadLayerCategoryRules.GetMetadata(category);
            states.Add(new DxfLayerCategoryState(category, display, description, members.Count,
                members.Count(static toggle => toggle.IsSelected)));
        }
        return states;
    }

    public void ApplyLayerCategorySelection(DxfLayerCategory category, bool isSelected)
    {
        if (!_guards.CanProceed()) return;
        var targets = GetLayerTogglesForCategory(category);
        if (targets.Count == 0) return;
        var snapshot = CaptureLayerSelectionSnapshot();
        if (!SetSelected(targets, _ => isSelected)) return;
        var (display, _) = CadLayerCategoryRules.GetMetadata(category);
        var action = isSelected ? "On" : "Off";
        CommitSelection(snapshot, $"DXF layer category {display} {action}");
    }

    private List<LayerToggle> GetLayerTogglesForCategory(DxfLayerCategory category) =>
        _state.LayerToggles.Where(toggle => LayerMatchesCategory(toggle.Name, category)).ToList();

    private bool LayerMatchesCategory(string name, DxfLayerCategory category)
    {
        var found = _inputs.Catalog().TryGetLayer(name, out var item);
        return CadLayerCategoryRules.Matches(category, found, item?.HasPolylineContent ?? false,
            item?.HasBlockSectionContent ?? false, item?.HasTextContent ?? false);
    }
}
