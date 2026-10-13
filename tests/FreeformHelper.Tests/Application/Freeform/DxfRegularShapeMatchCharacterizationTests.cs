using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how a DXF regular layer pad that is not a rectangle takes part in the matcher
/// (issue #64). The layer builder accepts any polygon as a regular pad. The matcher then clips the
/// CAD polygon against the bounding rectangle of the regular pad, so a CAD pad that lies in the
/// empty corner of a triangle still gets a link with a positive overlap. These tests describe the
/// current result, not the wanted one. A fix that rejects the shape or that clips against the
/// polygon changes the expected values here and must say so in its pull request.
/// </summary>
public sealed class DxfRegularShapeMatchCharacterizationTests
{
    // Regular pad: right triangle (0,0), (2,0), (0,2). Its bounding rectangle is (0,0)..(2,2).
    // CAD pad: square (1.5,1.5)..(1.8,1.8). Every corner has x + y > 2, so the square is outside
    // the triangle, and inside the bounding rectangle.
    private const double ExpectedRectangleOverlapArea = 0.09;

    [Fact]
    public void BuildFromLayer_TriangleLayerPad_IsAcceptedAsARegularPad()
    {
        var grid = BuildTriangleGrid();

        var pad = Assert.Single(grid.Pads);
        Assert.Equal(3, pad.Polygon.Vertices.Length);
        Assert.Equal(2.0, pad.Bounds.MaxX, 9);
        Assert.Equal(2.0, pad.Bounds.MaxY, 9);
    }

    [Fact]
    public void Match_CadSquareOutsideTheTriangleButInsideItsBounds_ProducesALink()
    {
        var grid = BuildTriangleGrid();
        var square = CreateCadSquare();
        Assert.All(square.Polygon.Vertices, static vertex => Assert.True(vertex.X + vertex.Y > 2.0));

        var result = PadMatcher.Match(new CadPadSet(new[] { square }), grid);

        var link = Assert.Single(result.Links);
        Assert.Equal(square.Id, link.CadPadId);
        Assert.Equal(ExpectedRectangleOverlapArea, link.OverlapArea, 9);
        Assert.Equal(1.0, link.CadCoverage, 9);
    }

    private static RegularGrid BuildTriangleGrid()
    {
        var triangle = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(2, 0),
            new Point2(0, 2),
        });
        var layer = new CadPadSet(new[] { new CadPad(10, "L", "REG", triangle) });
        var settings = new GridSettings { XChannels = 1, YChannels = 1 };
        return DxfRegularLayerGridBuilder.BuildFromLayer(layer, settings);
    }

    private static CadPad CreateCadSquare()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(1.5, 1.5),
            new Point2(1.8, 1.5),
            new Point2(1.8, 1.8),
            new Point2(1.5, 1.8),
        });
        return new CadPad(1, "CAD", "P", polygon);
    }
}
