using System.Collections.Immutable;

namespace FreeformHelper.Domain.Geometry;

/// <summary>
/// Represents a simple polygon defined by an ordered list of <see cref="Point2"/> vertices.
/// It is assumed that the polygon is non-self-intersecting for the geometric operations to be valid.
/// </summary>
public sealed class Polygon2
{
    /// <summary>
    /// Gets the immutable array of vertices that define the polygon.
    /// The vertices are stored in order, either clockwise or counterclockwise.
    /// </summary>
    public ImmutableArray<Point2> Vertices { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Polygon2"/> class with the specified vertices.
    /// </summary>
    /// <param name="vertices">A collection of <see cref="Point2"/> objects defining the polygon's vertices.</param>
    /// <exception cref="ArgumentException">Thrown if the polygon has fewer than 3 vertices.</exception>
    public Polygon2(IEnumerable<Point2> vertices)
    {
        Vertices = vertices.ToImmutableArray();
        if (Vertices.Length < 3)
        {
            throw new ArgumentException("A polygon must have at least 3 vertices.");
        }
    }

    /// <summary>
    /// Gets the bounding rectangle of the polygon.
    /// </summary>
    public Rect2 Bounds => Rect2.FromPoints(Vertices);

    /// <summary>
    /// Calculates the signed area of the polygon using the Shoelace formula.
    /// The sign indicates the orientation of the polygon's vertices (e.g., positive for counterclockwise, negative for clockwise).
    /// </summary>
    /// <returns>The signed area of the polygon.</returns>
    public double SignedArea()
    {
        // The Shoelace formula (or surveyor's formula) is used to calculate the area of a simple polygon
        // given the Cartesian coordinates of its vertices.
        double sum = 0;
        for (int i = 0; i < Vertices.Length; i++)
        {
            var a = Vertices[i];
            var b = Vertices[(i + 1) % Vertices.Length]; // Connects the last vertex to the first
            sum += a.X * b.Y - b.X * a.Y;
        }
        return 0.5 * sum;
    }

    /// <summary>
    /// Calculates the absolute area of the polygon.
    /// </summary>
    /// <returns>The non-negative area of the polygon.</returns>
    public double Area() => Math.Abs(SignedArea());

    /// <summary>
    /// Calculates the centroid (geometric center) of the polygon.
    /// </summary>
    /// <returns>A <see cref="Point2"/> representing the centroid of the polygon.</returns>
    public Point2 Centroid()
    {
        // The centroid formula for a polygon is derived from the Shoelace formula.
        double cx = 0;
        double cy = 0;
        double factor;
        double area6 = 0; // 2 * A from Shoelace, but multiplied by 3 later for convenience

        for (int i = 0; i < Vertices.Length; i++)
        {
            var a = Vertices[i];
            var b = Vertices[(i + 1) % Vertices.Length];
            factor = a.X * b.Y - b.X * a.Y;
            cx += (a.X + b.X) * factor;
            cy += (a.Y + b.Y) * factor;
            area6 += factor; // This sum is 2 * A
        }

        // Handle degenerate polygons (e.g., area is zero or near zero)
        if (Math.Abs(area6) < 1e-12)
        {
            // For degenerate polygons, fall back to the center of the bounding box.
            var b = Bounds;
            return new Point2((b.MinX + b.MaxX) * 0.5, (b.MinY + b.MaxY) * 0.5);
        }

        area6 *= 3.0; // Correct denominator is 6 * Area
        return new Point2(cx / area6, cy / area6);
    }

    /// <summary>
    /// Determines if a given point is strictly inside the polygon using the ray casting algorithm (also known as the "winding number" or "even-odd" rule).
    /// Points on the boundary are generally not considered "inside".
    /// </summary>
    /// <param name="p">The <see cref="Point2"/> to check.</param>
    /// <returns><c>true</c> if the point is inside the polygon; otherwise, <c>false</c>.</returns>
    public bool Contains(Point2 p)
    {
        // This implementation uses the ray casting algorithm.
        // It counts the number of times a ray from the point crosses the polygon's edges.
        // If the count is odd, the point is inside; if even, it's outside.
        bool inside = false;
        for (int i = 0, j = Vertices.Length - 1; i < Vertices.Length; j = i++)
        {
            var pi = Vertices[i];
            var pj = Vertices[j];

            // Check if the ray from p crosses the edge (pi, pj).
            // A crossing occurs if the edge spans p's Y-coordinate AND
            // p's X-coordinate is less than the X-coordinate of the intersection point.
            var intersect = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                            (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X);
            if (intersect)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    /// <summary>
    /// Calculates the area of intersection between this polygon and a given rectangle.
    /// Uses the Sutherland-Hodgman algorithm for polygon clipping.
    /// </summary>
    /// <param name="poly">The polygon to clip.</param>
    /// <param name="rect">The clipping rectangle.</param>
    /// <returns>The area of the intersecting region.</returns>
    public static double IntersectionAreaWithRect(Polygon2 poly, Rect2 rect)
    {
        // Clip the polygon against the rectangle.
        var clipped = ClipAgainstRect(poly.Vertices, rect);
        // If the clipped polygon has fewer than 3 vertices, it means there's no actual area.
        if (clipped.Count < 3) return 0;
        // Calculate the area of the resulting clipped polygon.
        return Math.Abs(SignedArea(clipped));
    }

    /// <summary>
    /// Helper method to calculate the signed area of a list of points (representing a polygon).
    /// This is a private overload of <see cref="SignedArea()"/> for use with dynamically generated point lists.
    /// </summary>
    private static double SignedArea(List<Point2> pts)
    {
        double sum = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            sum += a.X * b.Y - b.X * a.Y;
        }
        return 0.5 * sum;
    }

    /// <summary>
    /// Clips a subject polygon (defined by its vertices) against an axis-aligned rectangle
    /// using the Sutherland-Hodgman polygon clipping algorithm.
    /// </summary>
    /// <param name="subject">The vertices of the polygon to clip.</param>
    /// <param name="rect">The clipping rectangle.</param>
    /// <returns>A new list of <see cref="Point2"/> representing the vertices of the clipped polygon.</returns>
    private static List<Point2> ClipAgainstRect(ImmutableArray<Point2> subject, Rect2 rect)
    {
        // The Sutherland-Hodgman algorithm clips a polygon by processing its vertices
        // against each edge of the clip polygon (in this case, the rectangle).
        // It produces a new set of vertices that form the clipped polygon.
        var output = subject.ToList();

        // Clip against the left edge (X >= MinX)
        output = Clip(output, p => p.X >= rect.MinX, (s, e) => IntersectVertical(s, e, rect.MinX));
        // Clip against the right edge (X <= MaxX)
        output = Clip(output, p => p.X <= rect.MaxX, (s, e) => IntersectVertical(s, e, rect.MaxX));
        // Clip against the bottom edge (Y >= MinY)
        output = Clip(output, p => p.Y >= rect.MinY, (s, e) => IntersectHorizontal(s, e, rect.MinY));
        // Clip against the top edge (Y <= MaxY)
        output = Clip(output, p => p.Y <= rect.MaxY, (s, e) => IntersectHorizontal(s, e, rect.MaxY));

        return output;
    }

    /// <summary>
    /// Performs a single clipping pass of the Sutherland-Hodgman algorithm against one clip edge.
    /// </summary>
    /// <param name="input">The vertices of the polygon to be clipped (from the previous pass).</param>
    /// <param name="inside">A predicate function that determines if a point is "inside" the current clip edge.</param>
    /// <param name="intersect">A function that calculates the intersection point of an edge segment with the current clip edge.</param>
    /// <returns>A new list of <see cref="Point2"/> representing the vertices after clipping against one edge.</returns>
    private static List<Point2> Clip(
        List<Point2> input,
        Func<Point2, bool> inside,
        Func<Point2, Point2, Point2> intersect)
    {
        if (input.Count == 0) return input; // Nothing to clip

        var output = new List<Point2>(input.Count);
        var s = input[^1]; // Start with the last vertex to close the loop

        foreach (var e in input) // 'e' is the current vertex, 's' is the previous
        {
            var sInside = inside(s);
            var eInside = inside(e);

            if (eInside) // Current vertex is inside
            {
                if (!sInside) // Previous vertex was outside, so an intersection point is generated
                {
                    output.Add(intersect(s, e));
                }
                output.Add(e); // Add the current vertex
            }
            else if (sInside) // Current vertex is outside, but previous was inside, so an intersection point is generated
            {
                output.Add(intersect(s, e));
            }
            // If both are outside, nothing is added.

            s = e; // Move to the next segment
        }

        return output;
    }

    /// <summary>
    /// Calculates the intersection point of a line segment (s, e) with a vertical line at x.
    /// </summary>
    /// <param name="s">The start point of the segment.</param>
    /// <param name="e">The end point of the segment.</param>
    /// <param name="x">The X-coordinate of the vertical line.</param>
    /// <returns>The <see cref="Point2"/> where the segment intersects the vertical line.</returns>
    private static Point2 IntersectVertical(Point2 s, Point2 e, double x)
    {
        // Avoid division by zero if the segment is vertical.
        var dx = e.X - s.X;
        if (Math.Abs(dx) < 1e-12)
        {
            return new Point2(x, s.Y); // If vertical, any Y is valid on line x
        }
        // Calculate parameter t for line segment (s, e) where x = s.X + t * (e.X - s.X)
        var t = (x - s.X) / dx;
        return new Point2(x, s.Y + (e.Y - s.Y) * t);
    }

    /// <summary>
    /// Calculates the intersection point of a line segment (s, e) with a horizontal line at y.
    /// </summary>
    /// <param name="s">The start point of the segment.</param>
    /// <param name="e">The end point of the segment.</param>
    /// <param name="y">The Y-coordinate of the horizontal line.</param>
    /// <returns>The <see cref="Point2"/> where the segment intersects the horizontal line.</returns>
    private static Point2 IntersectHorizontal(Point2 s, Point2 e, double y)
    {
        // Avoid division by zero if the segment is horizontal.
        var dy = e.Y - s.Y;
        if (Math.Abs(dy) < 1e-12)
        {
            return new Point2(s.X, y); // If horizontal, any X is valid on line y
        }
        // Calculate parameter t for line segment (s, e) where y = s.Y + t * (e.Y - s.Y)
        var t = (y - s.Y) / dy;
        return new Point2(s.X + (e.X - s.X) * t, y);
    }
}
