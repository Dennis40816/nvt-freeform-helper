using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22ResolvedResultServiceTests
{
    private static readonly double[] Grid10 = [10.0];
    private static readonly double[] Grid10x2 = [10.0, 10.0];

    [Fact]
    public void Build_UsesSingleStage3Source_ForRatioAllocationAndFinalOutline()
    {
        var grid = CreateGrid(Grid10x2, Grid10);
        var cad = CreateCadRect(0, 0, 15, 5, id: 40);
        var blocker = CreateCadRect(12, 0, 20, 5, id: 41);
        var allCadPads = new[] { cad, blocker };
        var compensation = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: allCadPads);
        var resolved = new NotchV22ResolvedResultService().Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 40);

        Assert.True(compensation.IsToFullEnabled);
        Assert.Equal(142.5, compensation.Stage3Area, 6);
        Assert.Equal(1.9, compensation.ToFullRatio, 6);
        Assert.Equal(compensation.Stage3Area, resolved.TargetAllocation.Stage3Area, 6);
        Assert.Equal(2, resolved.Stage2CandidatePolygons.Count);
        Assert.True(
            resolved.Stage2CandidatePolygons.Sum(static polygon => polygon.Area()) >
            compensation.Stage3Area);
        var finalOutlineArea = resolved.Stage3FinalOutlinePolygons.Sum(static polygon => polygon.Area());
        Assert.Equal(compensation.Stage3Area, finalOutlineArea, 6);
    }

    [Fact]
    public void Build_UsesResolvedStageModel_ForSharedReachableExpansion()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 3, 3, 7, id: 10);
        var blocker = CreateCadRect(5, 0, 10, 10, id: 11);
        var allCadPads = new[] { cad, blocker };
        var compensation = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: allCadPads);
        var resolved = new NotchV22ResolvedResultService().Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001);

        var debug = Assert.Single(compensation.RegularDebugInfos);
        Assert.True(debug.IsToFullBoundaryCandidate);
        Assert.True(debug.IsToFullApplied);
        Assert.True(compensation.IsToFullEnabled);
        Assert.True(compensation.Stage3Area > cad.Area);
        Assert.NotEmpty(resolved.Stage1SeedPolygons);
        Assert.NotEmpty(resolved.Stage2CandidatePolygons);
        Assert.NotEmpty(resolved.Stage3FinalOutlinePolygons);
        Assert.Equal(compensation.Stage3Area, resolved.TargetAllocation.Stage3Area, 6);
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
                pads.Add(new RegularPad(row, col, index, polygon)
                {
                    DiffIndex = index,
                    IcIndex = 0
                });
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

    private static CadPad CreateCadRect(double minX, double minY, double maxX, double maxY, int id = 1)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(id, $"C{id}", "PAD", polygon);
    }
}
