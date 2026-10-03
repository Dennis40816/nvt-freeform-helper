using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

public sealed partial class ManualSizingService
{
    /// <summary>
    /// Applies manual sizing changes (width and/or height) to a selection of rows/columns.
    /// Handles both global and local sizing scopes.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to modify.</param>
    /// <param name="alignmentMode">The current grid alignment mode.</param>
    /// <param name="cad">The <see cref="CadPadSet"/>, used to determine target grid size if aligned to CAD bounds.</param>
    /// <param name="rows">A collection of row indices to apply height changes to.</param>
    /// <param name="cols">A collection of column indices to apply width changes to.</param>
    /// <param name="width">The desired width to apply, or null if not changing width.</param>
    /// <param name="height">The desired height to apply, or null if not changing height.</param>
    /// <returns>A <see cref="ManualSizingResult"/> indicating success or failure and the previous snapshot.</returns>
    public ManualSizingResult ApplySizing(
        GridSettings grid,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols,
        double? width,
        double? height)
    {
        EnsureSizingLists(grid); // Ensure all lists are correctly sized before modification.
        var snapshot = CaptureSnapshot(grid); // Capture current state for potential rollback.

        var hasWidth = width.HasValue && width.Value > 0;
        var hasHeight = height.HasValue && height.Value > 0;
        if (!hasWidth && !hasHeight)
        {
            return ManualSizingResult.Ok(snapshot); // Nothing to do.
        }

        // Apply width changes based on the configured width adjustment scope.
        if (hasWidth)
        {
            var result = grid.WidthScope == WidthAdjustmentScope.ColumnGlobal
                ? ApplyWidthGlobal(grid, cols, width!.Value, alignmentMode, cad, snapshot)
                : ApplyWidthRowLocal(grid, rows, cols, width!.Value, alignmentMode, cad, snapshot);

            if (!result.Success) return result; // If width application failed, return.
        }

        // Apply height changes based on the configured height adjustment scope.
        if (hasHeight)
        {
            var result = grid.HeightScope == HeightAdjustmentScope.RowGlobal
                ? ApplyHeightGlobal(grid, rows, height!.Value, alignmentMode, cad, snapshot)
                : ApplyHeightColumnLocal(grid, rows, cols, height!.Value, alignmentMode, cad, snapshot);

            if (!result.Success) return result; // If height application failed, return.
        }

        return ManualSizingResult.Ok(snapshot); // All changes applied successfully.
    }

