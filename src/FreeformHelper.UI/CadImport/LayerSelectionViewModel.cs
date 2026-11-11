using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.Application.CadImport;

namespace FreeformHelper.UI.CadImport;

/// <summary>Owns layer inputs, collection identities and subscriptions. UI thread only.</summary>
public sealed partial class LayerSelectionViewModel : ObservableObject, ICadLayerPresentation
{
    internal LayerSelectionViewModel(Func<ICadLayerPresentation, ICadLayerWorkflow> createWorkflow)
    {
        Workflow = createWorkflow(this);
    }

    internal ICadLayerWorkflow Workflow { get; }
    internal PendingLayerSelectionState Pending { get; } = new();
    PendingLayerSelectionState ICadLayerPresentation.Pending => Pending;

    // Arbitrary collection assignment deliberately keeps the old toggle handlers.
    [ObservableProperty] private ObservableCollection<LayerToggle> _layerToggles = new();
    [ObservableProperty] private ObservableCollection<BoundLayerOption> _boundLayerOptions = new();
    [ObservableProperty] private ObservableCollection<RegularSourceLayerOption> _regularSourceLayerOptions = new();
    [ObservableProperty] private bool _recalcBoundsOnLayerFilter;

    partial void OnRecalcBoundsOnLayerFilterChanged(bool value) => Workflow.OnRecalcBoundsChanged();

    public bool AreAllCadLayersSelected => LayerToggles.Count > 0 && LayerToggles.All(static toggle => toggle.IsSelected);

    public bool AreNoCadLayersSelected => LayerToggles.Count > 0 && LayerToggles.All(static toggle => !toggle.IsSelected);

    public bool AreSomeCadLayersSelected => LayerToggles.Count > 0 && !AreAllCadLayersSelected && !AreNoCadLayersSelected;

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
            : AreSomeCadLayersSelected ? "Partly on" : "All off";

    public string CadLayerBulkToggleToolTip => LayerToggles.Count == 0
        ? "Load a DXF before changing layer visibility."
        : AreAllCadLayersSelected
            ? "All layers are visible. Click to hide every layer."
            : "Layer visibility is not fully on. Click to show every layer.";

    public void NotifyCadLayerAggregateStateChanged()
    {
        OnPropertyChanged(nameof(AreAllCadLayersSelected));
        OnPropertyChanged(nameof(AreNoCadLayersSelected));
        OnPropertyChanged(nameof(AreSomeCadLayersSelected));
        OnPropertyChanged(nameof(IsCadLayerBulkToggleChecked));
        OnPropertyChanged(nameof(CadLayerBulkToggleText));
        OnPropertyChanged(nameof(CadLayerBulkToggleToolTip));
    }

    public void SelectAllLayers() => Workflow.SelectAllLayers();
    public void DeselectAllLayers() => Workflow.DeselectAllLayers();
    public void ApplyLayerSelection(LayerToggle toggle, bool soloSelect) => Workflow.ApplyLayerSelection(toggle, soloSelect);
    public void SelectCadAreaBucketFromPad(int anchorCadPadId) => Workflow.SelectCadAreaBucketFromPad(anchorCadPadId);
    internal IReadOnlyList<DxfLayerCategoryState> GetDxfLayerCategoryStates() => Workflow.GetDxfLayerCategoryStates();
    internal void ApplyLayerCategorySelection(DxfLayerCategory category, bool isSelected) => Workflow.ApplyLayerCategorySelection(category, isSelected);

    public void ReplaceToggles(IEnumerable<(string Name, bool IsSelected)> states)
    {
        DetachToggles();
        var toggles = new ObservableCollection<LayerToggle>();
        foreach (var state in states)
        {
            var toggle = new LayerToggle(state.Name, state.IsSelected);
            toggle.PropertyChanging += OnLayerToggleChanging;
            toggle.PropertyChanged += OnLayerToggleChanged;
            toggles.Add(toggle);
        }
        LayerToggles = toggles;
    }

    internal void DetachAndClearLayerToggles()
    {
        DetachToggles();
        LayerToggles.Clear();
    }

    private void DetachToggles()
    {
        foreach (var toggle in LayerToggles)
        {
            toggle.PropertyChanging -= OnLayerToggleChanging;
            toggle.PropertyChanged -= OnLayerToggleChanged;
        }
    }

    private void OnLayerToggleChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (e.PropertyName == nameof(LayerToggle.IsSelected)) Workflow.OnLayerToggleChanging();
    }

    private void OnLayerToggleChanged(object? sender, PropertyChangedEventArgs e) => Workflow.OnLayerToggleChanged(e.PropertyName);
}
