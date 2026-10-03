using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchV22CompensationService
{
    private static IReadOnlyList<Polygon2> BuildToFullPolygons(IReadOnlyList<Polygon2> polygons)
    {
        if (polygons.Count == 0)
        {
            return Array.Empty<Polygon2>();
        }

        var allAxisAlignedRectangles = polygons.All(
            polygon => IsAxisAlignedRectangle(polygon));
        if (!allAxisAlignedRectangles)
        {
            return polygons
                .Where(static polygon => polygon.Area() > AreaEpsilon)
                .ToList();
        }

        // Keep To Full preview in axis-aligned rectangles for clear in-app visualization.
        var rectangles = polygons
            .Select(polygon => polygon.Bounds)
            .Where(rect => rect.Width > AreaEpsilon && rect.Height > AreaEpsilon)
            .ToList();
        if (rectangles.Count == 0)
        {
            return Array.Empty<Polygon2>();
        }

        var merged = MergeRectangles(rectangles);
        return merged
            .Select(static rect => new Polygon2(new[]
            {
                new Point2(rect.MinX, rect.MinY),
                new Point2(rect.MaxX, rect.MinY),
                new Point2(rect.MaxX, rect.MaxY),
                new Point2(rect.MinX, rect.MaxY),
            }))
            .ToList();
    }

    private static List<Rect2> MergeRectangles(IReadOnlyList<Rect2> rectangles)
    {
        const int precision = 9;
        var groupedByX = rectangles
            .GroupBy(rect => (MinX: Math.Round(rect.MinX, precision), MaxX: Math.Round(rect.MaxX, precision)))
            .OrderBy(group => group.Key.MinX)
            .ThenBy(group => group.Key.MaxX);

        var merged = new List<Rect2>();
        foreach (var group in groupedByX)
        {
            var ordered = group
                .OrderBy(rect => rect.MinY)
                .ThenBy(rect => rect.MaxY)
                .ToList();

            if (ordered.Count == 0)
            {
                continue;
            }

            var current = ordered[0];
            for (var i = 1; i < ordered.Count; i++)
            {
                var next = ordered[i];
                if (Math.Abs(next.MinY - current.MaxY) <= AreaEpsilon)
                {
                    current = new Rect2(current.MinX, current.MinY, current.MaxX, Math.Max(current.MaxY, next.MaxY));
                    continue;
                }

                merged.Add(current);
                current = next;
            }

            merged.Add(current);
        }

        return merged;
    }

    private static Polygon2[] BuildCadOverlapPolygons(Polygon2 cadPolygon, Rect2 regularBounds)
    {
        var clipped = ClipPolygonToRect(cadPolygon.Vertices, regularBounds);
        if (clipped.Count < 3)
        {
            return Array.Empty<Polygon2>();
        }

        var normalized = NormalizePolygonVertices(clipped);
        if (normalized.Count < 3)
        {
            return Array.Empty<Polygon2>();
        }

        var overlapPolygon = new Polygon2(normalized);
        return overlapPolygon.Area() > AreaEpsilon
            ? new[] { overlapPolygon }
            : Array.Empty<Polygon2>();
    }

    private static double ComputeExactOccupiedAreaInRegular(
        CadPad targetCad,
        RegularPad regularPad,
        CadPad[] blockerCandidates)
    {
        var overlapPolygons = new List<Polygon2>(1 + blockerCandidates.Length);
        AddCadOverlapPolygons(overlapPolygons, targetCad.Polygon, regularPad.Bounds);
        foreach (var blocker in blockerCandidates)
        {
            AddCadOverlapPolygons(overlapPolygons, blocker.Polygon, regularPad.Bounds);
        }

        if (overlapPolygons.Count == 0)
        {
            return 0.0;
        }

        var union = CadPadUnionService.Union(overlapPolygons);
        return union.OuterPolygons.Sum(static polygon => polygon.Area());
    }

    private static void AddCadOverlapPolygons(
        List<Polygon2> target,
        Polygon2 cadPolygon,
        Rect2 regularBounds)
    {
        target.AddRange(BuildCadOverlapPolygons(cadPolygon, regularBounds));
    }

    private static List<Point2> ClipPolygonToRect(IReadOnlyList<Point2> subject, Rect2 rect)
    {
        var output = subject.ToList();
        if (output.Count == 0)
        {
            return output;
        }

        output = ClipPolygonEdge(output, static (point, r) => point.X >= r.MinX, static (a, b, value) => IntersectVertical(a, b, value), rect.MinX, rect);
        output = ClipPolygonEdge(output, static (point, r) => point.X <= r.MaxX, static (a, b, value) => IntersectVertical(a, b, value), rect.MaxX, rect);
        output = ClipPolygonEdge(output, static (point, r) => point.Y >= r.MinY, static (a, b, value) => IntersectHorizontal(a, b, value), rect.MinY, rect);
        output = ClipPolygonEdge(output, static (point, r) => point.Y <= r.MaxY, static (a, b, value) => IntersectHorizontal(a, b, value), rect.MaxY, rect);
        return output;
    }

    private static List<Point2> ClipPolygonEdge(
        List<Point2> input,
        Func<Point2, Rect2, bool> inside,
        Func<Point2, Point2, double, Point2> intersect,
        double edgeValue,
        Rect2 rect)
    {
        if (input.Count == 0)
        {
            return new List<Point2>();
        }

        var output = new List<Point2>(input.Count);
        var start = input[^1];
        foreach (var end in input)
        {
            var startInside = inside(start, rect);
            var endInside = inside(end, rect);
            if (endInside)
            {
                if (!startInside)
                {
                    output.Add(intersect(start, end, edgeValue));
                }

                output.Add(end);
            }
            else if (startInside)
            {
                output.Add(intersect(start, end, edgeValue));
            }

            start = end;
        }

        return output;
    }

    private static List<Point2> NormalizePolygonVertices(List<Point2> points)
    {
        var normalized = new List<Point2>(points.Count);
        foreach (var point in points)
        {
            if (normalized.Count > 0 && DistanceSquared(normalized[^1], point) <= AreaEpsilon * AreaEpsilon)
            {
                continue;
            }

            normalized.Add(point);
        }

        if (normalized.Count >= 2 && DistanceSquared(normalized[0], normalized[^1]) <= AreaEpsilon * AreaEpsilon)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return normalized;
    }

    private static bool IsAxisAlignedRectangle(Polygon2 polygon)
    {
        if (polygon.Vertices.Length != 4)
        {
            return false;
        }

        var bounds = polygon.Bounds;
        foreach (var point in polygon.Vertices)
        {
            var onHorizontalEdge = Math.Abs(point.Y - bounds.MinY) <= AreaEpsilon || Math.Abs(point.Y - bounds.MaxY) <= AreaEpsilon;
            var onVerticalEdge = Math.Abs(point.X - bounds.MinX) <= AreaEpsilon || Math.Abs(point.X - bounds.MaxX) <= AreaEpsilon;
            if (!onHorizontalEdge || !onVerticalEdge)
            {
                return false;
            }
        }

        return Math.Abs(polygon.Area() - (bounds.Width * bounds.Height)) <= AreaEpsilon;
    }

    private static Point2 IntersectVertical(Point2 start, Point2 end, double x)
    {
        var deltaX = end.X - start.X;
        if (Math.Abs(deltaX) < AreaEpsilon)
        {
            return new Point2(x, start.Y);
        }

        var t = (x - start.X) / deltaX;
        return new Point2(x, start.Y + ((end.Y - start.Y) * t));
    }

    private static Point2 IntersectHorizontal(Point2 start, Point2 end, double y)
    {
        var deltaY = end.Y - start.Y;
        if (Math.Abs(deltaY) < AreaEpsilon)
        {
            return new Point2(start.X, y);
        }

        var t = (y - start.Y) / deltaY;
        return new Point2(start.X + ((end.X - start.X) * t), y);
    }

    private static double DistanceSquared(Point2 a, Point2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
