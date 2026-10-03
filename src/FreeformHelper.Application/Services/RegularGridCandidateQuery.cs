using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Provides coarse candidate queries on <see cref="RegularGrid"/> by geometry bounds.
/// </summary>
public static class RegularGridCandidateQuery
{
    /// <summary>
    /// Returns regular pads whose bounds intersect with <paramref name="bounds"/>.
    /// Candidate range is narrowed by grid edges first, then refined by actual rectangle intersection.
    /// </summary>
    public static IEnumerable<RegularPad> QueryByBounds(RegularGrid grid, Rect2 bounds, int padding = 0)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.Rows <= 0 || grid.Cols <= 0 || grid.Pads.Count == 0)
        {
            yield break;
        }

        var (rowMin, rowMax, colMin, colMax) = GetCandidateRange(grid, bounds, padding);

        for (var row = rowMin; row <= rowMax; row++)
        {
            for (var col = colMin; col <= colMax; col++)
            {
                var pad = grid.GetPad(row, col);
                if (pad.Bounds.Intersects(bounds))
                {
                    yield return pad;
                }
            }
        }
    }

    public static (int rowMin, int rowMax, int colMin, int colMax) GetCandidateRange(
        RegularGrid grid,
        Rect2 bounds,
        int padding = 0)
    {
        ArgumentNullException.ThrowIfNull(grid);

        if (grid.Rows <= 0 || grid.Cols <= 0)
        {
            return (0, -1, 0, -1);
        }

        var safePadding = Math.Max(0, padding);
        var rowMin = FindCellIndex(grid.YEdges, bounds.MinY) - safePadding;
        var rowMax = FindCellIndex(grid.YEdges, bounds.MaxY) + safePadding;
        var colMin = FindCellIndex(grid.XEdges, bounds.MinX) - safePadding;
        var colMax = FindCellIndex(grid.XEdges, bounds.MaxX) + safePadding;

        rowMin = Math.Clamp(rowMin, 0, grid.Rows - 1);
        rowMax = Math.Clamp(rowMax, 0, grid.Rows - 1);
        colMin = Math.Clamp(colMin, 0, grid.Cols - 1);
        colMax = Math.Clamp(colMax, 0, grid.Cols - 1);

        if (rowMin > rowMax)
        {
            (rowMin, rowMax) = (rowMax, rowMin);
        }

        if (colMin > colMax)
        {
            (colMin, colMax) = (colMax, colMin);
        }

        return (rowMin, rowMax, colMin, colMax);
    }

    private static int FindCellIndex(IReadOnlyList<double> edges, double coord)
    {
        if (edges.Count < 2)
        {
            return 0;
        }

        if (coord <= edges[0])
        {
            return 0;
        }

        if (coord >= edges[^1])
        {
            return edges.Count - 2;
        }

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

        return Math.Clamp(lo, 0, edges.Count - 2);
    }
}
