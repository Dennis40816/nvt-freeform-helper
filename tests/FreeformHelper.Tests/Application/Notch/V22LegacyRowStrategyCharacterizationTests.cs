using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Characterizes the legacy v2.2 9-column row for every axis x border x overlap-case cell.
/// The grid is 3x3 with 10-unit cells; row 0 is the top row (y in [20, 30]).
/// Pad diff index is 100 + row * 3 + col. LenScale is 2 and the null value is 65535.
/// </summary>
public sealed class V22LegacyRowStrategyCharacterizationTests
{
    private const int NullValue = 65535;
    private const int LenScale = 2;
    private const int CadPadId = 77;

    public static IEnumerable<object[]> BoundaryCaseTable()
    {
        // X-way uses the middle row (y in [10, 20]); CAD y is [12, 18].
        // Trailing border (col 2): the trailing branch is evaluated first.
        yield return Cell(FreeformType.XWay, 1, 2, 23, 12, 34, 18, 0, "105,66,66,104,14,105,65535,8,65535");
        yield return Cell(FreeformType.XWay, 1, 2, 16, 12, 27, 18, 1, "105,66,66,104,8,104,65535,14,105");
        yield return Cell(FreeformType.XWay, 1, 2, 22, 12, 27, 18, 2, "105,30,30,65535,0,105,65535,0,105");

        // Leading border (col 0).
        yield return Cell(FreeformType.XWay, 1, 0, 3, 12, 14, 18, 0, "103,66,66,65535,6,103,104,14,104");
        yield return Cell(FreeformType.XWay, 1, 0, -4, 12, 7, 18, 1, "103,66,66,65535,8,65535,104,14,103");
        yield return Cell(FreeformType.XWay, 1, 0, 2, 12, 7, 18, 2, "103,30,30,65535,0,103,65535,0,103");

        // Interior (col 1).
        yield return Cell(FreeformType.XWay, 1, 1, 13, 12, 24, 18, 0, "104,66,66,103,14,104,105,8,105");
        yield return Cell(FreeformType.XWay, 1, 1, 6, 12, 17, 18, 1, "104,66,66,103,8,103,105,14,104");
        yield return Cell(FreeformType.XWay, 1, 1, 12, 12, 17, 18, 2, "104,30,30,65535,0,104,65535,0,104");

        // Y-way uses the middle column (x in [10, 20]); CAD x is [12, 18].
        // Leading border (row 0, y in [20, 30]): the leading branch is evaluated first.
        yield return Cell(FreeformType.YWay, 0, 1, 12, 16, 18, 27, 0, "101,66,66,65535,14,101,104,8,104");
        yield return Cell(FreeformType.YWay, 0, 1, 12, 23, 18, 34, 1, "101,66,66,65535,8,65535,104,14,101");
        yield return Cell(FreeformType.YWay, 0, 1, 12, 23, 18, 28, 2, "101,30,30,65535,0,101,65535,0,101");

        // Trailing border (row 2, y in [0, 10]).
        yield return Cell(FreeformType.YWay, 2, 1, 12, -4, 18, 7, 0, "107,66,66,104,14,107,65535,8,65535");
        yield return Cell(FreeformType.YWay, 2, 1, 12, 3, 18, 14, 1, "107,66,66,104,8,104,65535,14,107");
        yield return Cell(FreeformType.YWay, 2, 1, 12, 3, 18, 8, 2, "107,30,30,65535,0,107,65535,0,107");

        // Interior (row 1, y in [10, 20]).
        yield return Cell(FreeformType.YWay, 1, 1, 12, 6, 18, 17, 0, "104,66,66,101,14,104,107,8,107");
        yield return Cell(FreeformType.YWay, 1, 1, 12, 13, 18, 24, 1, "104,66,66,101,8,101,107,14,104");
        yield return Cell(FreeformType.YWay, 1, 1, 12, 13, 18, 18, 2, "104,30,30,65535,0,104,65535,0,104");
    }

    [Theory]
    [MemberData(nameof(BoundaryCaseTable))]
    public void Build_BoundaryByCaseTable_ProducesExpectedRow(
        FreeformType freeform,
        int row,
        int col,
        double cadMinX,
        double cadMinY,
        double cadMaxX,
        double cadMaxY,
        int expectedCase,
        string expectedValues)
    {
        var grid = CreateGrid(rows: 3, cols: 3, freeform);
        var reg = grid.GetPad(row, col);
        var cad = CreateCad(cadMinX, cadMinY, cadMaxX, cadMaxY);

        var result = V22LegacyRowStrategy.Build(reg, cad, grid, CreateRequest());

        Assert.Equal(expectedValues, string.Join(',', result.Values));
        Assert.Equal(NotchAlgorithmVersion.V22, result.Version);
        Assert.Equal(2, result.IcIndex);
        Assert.Equal(reg.DiffIndex, result.DiffIndex);
        Assert.Equal(reg.RegularPadId, result.RegularPadIndex);
        Assert.Equal(CadPadId, result.CadPadId);
        Assert.Equal($"Freeform={freeform} CadPadId={CadPadId} Case={expectedCase}", result.Comment);
    }

