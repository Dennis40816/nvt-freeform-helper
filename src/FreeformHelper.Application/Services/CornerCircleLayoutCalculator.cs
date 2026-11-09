// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

public sealed record CornerCircleLayoutRequest(Polygon2 ActiveArea, double Radius);

public enum CornerCircleLayoutStatus
{
    Ok,
    InvalidRadius,
    InvalidActiveArea,
    DoesNotFit,
}

public sealed record CornerCircleLayoutResult(
    CornerCircleLayoutStatus Status,
    double Radius,
    Rect2 CenterRectangle,
    IReadOnlyList<Point2> Centers,
    string Message)
{
    public bool Equals(CornerCircleLayoutResult? other) =>
        other is not null && Status == other.Status && Radius.Equals(other.Radius) &&
        CenterRectangle == other.CenterRectangle && Message == other.Message && Centers.SequenceEqual(other.Centers);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        hash.Add(Radius);
        hash.Add(CenterRectangle);
        hash.Add(Message);
        foreach (var center in Centers)
        {
            hash.Add(center);
        }

        return hash.ToHashCode();
    }
}

public static class CornerCircleLayoutCalculator
{
    private const double Tolerance = 1e-9;
    private const int SeedDivisions = 64;
    private const int SeedCount = 8;
    private const int RefinementLevels = 24;

    private readonly record struct Interval(double Min, double Max);
    private readonly record struct Edge(Point2 Start, Point2 End, Point2 Offset);
    private sealed record Section(double Y, List<Interval> Intervals);

    /// <summary>
    /// Calculates in pixels with downward-growing y and without rounding.
    /// A circle is inside when its center is inside the polygon, including its boundary,
    /// and its distance to every polygon segment is at least the radius, allowing 1e-9 tolerance.
    /// The centers are (x0,y0), (x1,y0), (x1,y1), (x0,y1), with x1 &gt; x0 and y1 &gt; y0.
    /// Only the four circles must be inside; the intervening rectangle need not be inside.
    /// Largest means maximum width times height; areas within 1e-9 tie, then smaller
    /// |width - height| wins, then smaller y0, then smaller x0.
    /// Edge contact follows from maximizing area and is not an additional constraint.
    /// Axis-aligned rectangle polygons use an exact inset fast path.
    /// Other polygons use analytic horizontal sections of the polygon minus open edge capsules.
    /// At most 128 seed rows are paired, then eight candidates receive 24 dyadic refinements,
    /// with two local sweeps per level; the final y step is inset height / (64 * 2^24).
    /// This is a deterministic approximate search, not a global error bound for arbitrary polygons.
    /// The rounded-square cases with 0.5-degree polylines are accurate within 0.01 pixel;
    /// section endpoints retain the circle containment constraint to floating-point tolerance.
    /// </summary>
    public static CornerCircleLayoutResult Calculate(CornerCircleLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var radius = request.Radius;
        if (!double.IsFinite(radius) || radius < 0)
        {
            return Failure(CornerCircleLayoutStatus.InvalidRadius, radius, "The radius must be finite and nonnegative.");
        }

        var polygon = request.ActiveArea;
        if (!IsValid(polygon))
        {
            return Failure(CornerCircleLayoutStatus.InvalidActiveArea, radius,
                "The active area must be a finite polygon with positive area.");
        }

        var bounds = polygon.Bounds.Inflate(-radius, -radius);
        var rectangle = bounds.IsEmpty ? default : IsRectangle(polygon) ? bounds : Search(polygon, radius, bounds);
        if (rectangle.IsEmpty)
        {
            return Failure(CornerCircleLayoutStatus.DoesNotFit, radius,
                "The radius is too large for the active area to fit four corner circles.");
        }

        Point2[] centers =
        [
            new(rectangle.MinX, rectangle.MinY), new(rectangle.MaxX, rectangle.MinY),
            new(rectangle.MaxX, rectangle.MaxY), new(rectangle.MinX, rectangle.MaxY),
        ];
        return new(CornerCircleLayoutStatus.Ok, radius, rectangle, Array.AsReadOnly(centers), string.Empty);
    }

    private static CornerCircleLayoutResult Failure(CornerCircleLayoutStatus status, double radius, string message) =>
        new(status, radius, default, Array.Empty<Point2>(), message);

