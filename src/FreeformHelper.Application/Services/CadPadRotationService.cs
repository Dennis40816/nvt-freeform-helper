using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Applies rigid-group rotation to a subset of CAD pads.
/// </summary>
public static class CadPadRotationService
{
    public static CadPadRotationResult Rotate(CadPadSet cad, IReadOnlySet<int> targetCadPadIds, double rotationDegrees)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(targetCadPadIds);

        var normalizedDegrees = NormalizeDegrees(rotationDegrees);
        if (targetCadPadIds.Count == 0 || Math.Abs(normalizedDegrees) < 1e-9)
        {
            return new CadPadRotationResult(cad, 0, normalizedDegrees);
        }

        var radians = normalizedDegrees * Math.PI / 180.0;
        var cos = SnapTrigonometric(Math.Cos(radians));
        var sin = SnapTrigonometric(Math.Sin(radians));
        var targetPads = cad.Pads
            .Where(pad => targetCadPadIds.Contains(pad.Id))
            .ToList();
        if (targetPads.Count == 0)
        {
            return new CadPadRotationResult(cad, 0, normalizedDegrees);
        }

        var rotationCenter = CalculateBoundsCenter(targetPads);

        var changedCount = 0;
        var updatedPads = new List<CadPad>(cad.Pads.Count);
        foreach (var pad in cad.Pads)
        {
            if (!targetCadPadIds.Contains(pad.Id))
            {
                updatedPads.Add(pad);
                continue;
            }

            var rotatedPolygon = RotatePolygonAroundCenter(pad.Polygon, rotationCenter, cos, sin);
            updatedPads.Add(new CadPad(pad.Id, pad.Name, pad.Layer, rotatedPolygon));
            changedCount++;
        }

        return new CadPadRotationResult(new CadPadSet(updatedPads), changedCount, normalizedDegrees);
    }

    private static Point2 CalculateBoundsCenter(IReadOnlyList<CadPad> targetPads)
    {
        var minX = targetPads.Min(static pad => pad.Bounds.MinX);
        var minY = targetPads.Min(static pad => pad.Bounds.MinY);
        var maxX = targetPads.Max(static pad => pad.Bounds.MaxX);
        var maxY = targetPads.Max(static pad => pad.Bounds.MaxY);
        return new Point2(
            NormalizeCoordinate((minX + maxX) / 2.0),
            NormalizeCoordinate((minY + maxY) / 2.0));
    }

    private static Polygon2 RotatePolygonAroundCenter(Polygon2 polygon, Point2 rotationCenter, double cos, double sin)
    {
        var rotatedVertices = polygon.Vertices
            .Select(vertex => RotatePoint(vertex, rotationCenter, cos, sin))
            .ToList();
        return new Polygon2(rotatedVertices);
    }

    private static Point2 RotatePoint(Point2 point, Point2 center, double cos, double sin)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var rotatedX = center.X + dx * cos - dy * sin;
        var rotatedY = center.Y + dx * sin + dy * cos;
        return new Point2(NormalizeCoordinate(rotatedX), NormalizeCoordinate(rotatedY));
    }

    private static double NormalizeDegrees(double rotationDegrees)
    {
        var normalized = rotationDegrees % 360.0;
        if (normalized <= -180.0)
        {
            normalized += 360.0;
        }
        else if (normalized > 180.0)
        {
            normalized -= 360.0;
        }

        return Math.Abs(normalized) < 1e-9 ? 0.0 : normalized;
    }

    private static double SnapTrigonometric(double value)
    {
        if (Math.Abs(value) < 1e-12)
        {
            return 0.0;
        }

        if (Math.Abs(value - 1.0) < 1e-12)
        {
            return 1.0;
        }

        if (Math.Abs(value + 1.0) < 1e-12)
        {
            return -1.0;
        }

        return value;
    }

    private static double NormalizeCoordinate(double value)
    {
        return Math.Abs(value) < 1e-9 ? 0.0 : value;
    }
}

public sealed record CadPadRotationResult(
    CadPadSet Cad,
    int ChangedCount,
    double AppliedDegrees);
