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
/// Tests with a <c>Skip</c> reason describe a defect found in production code. They are kept as the executable
/// description of the defect and are not fixed here.
/// </remarks>
public sealed class DxfPadImporterMalformedInputTests
{
    private const int ImportTimeoutMilliseconds = 10_000;

    private const string SkipNonNumericValue =
        "Production bug: DxfPadImporter.ParseDouble uses double.Parse, so a non-numeric value for a coordinate or scale " +
        "group code (for example a file cut after '-' or 'abc') throws a bare FormatException that does not name the " +
        "group code or the line. The import aborts instead of skipping the entity or reporting a DXF error.";

    private const string SkipNonFiniteCoordinate =
        "Production bug: TryCommitPolyline and NormalizeVertices filter NaN only. 'Infinity' or an overflowing literal " +
        "such as 1e400 parses to infinity and becomes a pad vertex, so Bounds and Area are not finite for downstream code.";

    private const string SkipDegenerateClosedRing =
        "Production bug: TryCommitPolyline counts vertices before it removes a repeated closing vertex. A ring of three " +
        "vertices whose first and last vertex are within 1e-9 (a block triangle inserted with scale 1e-10, or vertices " +
        "A, B, A, A) passes the count check, drops to two vertices and Polygon2 throws ArgumentException.";

    private const string SkipThousandsSeparator =
        "Production bug: ParseDouble calls double.Parse(string, IFormatProvider), which also allows a thousands separator. " +
        "The locale-style literal \"0,5\" is therefore read silently as 5, a wrong coordinate with no error or warning.";

    private const string SkipInsertArrayExplosion =
        "Production bug: ParseInsert accepts any int for the INSERT column and row counts (group codes 70 and 71) and " +
        "ExpandInsert loops columns x rows with no cap. A few bytes of input can ask for billions of pads and the import " +
        "does not finish.";

    private const string SkipLayerTableWithoutEndTab =
        "Production bug (low severity): DxfLayerCatalogReader.ReadLayerDefinitions adds a layer record only when the next " +
        "LAYER record or ENDTAB arrives. When the file ends inside the layer table, the last record is dropped and that layer " +
        "is reported as not defined in the layer table.";

