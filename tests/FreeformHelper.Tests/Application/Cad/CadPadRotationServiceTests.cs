using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadRotationServiceTests
{
    [Fact]
    public void Rotate_WhenRotatingSingleRectangle90Degrees_RotatesAroundSelectionCenter()
    {
        var pad = new CadPad(
            1,
            "CAD1",
            "L1",
            new Polygon2(
            [
                new Point2(0, 0),
                new Point2(4, 0),
                new Point2(4, 2),
                new Point2(0, 2),
            ]));
        var cad = new CadPadSet([pad]);

        var result = CadPadRotationService.Rotate(cad, new HashSet<int> { 1 }, 90);

        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(90, result.AppliedDegrees, 6);

        var rotated = Assert.Single(result.Cad.Pads);
        Assert.Equal(pad.Area, rotated.Area, 6);
        Assert.Equal(pad.Centroid.X, rotated.Centroid.X, 6);
        Assert.Equal(pad.Centroid.Y, rotated.Centroid.Y, 6);
        Assert.Equal(1, rotated.Bounds.MinX, 6);
        Assert.Equal(-1, rotated.Bounds.MinY, 6);
        Assert.Equal(3, rotated.Bounds.MaxX, 6);
        Assert.Equal(3, rotated.Bounds.MaxY, 6);
    }

    [Fact]
    public void Rotate_WhenRotatingTwoPads_RotatesThemAsRigidGroup()
    {
        var leftPad = CreateRectCad(1, 0, 0, 4, 2);
        var rightPad = CreateRectCad(2, 8, 0, 12, 2);
        var cad = new CadPadSet([leftPad, rightPad]);

        var result = CadPadRotationService.Rotate(cad, new HashSet<int> { 1, 2 }, 90);

        Assert.Equal(2, result.ChangedCount);
        Assert.Equal(90, result.AppliedDegrees, 6);

        var rotatedLeft = Assert.Single(result.Cad.Pads, pad => pad.Id == 1);
        var rotatedRight = Assert.Single(result.Cad.Pads, pad => pad.Id == 2);
        Assert.Equal(6, rotatedLeft.Centroid.X, 6);
        Assert.Equal(-3, rotatedLeft.Centroid.Y, 6);
        Assert.Equal(6, rotatedRight.Centroid.X, 6);
        Assert.Equal(5, rotatedRight.Centroid.Y, 6);
        Assert.Equal(4, rotatedLeft.Centroid.X - leftPad.Centroid.X, 6);
        Assert.Equal(-4, rotatedLeft.Centroid.Y - leftPad.Centroid.Y, 6);
        Assert.Equal(-4, rotatedRight.Centroid.X - rightPad.Centroid.X, 6);
        Assert.Equal(4, rotatedRight.Centroid.Y - rightPad.Centroid.Y, 6);
    }

    [Fact]
    public void Rotate_WhenDegreesNormalizeToZero_ReturnsOriginalCad()
    {
        var pad = CreateRectCad(1, 0, 0, 2, 2);
        var cad = new CadPadSet([pad]);

        var result = CadPadRotationService.Rotate(cad, new HashSet<int> { 1 }, 360);

        Assert.Equal(0, result.ChangedCount);
        Assert.Same(cad, result.Cad);
        Assert.Equal(0, result.AppliedDegrees, 6);
    }

    private static CadPad CreateRectCad(int id, double minX, double minY, double maxX, double maxY)
    {
        return new CadPad(
            id,
            $"CAD{id}",
            "L1",
            new Polygon2(
            [
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY),
            ]));
    }
}
