using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the CAD pad union does with a union that has a hole (issue #64). The result type
/// carries outer polygons only, so the hole is counted and then dropped, and the combined pad is
/// the filled outline. These tests describe the current result, not the wanted one. A fix that
/// rejects hole-bearing unions, or that changes the outer polygons or the path counts, changes the
/// expected values here and must say so in its pull request. A fix that only adds a separate hole
/// collection leaves these tests green and needs its own test for the holes.
/// </summary>
public sealed class CadPadUnionHoleCharacterizationTests
{
    private const double FrameOuterSize = 10.0;
    private const double FrameThickness = 2.0;

    [Fact]
    public void Union_FourStripsFormingAFrame_ReturnsTheFilledOutline()
    {
        var strips = CreateFrameStrips();
        var stripArea = strips.Sum(static strip => strip.Area());

        var result = CadPadUnionService.Union(strips);

        // Four strips of a 10 by 10 frame with thickness 2 hold 64 units of material.
        Assert.Equal(64.0, stripArea, 9);
        var outline = Assert.Single(result.OuterPolygons);
        Assert.Equal(FrameOuterSize * FrameOuterSize, outline.Area(), 9);
    }

    [Fact]
    public void Union_FourStripsFormingAFrame_CountsTheHoleButDoesNotReturnIt()
    {
        var result = CadPadUnionService.Union(CreateFrameStrips());

        Assert.Equal(4, result.InputPolygonCount);
        Assert.Equal(1, result.HolePathCount);
        Assert.Equal(2, result.OutputPathCount);
        Assert.Single(result.OuterPolygons);
    }

    private static Polygon2[] CreateFrameStrips()
    {
        const double inner = FrameOuterSize - FrameThickness;
        return
        [
            TestGeometryFactory.CreateRect(0, 0, FrameOuterSize, FrameThickness),
            TestGeometryFactory.CreateRect(0, inner, FrameOuterSize, FrameOuterSize),
            TestGeometryFactory.CreateRect(0, FrameThickness, FrameThickness, inner),
            TestGeometryFactory.CreateRect(inner, FrameThickness, FrameOuterSize, inner),
        ];
    }
}