    private static bool IsValid(Polygon2? polygon) =>
        polygon is not null && polygon.Vertices.Length >= 3 &&
        polygon.Vertices.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)) &&
        double.IsFinite(polygon.SignedArea()) && polygon.SignedArea() != 0;

    private static bool IsRectangle(Polygon2 polygon)
    {
        var bounds = polygon.Bounds;
        for (var i = 0; i < polygon.Vertices.Length; i++)
        {
            var a = polygon.Vertices[i];
            var b = polygon.Vertices[(i + 1) % polygon.Vertices.Length];
            var horizontal = a.Y == b.Y && (a.Y == bounds.MinY || a.Y == bounds.MaxY);
            var vertical = a.X == b.X && (a.X == bounds.MinX || a.X == bounds.MaxX);
            if (!horizontal && !vertical)
            {
                return false;
            }
        }

        return true;
    }

    private static Rect2 Search(Polygon2 polygon, double radius, Rect2 bounds)
    {
        var edges = CreateEdges(polygon, radius);
        var sections = new Dictionary<double, Section>();
        var rows = SeedRows(polygon, radius, bounds).Select(y => GetSection(edges, radius, y, sections)).ToArray();
        var seeds = new List<Rect2>();
        for (var i = 0; i < rows.Length; i++)
        {
            for (var j = i + 1; j < rows.Length; j++)
            {
                KeepSeed(seeds, Between(rows[i], rows[j]));
            }
        }

        var best = default(Rect2);
        foreach (var seed in seeds)
        {
            var candidate = Refine(seed, edges, radius, bounds, sections);
            if (IsBetter(candidate, best))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static Edge[] CreateEdges(Polygon2 polygon, double radius)
    {
        var edges = new Edge[polygon.Vertices.Length];
        for (var i = 0; i < edges.Length; i++)
        {
            var a = polygon.Vertices[i];
            var b = polygon.Vertices[(i + 1) % edges.Length];
            var delta = b - a;
            var length = Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            var offset = length > 0 ? new Point2(-delta.Y * radius / length, delta.X * radius / length) : default;
            edges[i] = new(a, b, offset);
        }

        return edges;
    }

    private static SortedSet<double> SeedRows(Polygon2 polygon, double radius, Rect2 bounds)
    {
        var events = new SortedSet<double>();
        foreach (var vertex in polygon.Vertices)
        {
            events.Add(Math.Clamp(vertex.Y - radius, bounds.MinY, bounds.MaxY));
            events.Add(Math.Clamp(vertex.Y, bounds.MinY, bounds.MaxY));
            events.Add(Math.Clamp(vertex.Y + radius, bounds.MinY, bounds.MaxY));
        }

        var coordinates = events.ToArray();
        var rows = new SortedSet<double>();
        for (var i = 0; i <= SeedDivisions; i++)
        {
            rows.Add(i == SeedDivisions ? bounds.MaxY : bounds.MinY + bounds.Height * i / SeedDivisions);
            rows.Add(coordinates[(coordinates.Length - 1) * i / SeedDivisions]);
        }

        return rows;
    }

    private static void KeepSeed(List<Rect2> seeds, Rect2 candidate)
    {
        if (candidate.IsEmpty || seeds.Contains(candidate))
        {
            return;
        }

        var index = seeds.FindIndex(seed => IsBetter(candidate, seed));
        seeds.Insert(index < 0 ? seeds.Count : index, candidate);
        if (seeds.Count > SeedCount)
        {
            seeds.RemoveAt(SeedCount);
        }
    }

    private static Rect2 Refine(Rect2 seed, Edge[] edges, double radius, Rect2 bounds, Dictionary<double, Section> sections)
    {
        var best = seed;
        var step = bounds.Height / (2 * SeedDivisions);
        for (var level = 0; level < RefinementLevels; level++, step *= 0.5)
        {
            for (var sweep = 0; sweep < 2; sweep++)
            {
                var previous = best;
                var top = NeighborSections(previous.MinY, step, edges, radius, bounds, sections);
                var bottom = NeighborSections(previous.MaxY, step, edges, radius, bounds, sections);
                foreach (var a in top)
                {
                    foreach (var b in bottom)
                    {
                        var candidate = Between(a, b);
                        if (IsBetter(candidate, best))
                        {
                            best = candidate;
                        }
                    }
                }
            }
        }

        return best;
    }

    private static Section[] NeighborSections(
        double y, double step, Edge[] edges, double radius, Rect2 bounds, Dictionary<double, Section> sections) =>
        [GetSection(edges, radius, Math.Clamp(y - step, bounds.MinY, bounds.MaxY), sections),
         GetSection(edges, radius, y, sections),
         GetSection(edges, radius, Math.Clamp(y + step, bounds.MinY, bounds.MaxY), sections)];

    private static Rect2 Between(Section top, Section bottom)
    {
        if (bottom.Y <= top.Y)
        {
            return default;
        }

        var left = double.PositiveInfinity;
        var right = double.NegativeInfinity;
        var i = 0;
        var j = 0;
        while (i < top.Intervals.Count && j < bottom.Intervals.Count)
        {
            var a = top.Intervals[i];
            var b = bottom.Intervals[j];
            if (Math.Max(a.Min, b.Min) <= Math.Min(a.Max, b.Max))
            {
                left = Math.Min(left, Math.Max(a.Min, b.Min));
                right = Math.Max(right, Math.Min(a.Max, b.Max));
            }

            if (a.Max < b.Max)
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return right > left ? new(left, top.Y, right, bottom.Y) : default;
    }

    private static bool IsBetter(Rect2 candidate, Rect2 best)
    {
        if (candidate.IsEmpty)
        {
            return false;
        }

        var difference = candidate.Width * candidate.Height - best.Width * best.Height;
        if (best.IsEmpty || Math.Abs(difference) > Tolerance)
        {
            return best.IsEmpty || difference > 0;
        }

        var shape = Math.Abs(candidate.Width - candidate.Height).CompareTo(Math.Abs(best.Width - best.Height));
        return shape < 0 || (shape == 0 && (candidate.MinY < best.MinY ||
            (candidate.MinY == best.MinY && candidate.MinX < best.MinX)));
    }

    private static Section GetSection(Edge[] edges, double radius, double y, Dictionary<double, Section> sections)
    {
        if (sections.TryGetValue(y, out var section))
        {
            return section;
        }

        var crossings = new List<double>();
        var inside = new List<Interval>();
        var forbidden = new List<Interval>();
        foreach (var edge in edges)
        {
            var a = edge.Start;
            var b = edge.End;
            if ((a.Y > y) != (b.Y > y))
            {
                crossings.Add(a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y));
            }

            if (a.Y == y && b.Y == y)
            {
                inside.Add(new(Math.Min(a.X, b.X), Math.Max(a.X, b.X)));
            }

            if (a.Y == y)
            {
                inside.Add(new(a.X, a.X));
            }

            AddCapsule(forbidden, edge, radius, y);
        }

        crossings.Sort();
        for (var i = 0; i + 1 < crossings.Count; i += 2)
        {
            inside.Add(new(crossings[i], crossings[i + 1]));
        }

        section = new(y, Subtract(Merge(inside, true), Merge(forbidden, false)));
        sections.Add(y, section);
        return section;
    }

    private static void AddCapsule(List<Interval> forbidden, Edge edge, double radius, double y)
    {
        if (radius == 0 || y <= Math.Min(edge.Start.Y, edge.End.Y) - radius ||
            y >= Math.Max(edge.Start.Y, edge.End.Y) + radius)
        {
            return;
        }

        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;
        AddDisk(edge.Start, radius, y, ref min, ref max);
        AddDisk(edge.End, radius, y, ref min, ref max);
        AddIntersection(edge.Start + edge.Offset, edge.End + edge.Offset, y, ref min, ref max);
        AddIntersection(edge.Start - edge.Offset, edge.End - edge.Offset, y, ref min, ref max);
        if (max > min)
        {
            forbidden.Add(new(min, max));
        }
    }

    private static void AddDisk(Point2 center, double radius, double y, ref double min, ref double max)
    {
        var delta = y - center.Y;
        if (Math.Abs(delta) <= radius)
        {
            var width = Math.Sqrt(Math.Max(0, (radius - delta) * (radius + delta)));
            min = Math.Min(min, center.X - width);
            max = Math.Max(max, center.X + width);
        }
    }

    private static void AddIntersection(Point2 a, Point2 b, double y, ref double min, ref double max)
    {
        if (a.Y != b.Y && y >= Math.Min(a.Y, b.Y) && y <= Math.Max(a.Y, b.Y))
        {
            var x = a.X + (b.X - a.X) * (y - a.Y) / (b.Y - a.Y);
            min = Math.Min(min, x);
            max = Math.Max(max, x);
        }
    }

    private static List<Interval> Merge(List<Interval> intervals, bool joinTouching)
    {
        intervals.Sort((a, b) => a.Min.CompareTo(b.Min));
        var result = new List<Interval>();
        foreach (var interval in intervals)
        {
            if (result.Count > 0 && (interval.Min < result[^1].Max || (joinTouching && interval.Min == result[^1].Max)))
            {
                result[^1] = new(result[^1].Min, Math.Max(result[^1].Max, interval.Max));
            }
            else
            {
                result.Add(interval);
            }
        }

        return result;
    }

    private static List<Interval> Subtract(List<Interval> inside, List<Interval> forbidden)
    {
        var result = new List<Interval>();
        var first = 0;
        foreach (var interval in inside)
        {
            var cursor = interval.Min;
            while (first < forbidden.Count && forbidden[first].Max <= cursor)
            {
                first++;
            }

            for (var i = first; i < forbidden.Count && forbidden[i].Min < interval.Max; i++)
            {
                var cut = forbidden[i];
                if (cut.Min >= cursor)
                {
                    result.Add(new(cursor, cut.Min));
                }

                cursor = Math.Max(cursor, cut.Max);
                if (cursor > interval.Max)
                {
                    break;
                }
            }

            if (cursor <= interval.Max)
            {
                result.Add(new(cursor, interval.Max));
            }
        }

        return result;
    }
}
