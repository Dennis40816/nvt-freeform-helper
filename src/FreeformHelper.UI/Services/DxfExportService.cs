using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Exports CAD pads to a minimal ASCII DXF document using LWPOLYLINE entities.
/// </summary>
public sealed class DxfExportService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string ExportAscii(IReadOnlyList<CadPad> pads)
    {
        var sb = new StringBuilder(capacity: Math.Max(512, pads.Count * 128));
        AppendDocumentStart(sb);

        foreach (var pad in pads)
        {
            AppendPad(sb, pad);
        }

        AppendDocumentEnd(sb);
        return sb.ToString();
    }

    private static void AppendDocumentStart(StringBuilder sb)
    {
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("HEADER");
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("SECTION");
        sb.AppendLine("2");
        sb.AppendLine("ENTITIES");
    }

    private static void AppendDocumentEnd(StringBuilder sb)
    {
        sb.AppendLine("0");
        sb.AppendLine("ENDSEC");
        sb.AppendLine("0");
        sb.AppendLine("EOF");
    }

    private static void AppendPad(StringBuilder sb, CadPad pad)
    {
        var vertices = NormalizeVertices(pad.Polygon.Vertices);
        if (vertices.Count < 3)
        {
            return;
        }

        sb.AppendLine("0");
        sb.AppendLine("LWPOLYLINE");
        sb.AppendLine("8");
        sb.AppendLine(string.IsNullOrWhiteSpace(pad.Layer) ? "0" : pad.Layer);
        sb.AppendLine("90");
        sb.AppendLine(vertices.Count.ToString(Invariant));
        sb.AppendLine("70");
        sb.AppendLine("1");

        foreach (var vertex in vertices)
        {
            sb.AppendLine("10");
            sb.AppendLine(vertex.X.ToString("0.###########", Invariant));
            sb.AppendLine("20");
            sb.AppendLine(vertex.Y.ToString("0.###########", Invariant));
        }
    }

    private static List<Point2> NormalizeVertices(IReadOnlyList<Point2> source)
    {
        var vertices = source.ToList();
        if (vertices.Count < 2)
        {
            return vertices;
        }

        if (vertices[0].DistanceTo(vertices[^1]) < 1e-9)
        {
            vertices.RemoveAt(vertices.Count - 1);
        }

        return vertices;
    }
}
