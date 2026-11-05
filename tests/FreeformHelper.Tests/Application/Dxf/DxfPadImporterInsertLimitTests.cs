// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Xunit;
using static FreeformHelper.Tests.DxfPadImporterMalformedInputTests;

namespace FreeformHelper.Tests;

/// <summary>
/// INSERT expansion limits of the DXF import. They share the harness of <see cref="DxfPadImporterMalformedInputTests"/>.
/// </summary>
public sealed class DxfPadImporterInsertLimitTests
{
    [Fact]
    public async Task Import_InsertWithHugeArrayCounts_FinishesOrReportsError()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nCELL\n10\n0\n20\n0");
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nCELL\n10\n0\n20\n0\n70\n2000000000\n71\n2000000000\n44\n2\n45\n2");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "10000");
    }

    [Fact]
    public async Task Import_NestedInsertChainWithExponentialGrowth_FinishesOrReportsError()
    {
        // Twelve block levels with eleven tenfold expansions would produce 10^11 pads.
        const int depth = 12;
        var lines = new List<string>(SectionHeader("BLOCKS"));
        for (var level = 0; level < depth; level++)
        {
            lines.AddRange(["0", "BLOCK", "8", "0", "2", $"L{level}", "10", "0", "20", "0"]);
            if (level == depth - 1)
            {
                lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
            }
            else
            {
                for (var copy = 0; copy < 10; copy++)
                {
                    lines.AddRange(["0", "INSERT", "8", "0", "2", $"L{level + 1}", "10", "0", "20", "0"]);
                }
            }

            AppendLines(lines, "0\nENDBLK");
        }

        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nL0\n10\n0\n20\n0");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "2000000");
    }

    [Fact]
    public async Task Import_NestedInsertChainWithoutPads_ThrowsInvalidDataAsync()
    {
        // No block holds a pad, so the pad count stays zero while the expander would walk 10^4 * 10^4 * 10^4 cells.
        const int depth = 4;
        var lines = new List<string>(SectionHeader("BLOCKS"));
        for (var level = 0; level < depth; level++)
        {
            lines.AddRange(["0", "BLOCK", "8", "0", "2", $"E{level}", "10", "0", "20", "0"]);
            if (level < depth - 1)
            {
                lines.AddRange(["0", "INSERT", "8", "0", "2", $"E{level + 1}", "70", "100", "71", "100"]);
            }

            AppendLines(lines, "0\nENDBLK");
        }

        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nE0\n10\n0\n20\n0");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "5000000");
    }

    [Fact]
    public async Task Import_BlockWithManyUnresolvedInsertsRepeatedByArray_ThrowsInvalidDataAsync()
    {
        // Inserts of a missing block do no work alone, but the array repeats all of them in every cell.
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n2\nHOLDER");
        for (var index = 0; index < 600; index++)
        {
            AppendLines(lines, "0\nINSERT\n2\nMISSING");
        }

        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n2\nHOLDER\n70\n1000\n71\n100");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "5000000");
    }

    [Fact]
    public async Task Import_LongPolylineRepeatedByArray_ThrowsInvalidDataAsync()
    {
        // 500 vertices in each of 100,000 copies is 50,000,000 vertex transforms from one small block.
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n2\nLONG\n0\nLWPOLYLINE\n70\n1");
        for (var vertex = 0; vertex < 500; vertex++)
        {
            lines.AddRange(["10", vertex.ToString(CultureInfo.InvariantCulture), "20", (vertex % 7).ToString(CultureInfo.InvariantCulture)]);
        }

        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n2\nLONG\n70\n1000\n71\n100");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "40000000");
    }

    [Theory]
    [InlineData(10_000, 1)]
    [InlineData(1, 10_000)]
    public async Task Import_InsertAxisCountAtLimit_ImportsEveryCopyAsync(int columns, int rows)
    {
        var outcome = await ImportFileAsync(BuildInsertArray(columns, rows, includePad: true));

        AssertCompleted(outcome);
        Assert.Null(outcome.Error);
        Assert.Equal(columns * rows, outcome.Pads!.Pads.Count);
    }

    [Theory]
    [InlineData(10_001, 1)]
    [InlineData(1, 10_001)]
    public async Task Import_InsertAxisCountOneOverLimit_ThrowsInvalidDataAsync(int columns, int rows)
    {
        var outcome = await ImportFileAsync(BuildInsertArray(columns, rows, includePad: true));

        AssertInsertLimit(outcome, "10000");
    }

    [Fact]
    public async Task Import_InsertCopyCountAtLimit_AcceptsArrayAsync()
    {
        // An empty block exercises the count boundary without allocating 100,000 polygons.
        var outcome = await ImportFileAsync(BuildInsertArray(1_000, 100, includePad: false));

        AssertCompleted(outcome);
        Assert.Null(outcome.Error);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_InsertCopyCountOneOverLimit_ThrowsInvalidDataAsync()
    {
        // 9,091 * 11 = 100,001; both axes are within the axis limit.
        var outcome = await ImportFileAsync(BuildInsertArray(9_091, 11, includePad: false));

        AssertInsertLimit(outcome, "100000");
    }

    [Fact]
    public async Task Import_SeparateModelInsertsExceedTotalPadLimit_ThrowsInvalidDataAsync()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n2\nCELL\n0\nLWPOLYLINE\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n0\n20\n1\n0\nENDBLK");
        AppendLines(lines, "0\nBLOCK\n2\nGROUP\n0\nINSERT\n2\nCELL\n70\n10000\n0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        // Each model INSERT expands to 1,000,000 pads. Only their combined count exceeds the limit.
        AppendLines(lines, "0\nINSERT\n2\nGROUP\n70\n100\n0\nINSERT\n2\nGROUP\n70\n100\n0\nINSERT\n2\nCELL");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInsertLimit(outcome, "2000000");
    }

    private static byte[] BuildInsertArray(int columns, int rows, bool includePad)
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n2\nCELL");
        if (includePad)
        {
            lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        }

        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange([
            "0", "INSERT", "2", "CELL",
            "70", columns.ToString(CultureInfo.InvariantCulture),
            "71", rows.ToString(CultureInfo.InvariantCulture)]);
        lines.AddRange(SectionEnd());
        return ToBytes(lines);
    }

    private static void AssertInsertLimit(ImportOutcome outcome, string limit)
    {
        AssertCompleted(outcome);
        var error = Assert.IsType<InvalidDataException>(outcome.Error);
        Assert.Contains("limit", error.Message, StringComparison.Ordinal);
        Assert.Contains(limit, error.Message, StringComparison.Ordinal);
    }
}
