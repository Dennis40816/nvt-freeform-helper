using Clipper2Lib;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed record CopperPillarSimulationRequest(
    double CenterX,
    double CenterY,
    double Diameter,
    double PeakValue,
    double BaselineValue = 0d,
    int CircleSegmentCount = 96);

public static class CopperPillarSimulationService
{
    private const double AreaEpsilon = 1e-12;
    private const int GeometryPrecisionDigits = 6;
    private const int MinimumCircleSegments = 16;
    private const int MaximumCircleSegments = 256;

    public static DiffFrameGridProjectionResult ProjectToRegularGrid(
        RegularGrid grid,
        CopperPillarSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(request);

        if (grid.Pads.Count == 0)
        {
            return DiffFrameGridProjectionResult.Incompatible("Regular grid has no pads.");
        }

        var diameter = Math.Max(0d, request.Diameter);
        if (diameter <= AreaEpsilon)
        {
            return DiffFrameGridProjectionResult.Incompatible("Copper diameter must be greater than 0.");
        }

        var circle = BuildCirclePolygon(
            new Point2(request.CenterX, request.CenterY),
            diameter * 0.5d,
            Math.Clamp(request.CircleSegmentCount, MinimumCircleSegments, MaximumCircleSegments));
        var circleBounds = circle.Bounds;
        var circleArea = Math.Max(circle.Area(), AreaEpsilon);
        var cells = new List<DiffFrameGridCellValue>(grid.Pads.Count);

        foreach (var pad in grid.Pads)
        {
            var overlapArea = pad.Bounds.Intersects(circleBounds)
                ? Polygon2.IntersectionAreaWithRect(circle, pad.Bounds)
                : 0d;
            var value = request.BaselineValue + (request.PeakValue * (overlapArea / circleArea));
            cells.Add(new DiffFrameGridCellValue(
                pad.Row,
                pad.Col,
                pad.Row,
                pad.Col,
                pad.RegularPadId,
                value));
        }

        return new DiffFrameGridProjectionResult(true, null, cells);
    }

