namespace FreeformHelper.Domain.Geometry;

/// <summary>
/// Represents a 2D point in double-precision world coordinates.
/// This is an immutable record struct, providing value-based equality.
/// </summary>
/// <param name="X">The X-coordinate of the point.</param>
/// <param name="Y">The Y-coordinate of the point.</param>
public readonly record struct Point2(double X, double Y)
{
    /// <summary>
    /// Adds two <see cref="Point2"/> vectors together.
    /// </summary>
    /// <param name="a">The first point/vector.</param>
    /// <param name="b">The second point/vector.</param>
    /// <returns>A new <see cref="Point2"/> representing the sum of the two input points/vectors.</returns>
    public static Point2 operator +(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>
    /// Subtracts the second <see cref="Point2"/> vector from the first.
    /// </summary>
    /// <param name="a">The minuend point/vector.</param>
    /// <param name="b">The subtrahend point/vector.</param>
    /// <returns>A new <see cref="Point2"/> representing the difference between the two input points/vectors.</returns>
    public static Point2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>
    /// Multiplies a <see cref="Point2"/> vector by a scalar value.
    /// </summary>
    /// <param name="a">The point/vector to multiply.</param>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>A new <see cref="Point2"/> with its components scaled by the scalar value.</returns>
    public static Point2 operator *(Point2 a, double scalar) => new(a.X * scalar, a.Y * scalar);

    /// <summary>
    /// Divides a <see cref="Point2"/> vector by a scalar value.
    /// </summary>
    /// <param name="a">The point/vector to divide.</param>
    /// <param name="scalar">The scalar value.</param>
    /// <returns>A new <see cref="Point2"/> with its components divided by the scalar value.</returns>
    public static Point2 operator /(Point2 a, double scalar) => new(a.X / scalar, a.Y / scalar);

    /// <summary>
    /// Calculates the Euclidean distance from this point to another point.
    /// </summary>
    /// <param name="other">The other point.</param>
    /// <returns>The distance between the two points.</returns>
    public double DistanceTo(Point2 other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
