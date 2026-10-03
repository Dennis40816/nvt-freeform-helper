using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public enum CoordinateSurfaceCurvatureDirection
{
    AlongX,
    AlongY,
}

public enum CoordinateSurfaceMappingMode
{
    ArcLength,
    Projection,
}

public enum CoordinateSurfaceZDirection
{
    Positive,
    Negative,
}

public sealed record CoordinateSurfaceProfile(
    CoordinateSurfaceCurvatureDirection CurvatureDirection,
    CoordinateSurfaceMappingMode MappingMode,
    CoordinateSurfaceZDirection ZDirection,
    double Radius,
    Point2 Origin,
    double OriginZ = 0d);

public readonly record struct CoordinateSurfacePoint(double X, double Y, double Z);

public static class CoordinateSurfaceTransformService
{
    public static CoordinateSurfacePoint ProjectMachinePoint(
        Point2 machinePoint,
        CoordinateSurfaceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var radius = Math.Max(1e-9, Math.Abs(profile.Radius));
        var signedZ = profile.ZDirection == CoordinateSurfaceZDirection.Negative ? -1d : 1d;
        var deltaX = machinePoint.X - profile.Origin.X;
        var deltaY = machinePoint.Y - profile.Origin.Y;
        var curveOffset = profile.CurvatureDirection == CoordinateSurfaceCurvatureDirection.AlongX
            ? deltaX
            : deltaY;
        var (surfaceOffset, zOffset) = profile.MappingMode == CoordinateSurfaceMappingMode.Projection
            ? ProjectFromPlanarProjection(curveOffset, radius)
            : ProjectFromArcLength(curveOffset, radius);

        return profile.CurvatureDirection == CoordinateSurfaceCurvatureDirection.AlongX
            ? new CoordinateSurfacePoint(profile.Origin.X + surfaceOffset, machinePoint.Y, profile.OriginZ + (signedZ * zOffset))
            : new CoordinateSurfacePoint(machinePoint.X, profile.Origin.Y + surfaceOffset, profile.OriginZ + (signedZ * zOffset));
    }

    private static (double surfaceOffset, double zOffset) ProjectFromArcLength(double arcLength, double radius)
    {
        var angle = arcLength / radius;
        return (
            radius * Math.Sin(angle),
            radius * (1d - Math.Cos(angle)));
    }

    private static (double surfaceOffset, double zOffset) ProjectFromPlanarProjection(double projectionOffset, double radius)
    {
        var clampedOffset = Math.Clamp(projectionOffset, -radius, radius);
        return (
            clampedOffset,
            radius - Math.Sqrt(Math.Max(0d, (radius * radius) - (clampedOffset * clampedOffset))));
    }
}
