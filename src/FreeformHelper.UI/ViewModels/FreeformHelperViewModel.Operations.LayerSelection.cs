using System.ComponentModel;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool AreAllCadLayersSelected => LayerToggles.Count > 0 && LayerToggles.All(static toggle => toggle.IsSelected);

    public bool AreNoCadLayersSelected => LayerToggles.Count > 0 && LayerToggles.All(static toggle => !toggle.IsSelected);

    public bool AreSomeCadLayersSelected => LayerToggles.Count > 0 && !AreAllCadLayersSelected && !AreNoCadLayersSelected;

    public bool IsCadLayersOffSegmentActive => LayerToggles.Count > 0 && !AreAllCadLayersSelected;

    public bool IsCadLayerBulkToggleChecked
    {
        get => AreAllCadLayersSelected;
        set
        {
            if (value == AreAllCadLayersSelected)
            {
                return;
            }

            if (value)
            {
                SelectAllLayers();
            }
            else
            {
                DeselectAllLayers();
            }

            NotifyCadLayerAggregateStateChanged();
        }
    }

    public string CadLayerBulkToggleText => LayerToggles.Count == 0
        ? "No layers"
        : AreAllCadLayersSelected
            ? "All on"
            : "All off";

    public string CadLayerBulkToggleToolTip => LayerToggles.Count == 0
        ? "Load a DXF before changing layer visibility."
        : AreAllCadLayersSelected
            ? "All layers are visible. Click to hide every layer."
            : "Layer visibility is not fully on. Click to show every layer.";

    /// <summary>
    /// Marks the project as having unsaved changes, triggering UI indications
    /// and prompting the user to save before closing.
    /// </summary>
    private void MarkUnsaved()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        HasUnsavedChanges = true;
    }

    private bool CanProceedWithPendingEdits()
    {
        return EnsureNoPendingEdits?.Invoke() ?? true;
    }

    private Dictionary<string, bool> CaptureLayerSelectionSnapshot()
    {
        return LayerToggles.ToDictionary(
            toggle => toggle.Name,
            toggle => toggle.IsSelected,
            StringComparer.Ordinal);
    }

    private void NotifyCadLayerAggregateStateChanged()
    {
        OnPropertyChanged(nameof(AreAllCadLayersSelected));
        OnPropertyChanged(nameof(AreNoCadLayersSelected));
        OnPropertyChanged(nameof(AreSomeCadLayersSelected));
        OnPropertyChanged(nameof(IsCadLayersOffSegmentActive));
        OnPropertyChanged(nameof(IsCadLayerBulkToggleChecked));
        OnPropertyChanged(nameof(CadLayerBulkToggleText));
        OnPropertyChanged(nameof(CadLayerBulkToggleToolTip));
    }

    private void RestoreLayerSelectionSnapshot(Dictionary<string, bool> snapshot)
    {
        if (snapshot is null || snapshot.Count == 0)
        {
            return;
        }

        _suppressLayerToggleChange = true;
        foreach (var toggle in LayerToggles)
        {
            if (snapshot.TryGetValue(toggle.Name, out var isSelected))
            {
                toggle.IsSelected = isSelected;
            }
        }
        _suppressLayerToggleChange = false;

        MarkUnsaved();
        FilterCadPadsByLayer();
        NotifyCadLayerAggregateStateChanged();
    }

    private void QueueLayerSelectionUndoSnapshot()
    {
        if (_suppressLayerToggleChange || _suppressUndo || _isLoadingSettings || LayerToggles.Count == 0)
        {
            return;
        }

        _pendingLayerSelectionUndoSnapshot ??= CaptureLayerSelectionSnapshot();
    }

    /// <summary>
    /// Selects all available layers, making them visible/active.
    /// </summary>
    public void SelectAllLayers()
    {
        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        var undoSnapshot = CaptureLayerSelectionSnapshot();
        var changed = false;
        _suppressLayerToggleChange = true;
        foreach (var t in LayerToggles)
        {
            if (!t.IsSelected)
            {
                t.IsSelected = true;
                changed = true;
            }
        }
        _suppressLayerToggleChange = false;

        if (changed)
        {
            MarkUnsaved();
            FilterCadPadsByLayer();
            PushUndo(() => RestoreLayerSelectionSnapshot(undoSnapshot), "Select all DXF layers");
        }

        NotifyCadLayerAggregateStateChanged();
    }

    /// <summary>
    /// Deselects all available layers, making them invisible/inactive.
    /// </summary>
    public void DeselectAllLayers()
    {
        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        var undoSnapshot = CaptureLayerSelectionSnapshot();
        var changed = false;
        _suppressLayerToggleChange = true;
        foreach (var t in LayerToggles)
        {
            if (t.IsSelected)
            {
                t.IsSelected = false;
                changed = true;
            }
        }
        _suppressLayerToggleChange = false;

        if (changed)
        {
            MarkUnsaved();
            FilterCadPadsByLayer();
            PushUndo(() => RestoreLayerSelectionSnapshot(undoSnapshot), "Deselect all DXF layers");
        }

        NotifyCadLayerAggregateStateChanged();
    }

    private void OnLayerToggleChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName != nameof(LayerToggle.IsSelected))
        {
            return;
        }

        QueueLayerSelectionUndoSnapshot();
    }

    /// <summary>
    /// Event handler for when a <see cref="LayerToggle"/>'s properties change.
    /// Specifically responds to changes in <see cref="LayerToggle.IsSelected"/>.
    /// </summary>
    private void OnLayerToggleChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressLayerToggleChange)
        {
            return;
        }

        if (e.PropertyName == nameof(LayerToggle.IsSelected))
        {
            if (ApplyRegularSourceLayerAutoHidePolicy())
            {
                _pendingLayerSelectionUndoSnapshot = null;
                return;
            }

            MarkUnsaved();
            FilterCadPadsByLayer();
            if (_pendingLayerSelectionUndoSnapshot is { Count: > 0 } undoSnapshot)
            {
                PushUndo(() => RestoreLayerSelectionSnapshot(undoSnapshot), "DXF layer selection");
            }

            _pendingLayerSelectionUndoSnapshot = null;
        }

        NotifyCadLayerAggregateStateChanged();
    }

    public void ApplyLayerSelection(LayerToggle toggle, bool soloSelect)
    {
        if (toggle is null)
        {
            return;
        }

        if (!CanProceedWithPendingEdits())
        {
            return;
        }

        var undoSnapshot = CaptureLayerSelectionSnapshot();
        var changed = false;
        _suppressLayerToggleChange = true;
        if (soloSelect)
        {
            foreach (var t in LayerToggles)
            {
                var shouldSelect = ReferenceEquals(t, toggle);
                if (t.IsSelected != shouldSelect)
                {
                    t.IsSelected = shouldSelect;
                    changed = true;
                }
            }
        }
        else
        {
            var next = !toggle.IsSelected;
            if (toggle.IsSelected != next)
            {
                toggle.IsSelected = next;
                changed = true;
            }
        }
        _suppressLayerToggleChange = false;

        if (changed)
        {
            MarkUnsaved();
            FilterCadPadsByLayer();
            PushUndo(() => RestoreLayerSelectionSnapshot(undoSnapshot), "DXF layer quick select");
        }

        NotifyCadLayerAggregateStateChanged();
    }
}
