using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Responsible for calculating and assigning per-IC diff indices and IC (Integrated Circuit) indices
/// to each pad in a <see cref="RegularGrid"/> based on specified <see cref="GridSettings"/>.
/// </summary>
public sealed class DiffIndexMapper
{
    /// <summary>
    /// Applies diff index and IC index mapping to all pads in the provided grid.
    /// </summary>
    /// <param name="grid">The <see cref="RegularGrid"/> to process.</param>
    /// <param name="settings">The <see cref="GridSettings"/> containing configuration for mapping, such as cascade number and scan order.</param>
    public static void ApplyMapping(RegularGrid grid, GridSettings settings)
    {
        settings.ValidateOrThrow();

        var cols = grid.Cols;
        var rows = grid.Rows;

        var perIcCols = GridIcChannelAllocationService.ResolvePerIcColumns(settings);
        var colToIc = new int[cols];
        var colToLocalX = new int[cols];

        // Create a lookup to map each global column index to its corresponding IC index and local column index within that IC.
        int col = 0;
        for (int ic = 0; ic < perIcCols.Count; ic++)
        {
            for (int localX = 0; localX < perIcCols[ic]; localX++)
            {
                if (col >= cols) break;
                colToIc[col] = ic;
                colToLocalX[col] = localX;
                col++;
            }
        }

        // Iterate through each pad and calculate its IC and diff index.
        foreach (var pad in grid.Pads)
        {
            var ic = colToIc[pad.Col];
            var localX = colToLocalX[pad.Col];
            var icCols = perIcCols[ic];

            // Apply the specified scan order to determine the final row and column for diff calculation.
            var scanRow = ApplyRowDirection(pad.Row, rows, settings.ScanOrder);
            var scanColLocal = ApplyColDirection(localX, icCols, settings.ScanOrder);

            pad.IcIndex = ic;
            pad.DiffIndex = scanRow * icCols + scanColLocal;
        }
    }

    /// <summary>
    /// Adjusts the row index based on the vertical scan direction.
    /// For TopToBottom scans, the row index is inverted.
    /// </summary>
    /// <param name="row">The original row index.</param>
    /// <param name="totalRows">The total number of rows in the grid.</param>
    /// <param name="order">The scan order.</param>
    /// <returns>The adjusted row index.</returns>
    private static int ApplyRowDirection(int row, int totalRows, ScanOrder order)
    {
        return order switch
        {
            // TopToBottom scan means row 0 is at the top, but in some systems, the origin is bottom-left.
            // This inverts the row index to match that expectation.
            ScanOrder.LeftToRight_TopToBottom => (totalRows - 1) - row,
            ScanOrder.RightToLeft_TopToBottom => (totalRows - 1) - row,
            _ => row, // BottomToTop scan uses the original row index.
        };
    }

    /// <summary>
    /// Adjusts the column index based on the horizontal scan direction.
    /// For RightToLeft scans, the column index is inverted within its IC.
    /// </summary>
    /// <param name="col">The local column index within an IC.</param>
    /// <param name="totalCols">The total number of columns in that IC.</param>
    /// <param name="order">The scan order.</param>
    /// <returns>The adjusted column index.</returns>
    private static int ApplyColDirection(int col, int totalCols, ScanOrder order)
    {
        return order switch
        {
            // RightToLeft scan inverts the column index.
            ScanOrder.RightToLeft_TopToBottom => (totalCols - 1) - col,
            ScanOrder.RightToLeft_BottomToTop => (totalCols - 1) - col,
            _ => col, // LeftToRight scan uses the original column index.
        };
    }
}

/// <summary>
/// Backward-compatible alias. Prefer <see cref="DiffIndexMapper"/>.
/// </summary>
[Obsolete("Use DiffIndexMapper instead.")]
public sealed class AfeMapper
{
    /// <summary>
    /// Applies diff index and IC index mapping to all pads in the provided grid.
    /// </summary>
    public void ApplyMapping(RegularGrid grid, GridSettings settings)
    {
        DiffIndexMapper.ApplyMapping(grid, settings);
    }
}