    public static DiffFrameGridProjectionResult ProjectCadOutputToRegularGrid(
        RegularGrid grid,
        IReadOnlyList<CadPad> cadPads,
        IReadOnlyDictionary<int, int> cadOutputFwDiffByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        CopperPillarSimulationRequest request)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(cadPads);
        ArgumentNullException.ThrowIfNull(cadOutputFwDiffByCadId);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);
        ArgumentNullException.ThrowIfNull(request);

        if (grid.Pads.Count == 0)
        {
            return DiffFrameGridProjectionResult.Incompatible("Regular grid has no pads.");
        }

        if (cadPads.Count == 0 || cadOutputFwDiffByCadId.Count == 0)
        {
            return DiffFrameGridProjectionResult.Incompatible(
                "Copper CAD projection requires CAD pads and CAD Output FW Diff mapping.");
        }

        // Physical source is CAD copper overlap; the FW-facing value is written to the CAD Output FW Diff slot.
        var diameter = Math.Max(0d, request.Diameter);
        if (diameter <= AreaEpsilon)
        {
            return DiffFrameGridProjectionResult.Incompatible("Copper diameter must be greater than 0.");
        }

        var circleCenter = new Point2(request.CenterX, request.CenterY);
        var circleRadius = diameter * 0.5d;
        var circle = BuildCirclePolygon(
            circleCenter,
            circleRadius,
            Math.Clamp(request.CircleSegmentCount, MinimumCircleSegments, MaximumCircleSegments));
        var circleBounds = circle.Bounds;
        var circleArea = Math.Max(circle.Area(), AreaEpsilon);
        var valueByRegularPadId = grid.Pads.ToDictionary(
            static pad => pad.RegularPadId,
            _ => request.BaselineValue);
        var primaryRegularByDiff = grid.Pads
            .GroupBy(static pad => new NotchDiffKey(pad.IcIndex, pad.DiffIndex))
            .ToDictionary(
                static group => group.Key,
                static group => group
                    .OrderBy(static pad => pad.Row)
                    .ThenBy(static pad => pad.Col)
                    .First());

        var touchedCadCount = 0;
        var missingCadOutputCount = 0;
        var missingTargetDiffCount = 0;
        foreach (var cadPad in cadPads)
        {
            if (!cadPad.Bounds.Intersects(circleBounds))
            {
                continue;
            }

            // A contained circle has exact full coverage; clipping rounds coordinates to six digits.
            var overlapArea = ContainsCircle(cadPad.Polygon, circleCenter, circleRadius)
                ? circleArea
                : IntersectionArea(cadPad.Polygon, circle);
            if (overlapArea <= AreaEpsilon)
            {
                continue;
            }

            touchedCadCount++;
            if (!cadOutputFwDiffByCadId.TryGetValue(cadPad.Id, out var outputFwDiffIndex) ||
                !cadIcIndexByCadId.TryGetValue(cadPad.Id, out var icIndex))
            {
                missingCadOutputCount++;
                continue;
            }

            if (!primaryRegularByDiff.TryGetValue(new NotchDiffKey(icIndex, outputFwDiffIndex), out var targetRegular))
            {
                missingTargetDiffCount++;
                continue;
            }

            valueByRegularPadId[targetRegular.RegularPadId] += request.PeakValue * (overlapArea / circleArea);
        }

        var cells = grid.Pads
            .Select(pad => new DiffFrameGridCellValue(
                pad.Row,
                pad.Col,
                pad.Row,
                pad.Col,
                pad.RegularPadId,
                valueByRegularPadId[pad.RegularPadId]))
            .ToArray();
        var diagnostics = BuildCadProjectionDiagnostic(touchedCadCount, missingCadOutputCount, missingTargetDiffCount);
        return new DiffFrameGridProjectionResult(true, diagnostics, cells);
    }

    private static Polygon2 BuildCirclePolygon(Point2 center, double radius, int segmentCount)
    {
        var points = new Point2[segmentCount];
        for (var index = 0; index < segmentCount; index++)
        {
            var angle = (Math.PI * 2d * index) / segmentCount;
            points[index] = new Point2(
                center.X + (radius * Math.Cos(angle)),
                center.Y + (radius * Math.Sin(angle)));
        }

        return new Polygon2(points);
    }

    private static string? BuildCadProjectionDiagnostic(
        int touchedCadCount,
        int missingCadOutputCount,
        int missingTargetDiffCount)
    {
        if (missingCadOutputCount == 0 && missingTargetDiffCount == 0)
        {
            return null;
        }

        return $"Copper CAD projection touched {touchedCadCount} CAD pad(s); missing CAD output mapping={missingCadOutputCount}, missing target regular diff={missingTargetDiffCount}.";
    }

    private static bool ContainsCircle(Polygon2 polygon, Point2 center, double radius)
    {
        if (!polygon.Contains(center))
        {
            return false;
        }

        // Checking every edge also handles concave pads; bounds or vertices alone cannot prove containment.
        for (var index = 0; index < polygon.Vertices.Length; index++)
        {
            var start = polygon.Vertices[index];
            var end = polygon.Vertices[(index + 1) % polygon.Vertices.Length];
            var edge = end - start;
            var offset = center - start;
            var lengthSquared = (edge.X * edge.X) + (edge.Y * edge.Y);
            var dot = (offset.X * edge.X) + (offset.Y * edge.Y);
            if (lengthSquared == 0d || dot <= 0d || dot >= lengthSquared)
            {
                if (center.DistanceTo(dot <= 0d ? start : end) < radius)
                {
                    return false;
                }
            }
            else
            {
                // Avoid rounding an interior closest point when the circle is exactly tangent.
                var cross = (offset.X * edge.Y) - (offset.Y * edge.X);
                if ((cross * cross) < (radius * radius * lengthSquared))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static double IntersectionArea(Polygon2 a, Polygon2 b)
    {
        var subjects = new PathsD { ToClipperPath(a) };
        var clips = new PathsD { ToClipperPath(b) };
        var solutionTree = new PolyTreeD();
        Clipper.BooleanOp(
            ClipType.Intersection,
            subjects,
            clips,
            solutionTree,
            FillRule.NonZero,
            GeometryPrecisionDigits);

        var area = 0d;
        foreach (PolyPathD child in solutionTree)
        {
            area += SumArea(child);
        }

        return Math.Max(0d, area);
    }

    private static double SumArea(PolyPathD node)
    {
        var area = 0d;
        var polygon = node.Polygon;
        if (polygon is not null && polygon.Count >= 3)
        {
            var absolute = Math.Abs(Clipper.Area(polygon));
            area += node.IsHole ? -absolute : absolute;
        }

        foreach (PolyPathD child in node)
        {
            area += SumArea(child);
        }

        return area;
    }

    private static PathD ToClipperPath(Polygon2 polygon)
    {
        var path = new PathD(polygon.Vertices.Length);
        foreach (var vertex in polygon.Vertices)
        {
            path.Add(new PointD(vertex.X, vertex.Y));
        }

        if (path.Count >= 3 && !Clipper.IsPositive(path))
        {
            path.Reverse();
        }

        return path;
    }
}
