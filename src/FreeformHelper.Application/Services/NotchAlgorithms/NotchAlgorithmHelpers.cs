using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Provides helper methods for notch algorithm strategies.
/// </summary>
internal static class NotchAlgorithmHelpers
{

    internal static NotchAxisKind ResolveAxisKind(FreeformType freeform)
    {
        return freeform == FreeformType.YWay
            ? NotchAxisKind.Y
            : NotchAxisKind.X;
    }

    internal static NotchAxisGeometryContext CreateAxisGeometryContext(RegularGrid grid, RegularPad reg, NotchAxisKind axis)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(reg);

        var cellRect = GetCellRect(grid, reg.Row, reg.Col);
        return axis == NotchAxisKind.X
            ? new NotchAxisGeometryContext(
                Axis: axis,
                CellRect: cellRect,
                IsLeadingBorder: reg.Col == 0,
                IsTrailingBorder: reg.Col == grid.Cols - 1)
            : new NotchAxisGeometryContext(
                Axis: axis,
                CellRect: cellRect,
                IsLeadingBorder: reg.Row == 0,
                IsTrailingBorder: reg.Row == grid.Rows - 1);
    }

    internal static NotchAxisNeighborContext CreateAxisNeighborContext(
        RegularGrid grid,
        RegularPad reg,
        NotchAxisKind axis,
        int nullVal,
        (int Row, int Col) leftNeighbor,
        (int Row, int Col) rightNeighbor)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(reg);

        return new NotchAxisNeighborContext(
            LeftNeighborDiff: GetNeighborDiff(grid, leftNeighbor.Row, leftNeighbor.Col, nullVal),
            RightNeighborDiff: GetNeighborDiff(grid, rightNeighbor.Row, rightNeighbor.Col, nullVal),
            LeftCadNeighborDiff: GetCadNeighborDiff(grid, leftNeighbor.Row, leftNeighbor.Col, nullVal),
            RightCadNeighborDiff: GetCadNeighborDiff(grid, rightNeighbor.Row, rightNeighbor.Col, nullVal),
            LeftNeighborLength: GetNeighborAxisLength(grid, leftNeighbor.Row, leftNeighbor.Col, reg, axis),
            RightNeighborLength: GetNeighborAxisLength(grid, rightNeighbor.Row, rightNeighbor.Col, reg, axis));
    }

    /// <summary>
    /// Gets the bounding rectangle of a cell in the regular grid.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index of the cell.</param>
    /// <param name="col">The column index of the cell.</param>
    /// <returns>The bounding rectangle of the specified cell.</returns>
    internal static Rect2 GetCellRect(RegularGrid grid, int row, int col)
    {
        var pad = grid.GetPad(row, col);
        return pad.Bounds;
    }

    /// <summary>
    /// Gets the diff index of a neighboring pad.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index of the neighbor.</param>
    /// <param name="col">The column index of the neighbor.</param>
    /// <param name="nullVal">The value to return if the neighbor is out of bounds.</param>
    /// <returns>The diff index of the neighbor, or <paramref name="nullVal"/> if out of bounds.</returns>
    internal static int GetNeighborDiff(RegularGrid grid, int row, int col, int nullVal)
    {
        if (row < 0 || row >= grid.Rows || col < 0 || col >= grid.Cols) return nullVal;
        return grid.GetPad(row, col).DiffIndex;
    }

    /// <summary>
    /// Tries to get a pad from the specified grid coordinates.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index.</param>
    /// <param name="col">The column index.</param>
    /// <returns>The <see cref="RegularPad"/> at the specified coordinates, or <c>null</c> if out of bounds.</returns>
    internal static RegularPad? TryGetPad(RegularGrid grid, int row, int col)
    {
        if (row < 0 || row >= grid.Rows || col < 0 || col >= grid.Cols)
        {
            return null;
        }

        return grid.GetPad(row, col);
    }

    /// <summary>
    /// Gets the diff index of a neighboring pad that has a matched CAD pad.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index of the neighbor.</param>
    /// <param name="col">The column index of the neighbor.</param>
    /// <param name="nullVal">The value to return if the neighbor is not found or has no matched CAD pad.</param>
    /// <returns>The diff index, or <paramref name="nullVal"/>.</returns>
    internal static int GetCadNeighborDiff(RegularGrid grid, int row, int col, int nullVal)
    {
        var pad = TryGetPad(grid, row, col);
        if (pad is null || pad.MatchedCadPadId is null)
        {
            return nullVal;
        }

        return pad.DiffIndex;
    }

    /// <summary>
    /// Determines the overlap case between a regular pad and a CAD pad.
    /// </summary>
    /// <param name="regL">The left/bottom coordinate of the regular pad.</param>
    /// <param name="regN">The right/top coordinate of the regular pad.</param>
    /// <param name="cadL">The left/bottom coordinate of the CAD pad.</param>
    /// <param name="cadN">The right/top coordinate of the CAD pad.</param>
    /// <returns>An integer representing the overlap case.</returns>
    internal static int RegPadOverlapCadPadCase(double regL, double regN, double cadL, double cadN)
    {
        if ((cadL >= regL && cadL <= regN) && (regN <= cadN && regN >= cadL))
        {
            return 0; // Overlap on the 'near' side
        }

        if ((regL >= cadL && regL <= cadN) && (cadN >= regL && cadN <= regN))
        {
            return 1; // Overlap on the 'far' side
        }

        if ((cadL >= regL && cadL <= regN) && (cadN >= regL && cadN <= regN))
        {
            return 2; // CAD pad is contained within regular pad
        }

        if ((regL >= cadL && regL <= cadN) && (regN >= cadL && regN <= cadN))
        {
            // Regular pad is contained within CAD pad, determine dominant overlap side
            var lenL = Math.Abs(cadL - regL);
            var lenN = Math.Abs(cadN - regN);
            return lenN > lenL ? 0 : 1;
        }

        return 3; // No overlap or other cases
    }

    /// <summary>
    /// Gets the width of a neighboring pad.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index of the neighbor.</param>
    /// <param name="col">The column index of the neighbor.</param>
    /// <param name="fallback">The fallback pad to use if the neighbor is out of bounds.</param>
    /// <returns>The width of the neighbor, or the fallback's width if out of bounds.</returns>
    internal static double GetNeighborLenX(RegularGrid grid, int row, int col, RegularPad fallback)
    {
        if (row < 0 || row >= grid.Rows || col < 0 || col >= grid.Cols) return fallback.Bounds.Width;
        return grid.GetPad(row, col).Bounds.Width;
    }

    /// <summary>
    /// Gets the height of a neighboring pad.
    /// </summary>
    /// <param name="grid">The regular grid.</param>
    /// <param name="row">The row index of the neighbor.</param>
    internal static double GetNeighborLenY(RegularGrid grid, int row, int col, RegularPad fallback)
    {
        if (row < 0 || row >= grid.Rows || col < 0 || col >= grid.Cols) return fallback.Bounds.Height;
        return grid.GetPad(row, col).Bounds.Height;
    }

    internal static double GetNeighborAxisLength(
        RegularGrid grid,
        int row,
        int col,
        RegularPad fallback,
        NotchAxisKind axis)
    {
        return axis == NotchAxisKind.X
            ? GetNeighborLenX(grid, row, col, fallback)
            : GetNeighborLenY(grid, row, col, fallback);
    }

    /// <summary>
    /// Builds a descriptive comment string for a notch table row.
    /// </summary>
    /// <param name="reg">The regular pad.</param>
    /// <param name="cad">The matched CAD pad, if any.</param>
    /// <returns>A formatted comment string.</returns>
    internal static string BuildComment(RegularPad reg, CadPad? cad)
    {
        var comment = $"Freeform={reg.Freeform}";
        if (cad is not null)
        {
            comment += $" CadPadId={cad.Id}";
        }
        return comment;
    }

}
