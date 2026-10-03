using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22FinalOutlineServiceTests
{
    [Fact]
    public void Build_WhenCadBodyAndAppliedRectFormFullFinalOutline_ReturnsSingleOuterPolygon()
    {
        var service = new NotchV22FinalOutlineService();
        var cadPad = new CadPad(
            id: 4818,
            name: "PAD_4819",
            layer: "PGT.drawing",
            polygon: new Polygon2(new[]
            {
                new Point2(8.31716, 15.82673),
                new Point2(3.08906, 15.82673),
                new Point2(3.08906, 16.95713),
                new Point2(2.94776, 16.95713),
                new Point2(2.94776, 20.34833),
                new Point2(8.31716, 20.34833),
            }));
        var appliedToFullPolygons = new[]
        {
            new Polygon2(new[]
            {
                new Point2(2.94776, 15.82673),
                new Point2(5.49116, 15.82673),
                new Point2(5.49116, 20.34833),
                new Point2(2.94776, 20.34833),
            }),
        };

        var result = service.Build(cadPad, appliedToFullPolygons, isToFullEnabled: true);

        var polygon = Assert.Single(result);
        Assert.True(polygon.Area() > 24.2782 && polygon.Area() < 24.2784);
        Assert.Equal(2.94776, polygon.Bounds.MinX, 5);
        Assert.Equal(15.82673, polygon.Bounds.MinY, 5);
        Assert.Equal(8.31716, polygon.Bounds.MaxX, 5);
        Assert.Equal(20.34833, polygon.Bounds.MaxY, 5);
    }
}
