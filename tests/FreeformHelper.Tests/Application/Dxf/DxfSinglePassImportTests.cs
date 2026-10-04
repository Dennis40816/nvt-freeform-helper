using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Dxf;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfSinglePassImportTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TwoPassImport_PreservesLayerDefinitionsAndEntityCategories(int fixture)
    {
        var path = WriteTempDxf(fixture);
        try
        {
            var pads = DxfPadImporter.Import(path);
            var catalog = DxfLayerCatalogReader.ReadFromPath(path);

            Assert.Equal(ExpectedPadLayers[fixture], pads.Pads.Select(static pad => pad.Layer));
            Assert.Equal(ExpectedLayers[fixture], catalog.Layers.Select(static layer =>
                (layer.Name, layer.IsDefinedInLayerTable, layer.ContentKinds, layer.EntityCount)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, true, false)]
    [InlineData(0, false, true)]
    [InlineData(0, false, false)]
    [InlineData(1, true, true)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    [InlineData(1, false, false)]
    [InlineData(2, true, true)]
    [InlineData(2, true, false)]
    [InlineData(2, false, true)]
    [InlineData(2, false, false)]
    [InlineData(3, true, true)]
    [InlineData(3, true, false)]
    [InlineData(3, false, true)]
    [InlineData(3, false, false)]
    public void Import_WithCatalog_EqualsTwoPassImport(int fixture, bool onlyClosed, bool includeBlocks)
    {
        var path = WriteTempDxf(fixture);
        try
        {
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = onlyClosed,
                IncludeBlockPolylines = includeBlocks,
            };

            // Keep the original public calls as the two-pass comparison.
            var expectedPads = DxfPadImporter.Import(path, options);
            var expectedCatalog = DxfLayerCatalogReader.ReadFromPath(path);

            var actualPads = DxfPadImporter.Import(path, out var actualCatalog, options);

            AssertPadsEqual(expectedPads, actualPads);
            AssertCatalogsEqual(expectedCatalog, actualCatalog);
        }
        finally
        {
            File.Delete(path);
        }
    }

    internal static void AssertPadsEqual(CadPadSet expected, CadPadSet actual)
    {
        Assert.Equal(expected.Bounds, actual.Bounds);
        Assert.Equal(expected.Pads.Count, actual.Pads.Count);
        for (var i = 0; i < expected.Pads.Count; i++)
        {
            var expectedPad = expected.Pads[i];
            var actualPad = actual.Pads[i];
            Assert.Equal(expectedPad.Id, actualPad.Id);
            Assert.Equal(expectedPad.Name, actualPad.Name);
            Assert.Equal(expectedPad.Layer, actualPad.Layer);
            Assert.Equal(expectedPad.Polygon.Vertices.ToArray(), actualPad.Polygon.Vertices.ToArray());
            Assert.Equal(expectedPad.Bounds, actualPad.Bounds);
            Assert.Equal(expectedPad.Area, actualPad.Area);
            Assert.Equal(expectedPad.Centroid, actualPad.Centroid);
        }
    }

    internal static void AssertCatalogsEqual(DxfLayerCatalog expected, DxfLayerCatalog actual)
    {
        Assert.Equal(expected.Layers.Count, actual.Layers.Count);
        Assert.Equal(expected.ByName.Keys, actual.ByName.Keys);
        for (var i = 0; i < expected.Layers.Count; i++)
        {
            var expectedLayer = expected.Layers[i];
            var actualLayer = actual.Layers[i];
            Assert.Equal(expectedLayer.Name, actualLayer.Name);
            Assert.Equal(expectedLayer.IsDefinedInLayerTable, actualLayer.IsDefinedInLayerTable);
            Assert.Equal(expectedLayer.ContentKinds, actualLayer.ContentKinds);
            Assert.Equal(expectedLayer.EntityCount, actualLayer.EntityCount);
            Assert.Equal(expectedLayer.HasPolylineContent, actualLayer.HasPolylineContent);
            Assert.Equal(expectedLayer.HasTextContent, actualLayer.HasTextContent);
            Assert.Equal(expectedLayer.HasBlockSectionContent, actualLayer.HasBlockSectionContent);
            Assert.Equal(expectedLayer.HasEntitySectionContent, actualLayer.HasEntitySectionContent);
            Assert.True(actual.TryGetLayer(expectedLayer.Name.ToUpperInvariant(), out var indexedLayer));
            Assert.Same(actualLayer, indexedLayer);
        }
    }

    internal static string WriteTempDxf(int fixture)
    {
        var path = Path.Combine(Path.GetTempPath(), $"dxf-single-pass-{Guid.NewGuid():N}.dxf");
        File.WriteAllText(path, DxfTexts[fixture]);
        return path;
    }

    private static readonly string[][] ExpectedPadLayers =
    [
        ["Pads", "Spare"],
        ["Regular", "Explicit"],
        [],
        ["技術"],
    ];

    private static readonly (string Name, bool Defined, DxfLayerContentKind Kinds, int Count)[][] ExpectedLayers =
    [
        [
            ("Empty", true, DxfLayerContentKind.None, 0),
            ("Notes", true, DxfLayerContentKind.Text | DxfLayerContentKind.EntitySection, 1),
            ("Pads", true, DxfLayerContentKind.Polyline | DxfLayerContentKind.EntitySection, 2),
            ("Spare", false, DxfLayerContentKind.Polyline | DxfLayerContentKind.EntitySection, 1),
            ("Undeclared", false, DxfLayerContentKind.Other | DxfLayerContentKind.EntitySection, 1),
        ],
        [
            ("0", false, DxfLayerContentKind.Polyline | DxfLayerContentKind.BlockSection, 1),
            ("BlockOnly", true, DxfLayerContentKind.Text | DxfLayerContentKind.BlockSection, 1),
            ("Empty", true, DxfLayerContentKind.None, 0),
            ("Explicit", false, DxfLayerContentKind.Polyline | DxfLayerContentKind.BlockSection, 1),
            ("Regular", true, DxfLayerContentKind.Insert | DxfLayerContentKind.EntitySection, 1),
        ],
        [
            ("Empty", true, DxfLayerContentKind.None, 0),
        ],
        [
            ("0", false, DxfLayerContentKind.Text | DxfLayerContentKind.Other | DxfLayerContentKind.EntitySection, 6),
            ("Ghost", false, DxfLayerContentKind.Other | DxfLayerContentKind.EntitySection, 1),
            ("技術", false, DxfLayerContentKind.Polyline | DxfLayerContentKind.EntitySection, 1),
        ],
    ];

    // Small ASCII DXFs keep the old two-pass baseline independent of private example data.
    private static readonly string[] DxfTexts =
    [
        string.Join('\n',
            "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER",
            "0\nLAYER\n2\nPads\n0\nLAYER\n2\npads\n0\nLAYER\n2\nEmpty\n0\nLAYER\n2\nNotes",
            "0\nENDTAB\n0\nENDSEC\n0\nSECTION\n2\nENTITIES",
            "0\nLWPOLYLINE\n8\nPads\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n1\n20\n1\n10\n0\n20\n1",
            "0\nLWPOLYLINE\n8\npads\n70\n0\n10\n2\n20\n0\n10\n3\n20\n0\n10\n3\n20\n1\n10\n2\n20\n1",
            "0\nLWPOLYLINE\n8\nSpare\n70\n1\n10\n5\n20\n0\n10\n6\n20\n0\n10\n6\n20\n1\n10\n5\n20\n1",
            "0\nTEXT\n8\nNotes\n1\nLabel\n0\nLINE\n8\nUndeclared\n10\n0\n20\n0\n11\n1\n21\n1",
            "0\nENDSEC\n0\nEOF"),
        string.Join('\n',
            "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER",
            "0\nLAYER\n2\nEmpty\n0\nLAYER\n2\nBlockOnly\n0\nLAYER\n2\nRegular\n0\nENDTAB\n0\nENDSEC",
            "0\nSECTION\n2\nBLOCKS\n0\nBLOCK\n8\n0\n2\nB1\n10\n0\n20\n0",
            "0\nLWPOLYLINE\n8\n0\n70\n1\n10\n0\n20\n0\n10\n1\n20\n0\n10\n1\n20\n1\n10\n0\n20\n1",
            "0\nTEXT\n8\nBlockOnly\n1\nBlock label",
            "0\nLWPOLYLINE\n8\nExplicit\n70\n1\n10\n2\n20\n0\n10\n3\n20\n0\n10\n3\n20\n1\n10\n2\n20\n1",
            "0\nENDBLK\n0\nENDSEC\n0\nSECTION\n2\nENTITIES",
            "0\nINSERT\n8\nRegular\n2\nB1\n10\n10\n20\n20\n41\n2\n42\n3",
            "0\nENDSEC\n0\nEOF"),
        string.Join('\n',
            "0\nSECTION\n2\nTABLES\n0\nTABLE\n2\nLAYER\n0\nLAYER\n2\nEmpty\n0\nENDTAB\n0\nENDSEC",
            "0\nSECTION\n2\nENTITIES\n0\nENDSEC\n0\nEOF"),
        string.Join('\n',
            "invalid-code\nignored value\n-999\nmetadata\n0\nSECTION\n +0002 \n ENTITIES ",
            "0\nPOLYLINE\n +0008 \n 技術 \n70\n1\n2147483647\nunknown\n-2147483648\nunknown",
            "0\nVERTEX\n10\n0\n20\n0\n0\nVERTEX\n10\n1\n20\n0",
            "bad-code\nignored value\n0\nVERTEX\n10\n1\n20\n1\n0\nVERTEX\n10\n0\n20\n1\n0\nSEQEND",
            "0\nTEXT\n8\n   \n1\nDefault layer\n999\ncomment\n0\nCIRCLE\n8\nGhost\n40\n1",
            "0\nENDSEC\n0\nEOF\n8"),
    ];
}
