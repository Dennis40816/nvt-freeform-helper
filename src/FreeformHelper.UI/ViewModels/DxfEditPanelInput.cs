namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the inputs of the DXF edit panel of the main view model: the target layer name, the rotation scope,
/// the rotation layer name, the new layer name, the rotation angle and the advanced panel expander state.
/// The view model replaces the whole value when one of them changes and forwards one property per member
/// so the bindings keep their names.
/// </summary>
/// <param name="DxfEditTargetLayerName">The target DXF layer name used to move selected pads.</param>
/// <param name="SelectedDxfEditRotationScopeOption">The currently selected DXF rotation scope option.</param>
/// <param name="DxfEditRotationLayerName">The target DXF layer name used when rotating by layer scope.</param>
/// <param name="DxfEditNewLayerName">The input text used to create a new DXF layer.</param>
/// <param name="DxfEditRotationDegrees">The rotation angle for selected CAD pads in DXF edit workflow.</param>
/// <param name="IsDxfEditAdvancedPanelExpanded">Whether the advanced DXF edit block is expanded.</param>
public sealed record DxfEditPanelInput(
    string DxfEditTargetLayerName,
    FreeformHelperViewModel.DxfEditRotationScopeOption SelectedDxfEditRotationScopeOption,
    string DxfEditRotationLayerName,
    string DxfEditNewLayerName,
    decimal DxfEditRotationDegrees,
    bool IsDxfEditAdvancedPanelExpanded)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static DxfEditPanelInput Initial { get; } = new(
        string.Empty,
        default,
        string.Empty,
        string.Empty,
        90m,
        true);
}
