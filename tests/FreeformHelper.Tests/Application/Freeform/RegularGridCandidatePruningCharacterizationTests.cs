using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins how the candidate range of the pad matcher behaves on a grid whose row widths differ
/// (issue #64). The grid keeps one shared list of column edges. The candidate range is narrowed
/// with those edges, so a pad that lies in a column that is wider in one row can be skipped even
/// though its bounds overlap the CAD pad. These tests describe the current result, not the wanted
/// one. A fix that makes the range conservative changes the expected values here and must say so
/// in its pull request.
/// </summary>
public sealed class RegularGridCandidatePruningCharacterizationTests
{
    // Row 0 has columns 0..10 and 10..20. Row 1 has columns 0..15 and 15..20.
    // The shared X edges are the ones of the first row: 0, 10, 20.
    private const int WideCellRegularPadId = 2;

    private static readonly double[] _gridEdges = [0.0, 10.0, 20.0];

    [Fact]
    public void CandidateQuery_CadInsideAWiderCellOfALaterRow_MissesThePadThatOverlaps()
    {
        var grid = CreateGridWithRowLocalWidths();
        var cadPad = CreateCadPad();

        var fullScan = grid.Pads
            .Where(pad => pad.Bounds.Intersects(cadPad.Bounds))
            .Select(static pad => pad.RegularPadId)
            .ToArray();
        var queried = RegularGridCandidateQuery.QueryByBounds(grid, cadPad.Bounds)
            .Select(static pad => pad.RegularPadId)
            .ToArray();

        Assert.Equal([WideCellRegularPadId], fullScan);
        Assert.Empty(queried);
    }

    [Fact]
    public void Match_CadInsideAWiderCellOfALaterRow_ProducesNoLink()
    {
        var grid = CreateGridWithRowLocalWidths();
        var cadPad = CreateCadPad();

        var result = PadMatcher.Match(new CadPadSet(new[] { cadPad }), grid);

        // The CAD pad lies fully inside regular pad 2 (area 4 of 150), but it is not linked.
        Assert.Empty(result.Links);
        Assert.Empty(result.CadToRegular);
    }

    private static CadPad CreateCadPad()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(12, 12),
            new Point2(14, 12),
            new Point2(14, 14),
            new Point2(12, 14),
        });
        return new CadPad(1, "L", "P", polygon);
    }

    private static RegularGrid CreateGridWithRowLocalWidths()
    {
        var pads = new List<RegularPad>
        {
            CreatePad(row: 0, col: 0, id: 0, minX: 0, maxX: 10, minY: 0, maxY: 10),
            CreatePad(row: 0, col: 1, id: 1, minX: 10, maxX: 20, minY: 0, maxY: 10),
            CreatePad(row: 1, col: 0, id: WideCellRegularPadId, minX: 0, maxX: 15, minY: 10, maxY: 20),
            CreatePad(row: 1, col: 1, id: 3, minX: 15, maxX: 20, minY: 10, maxY: 20),
        };
        return new RegularGrid(
            rows: 2,
            cols: 2,
            xEdges: _gridEdges,
            yEdges: _gridEdges,
            pads: pads);
    }

    private static RegularPad CreatePad(int row, int col, int id, double minX, double maxX, double minY, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });
        return new RegularPad(row, col, id, polygon)
        {
            IcIndex = 0,
            DiffIndex = id,
        };
    }
}
