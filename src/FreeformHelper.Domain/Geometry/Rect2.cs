namespace FreeformHelper.Domain.Geometry;

/// <summary>
/// Represents an immutable axis-aligned rectangle (bounding box) in double-precision world coordinates.
/// </summary>
/// <param name="MinX">The minimum X-coordinate of the rectangle.</param>
/// <param name="MinY">The minimum Y-coordinate of the rectangle.</param>
/// <param name="MaxX">The maximum X-coordinate of the rectangle.</param>
/// <param name="MaxY">The maximum Y-coordinate of the rectangle.</param>
public readonly record struct Rect2(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>
    /// Gets the width of the rectangle.
    /// </summary>
    public double Width => MaxX - MinX;

    /// <summary>
    /// Gets the height of the rectangle.
    /// </summary>
    public double Height => MaxY - MinY;

    /// <summary>
    /// Gets a value indicating whether the rectangle is empty (has zero or negative width or height).
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Creates a new <see cref="Rect2"/> that encloses all the given points.
    /// </summary>
    /// <param name="points">A collection of <see cref="Point2"/> objects.</param>
    /// <returns>A new <see cref="Rect2"/> covering all points, or an empty rect if no points are provided.</returns>
    public static Rect2 FromPoints(IEnumerable<Point2> points)
    {
        using var e = points.GetEnumerator();
        if (!e.MoveNext())
        {
            // If no points are provided, return an empty rectangle at the origin.
            return new Rect2(0, 0, 0, 0);
        }

        var minX = e.Current.X;
        var minY = e.Current.Y;
        var maxX = e.Current.X;
        var maxY = e.Current.Y;
        while (e.MoveNext())
        {
            var p = e.Current;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        return new Rect2(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Creates a new <see cref="Rect2"/> by inflating (expanding) the current rectangle
    /// by specified amounts in the X and Y directions.
    /// </summary>
    /// <param name="dx">The amount to inflate (or deflate if negative) horizontally from both sides.</param>
    /// <param name="dy">The amount to inflate (or deflate if negative) vertically from both sides.</param>
    /// <returns>A new, inflated <see cref="Rect2"/>.</returns>
    public Rect2 Inflate(double dx, double dy) => new(MinX - dx, MinY - dy, MaxX + dx, MaxY + dy);

    /// <summary>
    /// Determines if this rectangle contains the specified point.
    /// Points on the boundary are considered contained.
    /// </summary>
    /// <param name="p">The <see cref="Point2"/> to check.</param>
    /// <returns><c>true</c> if the point is within or on the boundary of the rectangle; otherwise, <c>false</c>.</returns>
    public bool Contains(Point2 p) => p.X >= MinX && p.X <= MaxX && p.Y >= MinY && p.Y <= MaxY;

    /// <summary>
    /// Determines if this rectangle intersects with another rectangle.
    /// </summary>
    /// <param name="other">The other <see cref="Rect2"/> to check for intersection.</param>
    /// <returns><c>true</c> if the rectangles intersect; otherwise, <c>false</c>.</returns>
    public bool Intersects(Rect2 other)
    {
        if (IsEmpty || other.IsEmpty) return false;
        // Rectangles intersect if they are not separated along either axis.
        return !(other.MinX > MaxX || other.MaxX < MinX || other.MinY > MaxY || other.MaxY < MinY);
    }

    /// <summary>
    /// Creates a new <see cref="Rect2"/> that is the smallest rectangle containing both input rectangles.
    /// </summary>
    /// <param name="a">The first <see cref="Rect2"/>.</param>
    /// <param name="b">The second <see cref="Rect2"/>.</param>
    /// <returns>A new <see cref="Rect2"/> representing the union of the two input rectangles.</returns>
    public static Rect2 Union(Rect2 a, Rect2 b)
    {
        if (a.IsEmpty) return b; // If 'a' is empty, union is 'b'.
        if (b.IsEmpty) return a; // If 'b' is empty, union is 'a'.

        return new Rect2(
            Math.Min(a.MinX, b.MinX),
            Math.Min(a.MinY, b.MinY),
            Math.Max(a.MaxX, b.MaxX),
            Math.Max(a.MaxY, b.MaxY));
    }
}
