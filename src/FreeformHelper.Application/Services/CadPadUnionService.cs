using System.Collections.ObjectModel;
using Clipper2Lib;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public sealed record CadPadUnionResult(
    IReadOnlyList<Polygon2> OuterPolygons,
    int InputPolygonCount,
    int OutputPathCount,
    int HolePathCount);

public sealed class CadPadUnionService
{
    private const int UnionPrecisionDigits = 6;
    private const double MinPolygonArea = 1e-9;
    private const double MinDuplicatePointDistanceSquared = 1e-18;

    public static CadPadUnionResult Union(IEnumerable<Polygon2> polygons)
    {
        var inputPaths = new PathsD();
        var inputCount = 0;
        foreach (var polygon in polygons)
        {
            inputCount++;
            if (polygon.Vertices.Length < 3)
            {
                continue;
            }

            var path = new PathD(polygon.Vertices.Length);
            foreach (var vertex in polygon.Vertices)
            {
                path.Add(new PointD(vertex.X, vertex.Y));
            }

            if (path.Count >= 3)
            {
                // All input polygons here represent solid filled regions. Normalize
                // orientation up front so Clipper won't reinterpret a valid CAD body
                // as a hole just because the DXF vertex order is reversed.
                if (!Clipper.IsPositive(path))
                {
                    path.Reverse();
                }

                inputPaths.Add(path);
            }
        }

        if (inputPaths.Count == 0)
        {
            return new CadPadUnionResult(Array.Empty<Polygon2>(), inputCount, 0, 0);
        }

        var unionTree = new PolyTreeD();
        Clipper.BooleanOp(
            ClipType.Union,
            inputPaths,
            new PathsD(),
            unionTree,
            FillRule.NonZero,
            UnionPrecisionDigits);
        if (unionTree.Count == 0)
        {
            return new CadPadUnionResult(Array.Empty<Polygon2>(), inputCount, 0, 0);
        }

        var selectedPaths = new List<PathD>();
        var validPathCount = 0;
        var holeCount = 0;
        CollectOuterPaths(unionTree, selectedPaths, ref validPathCount, ref holeCount);
        if (selectedPaths.Count == 0)
        {
            return new CadPadUnionResult(Array.Empty<Polygon2>(), inputCount, validPathCount, holeCount);
        }

        var resultPolygons = new List<Polygon2>(selectedPaths.Count);
        foreach (var path in selectedPaths)
        {
            var points = NormalizePath(path);
            if (points.Count < 3)
            {
                continue;
            }

            var polygon = new Polygon2(points);
            if (polygon.Area() <= MinPolygonArea)
            {
                continue;
            }

            resultPolygons.Add(polygon);
        }

        return new CadPadUnionResult(
            new ReadOnlyCollection<Polygon2>(resultPolygons),
            inputCount,
            validPathCount,
            holeCount);
    }

    private static void CollectOuterPaths(
        PolyTreeD unionTree,
        List<PathD> selectedPaths,
        ref int validPathCount,
        ref int holeCount)
    {
        foreach (PolyPathD child in unionTree)
        {
            CollectOuterPaths(child, selectedPaths, ref validPathCount, ref holeCount);
        }
    }

    private static void CollectOuterPaths(
        PolyPathD node,
        List<PathD> selectedPaths,
        ref int validPathCount,
        ref int holeCount)
    {
        var polygon = node.Polygon;
        if (polygon is not null && polygon.Count >= 3)
        {
            validPathCount++;
            if (node.IsHole)
            {
                holeCount++;
            }
            else
            {
                selectedPaths.Add(polygon);
            }
        }

        foreach (PolyPathD child in node)
        {
            CollectOuterPaths(child, selectedPaths, ref validPathCount, ref holeCount);
        }
    }

    private static List<Point2> NormalizePath(PathD path)
    {
        var points = new List<Point2>(path.Count);
        foreach (var point in path)
        {
            var current = new Point2(point.x, point.y);
            if (points.Count > 0 && DistanceSquared(points[^1], current) <= MinDuplicatePointDistanceSquared)
            {
                continue;
            }

            points.Add(current);
        }

        if (points.Count >= 2 && DistanceSquared(points[0], points[^1]) <= MinDuplicatePointDistanceSquared)
        {
            points.RemoveAt(points.Count - 1);
        }

        return points;
    }

    private static double DistanceSquared(Point2 a, Point2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
