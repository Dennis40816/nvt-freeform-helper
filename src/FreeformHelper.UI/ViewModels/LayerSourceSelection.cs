namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the layer source selection of the main view model: the CAD layer flag, the selected bound layer
/// option and its summary, and the selected regular source layer option and its hint.
/// The view model replaces the whole value when one of them changes and forwards one property per member
/// so the bindings keep their names. The option lists stay on the view model because a record start
/// value is shared.
/// </summary>
/// <param name="HasCadLayers">Whether any CAD layers are available. Used to hide the layer section when no DXF is loaded.</param>
/// <param name="SelectedBoundLayerOption">The currently selected layer option for bounding the grid.</param>
/// <param name="BoundLayerOptionSummary">The helper text shown under bounds layer selector, including truncation hint when available layers exceed dropdown cap.</param>
/// <param name="SelectedRegularSourceLayerOption">The currently selected layer option for DXF regular source mode.</param>
/// <param name="DxfRegularSourceHint">Helper text for DXF regular-source validation and suggested IC/grid settings.</param>
public sealed record LayerSourceSelection(
    bool HasCadLayers,
    FreeformHelperViewModel.BoundLayerOption SelectedBoundLayerOption,
    string BoundLayerOptionSummary,
    FreeformHelperViewModel.RegularSourceLayerOption SelectedRegularSourceLayerOption,
    string DxfRegularSourceHint)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static LayerSourceSelection Initial { get; } = new(
        false,
        new FreeformHelperViewModel.BoundLayerOption(null, "Auto (visible layers)"),
        "Hidden bounds layer is still valid.",
        new FreeformHelperViewModel.RegularSourceLayerOption(null, "Select layer"),
        string.Empty);
}