    private const string SkipNestedInsertExplosion =
        "Production bug: ExpandInsert guards only against a block that contains itself. A chain of blocks that each " +
        "insert the next one several times grows exponentially with depth, with no cap on expanded pads or depth.";

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
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "5", "20", "0", "10", "5"]);

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

    [Fact(Skip = SkipNonNumericValue)]
    public async Task Import_TruncatedInsideNumber_ReportsClearErrorOrSkips()
    {
        // The file ends in the middle of the literal "-1.5" so the last value is just "-".
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "3", "20", "0", "10", "-"]);

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
    }

    [Fact]
    public async Task Import_TruncatedPolylineWithoutSeqend_DoesNotLeakIntoNextEntity()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "POLYLINE", "8", "PAD", "70", "1", "0", "VERTEX", "10", "0", "20", "0", "0", "VERTEX", "10", "2", "20", "0"]);
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
        lines.AddRange(["0", "SPLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "1", "20", "0", "10", "1", "20", "1"]);
        lines.AddRange(["0", "3DSOLID", "8", "PAD", "1", "binary-ish payload", "10", "9", "20", "9"]);
        lines.AddRange(["0", "X_NOT_A_REAL_ENTITY", "8", "PAD", "999", "comment"]);
        lines.AddRange(["0", "HATCH", "8", "PAD", "70", "1"]);
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        lines.AddRange(["0", "ACAD_PROXY_ENTITY", "8", "PAD", "92", "1048576"]);
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
        lines.AddRange(["0", "POLYLINE", "8", "PAD", "70", "1"]);
        lines.AddRange(["0", "VERTEX", "10", "0", "20", "0", "0", "VERTEX", "10", "2", "20", "0"]);
        lines.AddRange(["0", "MYSTERY", "8", "PAD"]);
        lines.AddRange(["0", "VERTEX", "10", "2", "20", "2", "0", "SEQEND"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.All(outcome.Pads!.Pads, pad => Assert.True(pad.Polygon.Vertices.Length >= 3));
    }

    [Fact]
    public async Task Import_EntityWithoutTypeName_AndUnnamedBlock_AreSkipped()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "10", "0", "20", "0"]); // no group 2 name
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        lines.AddRange(["0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "", "8", "PAD"]); // empty entity type
        lines.AddRange(["0", "INSERT", "8", "0", "10", "0", "20", "0"]); // no block name
        lines.AddRange(["0", "INSERT", "8", "0", "2", "MISSING_BLOCK", "10", "0", "20", "0"]); // unknown block
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_MutuallyRecursiveBlocks_TerminateWithoutStackOverflow()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "A", "10", "0", "20", "0"]);
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "B", "10", "5", "20", "0", "0", "ENDBLK"]);
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "B", "10", "0", "20", "0"]);
        lines.AddRange(["0", "INSERT", "8", "0", "2", "A", "10", "5", "20", "0", "0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "A", "10", "0", "20", "0"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
    }

    [Fact(Skip = SkipInsertArrayExplosion)]
    public async Task Import_InsertWithHugeArrayCounts_FinishesOrReportsError()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "CELL", "10", "0", "20", "0"]);
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        lines.AddRange(["0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "CELL", "10", "0", "20", "0", "70", "2000000000", "71", "2000000000", "44", "2", "45", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
    }

    [Fact(Skip = SkipNestedInsertExplosion)]
    public async Task Import_NestedInsertChainWithExponentialGrowth_FinishesOrReportsError()
    {
        // 12 levels, each inserting the next level 10 times: 10^12 expanded pads from under 1 KB of input.
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

            lines.AddRange(["0", "ENDBLK"]);
        }

        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "L0", "10", "0", "20", "0"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
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

    [Theory(Skip = SkipNonFiniteCoordinate)]
    [InlineData("1E400")]
    [InlineData("-1E400")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task Import_NonFiniteCoordinateLiteral_IsNotImportedAsPadVertex(string literal)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", literal, "20", "0", "10", literal, "20", "1", "10", "0", "20", "1"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        AssertAllVerticesFinite(outcome);
    }

    [Fact(Skip = SkipNonFiniteCoordinate)]
    public async Task Import_InsertScaleOverflow_DoesNotProduceNonFinitePads()
    {
        var lines = new List<string>(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "CELL", "10", "0", "20", "0"]);
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1E200));
        lines.AddRange(["0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "CELL", "10", "0", "20", "0", "41", "1E200", "42", "1E200"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        AssertAllVerticesFinite(outcome);
    }

    // ---------------------------------------------------------------- NaN and non-numeric values

    [Theory]
    [InlineData("NaN")]
    [InlineData("nan")]
    [InlineData("-NaN")]
    public async Task Import_NanCoordinate_NeverProducesNanVertex(string literal)
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        // Square with one NaN vertex, then a triangle with one NaN vertex (too few valid vertices left).
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", literal, "20", "0", "10", "2", "20", "2", "10", "0", "20", "2"]);
        lines.AddRange(["0", "LWPOLYLINE", "8", "TRI", "70", "1", "10", "0", "20", "0", "10", "2", "20", literal, "10", "0", "20", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        AssertAllVerticesFinite(outcome);
        Assert.DoesNotContain(outcome.Pads!.Pads, pad => pad.Layer == "TRI");
    }

    [Fact]
    public async Task Import_NanInLegacyPolylineVertex_IsSkippedWithoutCrash()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "POLYLINE", "8", "PAD", "70", "1"]);
        lines.AddRange(["0", "VERTEX", "10", "0", "20", "0", "0", "VERTEX", "10", "NaN", "20", "NaN", "0", "VERTEX", "10", "2", "20", "0", "0", "VERTEX", "10", "2", "20", "2", "0", "SEQEND"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        AssertAllVerticesFinite(outcome);
    }

    [Fact(Skip = SkipNonNumericValue)]
    public async Task Import_NonNumericCoordinate_ReportsClearErrorOrSkips()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "abc", "20", "0", "10", "2", "20", "2"]);
        lines.AddRange(ClosedSquare("GOOD", 0, 0, 2));
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
    }

    [Fact(Skip = SkipNonNumericValue)]
    public async Task Import_EmptyCoordinateValue_ReportsClearErrorOrSkips()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "", "20", "0", "10", "2", "20", "0", "10", "2", "20", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
    }

    [Fact(Skip = SkipThousandsSeparator)]
    public async Task Import_CommaDecimalSeparator_IsNotReadAsAnotherNumber()
    {
        // A file written with a European locale: "0,5" means 0.5 there. It must not be accepted as 5.
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0,5", "20", "0", "10", "2", "20", "0", "10", "2", "20", "2"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.DoesNotContain(
            outcome.Pads?.Pads.SelectMany(pad => pad.Polygon.Vertices) ?? [],
            vertex => vertex.X == 5);
    }

    [Fact(Skip = SkipDegenerateClosedRing)]
    public async Task Import_BlockTriangleInsertedWithTinyScale_SkipsInsteadOfThrowing()
    {
        // The three corners collapse to within 1e-9 of each other, so the first and last vertex count as a repeated closing vertex.
        var lines = new List<string>(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "TRI", "10", "0", "20", "0"]);
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "1", "20", "0", "10", "0", "20", "1"]);
        lines.AddRange(["0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "TRI", "10", "0", "20", "0", "41", "1E-10", "42", "1E-10"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
    }

    [Fact(Skip = SkipDegenerateClosedRing)]
    public async Task Import_RingThatRepeatsTheFirstVertexTwiceAtTheEnd_SkipsInsteadOfThrowing()
    {
        // Vertices A, B, A, A: the parser removes one trailing A, TryCommitPolyline removes the next and leaves two vertices.
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "2", "20", "0", "10", "0", "20", "0", "10", "0", "20", "0"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    [Fact]
    public async Task Import_FewerThanThreeVertices_SkipsPolyline()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1"]);
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0"]);
        lines.AddRange(["0", "LWPOLYLINE", "8", "PAD", "70", "1", "10", "0", "20", "0", "10", "1", "20", "1"]);
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Empty(outcome.Pads!.Pads);
    }

    // ---------------------------------------------------------------- layer table

    [Fact]
    public async Task Import_MissingLayerTable_StillImportsPadsAndListsEntityLayers()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("UNDECLARED", 0, 0, 2));
        lines.AddRange(["0", "LWPOLYLINE", "70", "1", "10", "5", "20", "5", "10", "7", "20", "5", "10", "7", "20", "7"]); // no group 8
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Equal(["0", "UNDECLARED"], outcome.Pads!.Pads.Select(pad => pad.Layer).Order(StringComparer.Ordinal));
        Assert.True(outcome.Catalog!.TryGetLayer("UNDECLARED", out var layer));
        Assert.False(layer.IsDefinedInLayerTable);
        Assert.True(layer.HasPolylineContent);
        Assert.True(outcome.Catalog.TryGetLayer("0", out var defaultLayer));
        Assert.False(defaultLayer.IsDefinedInLayerTable);
    }

    [Fact]
    public async Task Import_UnterminatedOrNamelessLayerTable_NeverCrashes()
    {
        var lines = new List<string>(SectionHeader("TABLES"));
        lines.AddRange(["0", "TABLE", "2", "LAYER", "70", "3"]);
        lines.AddRange(["0", "LAYER", "70", "0"]); // record without a name
        lines.AddRange(["0", "LAYER", "2", "NAMED", "70", "0"]);
        // No ENDTAB and no ENDSEC: the file stops here.
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("NAMED", 0, 0, 2));
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
        Assert.True(outcome.Catalog!.TryGetLayer("NAMED", out _));
        Assert.DoesNotContain(outcome.Catalog.Layers, item => string.IsNullOrWhiteSpace(item.Name));
    }

    [Fact(Skip = SkipLayerTableWithoutEndTab)]
    public async Task Import_LayerTableCutBeforeEndTab_KeepsTheLastLayerRecord()
    {
        var lines = new List<string>(SectionHeader("TABLES"));
        lines.AddRange(["0", "TABLE", "2", "LAYER", "70", "2"]);
        lines.AddRange(["0", "LAYER", "2", "FIRST", "70", "0"]);
        lines.AddRange(["0", "LAYER", "2", "LAST", "70", "0"]);
        // The file was cut here: no ENDTAB, no ENDSEC.

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.True(outcome.Catalog!.TryGetLayer("FIRST", out var first));
        Assert.True(first.IsDefinedInLayerTable);
        Assert.True(outcome.Catalog.TryGetLayer("LAST", out var last));
        Assert.True(last.IsDefinedInLayerTable);
    }

    [Fact]
    public async Task Import_TablesSectionWithoutLayerTable_ReportsLayersAsUndeclared()
    {
        var lines = new List<string>(SectionHeader("TABLES"));
        lines.AddRange(["0", "TABLE", "2", "LTYPE", "70", "1", "0", "LTYPE", "2", "CONTINUOUS", "0", "ENDTAB"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", 0, 0, 2));
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Single(outcome.Pads!.Pads);
        Assert.True(outcome.Catalog!.TryGetLayer("PAD", out var layer));
        Assert.False(layer.IsDefinedInLayerTable);
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

    [Fact(Skip = SkipNonNumericValue)]
    public async Task Import_InvalidUtf8InsideNumber_ReportsClearErrorOrSkips()
    {
        var bytes = BuildBytes(
            Encoding.ASCII.GetBytes("0\r\nSECTION\r\n2\r\nENTITIES\r\n0\r\nLWPOLYLINE\r\n8\r\nPAD\r\n70\r\n1\r\n10\r\n0\r\n20\r\n0\r\n10\r\n1.5"),
            [0xFF, 0xFE],
            Encoding.ASCII.GetBytes("\r\n20\r\n0\r\n10\r\n2\r\n20\r\n2\r\n0\r\nENDSEC\r\n0\r\nEOF\r\n"));

        var outcome = await ImportFileAsync(bytes);

        AssertCompleted(outcome);
    }

    // ---------------------------------------------------------------- duplicate handles and names

    [Fact]
    public async Task Import_DuplicateEntityHandles_ImportsEveryEntityWithUniquePadIds()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        for (var copy = 0; copy < 3; copy++)
        {
            // Every entity claims handle 1F and the same owner handle.
            lines.AddRange(["0", "LWPOLYLINE", "5", "1F", "330", "1", "100", "AcDbEntity", "8", "PAD", "100", "AcDbPolyline", "90", "4", "70", "1"]);
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
            lines.AddRange(["0", "BLOCK", "8", "0", "2", "CELL", "5", "A1", "10", "0", "20", "0"]);
            lines.AddRange(ClosedSquare("PAD", 0, 0, copy + 1));
            lines.AddRange(["0", "ENDBLK", "5", "A2"]);
            lines.AddRange(SectionEnd());
            lines.AddRange(SectionHeader("ENTITIES"));
            lines.AddRange(["0", "INSERT", "5", "B1", "8", "0", "2", "cell", "10", "0", "20", "0"]); // block names compare case-insensitively
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
        lines.AddRange(["0", "TABLE", "2", "LAYER", "5", "2", "70", "3"]);
        lines.AddRange(["0", "LAYER", "5", "10", "2", "PAD", "70", "0"]);
        lines.AddRange(["0", "LAYER", "5", "10", "2", "PAD", "70", "0"]);
        lines.AddRange(["0", "LAYER", "5", "10", "2", "pad", "70", "0"]);
        lines.AddRange(["0", "ENDTAB"]);
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

    private static async Task<ImportOutcome> ImportFileAsync(byte[] bytes)
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
    private static void AssertCompleted(ImportOutcome outcome, string? context = null)
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
        lines.AddRange(["0", "TABLE", "2", "LAYER", "70", "1", "0", "LAYER", "2", "PAD", "70", "0", "0", "ENDTAB"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("BLOCKS"));
        lines.AddRange(["0", "BLOCK", "8", "0", "2", "CELL", "10", "0", "20", "0"]);
        lines.AddRange(ClosedSquare("PAD", 0, 0, 1));
        lines.AddRange(["0", "ENDBLK"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("PAD", -5, -5, 2));
        lines.AddRange(["0", "INSERT", "8", "0", "2", "CELL", "10", "10", "20", "10"]);
        lines.AddRange(SectionEnd());
        lines.AddRange(["0", "EOF"]);
        return lines;
    }

    private static string[] SectionHeader(string name) => ["0", "SECTION", "2", name];

    private static string[] SectionEnd() => ["0", "ENDSEC"];

    private static string[] ClosedSquare(string layer, double x, double y, double size)
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

    private static byte[] ToBytes(IEnumerable<string> lines)
    {
        return Encoding.UTF8.GetBytes(string.Concat(lines.Select(static line => line + "\r\n")));
    }

    private static byte[] BuildBytes(params byte[][] parts)
    {
        return [.. parts.SelectMany(static part => part)];
    }

    private sealed record ImportOutcome(
        CadPadSet? Pads,
        DxfLayerCatalog? Catalog,
        Exception? Error,
        bool TimedOut);
}
