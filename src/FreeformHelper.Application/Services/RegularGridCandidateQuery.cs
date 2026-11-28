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
        var rowMin = GridCellLookup.FindCellIndex(grid.YEdges, bounds.MinY, grid.YEdges.Count - 1) - safePadding;
        var rowMax = GridCellLookup.FindCellIndex(grid.YEdges, bounds.MaxY, grid.YEdges.Count - 1) + safePadding;
        var colMin = GridCellLookup.FindCellIndex(grid.XEdges, bounds.MinX, grid.XEdges.Count - 1) - safePadding;
        var colMax = GridCellLookup.FindCellIndex(grid.XEdges, bounds.MaxX, grid.XEdges.Count - 1) + safePadding;

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


}
