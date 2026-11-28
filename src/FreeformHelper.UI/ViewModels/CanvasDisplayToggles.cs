namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Supplies the existing default values for canvas display switches and DXF import choices.
/// Live values belong to the separate appearance and import owners; the root properties keep their binding names.
/// </summary>
/// <param name="ShowCad">Whether CAD pads should be displayed on the canvas.</param>
/// <param name="ShowRegular">Whether regular pads should be displayed on the canvas.</param>
/// <param name="HighlightUnmatched">Whether unmatched pads should be highlighted on the canvas.</param>
/// <param name="HighlightFreeform">Whether freeform pads should be highlighted on the canvas.</param>
/// <param name="ColorCadByArea">Whether CAD pads should be colored by area on the canvas.</param>
/// <param name="ImportOnlyClosedPolylines">Whether only closed polylines should be imported from DXF.</param>
/// <param name="ImportBlockPolylines">Whether polylines from the DXF BLOCKS section should be imported.</param>
public sealed record CanvasDisplayToggles(
    bool ShowCad,
    bool ShowRegular,
    bool HighlightUnmatched,
    bool HighlightFreeform,
    bool ColorCadByArea,
    bool ImportOnlyClosedPolylines,
    bool ImportBlockPolylines)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static CanvasDisplayToggles Initial { get; } = new(
        true,
        true,
        true,
        true,
        false,
        true,
        true);
}
