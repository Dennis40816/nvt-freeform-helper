using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds canonical geometry signatures for CAD polygons.
/// The signature is invariant to starting vertex and winding direction.
/// </summary>
public static class CadPadGeometrySignature
{
    public static string Build(Polygon2 polygon, int decimals = 6, double epsilon = 1e-9) =>
        BuildCore(polygon, decimals, epsilon);

    internal static string BuildExact(Polygon2 polygon) =>
        BuildCore(polygon, decimals: null, double.Epsilon);

    private static string BuildCore(Polygon2 polygon, int? decimals, double epsilon)
    {
        ArgumentNullException.ThrowIfNull(polygon);

        var minIndex = FindMinIndex(polygon.Vertices, decimals, epsilon);
        var forward = BuildTraversal(polygon.Vertices, minIndex, forward: true, decimals);
        var backward = BuildTraversal(polygon.Vertices, minIndex, forward: false, decimals);
        return string.CompareOrdinal(forward, backward) <= 0 ? forward : backward;
    }

    private static string BuildTraversal(
        IReadOnlyList<Point2> vertices,
        int minIndex,
        bool forward,
        int? decimals)
    {
        var count = vertices.Count;
        var sb = new StringBuilder(count * 24);

        for (var i = 0; i < count; i++)
        {
            var index = forward
                ? (minIndex + i) % count
                : (minIndex - i + count) % count;
            var vertex = vertices[index];
            sb.Append(Format(vertex.X, decimals))
              .Append(',')
              .Append(Format(vertex.Y, decimals))
              .Append(';');
        }

        return sb.ToString();
    }

    private static int FindMinIndex(IReadOnlyList<Point2> vertices, int? decimals, double epsilon)
    {
        var minIndex = 0;
        var minX = Normalize(vertices[0].X, decimals);
        var minY = Normalize(vertices[0].Y, decimals);

        for (var i = 1; i < vertices.Count; i++)
        {
            var x = Normalize(vertices[i].X, decimals);
            var y = Normalize(vertices[i].Y, decimals);
            if (x < minX || (Math.Abs(x - minX) < epsilon && y < minY))
            {
                minX = x;
                minY = y;
                minIndex = i;
            }
        }

        return minIndex;
    }

    private static double Round(double value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static double Normalize(double value, int? decimals) =>
        decimals.HasValue ? Round(value, decimals.Value) : value;

    private static string Format(double value, int? decimals) =>
        decimals is int precision
            ? Round(value, precision)
                .ToString($"0.{new string('#', precision)}", CultureInfo.InvariantCulture)
            : (value == 0d ? 0d : value).ToString("R", CultureInfo.InvariantCulture);
}
