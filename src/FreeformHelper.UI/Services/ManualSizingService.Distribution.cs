using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed partial class ManualSizingService
{
    /// <summary>
    /// Composes the effective widths for a given row by combining global column widths and row-local overrides.
    /// </summary>
    private static List<double> ComposeRowWidths(GridSettings grid, int row)
    {
        var cols = Math.Max(1, grid.XChannels);
        var baseWidths = Resize(grid.ColumnWidths, cols, 0.0); // Start with global column widths.
        if (grid.RowWidthOverrides.Count > row)
        {
            var overrides = Resize(grid.RowWidthOverrides[row], cols, 0.0); // Get row-specific overrides.

            // Apply row-specific overrides where they exist and are positive.
            for (var c = 0; c < cols; c++)
            {
                if (overrides[c] > 0)
                {
                    baseWidths[c] = overrides[c];
                }
            }
        }

        return baseWidths;
    }

    /// <summary>
    /// Composes the effective heights for a given column by combining global row heights and column-local overrides.
    /// </summary>
    private static List<double> ComposeColumnHeights(GridSettings grid, int col)
    {
        var rows = Math.Max(1, grid.YChannels);
        var baseHeights = Resize(grid.RowHeights, rows, 0.0); // Start with global row heights.
        if (grid.ColumnHeightOverrides.Count > col)
        {
            var overrides = Resize(grid.ColumnHeightOverrides[col], rows, 0.0); // Get column-specific overrides.

            // Apply column-specific overrides where they exist and are positive.
            for (var r = 0; r < rows; r++)
            {
                if (overrides[r] > 0)
                {
                    baseHeights[r] = overrides[r];
                }
            }
        }

        return baseHeights;
    }

    /// <summary>
    /// Helper method to restore grid settings from a snapshot and return a failed result.
    /// (Note: This method is not directly used in the current version of the provided code snippet,
    /// but is kept as a utility if needed).
    /// </summary>
    private static ManualSizingResult FailWithRestore(GridSettings grid, ManualSizingSnapshot snapshot, string error)
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
        return ManualSizingResult.Fail(snapshot, error);
    }

    /// <summary>
    /// Determines the target total width and height of the grid based on alignment mode and CAD data.
    /// </summary>
    private static (double width, double height) GetTargetGridSize(GridSettings grid, GridAlignmentMode alignmentMode, CadPadSet? cad)
    {
        if (alignmentMode == GridAlignmentMode.FromCadBounds && cad is not null)
        {
            var bounds = cad.Bounds;
            // Calculate padding based on a ratio of the CAD bounds.
            var padX = bounds.Width * grid.BoundsPaddingRatio;
            var padY = bounds.Height * grid.BoundsPaddingRatio;
            return (bounds.Width + padX * 2.0, bounds.Height + padY * 2.0); // Add padding to both sides.
        }

        // If not aligned to CAD bounds, use active area dimensions.
        return (grid.ActiveAreaWidth, grid.ActiveAreaHeight);
    }

    /// <summary>
    /// Attempts to distribute sizes (widths or heights) among elements, respecting overrides.
    /// This is a convenience wrapper for <see cref="TryDistributeSizes"/>.
    /// </summary>
    private static bool TryDistributeSizesWithOverrides(List<double> sizes, List<bool> overrides, double total, out string error)
    {
        // Build autoFlags: true for elements that should automatically adjust, false for overridden elements.
        return TryDistributeSizes(sizes, BuildAutoFlags(overrides, sizes.Count), total, out error);
    }

    /// <summary>
    /// Core logic for distributing sizes to fit a total, allowing some elements to be "auto-sized".
    /// Fixed elements maintain their specified size, and the remaining space is distributed proportionally
    /// among auto-sized elements based on their current relative sizes, or equally if all are zero.
    /// </summary>
    /// <param name="sizes">The list of sizes (input and output). Values can be modified.</param>
    /// <param name="autoFlags">Boolean flags indicating which elements should be automatically sized (true).</param>
    /// <param name="total">The target total size that all elements should sum up to.</param>
    /// <param name="error">Output: An error message if distribution fails (e.g., fixed sizes exceed total).</param>
    /// <returns>True if successful, false otherwise.</returns>
    private static bool TryDistributeSizes(List<double> sizes, List<bool> autoFlags, double total, out string error)
    {
        error = string.Empty;
        if (sizes.Count == 0) return true;

        var fixedSum = 0.0;
        var autoIndices = new List<int>();
        for (var i = 0; i < sizes.Count; i++)
        {
            if (autoFlags[i])
            {
                autoIndices.Add(i); // Collect indices of auto-sized elements.
            }
            else
            {
                fixedSum += Math.Max(0.0, sizes[i]); // Sum up fixed sizes (ensure non-negative).
            }
        }

        var remaining = total - fixedSum; // Remaining space to distribute among auto-sized elements.
        if (autoIndices.Count == 0) // No auto-sized elements available for distribution.
        {
            // If fixed elements already sum to the total (within a small epsilon), it's successful.
            if (Math.Abs(remaining) <= 1e-6)
            {
                return true;
            }

            error = "No auto cells available to keep total size constant.";
            return false;
        }

        if (remaining < 0) // Fixed sizes exceed the available total space.
        {
            error = "Manual sizes exceed available total.";
            return false;
        }

        var autoSum = autoIndices.Sum(i => Math.Max(0.0, sizes[i])); // Sum of current sizes of auto-elements.
        if (autoSum <= 1e-9) // If current auto sizes sum to zero, distribute remaining space equally.
        {
            var per = remaining / autoIndices.Count;
            foreach (var idx in autoIndices)
            {
                sizes[idx] = per;
            }
        }
        else // Distribute remaining space proportionally based on current auto-element sizes.
        {
            var scale = remaining / autoSum;
            foreach (var idx in autoIndices)
            {
                sizes[idx] = Math.Max(0.0, sizes[idx] * scale);
            }
        }

        return true;
    }

    /// <summary>
    /// Builds a list of boolean flags where 'true' indicates an element should be automatically sized,
    /// and 'false' indicates it is manually overridden. This is the inverse of the <paramref name="overrideFlags"/>.
    /// </summary>
    /// <param name="overrideFlags">A list of boolean flags where 'true' means manually overridden.</param>
    /// <param name="count">The desired length of the resulting flags list.</param>
    /// <returns>A new list of boolean flags for auto-sizing.</returns>
    private static List<bool> BuildAutoFlags(List<bool> overrideFlags, int count)
    {
        var flags = new List<bool>(count);
        for (var i = 0; i < count; i++)
        {
            var isOverride = i < overrideFlags.Count && overrideFlags[i];
            flags.Add(!isOverride); // If it's NOT overridden, it's auto-sized.
        }
        return flags;
    }

    /// <summary>
    /// Resizes a list to the specified count, filling new elements with a default value.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The original list.</param>
    /// <param name="count">The desired new size of the list.</param>
    /// <param name="fill">The default value to fill new elements with.</param>
    /// <returns>A new list with the specified size.</returns>
    private static List<T> Resize<T>(List<T> list, int count, T fill)
    {
        var resized = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            resized.Add(i < list.Count ? list[i] : fill);
        }
        return resized;
    }

    /// <summary>
    /// Ensures a jagged list (list of lists) has the correct outer and inner dimensions,
    /// filling new elements with a default value.
    /// </summary>
    /// <typeparam name="T">The type of elements in the inner lists.</typeparam>
    /// <param name="list">The original jagged list.</param>
    /// <param name="outer">The desired number of outer lists (rows).</param>
    /// <param name="inner">The desired number of elements in each inner list (columns).</param>
    /// <param name="fill">The default value to fill new elements with.</param>
    /// <returns>A new jagged list with the specified dimensions.</returns>
    private static List<List<T>> EnsureJagged<T>(List<List<T>> list, int outer, int inner, T fill)
    {
        var result = new List<List<T>>(outer);
        for (var r = 0; r < outer; r++)
        {
            if (r < list.Count)
            {
                result.Add(Resize(list[r], inner, fill));
            }
            else
            {
                result.Add(Enumerable.Repeat(fill, inner).ToList());
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a deep clone of a jagged list (list of lists).
    /// </summary>
    /// <typeparam name="T">The type of elements in the lists.</typeparam>
    /// <param name="list">The original jagged list to clone.</param>
    /// <returns>A new jagged list with new inner lists, containing copies of the original elements.</returns>
    private static List<List<T>> CloneJagged<T>(List<List<T>> list)
    {
        return list.Select(inner => new List<T>(inner)).ToList();
    }
}