    [Fact]
    public void Build_InteriorXWayWithoutOverlap_UsesFallbackCaseAndClearsNeighbors()
    {
        var grid = CreateGrid(rows: 3, cols: 3, FreeformType.XWay);
        var reg = grid.GetPad(1, 1);

        var result = V22LegacyRowStrategy.Build(reg, CreateCad(25, 12, 29, 18), grid, CreateRequest());

        Assert.Equal("104,24,24,65535,0,104,65535,0,104", string.Join(',', result.Values));
        Assert.EndsWith(" Case=3", result.Comment, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SingleColumnXWay_EvaluatesTrailingBorderBeforeLeadingBorder()
    {
        var grid = CreateGrid(rows: 1, cols: 1, FreeformType.XWay);
        var reg = grid.GetPad(0, 0);

        var result = V22LegacyRowStrategy.Build(reg, CreateCad(3, 2, 14, 8), grid, CreateRequest());

        // Trailing branch: leftLen = 2 * (10 - 3) = 14. The leading branch would give 2 * (3 - 0) = 6.
        Assert.Equal("100,66,66,65535,14,100,65535,8,65535", string.Join(',', result.Values));
    }

    [Fact]
    public void Build_NeighborWithoutMatchedCad_ClearsOnlyCadNeighborColumn()
    {
        var grid = CreateGrid(rows: 3, cols: 3, FreeformType.XWay);
        grid.GetPad(1, 2).MatchedCadPadId = null;
        var reg = grid.GetPad(1, 1);

        var result = V22LegacyRowStrategy.Build(reg, CreateCad(13, 12, 24, 18), grid, CreateRequest());

        // v[6] follows the CAD-neighbor lookup; v[8] still uses the plain neighbor diff.
        Assert.Equal("104,66,66,103,14,104,65535,8,105", string.Join(',', result.Values));
    }

    [Fact]
    public void Build_FractionalLengths_RoundHalfToEven()
    {
        var grid = CreateGrid(rows: 3, cols: 3, FreeformType.XWay);
        var reg = grid.GetPad(1, 1);

        // leftLen = 2 * (20 - 13.75) = 12.5 -> 12, rightLen = 2 * (24.25 - 20) = 8.5 -> 8.
        var result = V22LegacyRowStrategy.Build(reg, CreateCad(13.75, 12, 24.25, 18), grid, CreateRequest());

        Assert.Equal("104,63,63,103,12,104,105,8,105", string.Join(',', result.Values));
    }

    [Fact]
    public void Build_NullCad_Throws()
    {
        var grid = CreateGrid(rows: 3, cols: 3, FreeformType.XWay);

        Assert.Throws<ArgumentNullException>(
            () => V22LegacyRowStrategy.Build(grid.GetPad(1, 1), null, grid, CreateRequest()));
    }

    [Theory]
    [InlineData(FreeformType.XWay, true)]
    [InlineData(FreeformType.YWay, true)]
    [InlineData(FreeformType.XYWay, false)]
    [InlineData(FreeformType.None, false)]
    public void CanHandle_RequiresMatchedCadAndSingleAxisFreeform(FreeformType freeform, bool expected)
    {
        var grid = CreateGrid(rows: 3, cols: 3, freeform);
        var reg = grid.GetPad(1, 1);

        Assert.Equal(expected, V22LegacyRowStrategy.CanHandle(reg, CreateCad(0, 0, 1, 1)));
        Assert.False(V22LegacyRowStrategy.CanHandle(reg, null));
    }

    private static object[] Cell(
        FreeformType freeform,
        int row,
        int col,
        double cadMinX,
        double cadMinY,
        double cadMaxX,
        double cadMaxY,
        int expectedCase,
        string expectedValues)
    {
        return [freeform, row, col, cadMinX, cadMinY, cadMaxX, cadMaxY, expectedCase, expectedValues];
    }

    private static LegacyNotchGenerationRequest CreateRequest()
    {
        return new LegacyNotchGenerationRequest(
            EnabledVersions: [NotchAlgorithmVersion.V22],
            LenScale: LenScale,
            NullValue: NullValue,
            ThresholdQ7: 0,
            EffectiveV22ThresholdPercent: 0.0);
    }

    private static CadPad CreateCad(double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(CadPadId, $"C{CadPadId}", "PAD", polygon);
    }

    private static RegularGrid CreateGrid(int rows, int cols, FreeformType freeform)
    {
        const double cell = 10.0;
        var xEdges = Enumerable.Range(0, cols + 1).Select(static i => i * cell).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(static i => i * cell).ToArray();
        var pads = new List<RegularPad>(rows * cols);
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var x0 = col * cell;
                var x1 = x0 + cell;
                var y1 = (rows - row) * cell;
                var y0 = y1 - cell;
                var polygon = new Polygon2(new[]
                {
                    new Point2(x0, y0),
                    new Point2(x1, y0),
                    new Point2(x1, y1),
                    new Point2(x0, y1),
                });
                var index = (row * cols) + col;
                pads.Add(new RegularPad(row, col, index, polygon)
                {
                    IcIndex = 2,
                    DiffIndex = 100 + index,
                    Freeform = freeform,
                    MatchedCadPadId = 900 + index,
                });
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }
}
