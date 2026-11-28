using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public sealed partial class DxfOverlapAnalyzer
{
    /// <summary>
    /// Checks if two polygons have a "proper" overlap, ignoring simple edge touches.
    /// </summary>
    private static bool PolygonsOverlap(Polygon2 a, Polygon2 b)
    {
        if (BoundsOverlapArea(a.Bounds, b.Bounds) <= OverlapAreaEpsilon)
        {
            return false;
        }

        // Check if any vertex of one polygon is strictly inside the other
        foreach (var vertex in a.Vertices)
        {
            if (IsPointStrictlyInside(b, vertex))
            {
                return true;
            }
        }

        foreach (var vertex in b.Vertices)
        {
            if (IsPointStrictlyInside(a, vertex))
            {
                return true;
            }
        }

        // A centroid of a concave pad can lie outside the pad, so it only counts when it is inside both pads.
        if (IsCentroidInsideBoth(a, b) || IsCentroidInsideBoth(b, a))
        {
            return true;
        }

        // Check for proper edge intersections (edges cross each other, not just touch at a point).
        if (EdgesProperlyIntersect(a, b))
        {
            return true;
        }

        // Pads with collinear edges and vertices only on the other pad's edges (for example staggered
        // rectangles of the same height) are caught by sampling the cells between the vertex coordinates.
        return HasInteriorSampleInsideBoth(a, b);
    }

    private static bool IsCentroidInsideBoth(Polygon2 owner, Polygon2 other)
    {
        var centroid = owner.Centroid();
        return IsPointStrictlyInside(owner, centroid) && IsPointStrictlyInside(other, centroid);
    }

    /// <summary>
    /// Samples the center of every grid cell formed by the vertex coordinates inside the shared bounds.
    /// A sample strictly inside both polygons proves that the overlap has a positive area.
    /// </summary>
    private static bool HasInteriorSampleInsideBoth(Polygon2 a, Polygon2 b)
    {
        var xCuts = CollectCuts(a, b, a.Bounds.MinX, a.Bounds.MaxX, b.Bounds.MinX, b.Bounds.MaxX, useX: true);
        var yCuts = CollectCuts(a, b, a.Bounds.MinY, a.Bounds.MaxY, b.Bounds.MinY, b.Bounds.MaxY, useX: false);
        if (xCuts.Count > MaxInteriorSampleCuts || yCuts.Count > MaxInteriorSampleCuts)
        {
            return false;
        }

        for (var i = 0; i + 1 < xCuts.Count; i++)
        {
            for (var j = 0; j + 1 < yCuts.Count; j++)
            {
                var center = new Point2((xCuts[i] + xCuts[i + 1]) / 2.0, (yCuts[j] + yCuts[j + 1]) / 2.0);
                if (IsPointStrictlyInside(a, center) && IsPointStrictlyInside(b, center))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<double> CollectCuts(
        Polygon2 a, Polygon2 b, double aMin, double aMax, double bMin, double bMax, bool useX)
    {
        var min = Math.Max(aMin, bMin);
        var max = Math.Min(aMax, bMax);
        var values = new List<double> { min, max };
        AddCutsInside(values, a, min, max, useX);
        AddCutsInside(values, b, min, max, useX);
        values.Sort();

        var cuts = new List<double>(values.Count);
        foreach (var value in values)
        {
            if (cuts.Count == 0 || value - cuts[^1] > Epsilon)
            {
                cuts.Add(value);
            }
        }

        return cuts;
    }

    private static void AddCutsInside(List<double> values, Polygon2 polygon, double min, double max, bool useX)
    {
        foreach (var vertex in polygon.Vertices)
        {
            var value = useX ? vertex.X : vertex.Y;
            if (value > min && value < max)
            {
                values.Add(value);
            }
        }
    }

    private static bool IsPointStrictlyInside(Polygon2 poly, Point2 point)
    {
        return poly.Contains(point) && !IsPointOnEdge(poly, point);
    }

    private static bool IsPointOnEdge(Polygon2 poly, Point2 point)
    {
        var vertices = poly.Vertices;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Length];
            if (IsPointOnSegment(start, end, point))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointOnSegment(Point2 a, Point2 b, Point2 p)
    {
        var ab = b - a;
        var ap = p - a;
        // Check for collinearity.
        if (Math.Abs(Cross(ab, ap)) > Epsilon)
        {
            return false;
        }

        // Check if point p is between a and b.
        var dot = (p.X - a.X) * (p.X - b.X) + (p.Y - a.Y) * (p.Y - b.Y);
        return dot <= Epsilon;
    }

    /// <summary>
    /// Checks if any edges of two polygons properly intersect (cross).
    /// </summary>
    private static bool EdgesProperlyIntersect(Polygon2 a, Polygon2 b)
    {
        var aVertices = a.Vertices;
        var bVertices = b.Vertices;

        for (var i = 0; i < aVertices.Length; i++)
        {
            var a1 = aVertices[i];
            var a2 = aVertices[(i + 1) % aVertices.Length];
            for (var j = 0; j < bVertices.Length; j++)
            {
                var b1 = bVertices[j];
                var b2 = bVertices[(j + 1) % bVertices.Length];
                if (SegmentsProperlyIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Uses orientation tests to determine if two line segments properly intersect.
    /// It returns false for collinear or touching segments.
    /// </summary>
    private static bool SegmentsProperlyIntersect(Point2 a1, Point2 a2, Point2 b1, Point2 b2)
    {
        var a = a2 - a1;
        var b = b2 - b1;

        var o1 = Cross(a, b1 - a1);
        var o2 = Cross(a, b2 - a1);
        var o3 = Cross(b, a1 - b1);
        var o4 = Cross(b, a2 - b1);

        // If any orientation is zero, the segments are collinear or touching, which is not a proper intersection.
        if (Math.Abs(o1) < Epsilon || Math.Abs(o2) < Epsilon || Math.Abs(o3) < Epsilon || Math.Abs(o4) < Epsilon)
        {
            return false;
        }

        // A proper intersection occurs if the endpoints of each segment are on opposite sides of the other segment.
        return (o1 * o2 < 0) && (o3 * o4 < 0);
    }

    private static double Cross(Point2 a, Point2 b) => (a.X * b.Y) - (a.Y * b.X);

    private static double BoundsOverlapArea(Rect2 a, Rect2 b)
    {
        var minX = Math.Max(a.MinX, b.MinX);
        var maxX = Math.Min(a.MaxX, b.MaxX);
        var minY = Math.Max(a.MinY, b.MinY);
        var maxY = Math.Min(a.MaxY, b.MaxY);
        var width = maxX - minX;
        var height = maxY - minY;
        return width <= 0 || height <= 0 ? 0 : width * height;
    }
}
