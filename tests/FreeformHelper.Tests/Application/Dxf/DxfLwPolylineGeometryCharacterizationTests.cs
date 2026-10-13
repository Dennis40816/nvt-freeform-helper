using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the DXF importer does today with LWPOLYLINE geometry that a straight, planar polygon
/// cannot hold: arc segments (group code 42, "bulge") and a non-default extrusion direction (group
/// codes 210, 220 and 230). The importer reads only the layer, the closed flag and the vertex
/// coordinates, so both are dropped without a diagnostic. These tests describe the current result,
/// not the wanted one (issue #64, inventory finding D03). A fix must either represent the arcs and
/// convert the object coordinates to the world through the extrusion direction, or report a
/// diagnostic. It then changes the expected values here and must show the new numbers in its pull
/// request. Only the LWPOLYLINE path is pinned. The legacy POLYLINE and VERTEX path and block
/// polylines expanded through INSERT drop bulge and extrusion in the same way and are not pinned.
/// The class joins the serial collection because the importer keeps a process-wide tokenization
/// counter that other tests read.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class DxfLwPolylineGeometryCharacterizationTests
{
    private const double SquareSide = 10d;
    private const double HalfCircleArea = Math.PI * 5d * 5d / 2d;

    [Fact]
    public void Import_OutwardBulgeOnOneSide_ImportsTheStraightSquare()
    {
        // The square runs counter-clockwise. Bulge +1 on the first segment (0,0) to (10,0) is a
        // counter-clockwise half circle of radius 5. It bulges to the right of the travel direction,
        // so it leaves the square: the true area is 100 + 39.27 = 139.27, 39.3 percent more.
        var set = Import(SquareLines(firstSegmentBulge: 1d, extrusionZ: null));

        var polygon = Assert.Single(set.Pads).Polygon;
        Assert.Equal(4, polygon.Vertices.Length);
        Assert.Equal(SquareSide * SquareSide, polygon.Area(), 6);
        Assert.True((SquareSide * SquareSide) + HalfCircleArea > polygon.Area());
    }

    [Fact]
    public void Import_InwardBulgeOnOneSide_ImportsTheStraightSquare()
    {
        // Bulge -1 is a clockwise half circle. It bulges to the left of the travel direction, so it
        // cuts into the square: the true area is 100 - 39.27 = 60.73, 39.3 percent less.
        var set = Import(SquareLines(firstSegmentBulge: -1d, extrusionZ: null));

        var polygon = Assert.Single(set.Pads).Polygon;
        Assert.Equal(4, polygon.Vertices.Length);
        Assert.Equal(SquareSide * SquareSide, polygon.Area(), 6);
        Assert.True((SquareSide * SquareSide) - HalfCircleArea < polygon.Area());
    }

    [Fact]
    public void Import_BulgeZeroAndBulgeOne_GiveTheSameVertices()
    {
        var straight = Assert.Single(Import(SquareLines(firstSegmentBulge: 0d, extrusionZ: null)).Pads).Polygon;
        var arc = Assert.Single(Import(SquareLines(firstSegmentBulge: 1d, extrusionZ: null)).Pads).Polygon;

        Assert.Equal(straight.Vertices.ToArray(), arc.Vertices.ToArray());
    }

    [Fact]
    public void Import_ExtrusionPointingDown_KeepsTheOcsCoordinates()
    {
        // With extrusion (0, 0, -1) the object coordinates are mirrored in x. The square at
        // x = 100..110 is at x = -110..-100 in the world. The importer keeps 100..110.
        var set = Import(SquareLines(firstSegmentBulge: 0d, extrusionZ: -1d, originX: 100d));

        var bounds = Assert.Single(set.Pads).Polygon.Bounds;
        Assert.Equal(100d, bounds.MinX, 6);
        Assert.Equal(110d, bounds.MaxX, 6);
    }

    private static CadPadSet Import(IReadOnlyList<string> lines)
    {
        var bytes = Encoding.ASCII.GetBytes(string.Join("\n", lines));
        using var stream = new MemoryStream(bytes);
        return DxfPadImporter.Import(stream, new DxfImportOptions { OnlyClosedPolylines = true });
    }

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static List<string> SquareLines(double firstSegmentBulge, double? extrusionZ, double originX = 0d)
    {
        var lines = new List<string> { "0", "SECTION", "2", "ENTITIES", "0", "LWPOLYLINE", "8", "PAD", "70", "1" };
        var corners = new[]
        {
            (originX, 0d),
            (originX + SquareSide, 0d),
            (originX + SquareSide, SquareSide),
            (originX, SquareSide),
        };
        for (var index = 0; index < corners.Length; index++)
        {
            lines.AddRange(["10", Number(corners[index].Item1), "20", Number(corners[index].Item2)]);
            if (index == 0 && firstSegmentBulge != 0d)
            {
                lines.AddRange(["42", Number(firstSegmentBulge)]);
            }
        }

        if (extrusionZ is not null)
        {
            lines.AddRange(["210", "0", "220", "0", "230", Number(extrusionZ.Value)]);
        }

        lines.AddRange(["0", "ENDSEC", "0", "EOF"]);
        return lines;
    }
}
