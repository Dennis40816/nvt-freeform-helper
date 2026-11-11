using System.Collections.ObjectModel;
using FreeformHelper.Application.CadImport;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadImport;
using FreeformHelper.UI.Services.CadImport;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    // Compatibility surface for project, editing, IPC and existing callers. All state belongs to the children.
    private Task ApplyCadLoadOutcomeAsync(CadLoadOutcome outcome, UiViewSnapshot? snapshot) => CadImport.Workflow.ApplyOutcomeAsync(outcome, snapshot);
    private DxfImportOptions BuildDxfOptions() => CadImport.BuildDxfOptions();
    private void ClearDxfLayerCatalog() => CadImport.Workflow.ClearLayerCatalog();
    private Task TryLoadDxfLayerCatalogFromPathAsync(string path) => CadImport.Workflow.LoadCatalogFromPathAsync(path);
    private Task TryLoadDxfLayerCatalogFromEmbeddedAsync(byte[]? data) => CadImport.Workflow.LoadCatalogFromEmbeddedAsync(data);
    private static string BuildCadLoadStatus(CadLoadOutcome outcome, string? projectPath) => CadImportWorkflow.BuildCadLoadStatus(outcome, false, projectPath);
    private void LogCadLoadOutcome(CadLoadOutcome outcome) => ((CadImportWorkflow)CadImport.Workflow).LogCadLoadOutcome(outcome, false);
    private CadPadSet? BuildFilteredCadPadSet() => Layers.Workflow.BuildFilteredCadPadSet();
    private CadPadSet? BuildCadPadSetForBounds() => Layers.Workflow.BuildCadPadSetForBounds();
    internal void FilterCadPadsByLayer() => Layers.Workflow.FilterCadPadsByLayer();
    private void UpdateLayerToggles(CadPadSet cad) => Layers.Workflow.UpdateLayerToggles(cad);
    private void InitializeLayerSelectionOptions() => Layers.Workflow.InitializeLayerSelectionOptions();
    private void NotifyCadLayerAggregateStateChanged() => Layers.NotifyCadLayerAggregateStateChanged();
    private void RefreshDxfRegularSourceHint() => Layers.Workflow.RefreshDxfRegularSourceHint();
    private bool ApplyRegularSourceLayerAutoHidePolicy(bool filterCadPads = true) => Layers.Workflow.ApplyRegularSourceLayerAutoHidePolicy(filterCadPads);
    private Dictionary<string, bool> CaptureLayerSelectionSnapshot() => Layers.Workflow.CaptureLayerSelectionSnapshot();
    private PendingLayerSelectionState _pendingLayerSelection => Layers.Pending;
    public void SelectAllLayers() => Layers.SelectAllLayers();
    public void DeselectAllLayers() => Layers.DeselectAllLayers();
    public void ApplyLayerSelection(LayerToggle toggle, bool soloSelect) => Layers.ApplyLayerSelection(toggle, soloSelect);
    public void SelectCadAreaBucketFromPad(int anchorCadPadId) => Layers.SelectCadAreaBucketFromPad(anchorCadPadId);
    internal IReadOnlyList<DxfLayerCategoryState> GetDxfLayerCategoryStates() => Layers.GetDxfLayerCategoryStates();
    internal void ApplyLayerCategorySelection(DxfLayerCategory category, bool isSelected) => Layers.ApplyLayerCategorySelection(category, isSelected);

    public ObservableCollection<LayerToggle> LayerToggles { get => Layers.LayerToggles; set => Layers.LayerToggles = value; }
    public ObservableCollection<BoundLayerOption> BoundLayerOptions { get => Layers.BoundLayerOptions; set => Layers.BoundLayerOptions = value; }
    public ObservableCollection<RegularSourceLayerOption> RegularSourceLayerOptions { get => Layers.RegularSourceLayerOptions; set => Layers.RegularSourceLayerOptions = value; }
    public bool RecalcBoundsOnLayerFilter { get => Layers.RecalcBoundsOnLayerFilter; set => Layers.RecalcBoundsOnLayerFilter = value; }
    public bool AreAllCadLayersSelected => Layers.AreAllCadLayersSelected;
    public bool AreNoCadLayersSelected => Layers.AreNoCadLayersSelected;
    public bool AreSomeCadLayersSelected => Layers.AreSomeCadLayersSelected;
    public bool IsCadLayerBulkToggleChecked { get => Layers.IsCadLayerBulkToggleChecked; set => Layers.IsCadLayerBulkToggleChecked = value; }
    public string CadLayerBulkToggleText => Layers.CadLayerBulkToggleText;
    public string CadLayerBulkToggleToolTip => Layers.CadLayerBulkToggleToolTip;
}