    /// <summary>
    /// Resets manual sizing for a selection of rows/columns back to their automatic distribution.
    /// Handles both global and local sizing scopes.
    /// </summary>
    /// <param name="grid">The <see cref="GridSettings"/> to modify.</param>
    /// <param name="alignmentMode">The current grid alignment mode.</param>
    /// <param name="cad">The <see cref="CadPadSet"/>, used to determine target grid size if aligned to CAD bounds.</param>
    /// <param name="rows">A collection of row indices to reset height changes for.</param>
    /// <param name="cols">A collection of column indices to reset width changes for.</param>
    /// <returns>A <see cref="ManualSizingResult"/> indicating success or failure and the previous snapshot.</returns>
    public ManualSizingResult ResetSizing(
        GridSettings grid,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        IReadOnlyCollection<int> rows,
        IReadOnlyCollection<int> cols)
    {
        EnsureSizingLists(grid);
        var snapshot = CaptureSnapshot(grid);

        // --- Reset Widths ---
        if (grid.WidthScope == WidthAdjustmentScope.ColumnGlobal)
        {
            // Reset individual column widths and overrides to default (0.0 for width, false for override).
            foreach (var c in cols.Distinct())
            {
                if (c < 0 || c >= grid.ColumnWidths.Count) continue;
                grid.ColumnWidths[c] = 0.0;
                grid.ColumnOverrides[c] = false;
            }

            // Redistribute remaining space among non-overridden columns.
            var target = GetTargetGridSize(grid, alignmentMode, cad);
            if (!TryDistributeSizesWithOverrides(grid.ColumnWidths, grid.ColumnOverrides, target.width, out var colError))
            {
                RestoreSnapshot(grid, snapshot);
                return ManualSizingResult.Fail(snapshot, colError);
            }
        }
        else // RowLocal width scope
        {
            // Iterate through affected rows.
            var target = GetTargetGridSize(grid, alignmentMode, cad);
            foreach (var r in rows.Distinct())
            {
                if (r < 0 || r >= grid.YChannels) continue;
                var rowFlags = grid.RowWidthOverrideFlags[r];
                var rowVals = grid.RowWidthOverrides[r];
                // Reset specific cells within the row.
                foreach (var c in cols.Distinct())
                {
                    if (c < 0 || c >= grid.XChannels) continue;
                    rowVals[c] = 0.0;
                    rowFlags[c] = false;
                }

                // Recalculate and redistribute widths for this row.
                var composed = ComposeRowWidths(grid, r);
                // autoFlags indicates which cells should automatically adjust their size.
                var autoFlags = BuildAutoFlags(rowFlags, composed.Count);
                if (!TryDistributeSizes(composed, autoFlags, target.width, out var err))
                {
                    RestoreSnapshot(grid, snapshot);
                    return ManualSizingResult.Fail(snapshot, err);
                }

                // Update the grid settings with the new distributed values.
                grid.RowWidthOverrides[r] = composed;
                grid.RowWidthOverrideFlags[r] = rowFlags; // Store the flags (which are now inverted from autoFlags)
            }
        }

        // --- Reset Heights ---
        if (grid.HeightScope == HeightAdjustmentScope.RowGlobal)
        {
            // Reset individual row heights and overrides to default.
            foreach (var r in rows.Distinct())
            {
                if (r < 0 || r >= grid.RowHeights.Count) continue;
                grid.RowHeights[r] = 0.0;
                grid.RowOverrides[r] = false;
            }

            // Redistribute remaining space among non-overridden rows.
            var target = GetTargetGridSize(grid, alignmentMode, cad);
            if (!TryDistributeSizesWithOverrides(grid.RowHeights, grid.RowOverrides, target.height, out var rowError))
            {
                RestoreSnapshot(grid, snapshot);
                return ManualSizingResult.Fail(snapshot, rowError);
            }
        }
        else // ColumnLocal height scope
        {
            // Iterate through affected columns.
            var target = GetTargetGridSize(grid, alignmentMode, cad);
            foreach (var c in cols.Distinct())
            {
                if (c < 0 || c >= grid.XChannels) continue;
                var colFlags = grid.ColumnHeightOverrideFlags[c];
                var colVals = grid.ColumnHeightOverrides[c];
                // Reset specific cells within the column.
                foreach (var r in rows.Distinct())
                {
                    if (r < 0 || r >= grid.YChannels) continue;
                    colVals[r] = 0.0;
                    colFlags[r] = false;
                }

                // Recalculate and redistribute heights for this column.
                var composed = ComposeColumnHeights(grid, c);
                var autoFlags = BuildAutoFlags(colFlags, composed.Count);
                if (!TryDistributeSizes(composed, autoFlags, target.height, out var err))
                {
                    RestoreSnapshot(grid, snapshot);
                    return ManualSizingResult.Fail(snapshot, err);
                }

                // Update the grid settings.
                grid.ColumnHeightOverrides[c] = composed;
                grid.ColumnHeightOverrideFlags[c] = colFlags; // Store the flags (which are now inverted from autoFlags)
            }
        }

        return ManualSizingResult.Ok(snapshot);
    }

    /// <summary>
    /// Sets specific dimensions for a single pad (cell) in the grid.
    /// This is a convenience method that calls <see cref="ApplySizing"/> for a single row and column.
    /// </summary>
    public ManualSizingResult SetPadDimensions(
        GridSettings grid,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        int row,
        int col,
        double? width,
        double? height)
    {
        EnsureSizingLists(grid);
        var snapshot = CaptureSnapshot(grid);

        var rows = new[] { row };
        var cols = new[] { col };
        var result = ApplySizing(grid, alignmentMode, cad, rows, cols, width, height);
        if (!result.Success)
        {
            RestoreSnapshot(grid, snapshot); // Rollback changes if unsuccessful.
            return result;
        }

        return result;
    }

    /// <summary>
    /// Applies a specific width globally to a selection of columns, distributing remaining space.
    /// </summary>
    private ManualSizingResult ApplyWidthGlobal(
        GridSettings grid,
        IEnumerable<int> cols,
        double width,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        ManualSizingSnapshot snapshot)
    {
        // Set the specified width and mark as overridden for each selected column.
        foreach (var c in cols.Distinct())
        {
            if (c < 0 || c >= grid.ColumnWidths.Count) continue;
            grid.ColumnWidths[c] = width;
            grid.ColumnOverrides[c] = true;
        }

        // Redistribute sizes to maintain total grid width.
        var target = GetTargetGridSize(grid, alignmentMode, cad);
        if (!TryDistributeSizesWithOverrides(grid.ColumnWidths, grid.ColumnOverrides, target.width, out var error))
        {
            RestoreSnapshot(grid, snapshot); // Rollback on failure.
            return ManualSizingResult.Fail(snapshot, error);
        }

        return ManualSizingResult.Ok(snapshot);
    }

