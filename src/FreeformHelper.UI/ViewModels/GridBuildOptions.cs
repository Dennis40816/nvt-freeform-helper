using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the grid build options of the main view model: the scan order, the grid alignment and regular
/// source modes, the local sizing flags, the area bucket limits and the preferred AA outline layer name.
/// The view model replaces the whole value when one of them changes and forwards one property per member
/// so the bindings keep their names.
/// </summary>
/// <param name="SelectedScanOrder">The currently selected <see cref="ScanOrder"/> enumeration value.</param>
/// <param name="GridAlignmentMode">The currently selected <see cref="GridAlignmentMode"/> enumeration value.</param>
/// <param name="RegularSourceMode">The currently selected <see cref="RegularSourceMode"/> enumeration value.</param>
/// <param name="UseLocalSizing">Whether local sizing (row-local width, column-local height) is enabled.</param>
/// <param name="IsWidthRowLocal">Whether width adjustment is row-local.</param>
/// <param name="IsHeightColumnLocal">Whether height adjustment is column-local.</param>
/// <param name="AreaBucketTolerance">The area bucket tolerance for coloring pads by area.</param>
/// <param name="MaxAreaBuckets">The maximum number of area buckets to use for coloring pads.</param>
/// <param name="CoordinatePreferredAaOutlineLayerName">The preferred AA outline layer name used by Coordinate.</param>
public sealed record GridBuildOptions(
    ScanOrder SelectedScanOrder,
    GridAlignmentMode GridAlignmentMode,
    RegularSourceMode RegularSourceMode,
    bool UseLocalSizing,
    bool IsWidthRowLocal,
    bool IsHeightColumnLocal,
    decimal AreaBucketTolerance,
    decimal MaxAreaBuckets,
    string CoordinatePreferredAaOutlineLayerName)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static GridBuildOptions Initial { get; } = new(
        default,
        default,
        default,
        true,
        true,
        true,
        0.001m,
        32m,
        string.Empty);
}
