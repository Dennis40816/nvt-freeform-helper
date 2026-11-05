using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Dxf;

public sealed partial class DxfPadImporter
{
    /// <summary>
    /// Attempts to commit a polyline to the list of CAD pads.
    /// Performs validation such as minimum vertex count and closed-polyline filtering.
    /// </summary>
    private static void TryCommitPolyline(
        List<CadPad> pads,
        ref int nextId,
        string layer,
        bool closed,
        List<Point2> vertices,
        DxfImportOptions options)
    {
        // Filter out incomplete or non-finite coordinates before validating the ring.
        var vs = vertices.Where(HasFiniteCoordinates).ToList();
        // Some DXF writers might repeat the first vertex at the end. Remove it if present.
        if (vs.Count >= 2 && vs[0].DistanceTo(vs[^1]) < 1e-9) // Using a small epsilon for double comparison
        {
            vs.RemoveAt(vs.Count - 1);
        }

        // A polygon must have at least 3 vertices after removing the closing vertex.
        if (vs.Count < 3)
        {
            return;
        }

        var isClosed = closed;
        // If option is set to only closed polylines and this one isn't, then skip.
        if (!isClosed && options.OnlyClosedPolylines)
        {
            return;
        }

        var poly = new Polygon2(vs);
        // Add the new CAD pad with a unique ID and a descriptive name.
        pads.Add(new CadPad(nextId++, $"PAD_{nextId}", layer, poly));
    }

    private static List<Point2> NormalizeVertices(IEnumerable<Point2> vertices)
    {
        var vs = vertices.Where(HasFiniteCoordinates).ToList();
        if (vs.Count >= 2 && vs[0].DistanceTo(vs[^1]) < 1e-9)
        {
            vs.RemoveAt(vs.Count - 1);
        }

        return vs;
    }

    private static bool HasFiniteCoordinates(Point2 point)
    {
        return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }
}
