using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides services for managing manual sizing adjustments of the regular grid.
/// This includes applying specific widths/heights to rows/columns/pads, resetting sizes,
/// and handling the complex logic of distributing remaining space when some dimensions are fixed.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance service API is retained for workflow composition stability.")]
public sealed partial class ManualSizingService
{
    /// <summary>
    /// Captures the current state of grid sizing settings into a <see cref="ManualSizingSnapshot"/>.
    /// This is useful for implementing undo/redo functionality or reverting changes.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to capture.</param>
    /// <returns>A <see cref="ManualSizingSnapshot"/> representing the current sizing state.</returns>
    public static ManualSizingSnapshot CaptureSnapshot(GridSettings grid)
    {
        return new ManualSizingSnapshot(
            new List<double>(grid.ColumnWidths),
            new List<double>(grid.RowHeights),
            new List<bool>(grid.ColumnOverrides),
            new List<bool>(grid.RowOverrides),
            CloneJagged(grid.RowWidthOverrides),
            CloneJagged(grid.RowWidthOverrideFlags),
            CloneJagged(grid.ColumnHeightOverrides),
            CloneJagged(grid.ColumnHeightOverrideFlags),
            grid.WidthScope,
            grid.HeightScope);
    }

    /// <summary>
    /// Restores the grid sizing settings from a previously captured <see cref="ManualSizingSnapshot"/>.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to restore to.</param>
    /// <param name="snapshot">The <see cref="ManualSizingSnapshot"/> containing the state to restore.</param>
    public static void RestoreSnapshot(GridSettings grid, ManualSizingSnapshot snapshot)
    {
        grid.ReplaceSizingCollections(
            snapshot.ColumnWidths,
            snapshot.RowHeights,
            snapshot.ColumnOverrides,
            snapshot.RowOverrides,
            snapshot.RowWidthOverrides,
            snapshot.RowWidthOverrideFlags,
            snapshot.ColumnHeightOverrides,
            snapshot.ColumnHeightOverrideFlags);
        grid.WidthScope = snapshot.WidthScope;
        grid.HeightScope = snapshot.HeightScope;
    }

    /// <summary>
    /// Ensures that all sizing-related lists within <see cref="GridSettings"/> are
    /// correctly sized (number of columns/rows) and filled with default values if necessary.
    /// This prevents out-of-bounds errors and ensures consistent list lengths.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to update.</param>
    public static void EnsureSizingLists(GridSettings grid)
    {
        var cols = Math.Max(1, grid.XChannels);
        var rows = Math.Max(1, grid.YChannels);

        // Ensure main column widths/overrides and row heights/overrides lists are correctly sized.
        // Also ensure jagged lists for row-local width overrides and column-local height overrides are correctly sized.
        grid.ReplaceSizingCollections(
            Resize(grid.ColumnWidths, cols, 0.0),
            Resize(grid.RowHeights, rows, 0.0),
            Resize(grid.ColumnOverrides, cols, false),
            Resize(grid.RowOverrides, rows, false),
            EnsureJagged(grid.RowWidthOverrides, rows, cols, 0.0),
            EnsureJagged(grid.RowWidthOverrideFlags, rows, cols, false),
            EnsureJagged(grid.ColumnHeightOverrides, cols, rows, 0.0),
            EnsureJagged(grid.ColumnHeightOverrideFlags, cols, rows, false));
    }
}

/// <summary>
/// Represents a snapshot of the grid's manual sizing configuration at a specific point in time.
/// Used for undo/redo or restoring state. This record contains copies of all relevant sizing
/// lists and scope settings from <see cref="GridSettings"/>.
/// </summary>
public sealed record ManualSizingSnapshot(
    List<double> ColumnWidths,
    List<double> RowHeights,
    List<bool> ColumnOverrides,
    List<bool> RowOverrides,
    List<List<double>> RowWidthOverrides,
    List<List<bool>> RowWidthOverrideFlags,
    List<List<double>> ColumnHeightOverrides,
    List<List<bool>> ColumnHeightOverrideFlags,
    WidthAdjustmentScope WidthScope,
    HeightAdjustmentScope HeightScope);

/// <summary>
/// Represents the result of a manual sizing operation.
/// It indicates whether the operation was successful and provides an optional error message
/// along with a snapshot of the <see cref="GridSettings"/> before the operation was attempted.
/// </summary>
/// <param name="Success">True if the operation was successful, false otherwise.</param>
/// <param name="Error">An error message if the operation failed, otherwise null.</param>
/// <param name="Snapshot">A <see cref="ManualSizingSnapshot"/> captured before the operation, for potential rollback.</param>
public sealed record ManualSizingResult(bool Success, string? Error, ManualSizingSnapshot Snapshot)
{
    /// <summary>
    /// Creates a successful <see cref="ManualSizingResult"/> with the provided snapshot.
    /// </summary>
    /// <param name="snapshot">The <see cref="ManualSizingSnapshot"/> prior to the successful operation.</param>
    /// <returns>A successful <see cref="ManualSizingResult"/> instance.</returns>
    public static ManualSizingResult Ok(ManualSizingSnapshot snapshot) => new(true, null, snapshot);
    /// <summary>
    /// Creates a failed <see cref="ManualSizingResult"/> with an error message and the provided snapshot.
    /// </summary>
    /// <param name="snapshot">The <see cref="ManualSizingSnapshot"/> prior to the failed operation.</param>
    /// <param name="error">A string describing the reason for the failure.</param>
    /// <returns>A failed <see cref="ManualSizingResult"/> instance.</returns>
    public static ManualSizingResult Fail(ManualSizingSnapshot snapshot, string error) => new(false, error, snapshot);
}
