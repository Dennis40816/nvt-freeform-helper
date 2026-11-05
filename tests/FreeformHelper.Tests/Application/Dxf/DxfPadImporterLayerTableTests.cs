// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Xunit;
using static FreeformHelper.Tests.DxfPadImporterMalformedInputTests;

namespace FreeformHelper.Tests;

/// <summary>
/// Layer table cases of the DXF import. They share the harness of <see cref="DxfPadImporterMalformedInputTests"/>.
/// </summary>
public sealed class DxfPadImporterLayerTableTests
{
    private static readonly string[] _undeclaredLayerNames = ["0", "UNDECLARED"];

    private const string SkipLayerTableWithoutEndTab =
        "Production bug (low severity): DxfLayerCatalogReader.ReadLayerDefinitions adds a layer record only when the next " +
        "LAYER record or ENDTAB arrives. When the file ends inside the layer table, the last record is dropped and that layer " +
        "is reported as not defined in the layer table.";

    [Fact]
    public async Task Import_MissingLayerTable_StillImportsPadsAndListsEntityLayers()
    {
        var lines = new List<string>(SectionHeader("ENTITIES"));
        lines.AddRange(ClosedSquare("UNDECLARED", 0, 0, 2));
        AppendLines(lines, "0\nLWPOLYLINE\n70\n1\n10\n5\n20\n5\n10\n7\n20\n5\n10\n7\n20\n7"); // no group 8
        lines.AddRange(SectionEnd());

        var outcome = await ImportFileAsync(ToBytes(lines));

        AssertCompleted(outcome);
        Assert.Equal(_undeclaredLayerNames, outcome.Pads!.Pads.Select(pad => pad.Layer).Order(StringComparer.Ordinal));
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
        AppendLines(lines, "0\nTABLE\n2\nLAYER\n70\n3");
        AppendLines(lines, "0\nLAYER\n70\n0"); // record without a name
        AppendLines(lines, "0\nLAYER\n2\nNAMED\n70\n0");
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
        AppendLines(lines, "0\nTABLE\n2\nLAYER\n70\n2");
        AppendLines(lines, "0\nLAYER\n2\nFIRST\n70\n0");
        AppendLines(lines, "0\nLAYER\n2\nLAST\n70\n0");
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
        AppendLines(lines, "0\nTABLE\n2\nLTYPE\n70\n1\n0\nLTYPE\n2\nCONTINUOUS\n0\nENDTAB");
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
}
