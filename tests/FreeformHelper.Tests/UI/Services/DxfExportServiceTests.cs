using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfExportServiceTests
{
    private static readonly string[] _lineSeparators = ["\r\n", "\n"];

    private sealed record DxfEntity(string Type, List<(string Code, string Value)> Fields);

    private static List<DxfEntity> ReadEntities(string dxf)
    {
        var lines = dxf.Split(_lineSeparators, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(0, lines.Length % 2);

        var entities = new List<DxfEntity>();
        DxfEntity? current = null;
        for (var index = 0; index < lines.Length; index += 2)
        {
            var code = lines[index];
            var value = lines[index + 1];
            if (code == "0")
            {
                current = new DxfEntity(value, []);
                entities.Add(current);
            }
            else
            {
                Assert.NotNull(current);
                current!.Fields.Add((code, value));
            }
        }

        return entities;
    }

    private static List<DxfEntity> ReadPolylines(string dxf)
    {
        return ReadEntities(dxf).Where(static entity => entity.Type == "LWPOLYLINE").ToList();
    }

    private static string FieldValue(DxfEntity entity, string code)
    {
        return entity.Fields.First(field => field.Code == code).Value;
    }

    private static List<(string X, string Y)> Vertices(DxfEntity entity)
    {
        var xs = entity.Fields.Where(static field => field.Code == "10").Select(static field => field.Value).ToList();
        var ys = entity.Fields.Where(static field => field.Code == "20").Select(static field => field.Value).ToList();
        Assert.Equal(xs.Count, ys.Count);
        return xs.Zip(ys, static (x, y) => (x, y)).ToList();
    }

    private static CadPad Pad(int id, string layer, params Point2[] vertices)
    {
        return new CadPad(id, $"pad-{id}", layer, new Polygon2(vertices));
    }

    private static Point2[] Square(double size)
    {
        return [new Point2(0, 0), new Point2(size, 0), new Point2(size, size), new Point2(0, size)];
    }

    [Fact]
    public void ExportAscii_WithoutPads_WritesTheDocumentFrameAndNoEntity()
    {
        var dxf = DxfExportService.ExportAscii([]);

        var entities = ReadEntities(dxf);
        Assert.Equal(["SECTION", "ENDSEC", "SECTION", "ENDSEC", "EOF"], entities.Select(static entity => entity.Type).ToArray());
        Assert.Equal("HEADER", FieldValue(entities[0], "2"));
        Assert.Equal("ENTITIES", FieldValue(entities[2], "2"));
        Assert.Empty(ReadPolylines(dxf));
    }

    [Fact]
    public void ExportAscii_WritesOneClosedPolylinePerPadInInputOrderWithItsLayerAndVertexCount()
    {
        var dxf = DxfExportService.ExportAscii(
        [
            Pad(1, "SIG", new Point2(0, 0), new Point2(4, 0), new Point2(4, 2)),
            Pad(2, "AA.drawing", Square(10)),
        ]);

        var polylines = ReadPolylines(dxf);
        Assert.Equal(2, polylines.Count);
        Assert.Equal("SIG", FieldValue(polylines[0], "8"));
        Assert.Equal("3", FieldValue(polylines[0], "90"));
        Assert.Equal("1", FieldValue(polylines[0], "70"));
        Assert.Equal("AA.drawing", FieldValue(polylines[1], "8"));
        Assert.Equal("4", FieldValue(polylines[1], "90"));
        Assert.Equal(4, Vertices(polylines[1]).Count);
    }

    [Fact]
    public void ExportAscii_WritesTheVertexCoordinatesWithoutTrailingZeros()
    {
        var dxf = DxfExportService.ExportAscii(
        [
            Pad(1, "SIG", new Point2(1.5, -0.25), new Point2(10, 0), new Point2(10, 2.125)),
        ]);

        var vertices = Vertices(Assert.Single(ReadPolylines(dxf)));
        Assert.Equal([("1.5", "-0.25"), ("10", "0"), ("10", "2.125")], vertices);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ExportAscii_WithABlankLayer_WritesLayerZero(string layer)
    {
        var dxf = DxfExportService.ExportAscii([Pad(1, layer, Square(5))]);

        Assert.Equal("0", FieldValue(Assert.Single(ReadPolylines(dxf)), "8"));
    }

    [Fact]
    public void ExportAscii_DropsTheRepeatedClosingVertex()
    {
        var closed = new[] { new Point2(0, 0), new Point2(6, 0), new Point2(6, 6), new Point2(0, 6), new Point2(0, 0) };

        var dxf = DxfExportService.ExportAscii([Pad(1, "SIG", closed)]);

        var polyline = Assert.Single(ReadPolylines(dxf));
        Assert.Equal("4", FieldValue(polyline, "90"));
        Assert.Equal(4, Vertices(polyline).Count);
    }

    [Fact]
    public void ExportAscii_SkipsAPadThatHasFewerThanThreeDistinctVertices()
    {
        var degenerate = new[] { new Point2(0, 0), new Point2(5, 5), new Point2(0, 0) };

        var dxf = DxfExportService.ExportAscii([Pad(1, "SIG", degenerate), Pad(2, "SIG", Square(3))]);

        var polyline = Assert.Single(ReadPolylines(dxf));
        Assert.Equal("4", FieldValue(polyline, "90"));
    }
}
