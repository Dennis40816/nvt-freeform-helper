// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using System.Text;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Robustness contract for the DXF import path (<see cref="DxfPadImporter"/> and <see cref="DxfLayerCatalogReader"/>)
/// when a customer file is malformed. Every sample is built in this file; no binary fixture is checked in.
/// </summary>
/// <remarks>
/// Contract for every input: the import ends within <see cref="ImportTimeoutMilliseconds"/> and either returns a result
/// (a malformed entity is skipped) or throws an <see cref="InvalidDataException"/> whose message names the problem.
/// It must never throw another exception type, hang, or return a pad with a non-finite vertex.
/// The skipped layer-table case describes the remaining out-of-scope defect.
/// </remarks>
public sealed class DxfPadImporterMalformedInputTests
{
    private const int ImportTimeoutMilliseconds = 10_000;

    private static readonly byte[] _invalidNumberBytes = [0xFF, 0xFE];

    // ---------------------------------------------------------------- empty and non-DXF content

    [Fact]
    public async Task Import_EmptyFile_ReturnsNoPadsAndEmptyCatalog()
    {
        var outcome = await ImportFileAsync([]);

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
        Assert.Empty(outcome.Catalog!.Layers);
    }

    [Fact]
    public async Task Import_EmptyStream_ReturnsNoPads()
    {
        var outcome = await RunWithTimeoutAsync(() =>
        {
            using var stream = new MemoryStream([]);
            return (DxfPadImporter.Import(stream), DxfLayerCatalog.Empty);
        });

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Theory]
    [InlineData("\n\n\n")]
    [InlineData("   \r\n   \r\n")]
    [InlineData("0\n")]
    [InlineData("0\nEOF\n")]
    [InlineData("not a dxf file at all\njust some text\n")]
    public async Task Import_BlankOrTextOnlyFile_ReturnsNoPads(string content)
    {
        var outcome = await ImportFileAsync(Encoding.UTF8.GetBytes(content));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_BinaryDxfSentinelAndNulBytes_ReturnsNoPads()
    {
        var header = Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n");
        byte[] body = [0x1A, 0x00, 0x00, 0x53, 0x45, 0x43, 0x54, 0x49, 0x4F, 0x4E, 0x00, 0x02, 0x00, 0x48, 0x45, 0x41, 0x44, 0x45, 0x52, 0x00, 0xFF, 0xFE];

        var outcome = await ImportFileAsync([.. header, .. body]);

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_RandomBytes_NeverCrashesOrHangs()
    {
        var random = new Random(20261105);
        for (var round = 0; round < 40; round++)
        {
            var bytes = new byte[random.Next(1, 4096)];
            random.NextBytes(bytes);

            var outcome = await ImportFileAsync(bytes);

            AssertCompleted(outcome, $"random round {round}");
        }
    }

    // ---------------------------------------------------------------- truncation

    [Fact]
    public async Task Import_TruncatedAtEveryLineBoundary_NeverCrashesAndKeepsCompleteEntities()
    {
        var lines = ValidDocumentLines();
        for (var keep = 0; keep <= lines.Count; keep++)
        {
            var outcome = await ImportFileAsync(ToBytes(lines.Take(keep)));

            AssertCompleted(outcome, $"kept {keep} of {lines.Count} lines");
            Assert.True(outcome.Pads!.Pads.Count <= 2, $"kept {keep} lines produced {outcome.Pads.Pads.Count} pads");
        }
    }

    [Fact]
    public async Task Import_TruncatedMidVertex_KeepsOnlyTheIncompletePadOutOfTheResult()
    {
        // The polyline is cut after the X of its last vertex: the pad has a vertex with no Y.
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n5\n20\n0\n10\n5");

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
        Assert.Equal(4, outcome.Pads.Pads[0].Polygon.Vertices.Length);
    }

    [Fact]
    public async Task Import_TruncatedBetweenGroupCodeAndValue_NeverCrashes()
    {
        // An odd number of lines: the last group code has no value line.
        var lines = ValidDocumentLines();
        var text = string.Join("\r\n", lines.Take(lines.Count - 1)) + "\r\n20";

        var outcome = await ImportFileAsync(Encoding.UTF8.GetBytes(text));

        AssertCompleted(outcome);
    }

    [Fact]
    public async Task Import_TruncatedInsideNumber_ReportsClearErrorOrSkips()
    {
        // The file ends in the middle of the literal "-1.5" so the last value is just "-".
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n3\n20\n0\n10\n-");

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, "-");
    }

    [Fact]
    public async Task Import_TruncatedPolylineWithoutSeqend_DoesNotLeakIntoNextEntity()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nPOLYLINE\n8\nPAD\n70\n1\n0\nVERTEX\n10\n0\n20\n0\n0\nVERTEX\n10\n2\n20\n0");
        lines.AddRange(ClosedSquare("OTHER", 10, 10, 2));

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        var pad = Assert.Single(outcome.Pads!.Pads);
        Assert.Equal("OTHER", pad.Layer);
    }