    /// <summary>
    /// Applies a specific width to a selection of cells within specific rows (row-local scope).
    /// </summary>
    private ManualSizingResult ApplyWidthRowLocal(
        GridSettings grid,
        IEnumerable<int> rows,
        IEnumerable<int> cols,
        double width,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        ManualSizingSnapshot snapshot)
    {
        var target = GetTargetGridSize(grid, alignmentMode, cad);
        foreach (var r in rows.Distinct())
        {
            if (r < 0 || r >= grid.YChannels) continue;
            var rowVals = grid.RowWidthOverrides[r];
            var rowFlags = grid.RowWidthOverrideFlags[r];

            // Set the width and mark as overridden for each selected cell in this row.
            foreach (var c in cols.Distinct())
            {
                if (c < 0 || c >= grid.XChannels) continue;
                rowVals[c] = width;
                rowFlags[c] = true;
            }

            // Compose the effective widths for this row and redistribute.
            var composed = ComposeRowWidths(grid, r);
            var autoFlags = BuildAutoFlags(rowFlags, composed.Count);
            if (!TryDistributeSizes(composed, autoFlags, target.width, out var err))
            {
                RestoreSnapshot(grid, snapshot);
                return ManualSizingResult.Fail(snapshot, err);
            }

            grid.RowWidthOverrides[r] = composed;
            grid.RowWidthOverrideFlags[r] = rowFlags;
        }

        return ManualSizingResult.Ok(snapshot);
    }

    /// <summary>
    /// Applies a specific height globally to a selection of rows, distributing remaining space.
    /// </summary>
    private ManualSizingResult ApplyHeightGlobal(
        GridSettings grid,
        IEnumerable<int> rows,
        double height,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        ManualSizingSnapshot snapshot)
    {
        // Set the specified height and mark as overridden for each selected row.
        foreach (var r in rows.Distinct())
        {
            if (r < 0 || r >= grid.RowHeights.Count) continue;
            grid.RowHeights[r] = height;
            grid.RowOverrides[r] = true;
        }

        // Redistribute sizes to maintain total grid height.
        var target = GetTargetGridSize(grid, alignmentMode, cad);
        if (!TryDistributeSizesWithOverrides(grid.RowHeights, grid.RowOverrides, target.height, out var error))
        {
            RestoreSnapshot(grid, snapshot);
            return ManualSizingResult.Fail(snapshot, error);
        }

        return ManualSizingResult.Ok(snapshot);
    }

    /// <summary>
    /// Applies a specific height to a selection of cells within specific columns (column-local scope).
    /// </summary>
    private ManualSizingResult ApplyHeightColumnLocal(
        GridSettings grid,
        IEnumerable<int> rows,
        IEnumerable<int> cols,
        double height,
        GridAlignmentMode alignmentMode,
        CadPadSet? cad,
        ManualSizingSnapshot snapshot)
    {
        var target = GetTargetGridSize(grid, alignmentMode, cad);
        foreach (var c in cols.Distinct())
        {
            if (c < 0 || c >= grid.XChannels) continue;

            var colVals = grid.ColumnHeightOverrides[c];
            var colFlags = grid.ColumnHeightOverrideFlags[c];

            // Set the height and mark as overridden for each selected cell in this column.
            foreach (var r in rows.Distinct())
            {
                if (r < 0 || r >= grid.YChannels) continue;
                colVals[r] = height;
                colFlags[r] = true;
            }

            // Compose the effective heights for this column and redistribute.
            var composed = ComposeColumnHeights(grid, c);
            var autoFlags = BuildAutoFlags(colFlags, composed.Count);
            if (!TryDistributeSizes(composed, autoFlags, target.height, out var err))
            {
                RestoreSnapshot(grid, snapshot);
                return ManualSizingResult.Fail(snapshot, err);
            }

            grid.ColumnHeightOverrides[c] = composed;
            grid.ColumnHeightOverrideFlags[c] = colFlags;
        }

        return ManualSizingResult.Ok(snapshot);
    }
}
