using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularGridCandidateQueryTests
{
    private static readonly double[] Uniform10x2 = [10.0, 10.0];
    private static readonly double[] Uniform10x3 = [10.0, 10.0, 10.0];
    private static readonly double[] IrregularWidths = [8.0, 12.0, 5.0, 20.0];
    private static readonly double[] IrregularHeights = [6.0, 14.0, 9.0];
    private static readonly int[] IntersectingIndices = [0, 1, 3, 4];

    [Fact]
    public void QueryByBounds_ReturnsOnlyIntersectingPads()
    {
        var grid = CreateGrid(widths: Uniform10x3, heights: Uniform10x3);
        var rect = new Rect2(5, 5, 15, 15);

        var result = RegularGridCandidateQuery.QueryByBounds(grid, rect)
            .Select(static pad => pad.Index)
            .OrderBy(static index => index)
            .ToArray();

        Assert.Equal(IntersectingIndices, result);
    }

    [Fact]
    public void QueryByBounds_WhenOutsideGrid_ReturnsEmpty()
    {
        var grid = CreateGrid(widths: Uniform10x2, heights: Uniform10x2);
        var rect = new Rect2(100, 100, 120, 120);

        var result = RegularGridCandidateQuery.QueryByBounds(grid, rect).ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void QueryByBounds_MatchesLinearScan_ForIrregularCellSizes()
    {
        var grid = CreateGrid(widths: IrregularWidths, heights: IrregularHeights);
        var rect = new Rect2(7, 4, 30, 20);

        var indexed = RegularGridCandidateQuery.QueryByBounds(grid, rect)
            .Select(static pad => pad.Index)
            .OrderBy(static index => index)
            .ToArray();
        var linear = grid.Pads
            .Where(pad => pad.Bounds.Intersects(rect))
            .Select(static pad => pad.Index)
            .OrderBy(static index => index)
            .ToArray();

        Assert.Equal(linear, indexed);
    }

    [Fact]
    public void GetCandidateRange_IncludesPaddingAndClampsToGrid()
    {
        var grid = CreateGrid(widths: Uniform10x3, heights: Uniform10x3);
        var rect = new Rect2(11, 11, 12, 12);

        var range = RegularGridCandidateQuery.GetCandidateRange(grid, rect, padding: 1);

        Assert.Equal((0, 2, 0, 2), range);
    }

    private static RegularGrid CreateGrid(double[] widths, double[] heights)
    {
        var xEdges = BuildEdges(widths);
        var yEdges = BuildEdges(heights);
        var rows = heights.Length;
        var cols = widths.Length;
        var pads = new List<RegularPad>(rows * cols);

        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var x0 = xEdges[col];
                var x1 = xEdges[col + 1];
                var y0 = yEdges[row];
                var y1 = yEdges[row + 1];
                var polygon = new Polygon2(new[]
                {
                    new Point2(x0, y0),
                    new Point2(x1, y0),
                    new Point2(x1, y1),
                    new Point2(x0, y1),
                });

                pads.Add(new RegularPad(row, col, index, polygon));
                index++;
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static double[] BuildEdges(double[] sizes)
    {
        var edges = new double[sizes.Length + 1];
        for (var i = 0; i < sizes.Length; i++)
        {
            edges[i + 1] = edges[i] + sizes[i];
        }

        return edges;
    }
}
