namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Holds the manual sizing inputs of the main view model: the pending column width and row height, their
/// mixed flags, the manual row and column ranges and the range status. The view model
/// replaces the whole value when one of them changes and forwards one property per member so the bindings
/// keep their names.
/// </summary>
/// <param name="PendingColumnWidth">The pending width value for manual column/pad sizing.</param>
/// <param name="PendingRowHeight">The pending height value for manual row/pad sizing.</param>
/// <param name="IsPendingColumnWidthMixed">Whether <see cref="PendingColumnWidth"/> represents mixed values in a selection.</param>
/// <param name="IsPendingRowHeightMixed">Whether <see cref="PendingRowHeight"/> represents mixed values in a selection.</param>
/// <param name="ManualRowsRange">The string input for manual row range selection (e.g., "1-3,5").</param>
/// <param name="ManualColsRange">The string input for manual column range selection (e.g., "A-C,E").</param>
/// <param name="ManualRangeStatus">The status message for the manual range input (e.g., "No manual range.", "Invalid input.").</param>
public sealed record ManualSizingInput(
    decimal PendingColumnWidth,
    decimal PendingRowHeight,
    bool IsPendingColumnWidthMixed,
    bool IsPendingRowHeightMixed,
    string ManualRowsRange,
    string ManualColsRange,
    string ManualRangeStatus)
{
    /// <summary>
    /// Gets the value of a new view model before any project settings are loaded.
    /// </summary>
    public static ManualSizingInput Initial { get; } = new(
        0m,
        0m,
        false,
        false,
        string.Empty,
        string.Empty,
        "No manual range.");
}
