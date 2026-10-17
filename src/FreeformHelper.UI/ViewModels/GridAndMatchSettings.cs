namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the grid, coordinate, active area, panel offset and match settings that the main view model
/// edits as plain values. The view model replaces the whole value when one of them changes and forwards
/// one property per member so the bindings keep their names.
/// </summary>
/// <param name="XChannels">The number of X channels (columns) in the grid.</param>
/// <param name="YChannels">The number of Y channels (rows) in the grid.</param>
/// <param name="CascadeNum">The number of cascaded ICs.</param>
/// <param name="CoordinatePixelWidth">The default coordinate-planner pixel width.</param>
/// <param name="CoordinatePixelHeight">The default coordinate-planner pixel height.</param>
/// <param name="GridPaddingPercent">The grid padding percentage.</param>
/// <param name="ActiveAreaWidth">The active area width.</param>
/// <param name="ActiveAreaHeight">The active area height.</param>
/// <param name="PanelBiasX">The panel bias X.</param>
/// <param name="PanelBiasY">The panel bias Y.</param>
/// <param name="MatchThreshold">The diagnostic threshold for highlighting low-coverage regular pads.</param>
/// <param name="FreeformAxisThreshold">The directional spread threshold used for freeform auto-detection.</param>
/// <param name="EnableAutoDetectXy">Whether auto-detect can classify XYWay.</param>
/// <param name="EnableFreeformEdgeSpecialization">Whether freeform auto-detect should keep one-side boundary spill as freeform when TH would otherwise suppress it.</param>
public sealed record GridAndMatchSettings(
    decimal XChannels,
    decimal YChannels,
    decimal CascadeNum,
    decimal CoordinatePixelWidth,
    decimal CoordinatePixelHeight,
    decimal GridPaddingPercent,
    decimal ActiveAreaWidth,
    decimal ActiveAreaHeight,
    decimal PanelBiasX,
    decimal PanelBiasY,
    decimal MatchThreshold,
    decimal FreeformAxisThreshold,
    bool EnableAutoDetectXy,
    bool EnableFreeformEdgeSpecialization)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static GridAndMatchSettings Initial { get; } = new(
        0m,
        0m,
        0m,
        1m,
        1m,
        0m,
        310m,
        174m,
        0m,
        0m,
        0m,
        0.55m,
        false,
        false);
}
