using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinateSurfaceTransformServiceTests
{
    [Fact]
    public void ProjectMachinePoint_WhenArcLengthAlongX_ProducesCylindricalZ()
    {
        var profile = new CoordinateSurfaceProfile(
            CoordinateSurfaceCurvatureDirection.AlongX,
            CoordinateSurfaceMappingMode.ArcLength,
            CoordinateSurfaceZDirection.Positive,
            Radius: 100d,
            Origin: new Point2(0d, 0d));

        var point = CoordinateSurfaceTransformService.ProjectMachinePoint(new Point2(10d, 20d), profile);

        Assert.Equal(9.983341664682815d, point.X, 12);
        Assert.Equal(20d, point.Y, 12);
        Assert.Equal(0.49958347219741783d, point.Z, 12);
    }

    [Fact]
    public void ProjectMachinePoint_WhenProjectionAlongYAndNegativeZ_UsesSagDirection()
    {
        var profile = new CoordinateSurfaceProfile(
            CoordinateSurfaceCurvatureDirection.AlongY,
            CoordinateSurfaceMappingMode.Projection,
            CoordinateSurfaceZDirection.Negative,
            Radius: 100d,
            Origin: new Point2(5d, 10d),
            OriginZ: 2d);

        var point = CoordinateSurfaceTransformService.ProjectMachinePoint(new Point2(25d, 20d), profile);

        Assert.Equal(25d, point.X, 12);
        Assert.Equal(20d, point.Y, 12);
        Assert.Equal(1.498743710662d, point.Z, 12);
    }
}
