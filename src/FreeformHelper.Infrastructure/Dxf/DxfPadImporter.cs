using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Infrastructure.Dxf;

/// <summary>
/// Provides minimal DXF (ASCII) import functionality, specifically designed to extract
/// closed polyline entities and represent them as <see cref="CadPad"/> objects.
/// This importer focuses on being lightweight and intentionally avoids a full-fledged
/// DXF parser library.
/// </summary>
/// <remarks>
/// Supported DXF entities for pad extraction include LWPOLYLINE and POLYLINE/VERTEX pairs.
/// </remarks>
public sealed partial class DxfPadImporter
{
    /// <summary>
    /// Imports CAD pads from a DXF file specified by its path.
    /// </summary>
    /// <param name="path">The file path to the DXF file.</param>
    /// <param name="options">Optional import options to control behavior like filtering.</param>
    /// <returns>A <see cref="CadPadSet"/> containing the imported pads.</returns>
    public static CadPadSet Import(string path, DxfImportOptions? options = null)
    {
        options ??= new DxfImportOptions();

        using var sr = new StreamReader(path);
        return ImportFromReader(sr, options);
    }

    /// <summary>
    /// Imports CAD pads from a DXF file provided as a stream.
    /// </summary>
    /// <param name="stream">The input stream containing the DXF data.</param>
    /// <param name="options">Optional import options to control behavior like filtering.</param>
    /// <returns>A <see cref="CadPadSet"/> containing the imported pads.</returns>
    public static CadPadSet Import(Stream stream, DxfImportOptions? options = null)
    {
        options ??= new DxfImportOptions();

        using var sr = new StreamReader(stream);
        return ImportFromReader(sr, options);
    }

    /// <summary>
    /// Core import logic that reads DXF data from a <see cref="TextReader"/> and constructs a <see cref="CadPadSet"/>.
    /// </summary>
    /// <param name="reader">The TextReader providing DXF content.</param>
    /// <param name="options">The import options.</param>
    /// <returns>A <see cref="CadPadSet"/>.</returns>
    private static CadPadSet ImportFromReader(TextReader reader, DxfImportOptions options)
    {
        // DXF files are structured as group code-value pairs. Read them all first.
        var tokens = ReadPairs(reader).ToList();

        var blocks = ParseBlockDefinitions(tokens);
        var modelPolylines = new List<ParsedPolyline>();
        var modelInserts = new List<ParsedInsert>();
        ParseEntitySection(tokens, "ENTITIES", modelPolylines, modelInserts);

        var pads = new List<CadPad>();
        var nextId = 0; // Unique ID counter for imported pads

        foreach (var poly in modelPolylines)
        {
            TryCommitPolyline(pads, ref nextId, poly.Layer, poly.IsClosed, poly.Vertices, options);
        }

        if (options.IncludeBlockPolylines && modelInserts.Count > 0)
        {
            var recursionStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var insert in modelInserts)
            {
                ExpandInsert(
                    insert,
                    inheritedLayer: null,
                    parentTransform: Transform2.Identity,
                    blocks,
                    pads,
                    ref nextId,
                    options,
                    recursionStack);
            }
        }

        return new CadPadSet(pads);
    }

    private sealed class BlockDefinition
    {
        private readonly List<ParsedPolyline> _polylines = new();
        private readonly List<ParsedInsert> _inserts = new();

        public BlockDefinition(string name, Point2 basePoint)
        {
            Name = name;
            BasePoint = basePoint;
        }

        public string Name { get; }

        public Point2 BasePoint { get; }

        public IReadOnlyList<ParsedPolyline> Polylines => _polylines;

        public IReadOnlyList<ParsedInsert> Inserts => _inserts;

        public void AddPolyline(ParsedPolyline polyline)
        {
            _polylines.Add(polyline);
        }

        public void AddInsert(ParsedInsert insert)
        {
            _inserts.Add(insert);
        }
    }

    private sealed record ParsedPolyline(string Layer, bool IsClosed, List<Point2> Vertices);

    private sealed record ParsedInsert(
        string Layer,
        string BlockName,
        double InsertionX,
        double InsertionY,
        double ScaleX,
        double ScaleY,
        double RotationDegrees,
        int Columns,
        int Rows,
        double ColumnSpacing,
        double RowSpacing);

    private readonly record struct Transform2(
        double M11,
        double M12,
        double M21,
        double M22,
        double Tx,
        double Ty)
    {
        public static Transform2 Identity => new(1, 0, 0, 1, 0, 0);

        public static Transform2 Translation(double dx, double dy) => new(1, 0, 0, 1, dx, dy);

        public static Transform2 Scale(double sx, double sy) => new(sx, 0, 0, sy, 0, 0);

        public static Transform2 RotationDegrees(double degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            return new(cos, -sin, sin, cos, 0, 0);
        }

        /// <summary>
        /// Returns a transform equivalent to applying <paramref name="inner"/> first, then <paramref name="outer"/>.
        /// </summary>
        public static Transform2 Combine(Transform2 outer, Transform2 inner)
        {
            return new Transform2(
                (outer.M11 * inner.M11) + (outer.M12 * inner.M21),
                (outer.M11 * inner.M12) + (outer.M12 * inner.M22),
                (outer.M21 * inner.M11) + (outer.M22 * inner.M21),
                (outer.M21 * inner.M12) + (outer.M22 * inner.M22),
                (outer.M11 * inner.Tx) + (outer.M12 * inner.Ty) + outer.Tx,
                (outer.M21 * inner.Tx) + (outer.M22 * inner.Ty) + outer.Ty);
        }

        public Point2 Apply(Point2 point)
        {
            return new Point2(
                (M11 * point.X) + (M12 * point.Y) + Tx,
                (M21 * point.X) + (M22 * point.Y) + Ty);
        }
    }
}
