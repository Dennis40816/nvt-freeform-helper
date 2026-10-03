using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadUnionServiceTests
{
    [Fact]
    public void Union_WhenPadsTouch_ReturnsSinglePolygon()
    {
        _ = new CadPadUnionService();
        var polygons = new[]
        {
            TestGeometryFactory.CreateRect(0, 0, 10, 10),
            TestGeometryFactory.CreateRect(10, 0, 20, 10),
        };

        var result = CadPadUnionService.Union(polygons);

        Assert.Single(result.OuterPolygons);
        Assert.Equal(2, result.InputPolygonCount);
        Assert.True(result.OuterPolygons[0].Area() > 199.999);
    }

    [Fact]
    public void Union_WhenPadsDisjoint_ReturnsMultiplePolygons()
    {
        _ = new CadPadUnionService();
        var polygons = new[]
        {
            TestGeometryFactory.CreateRect(0, 0, 10, 10),
            TestGeometryFactory.CreateRect(30, 0, 40, 10),
        };

        var result = CadPadUnionService.Union(polygons);

        Assert.Equal(2, result.OuterPolygons.Count);
        var areas = result.OuterPolygons.Select(p => p.Area()).OrderBy(a => a).ToList();
        Assert.Equal(new List<double> { 100.0, 100.0 }, areas);
    }

    [Fact]
    public void Union_WhenCadPolygonUsesProjectVertexOrder_DoesNotDropFinalOutline()
    {
        var polygons = new[]
        {
            new Polygon2(new[]
            {
                new Point2(8.31716, 15.82673),
                new Point2(3.08906, 15.82673),
                new Point2(3.08906, 16.95713),
                new Point2(2.94776, 16.95713),
                new Point2(2.94776, 20.34833),
                new Point2(8.31716, 20.34833),
            }),
            TestGeometryFactory.CreateRect(2.94776, 15.82673, 5.49116, 20.34833),
        };

        var result = CadPadUnionService.Union(polygons);

        var polygon = Assert.Single(result.OuterPolygons);
        Assert.Equal(2, result.InputPolygonCount);
        Assert.True(polygon.Area() > 24.2782 && polygon.Area() < 24.2784);
        Assert.Equal(2.94776, polygon.Bounds.MinX, 5);
        Assert.Equal(15.82673, polygon.Bounds.MinY, 5);
        Assert.Equal(8.31716, polygon.Bounds.MaxX, 5);
        Assert.Equal(20.34833, polygon.Bounds.MaxY, 5);
    }
}
