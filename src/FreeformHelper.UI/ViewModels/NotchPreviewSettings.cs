namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the notch canvas preview settings of the main view model: the preview and label switches,
/// the staged visualization step and the auto-play switch and interval. The view model replaces the
/// whole value when one of them changes and forwards one property per member so the bindings keep
/// their names.
/// </summary>
/// <param name="ShowNotchCanvasPreview">Whether Notch 2.2 canvas preview is active.</param>
/// <param name="ShowNotchToRegularLabels">Whether To Regular ratio labels should render on AA canvas.</param>
/// <param name="NotchPreviewVisualizationStep">Staged To Full visualization step on AA canvas. 1 = polygon-overlap seed, 2 = boundary candidate regulars, 3 = final To Full result.</param>
/// <param name="NotchPreviewAutoPlayEnabled">Whether Step 3 preview stage should auto-play (1->2->3 loop).</param>
/// <param name="NotchPreviewAutoPlayIntervalMs">Step 3 preview auto-play interval in milliseconds.</param>
public sealed record NotchPreviewSettings(
    bool ShowNotchCanvasPreview,
    bool ShowNotchToRegularLabels,
    decimal NotchPreviewVisualizationStep,
    bool NotchPreviewAutoPlayEnabled,
    decimal NotchPreviewAutoPlayIntervalMs)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static NotchPreviewSettings Initial { get; } = new(
        true,
        true,
        3m,
        true,
        700m);
}
