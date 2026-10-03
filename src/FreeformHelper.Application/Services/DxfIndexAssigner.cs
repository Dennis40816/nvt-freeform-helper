using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Assigns a contiguous DXF index to CAD output pads based on physical position and <see cref="ScanOrder"/>.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "Instance API is intentionally preserved for service call-site stability.")]
public sealed class DxfIndexAssigner
{
    public static (int Row, int Col) ResolveGridCell(RegularGrid grid, Point2 centroid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var rows = Math.Max(1, grid.Rows);
        var cols = Math.Max(1, grid.Cols);
        return ResolveGridCell(grid, centroid, rows, cols);
    }

    public IReadOnlyDictionary<int, int> BuildIndexByCadId(IReadOnlyList<CadPad> visibleCadPads, RegularGrid? grid, ScanOrder scanOrder)
    {
        ArgumentNullException.ThrowIfNull(visibleCadPads);

        var ordered = OrderPads(visibleCadPads, grid, scanOrder);
        var map = new Dictionary<int, int>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            map[ordered[i].Id] = i;
        }

        return map;
    }

    public static IReadOnlyList<CadPad> OrderPads(IReadOnlyList<CadPad> visibleCadPads, RegularGrid? grid, ScanOrder scanOrder)
    {
        ArgumentNullException.ThrowIfNull(visibleCadPads);
        if (visibleCadPads.Count <= 1)
        {
            return visibleCadPads.ToList();
        }

        if (grid is null || grid.YEdges.Count < 2)
        {
            return OrderByCentroidFallback(visibleCadPads, scanOrder);
        }

        var rows = Math.Max(1, grid.Rows);
        var cols = Math.Max(1, grid.Cols);
        var rightToLeft = scanOrder is ScanOrder.RightToLeft_TopToBottom or ScanOrder.RightToLeft_BottomToTop;
        var topToBottom = scanOrder is ScanOrder.LeftToRight_TopToBottom or ScanOrder.RightToLeft_TopToBottom;

        var items = new List<(CadPad pad, int scanRow, int scanCol, double xKey, double yKey, int id)>(visibleCadPads.Count);
        foreach (var pad in visibleCadPads)
        {
            var c = pad.Centroid;
            var (row, col) = ResolveGridCell(grid, c, rows, cols);
            var scanRow = topToBottom ? (rows - 1) - row : row;
            var scanCol = rightToLeft ? (cols - 1) - col : col;
            var xKey = rightToLeft ? -c.X : c.X;
            var yKey = topToBottom ? -c.Y : c.Y;
            items.Add((pad, scanRow, scanCol, xKey, yKey, pad.Id));
        }

        items.Sort(static (a, b) =>
        {
            var cmp = a.scanRow.CompareTo(b.scanRow);
            if (cmp != 0) return cmp;
            cmp = a.scanCol.CompareTo(b.scanCol);
            if (cmp != 0) return cmp;
            cmp = a.xKey.CompareTo(b.xKey);
            if (cmp != 0) return cmp;
            cmp = a.yKey.CompareTo(b.yKey);
            if (cmp != 0) return cmp;
            return a.id.CompareTo(b.id);
        });

        return items.Select(t => t.pad).ToList();
    }

    private static (int row, int col) ResolveGridCell(RegularGrid grid, Point2 centroid, int rows, int cols)
    {
        var coarseRow = FindCellIndex(grid.YEdges, centroid.Y, rows);
        var coarseCol = FindCellIndex(grid.XEdges, centroid.X, cols);
        coarseRow = Math.Clamp(coarseRow, 0, rows - 1);
        coarseCol = Math.Clamp(coarseCol, 0, cols - 1);

        var direct = grid.GetPad(coarseRow, coarseCol);
        if (direct.Bounds.Contains(centroid))
        {
            return (coarseRow, coarseCol);
        }

        if (TryFindContainingCell(grid, centroid, coarseRow, coarseCol, windowRadius: 3, out var containingRow, out var containingCol))
        {
            return (containingRow, containingCol);
        }

        // Fallback: choose nearest regular pad centroid when local sizing skews global edges.
        return FindNearestCellByCentroid(grid, centroid, coarseRow, coarseCol, windowRadius: 4);
    }

    private static bool TryFindContainingCell(
        RegularGrid grid,
        Point2 centroid,
        int coarseRow,
        int coarseCol,
        int windowRadius,
        out int row,
        out int col)
    {
        row = 0;
        col = 0;

        var rowMin = Math.Max(0, coarseRow - windowRadius);
        var rowMax = Math.Min(grid.Rows - 1, coarseRow + windowRadius);
        var colMin = Math.Max(0, coarseCol - windowRadius);
        var colMax = Math.Min(grid.Cols - 1, coarseCol + windowRadius);

        var has = false;
        var bestCellDistance = int.MaxValue;
        var bestCentroidDistance = double.MaxValue;
        for (var r = rowMin; r <= rowMax; r++)
        {
            for (var c = colMin; c <= colMax; c++)
            {
                var pad = grid.GetPad(r, c);
                if (!pad.Bounds.Contains(centroid))
                {
                    continue;
                }

                var cellDistance = Math.Abs(r - coarseRow) + Math.Abs(c - coarseCol);
                var centroidDistance = DistanceSquared(centroid, pad.Centroid);
                if (!has ||
                    cellDistance < bestCellDistance ||
                    (cellDistance == bestCellDistance && centroidDistance < bestCentroidDistance))
                {
                    has = true;
                    bestCellDistance = cellDistance;
                    bestCentroidDistance = centroidDistance;
                    row = r;
                    col = c;
                }
            }
        }

        return has;
    }

    private static (int row, int col) FindNearestCellByCentroid(
        RegularGrid grid,
        Point2 centroid,
        int coarseRow,
        int coarseCol,
        int windowRadius)
    {
        var rowMin = Math.Max(0, coarseRow - windowRadius);
        var rowMax = Math.Min(grid.Rows - 1, coarseRow + windowRadius);
        var colMin = Math.Max(0, coarseCol - windowRadius);
        var colMax = Math.Min(grid.Cols - 1, coarseCol + windowRadius);

        var has = false;
        var bestRow = coarseRow;
        var bestCol = coarseCol;
        var bestDistance = double.MaxValue;

        for (var r = rowMin; r <= rowMax; r++)
        {
            for (var c = colMin; c <= colMax; c++)
            {
                var distance = DistanceSquared(centroid, grid.GetPad(r, c).Centroid);
                if (!has || distance < bestDistance)
                {
                    has = true;
                    bestDistance = distance;
                    bestRow = r;
                    bestCol = c;
                }
            }
        }

        if (has)
        {
            return (bestRow, bestCol);
        }

        // Should never happen for a non-empty grid, but keep a safe fallback.
        return (Math.Clamp(coarseRow, 0, grid.Rows - 1), Math.Clamp(coarseCol, 0, grid.Cols - 1));
    }

    private static double DistanceSquared(Point2 a, Point2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }

    private static List<CadPad> OrderByCentroidFallback(IReadOnlyList<CadPad> pads, ScanOrder scanOrder)
    {
        var items = pads
            .Select(p =>
            {
                var c = p.Centroid;
                return (pad: p, y: c.Y, x: c.X);
            })
            .ToList();

        items.Sort((a, b) =>
        {
            var topToBottom = scanOrder is ScanOrder.LeftToRight_TopToBottom or ScanOrder.RightToLeft_TopToBottom;
            var rightToLeft = scanOrder is ScanOrder.RightToLeft_TopToBottom or ScanOrder.RightToLeft_BottomToTop;

            // Primary: Y direction.
            var cmp = topToBottom ? b.y.CompareTo(a.y) : a.y.CompareTo(b.y);
            if (cmp != 0) return cmp;

            // Secondary: X direction.
            cmp = rightToLeft ? b.x.CompareTo(a.x) : a.x.CompareTo(b.x);
            if (cmp != 0) return cmp;

            return a.pad.Id.CompareTo(b.pad.Id);
        });

        return items.Select(t => t.pad).ToList();
    }

    private static int FindCellIndex(IReadOnlyList<double> edges, double coord, int cellCount)
    {
        if (cellCount <= 1 || edges.Count < 2)
        {
            return 0;
        }

        if (coord <= edges[0]) return 0;
        if (coord >= edges[^1]) return cellCount - 1;

        var lo = 0;
        var hi = edges.Count - 1;
        while (lo + 1 < hi)
        {
            var mid = (lo + hi) / 2;
            if (coord < edges[mid])
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        return Math.Clamp(lo, 0, cellCount - 1);
    }
}
