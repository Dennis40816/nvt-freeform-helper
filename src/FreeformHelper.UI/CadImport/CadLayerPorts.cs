using System.Collections.ObjectModel;
using FreeformHelper.Application.CadImport;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.CadImport;

internal interface ICadLayerWorkflow
{
    void SelectAllLayers();
    void DeselectAllLayers();
    void ApplyLayerSelection(LayerToggle toggle, bool soloSelect);
    void OnLayerToggleChanging();
    void OnLayerToggleChanged(string? propertyName);
    void OnBoundSelectionChanged();
    void OnSourceSelectionChanged();
    void OnRecalcBoundsChanged();
    IReadOnlyList<DxfLayerCategoryState> GetDxfLayerCategoryStates();
    void ApplyLayerCategorySelection(DxfLayerCategory category, bool isSelected);
    Dictionary<string, bool> CaptureLayerSelectionSnapshot();
    void UpdateLayerToggles(CadPadSet cad);
    bool CadLayersMatchDocument(CadPadSet cad);
    Task LoadLayerOptionsAsync(CadPadSet cad, IReadOnlyList<LayerSelectionSnapshot>? selections);
    void InitializeLayerSelectionOptions();
    bool ApplyRegularSourceLayerAutoHidePolicy(bool filterCadPads = true);
    void RefreshDxfRegularSourceHint();
    void FilterCadPadsByLayer();
    CadPadSet? BuildFilteredCadPadSet();
    CadPadSet? BuildCadPadSetForBounds();
    void SelectCadAreaBucketFromPad(int anchorCadPadId);
}

internal interface ICadLayerPresentation
{
    ObservableCollection<LayerToggle> LayerToggles { get; set; }
    ObservableCollection<BoundLayerOption> BoundLayerOptions { get; set; }
    ObservableCollection<RegularSourceLayerOption> RegularSourceLayerOptions { get; set; }
    bool HasCadLayers { get; set; }
    BoundLayerOption SelectedBoundLayerOption { get; set; }
    string BoundLayerOptionSummary { get; set; }
    RegularSourceLayerOption SelectedRegularSourceLayerOption { get; set; }
    string DxfRegularSourceHint { get; set; }
    bool RecalcBoundsOnLayerFilter { get; set; }
    PendingLayerSelectionState Pending { get; }
    void ReplaceToggles(IEnumerable<(string Name, bool IsSelected)> states);
    void NotifyCadLayerAggregateStateChanged();
}

internal sealed record CadLayerGuards(
    Func<bool> CanProceed, Func<bool> IsLoading, Func<bool> IsUndoSuppressed,
    Func<IDisposable> EnterToggleChange, Func<bool> IsToggleChangeActive,
    Func<IDisposable> EnterBoundChange, Func<bool> IsBoundChangeActive,
    Func<IDisposable> EnterSourceChange, Func<bool> IsSourceChangeActive);

internal sealed record CadLayerInputs(
    Func<DxfLayerCatalog> Catalog, Func<RegularSourceMode> SourceMode, Func<GridAlignmentMode> Alignment,
    Func<decimal> XChannels, Func<decimal> YChannels, Func<decimal> CascadeNum,
    Func<string> EditTarget, Func<string> EditRotation,
    Func<bool> ColorByArea, Func<decimal> AreaTolerance, Func<IReadOnlyList<CadPad>> VisiblePads);

internal sealed record CadLayerEffects(
    Action MarkUnsaved, Action<Action, string> PushUndo, Action<string> ApplySettings,
    Func<bool, Task> Rebuild, Action<string> SetStatus, Action<string, Exception> SetError,
    Action<IReadOnlyList<string>, string, string> PublishEditOptions, Action NotifyEditActions,
    Action ClearOutput, Action<IReadOnlyList<CadPad>> UpdateOutput, Action SyncHidden,
    Action ClearMatches, Action ResetWorkflow, Action<IReadOnlyList<int>> SelectPads);
