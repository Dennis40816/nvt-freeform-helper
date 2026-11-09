// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22CompensationGeometryBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compute_ClosedTrapezoidWithRepeatedVertices_KeepsTheSlopedSeed(bool transpose)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        Point2[] vertices =
        [
            new(0, 0), new(8, 0), new(8, 0), new(6, 10), new(0, 10), new(0, 0),
        ];
        var polygon = new Polygon2(vertices.Select(point => transpose ? new Point2(point.Y, point.X) : point));
        var target = new CadPad(1, "CAD", "PAD", polygon);

        var result = NotchV22CompensationService.Compute(target, grid, enableToRegular: true, enableToFull: true);

        // A trapezoid with parallel sides 8 and 6 has area 70, while its bounding rectangle has area 80.
        var seed = Assert.Single(result.ToFullSeedPolygons);
        Assert.Equal(70.0, seed.Area(), 9);
        Assert.Equal(4, seed.Vertices.Length);
        Assert.Contains(transpose ? new Point2(10, 6) : new Point2(6, 10), seed.Vertices);
        Assert.Equal(new Rect2(0, 0, 10, 10), Assert.Single(result.ToFullPolygons).Bounds);
        Assert.Equal(70.0, result.OverlapAreaTotal, 9);
        Assert.Equal(100.0, result.Stage3Area, 9);
        Assert.Equal(0.7, result.ToRegularRatio, 9);
        Assert.Equal(1.0, result.CombinedRatio, 9);
    }

    [Fact]
    public void Compute_DiamondCrossingAllGridEdges_ClipsSeedAndPreservesOutsideCadArea()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var target = new CadPad(1, "CAD", "PAD", new Polygon2(
        [
            new Point2(-2.5, 5), new Point2(5, -2.5), new Point2(12.5, 5), new Point2(5, 12.5),
        ]));

        var result = NotchV22CompensationService.Compute(target, grid, enableToRegular: true, enableToFull: true);

        // Four corner triangles of area 3.125 are outside the diamond; 25 CAD area is outside the regular.
        var seed = Assert.Single(result.ToFullSeedPolygons);
        Assert.Equal(87.5, seed.Area(), 9);
        Assert.Equal(8, seed.Vertices.Length);
        Assert.All(seed.Vertices, point =>
        {
            Assert.InRange(point.X, 0.0, 10.0);
            Assert.InRange(point.Y, 0.0, 10.0);
        });
        Assert.Equal(87.5, result.OverlapAreaTotal, 9);
        Assert.Equal(125.0, result.Stage3Area, 9);
        Assert.Equal(10.0 / 9.0, result.ToFullRatio, 9);
        Assert.Equal(new Rect2(0, 0, 10, 10), Assert.Single(result.ToFullPolygons).Bounds);
    }

    [Fact]
    public void Compute_CadConnectedOutsideGrid_KeepsSeparatedExpansionRectangles()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(3, 1, cellWidth: 10, cellHeight: 10);
        // The right-hand connector lies outside the grid; only the bottom and top regulars overlap.
        var target = new CadPad(1, "CAD", "PAD", new Polygon2(
        [
            new Point2(0, 0), new Point2(15, 0), new Point2(15, 30), new Point2(0, 30),
            new Point2(0, 25), new Point2(12, 25), new Point2(12, 5), new Point2(0, 5),
        ]));

        var result = NotchV22CompensationService.Compute(target, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(2, result.OverlapRegularCount);
        Assert.Equal(100.0, result.OverlapAreaTotal, 9);
        Assert.Equal(100.0, result.ToFullSeedPolygons.Sum(static polygon => polygon.Area()), 9);
        Assert.Collection(result.ToFullPolygons,
            polygon => Assert.Equal(new Rect2(0, 0, 10, 10), polygon.Bounds),
            polygon => Assert.Equal(new Rect2(0, 20, 10, 30), polygon.Bounds));
        Assert.Equal(310.0, result.Stage3Area, 9);
        Assert.Equal(31.0 / 21.0, result.ToFullRatio, 9);
        Assert.All(result.RegularDebugInfos, static info => Assert.True(info.IsToFullApplied));
    }
}