    // ---------------------------------------------------------------- unknown entity types

    [Fact]
    public async Task Import_UnknownEntityTypes_AreSkippedAndDoNotContributeVertices()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        // Unknown entities carry group 10/20 coordinates and a closed-looking flag that must not be read as pad vertices.
        AppendLines(lines, "0\nSPLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n1\n20\n1");
        AppendLines(lines, "0\n3DSOLID\n8\nPAD\n1\nbinary-ish payload\n10\n9\n20\n9");
        AppendLines(lines, "0\nX_NOT_A_REAL_ENTITY\n8\nPAD\n999\ncomment");
        AppendLines(lines, "0\nHATCH\n8\nPAD\n70\n1");
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        AppendLines(lines, "0\nACAD_PROXY_ENTITY\n8\nPAD\n92\n1048576");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        var pad = Assert.Single(outcome.Pads!.Pads);
        Assert.Equal(4, pad.Polygon.Vertices.Length);
        Assert.True(outcome.Catalog!.TryGetLayer("PAD", out var layer));
        Assert.Equal(6, layer.EntityCount);
        Assert.True(layer.HasPolylineContent);
    }

    [Fact]
    public async Task Import_UnknownEntityInsideLegacyPolyline_EndsThePolylineWithoutCrash()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nPOLYLINE\n8\nPAD\n70\n1");
        AppendLines(lines, "0\nVERTEX\n10\n0\n20\n0\n0\nVERTEX\n10\n2\n20\n0");
        AppendLines(lines, "0\nMYSTERY\n8\nPAD");
        AppendLines(lines, "0\nVERTEX\n10\n2\n20\n2\n0\nSEQEND");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.All(outcome.Pads!.Pads, pad => Assert.True(pad.Polygon.Vertices.Length >= 3));
    }

    [Fact]
    public async Task Import_EntityWithoutTypeName_AndUnnamedBlock_AreSkipped()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n10\n0\n20\n0"); // no group 2 name
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\n\n8\nPAD"); // empty entity type
        AppendLines(lines, "0\nINSERT\n8\n0\n10\n0\n20\n0"); // no block name
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nMISSING_BLOCK\n10\n0\n20\n0"); // unknown block
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_MutuallyRecursiveBlocks_TerminateWithoutStackOverflow()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nA\n10\n0\n20\n0");
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nB\n10\n5\n20\n0\n0\nENDBLK");
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nB\n10\n0\n20\n0");
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nA\n10\n5\n20\n0\n0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nA\n10\n0\n20\n0");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
    }

    // ---------------------------------------------------------------- huge coordinates

    [Theory]
    [InlineData("1E308")]
    [InlineData("-1E308")]
    [InlineData("1.7976931348623157E308")]
    [InlineData("4.9E-324")]
    [InlineData("123456789012345678901234567890")]
    public async Task Import_FiniteExtremeCoordinates_ReturnsPadWithFiniteVertices(string extreme)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", extreme, "20", "0", "10", extreme, "20", "1", "10", "0", "20", "1"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        AssertAllVerticesFinite(outcome);
        Assert.Single(outcome.Pads!.Pads);
    }

    [Theory]
    [InlineData("1E400")]
    [InlineData("-1E400")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task Import_NonFiniteCoordinateLiteral_IsRejectedWithGroupCode(string literal)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", literal, "20", "0", "10", literal, "20", "1", "10", "0", "20", "1"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, literal);
    }

    [Theory]
    [InlineData("1E400", 10)]
    [InlineData("Infinity", 20)]
    public async Task Import_FourCornerPadWithOneNonFiniteCorner_IsNotImportedAsTriangle(string literal, int groupCode)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "2", "20", "0"]);
        lines.AddRange(groupCode == 10 ? ["10", literal, "20", "2"] : ["10", "2", "20", literal]);
        lines.AddRange(["10", "0", "20", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, groupCode, literal);
        Assert.Null(outcome.Pads);
    }

    [Fact]
    public async Task Import_InsertTransformOverflowOnOneCorner_RejectsTheWholePad()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nCELL\n10\n0\n20\n0");
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "1", "20", "0", "10", "1", "20", "1", "10", "0", "20", "1E200"]);
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nCELL\n10\n0\n20\n0\n41\n1E200\n42\n1E200");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Null(outcome.Error);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_InsertScaleOverflow_DoesNotProduceNonFinitePads()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nCELL\n10\n0\n20\n0");
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1E200));
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nCELL\n10\n0\n20\n0\n41\n1E200\n42\n1E200");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Null(outcome.Error);
        AssertAllVerticesFinite(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    // ---------------------------------------------------------------- NaN and non-numeric values

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("-NaN")]
    public async Task Import_NanCoordinate_IsRejectedWithGroupCode(string literal)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", literal, "20", "0", "10", "2", "20", "2", "10", "0", "20", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, literal);
    }

    [Fact]
    public async Task Import_NanInLegacyPolylineVertex_IsRejectedWithGroupCode()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nPOLYLINE\n8\nPAD\n70\n1");
        AppendLines(lines, "0\nVERTEX\n10\n0\n20\n0\n0\nVERTEX\n10\nNaN\n20\nNaN\n0\nVERTEX\n10\n2\n20\n0\n0\nVERTEX\n10\n2\n20\n2\n0\nSEQEND");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, "NaN");
    }

    [Fact]
    public async Task Import_NonNumericCoordinate_ReportsClearErrorOrSkips()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\nabc\n20\n0\n10\n2\n20\n2");
        lines.AddRange(ClosedSquare("GOOD", 0, 0, 2));
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, "abc");
    }

    [Fact]
    public async Task Import_EmptyCoordinateValue_ReportsClearErrorOrSkips()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n\n20\n0\n10\n2\n20\n0\n10\n2\n20\n2");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, "");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(41)]
    [InlineData(42)]
    [InlineData(44)]
    [InlineData(45)]
    [InlineData(50)]
    public async Task Import_InvalidInsertNumber_ReportsGroupCodeAsync(int groupCode)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "2", "CELL", groupCode.ToString(CultureInfo.InvariantCulture), "bad"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, groupCode, "bad");
    }

    [Fact]
    public async Task Import_LongInvalidNumber_TruncatesReportedTextAsync()
    {
        var prefix = new string('x', 40);
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "10", prefix + "omitted"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, prefix);
        Assert.DoesNotContain("omitted", outcome.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_CommaDecimalSeparator_IsNotReadAsAnotherNumber()
    {
        // A file written with a European locale: "0,5" means 0.5 there. It must not be accepted as 5.
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0,5\n20\n0\n10\n2\n20\n0\n10\n2\n20\n2");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertInvalidNumber(outcome, 10, "0,5");
    }

    [Fact]
    public async Task Import_BlockTriangleInsertedWithTinyScale_SkipsInsteadOfThrowing()
    {
        // The three corners collapse to within 1e-9 of each other, so the first and last vertex count as a repeated closing vertex.
        var lines = new List<string>(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nTRI\n10\n0\n20\n0");
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n0\n20\n1");
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nTRI\n10\n0\n20\n0\n41\n1E-10\n42\n1E-10");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Null(outcome.Error);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_RingThatRepeatsTheFirstVertexTwiceAtTheEnd_SkipsInsteadOfThrowing()
    {
        // Vertices A, B, A, A: the parser removes one trailing A, TryCommitPolyline removes the next and leaves two vertices.
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n2\n20\n0\n10\n0\n20\n0\n10\n0\n20\n0");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_FewerThanThreeVertices_SkipsPolyline()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1");
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0");
        AppendLines(lines, "0\nLWPOLYLINE\n8\nPAD\n70\n1\n10\n0\n20\n0\n10\n1\n20\n1");
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    // ---------------------------------------------------------------- text encoding

    [Fact]
    public async Task Import_InvalidUtf8LayerName_ImportsPadWithReplacementCharacters()
    {
        // 0xA5 0xBB is a Big5 sequence and is not valid UTF-8; DXF files from older CAD tools use such code pages.
        byte[] big5LayerName = [0xA5, 0xBB, 0xA4, 0xA4];
        var bytes = BuildBytes(
            Encoding.ASCII.GetBytes("0\r\nSECTION\r\n2\r\nENTITIES\r\n"),
            Encoding.ASCII.GetBytes("0\r\nLWPOLYLINE\r\n8\r\n"),
            big5LayerName,
            Encoding.ASCII.GetBytes("\r\n70\r\n1\r\n10\r\n0\r\n20\r\n0\r\n10\r\n2\r\n20\r\n0\r\n10\r\n2\r\n20\r\n2\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n"));

        var outcome = await ImportFileAsync(bytes);

        AssertCompleted(outcome);
        var pad = Assert.Single(outcome.Pads!.Pads);
        Assert.False(string.IsNullOrWhiteSpace(pad.Layer));
        Assert.True(outcome.Catalog!.TryGetLayer(pad.Layer, out _));
    }

    [Fact]
    public async Task Import_Utf8BomAndUtf16WithBom_ReadTheSamePad()
    {
        var text = string.Join("\r\n", [.. SectionHeader("ENTITIES"), .. ClosedSquare("PAD", 0, 0, 2), .. SectionEnd()]) + "\r\n";

        foreach (var encoding in new Encoding[] { new UTF8Encoding(true), new UnicodeEncoding(false, true), new UnicodeEncoding(true, true) })
        {
            var outcome = await ImportFileAsync([.. encoding.GetPreamble(), .. encoding.GetBytes(text)]);

            AssertCompleted(outcome, encoding.WebName);
            var pad = Assert.Single(outcome.Pads!.Pads);
            Assert.Equal("PAD", pad.Layer);
        }
    }

    [Fact]
    public async Task Import_NulBytesInsideTextValues_NeverCrashes()
    {
        var text = string.Join("\r\n", [.. SectionHeader("ENTITIES"), .. ClosedSquare("PA\0D", 0, 0, 2), .. SectionEnd()]) + "\r\n";

        var outcome = await ImportFileAsync(Encoding.UTF8.GetBytes(text));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_InvalidUtf8InsideNumber_ReportsClearErrorOrSkips()
    {
        var bytes = BuildBytes(
            Encoding.ASCII.GetBytes("0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLWPOLYLINE\r\n8\r\nPAD\r\n70\r\n1\r\n10\r\n0\r\n20\r\n0\r\n10\r\n1.5"),
            _invalidNumberBytes,
            Encoding.ASCII.GetBytes("\r\n20\r\n0\r\n10\r\n2\r\n20\r\n2\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n"));

        var outcome = await ImportFileAsync(bytes);

        AssertInvalidNumber(outcome, 10, "1.5");
    }

    // ---------------------------------------------------------------- duplicate handles and names

    [Fact]
    public async Task Import_DuplicateEntityHandles_ImportsEveryEntityWithUniquePadIds()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        for (var copy = 0; copy < 3; copy++)
        {
            // Every entity claims handle 1F and the same owner handle.
            AppendLines(lines, "0\nLWPOLYLINE\n5\n1F\n330\n1\n100\nAcDbEntity\n8\nPAD\n100\nAcDbPolyline\n90\n4\n70\n1");
            lines.AddRange(["10", (copy * 10).ToString(CultureInfo.InvariantCulture), "20", "0", "10", (copy * 10 + 2).ToString(CultureInfo.InvariantCulture), "20", "0"]);
            lines.AddRange(["10", (copy * 10 + 2).ToString(CultureInfo.InvariantCulture), "20", "2", "10", (copy * 10).ToString(CultureInfo.InvariantCulture), "20", "2"]);
        }

        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Equal(3, outcome.Pads!.Pads.Count);
        Assert.Equal(3, outcome.Pads.Pads.Select(pad => pad.Id).Distinct().Count());
    }

    [Fact]
    public async Task Import_IdenticalEntityRepeated_KeepsEveryCopy()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        for (var copy = 0; copy < 4; copy++)
        {
            lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        }

        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Equal(4, outcome.Pads!.Pads.Count);
        Assert.Equal(4, outcome.Pads.Pads.Select(pad => pad.Id).Distinct().Count());
        Assert.True(outcome.Catalog!.TryGetLayer("PAD", out var layer));
        Assert.Equal(4, layer.EntityCount);
    }

    [Fact]
    public async Task Import_DuplicateBlockNamesAndRepeatedSections_AreDeterministic()
    {
        var lines = new List<string>();
        for (var copy = 0; copy < 2; copy++)
        {
            lines.AddRange(SectionHeader("BLOCKS"));
            AppendLines(lines, "0\nBLOCK\n8\n0\n2\nCELL\n5\nA1\n10\n0\n20\n0");
            lines.AddRange(ClosedSquare("PAD", 0, 0, copy + 1));
            AppendLines(lines, "0\nENDBLK\n5\nA2");
            lines.AddRange(SectionEnd());
            lines.AddRange(SectionHeader("ENTITIES"));
            AppendLines(lines, "0\nINSERT\n5\nB1\n8\n0\n2\ncell\n10\n0\n20\n0"); // block names compare case-insensitively
            lines.AddRange(SectionEnd());
        }

        var first = await ImportFileAsync(ToBytes(lines));
        var second = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(first);
        AssertCompleted(second);
        Assert.Equal(2, first.Pads!.Pads.Count);
        Assert.Equal(
            first.Pads.Pads.Select(pad => (pad.Id, pad.Layer, pad.Polygon.Vertices.Length)),
            second.Pads!.Pads.Select(pad => (pad.Id, pad.Layer, pad.Polygon.Vertices.Length)));
    }

    [Fact]
    public async Task Import_DuplicateLayerTableRecords_ListEachLayerOnce()
    {
        var lines = new List<string>(SectionHeader("TABLES"));
        AppendLines(lines, "0\nTABLE\n2\nLAYER\n5\n2\n70\n3");
        AppendLines(lines, "0\nLAYER\n5\n10\n2\nPAD\n70\n0");
        AppendLines(lines, "0\nLAYER\n5\n10\n2\nPAD\n70\n0");
        AppendLines(lines, "0\nLAYER\n5\n10\n2\npad\n70\n0");
        AppendLines(lines, "0\nENDTAB");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
        var padLayers = outcome.Catalog!.Layers.Where(item => string.Equals(item.Name, "PAD", StringComparison.OrdinalIgnoreCase)).ToList();
        var layer = Assert.Single(padLayers);
        Assert.True(layer.IsDefinedInLayerTable);
    }

    // ---------------------------------------------------------------- harness

    internal static void AppendLines(List<string> lines, string text)
    {
        lines.AddRange(text.Split('\n'));
    }

    private static void AssertInvalidNumber(ImportOutcome outcome, int groupCode, string badText)
    {
        AssertCompleted(outcome);
        var error = Assert.IsType<InvalidDataException>(outcome.Error);
        Assert.Contains(FormattableString.Invariant($"group code {groupCode}"), error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{badText}", error.Message, StringComparison.Ordinal);
    }

    internal static async Task<ImportOutcome> ImportFileAsync(byte[] bytes)
    {
        var path = TestFiles.NewTempFilePath("malformed-", ".dxf");
        TestFiles.WriteBytes(path, bytes);
        try
        {
            return await RunWithTimeoutAsync(() =>
            {
                var pads = DxfPadImporter.Import(path, out var catalog, new DxfImportOptions());
                return (pads, catalog);
            });
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Runs one import on a dedicated background thread so that a hung import fails the test and does not block the test host from exiting.
    /// </summary>
    private static async Task<ImportOutcome> RunWithTimeoutAsync(Func<(CadPadSet Pads, DxfLayerCatalog Catalog)> import)
    {
        var task = Task.Factory.StartNew(import, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            var (pads, catalog) = await task.WaitAsync(TimeSpan.FromMilliseconds(ImportTimeoutMilliseconds));
            return new ImportOutcome(pads, catalog, null, TimedOut: false);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            return new ImportOutcome(null, null, null, TimedOut: true);
        }
        catch (Exception ex) when (task.IsFaulted)
        {
            return new ImportOutcome(null, null, ex, TimedOut: false);
        }
    }

    /// <summary>The import ended in time and returned a result, or threw an <see cref="InvalidDataException"/> that explains the problem.</summary>
    internal static void AssertCompleted(ImportOutcome outcome, string? context = null)
    {
        var label = context is null ? string.Empty : $" [{context}]";
        Assert.False(outcome.TimedOut, $"The import did not finish within {ImportTimeoutMilliseconds} ms.{label}");

        if (outcome.Error is not null)
        {
            var clear = outcome.Error is InvalidDataException && !string.IsNullOrWhiteSpace(outcome.Error.Message);
            Assert.True(clear, $"The import threw {outcome.Error.GetType().Name}: {outcome.Error.Message}{label}");
            return;
        }

        Assert.NotNull(outcome.Pads);
        Assert.NotNull(outcome.Catalog);
    }

    private static void AssertAllVerticesFinite(ImportOutcome outcome)
    {
        if (outcome.Pads is null)
        {
            return;
        }

        foreach (var pad in outcome.Pads.Pads)
        {
            foreach (var vertex in pad.Polygon.Vertices)
            {
                Assert.True(double.IsFinite(vertex.X) && double.IsFinite(vertex.Y), $"Pad {pad.Id} on layer '{pad.Layer}' has the non-finite vertex ({vertex.X}, {vertex.Y}).");
            }
        }
    }

    private static List<string> ValidDocumentLines()
    {
        var lines = new List<string>(SectionHeader("TABLES"));
        AppendLines(lines, "0\nTABLE\n2\nLAYER\n70\n1\n0\nLAYER\n2\nPAD\n70\n0\n0\nENDTAB");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("BLOCKS"));
        AppendLines(lines, "0\nBLOCK\n8\n0\n2\nCELL\n10\n0\n20\n0");
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        AppendLines(lines, "0\nENDBLK");
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", -5, -5, 2));
        AppendLines(lines, "0\nINSERT\n8\n0\n2\nCELL\n10\n10\n20\n10");
        lines.AddRange(SectionEnd());
        AppendLines(lines, "0\nEOF");
        return lines;
    }

    internal static string[] SectionHeader(string name) => ["0", "SECTION", "2", name];

    internal static string[] SectionEnd() => ["0", "ENDSEC"];

    internal static string[] ClosedSquare(string layer, double x, double y, double size)
    {
        static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        return
        [
            "0", "LWPOLYLINE", "8", layer, "70", "1",
            "10", N(x), "20", N(y),
            "10", N(x + size), "20", N(y),
            "10", N(x + size), "20", N(y + size),
            "10", N(x), "20", N(y + size),
        ];
    }

    internal static byte[] ToBytes(IEnumerable<string> lines)
    {
        return Encoding.UTF8.GetBytes(string.Concat(lines.Select(static line => line + "\r\n")));
    }

    private static byte[] BuildBytes(params byte[][] parts)
    {
        return [.. parts.SelectMany(static part => part)];
    }

    internal sealed record ImportOutcome(
        CadPadSet? Pads,
        DxfLayerCatalog? Catalog,
        Exception? Error,
        bool TimedOut);
}
