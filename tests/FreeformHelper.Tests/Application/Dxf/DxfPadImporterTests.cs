using FreeformHelper.Infrastructure.Dxf;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfPadImporterTests
{
    private static readonly string[] NestedInsertLayerInheritanceDxfLines =
    [
        "0",
        "SECTION",
        "2",
        "BLOCKS",
        "0",
        "BLOCK",
        "8",
        "0",
        "2",
        "CHILD",
        "10",
        "0",
        "20",
        "0",
        "0",
        "LWPOLYLINE",
        "8",
        "0",
        "70",
        "1",
        "10",
        "0",
        "20",
        "0",
        "10",
        "2",
        "20",
        "0",
        "10",
        "2",
        "20",
        "2",
        "10",
        "0",
        "20",
        "2",
        "0",
        "ENDBLK",
        "0",
        "BLOCK",
        "8",
        "0",
        "2",
        "ROOT",
        "10",
        "0",
        "20",
        "0",
        "0",
        "INSERT",
        "8",
        "0",
        "2",
        "CHILD",
        "10",
        "10",
        "20",
        "0",
        "0",
        "INSERT",
        "8",
        "0",
        "2",
        "CHILD",
        "10",
        "30",
        "20",
        "0",
        "41",
        "-1",
        "42",
        "1",
        "0",
        "ENDBLK",
        "0",
        "ENDSEC",
        "0",
        "SECTION",
        "2",
        "ENTITIES",
        "0",
        "INSERT",
        "8",
        "regular",
        "2",
        "ROOT",
        "10",
        "100",
        "20",
        "0",
        "0",
        "ENDSEC",
        "0",
        "EOF",
    ];

    [Fact]
    public void Import_ClosedPolyline_ReturnsPad()
    {
        var path = WriteTempDxf(BuildClosedLwPolyline("PAD"));
        try
        {
            var importer = new DxfPadImporter();
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = true
            };

            var set = DxfPadImporter.Import(path, options);

            Assert.Single(set.Pads);
            Assert.Equal("PAD", set.Pads[0].Layer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_OpenPolyline_SkipsWhenClosedOnly()
    {
        var path = WriteTempDxf(BuildOpenLwPolyline("PADS"));
        try
        {
            var importer = new DxfPadImporter();
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = true
            };

            var set = DxfPadImporter.Import(path, options);

            Assert.Empty(set.Pads);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_BlockPolyline_IncludedByDefault()
    {
        var path = WriteTempDxf(BuildDxfWithEntityAndBlock("ENTITY", "regular"));
        try
        {
            var importer = new DxfPadImporter();
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = true,
            };

            var set = DxfPadImporter.Import(path, options);

            Assert.Equal(2, set.Pads.Count);
            Assert.Contains(set.Pads, pad => pad.Layer == "ENTITY");
            Assert.Contains(set.Pads, pad => pad.Layer == "regular");
            var regular = set.Pads.Single(pad => pad.Layer == "regular");
            Assert.InRange(regular.Centroid.X, 101.9, 102.1);
            Assert.InRange(regular.Centroid.Y, 201.9, 202.1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_BlockPolyline_SkippedWhenDisabled()
    {
        var path = WriteTempDxf(BuildDxfWithEntityAndBlock("ENTITY", "regular"));
        try
        {
            var importer = new DxfPadImporter();
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = true,
                IncludeBlockPolylines = false,
            };

            var set = DxfPadImporter.Import(path, options);

            Assert.Single(set.Pads);
            Assert.Equal("ENTITY", set.Pads[0].Layer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_NestedInsert_ResolvesTransformAndLayerInheritance()
    {
        var path = WriteTempDxf(BuildDxfWithNestedInsertLayerInheritance());
        try
        {
            var importer = new DxfPadImporter();
            var options = new DxfImportOptions
            {
                OnlyClosedPolylines = true,
                IncludeBlockPolylines = true,
            };

            var set = DxfPadImporter.Import(path, options);

            Assert.Equal(2, set.Pads.Count);
            Assert.All(set.Pads, pad => Assert.Equal("regular", pad.Layer));
            Assert.InRange(set.Pads.Min(pad => pad.Bounds.MinX), 109.9, 110.1);
            Assert.InRange(set.Pads.Max(pad => pad.Bounds.MaxX), 129.9, 130.1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteTempDxf(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cad_test_{Guid.NewGuid():N}.dxf");
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildClosedLwPolyline(string layer)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "0",
            "SECTION",
            "2",
            "ENTITIES",
            "0",
            "LWPOLYLINE",
            "8",
            layer,
            "70",
            "1",
            "10",
            "0",
            "20",
            "0",
            "10",
            "10",
            "20",
            "0",
            "10",
            "10",
            "20",
            "10",
            "10",
            "0",
            "20",
            "10",
            "0",
            "ENDSEC",
            "0",
            "EOF",
        });
    }

    private static string BuildOpenLwPolyline(string layer)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "0",
            "SECTION",
            "2",
            "ENTITIES",
            "0",
            "LWPOLYLINE",
            "8",
            layer,
            "70",
            "0",
            "10",
            "0",
            "20",
            "0",
            "10",
            "10",
            "20",
            "0",
            "10",
            "10",
            "20",
            "10",
            "10",
            "0",
            "20",
            "10",
            "0",
            "ENDSEC",
            "0",
            "EOF",
        });
    }

    private static string BuildDxfWithEntityAndBlock(string entityLayer, string blockLayer)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "0",
            "SECTION",
            "2",
            "BLOCKS",
            "0",
            "BLOCK",
            "8",
            "0",
            "2",
            "B1",
            "10",
            "0",
            "20",
            "0",
            "0",
            "LWPOLYLINE",
            "8",
            blockLayer,
            "70",
            "1",
            "10",
            "0",
            "20",
            "0",
            "10",
            "4",
            "20",
            "0",
            "10",
            "4",
            "20",
            "4",
            "10",
            "0",
            "20",
            "4",
            "0",
            "ENDBLK",
            "0",
            "ENDSEC",
            "0",
            "SECTION",
            "2",
            "ENTITIES",
            "0",
            "LWPOLYLINE",
            "8",
            entityLayer,
            "70",
            "1",
            "10",
            "10",
            "20",
            "10",
            "10",
            "14",
            "20",
            "10",
            "10",
            "14",
            "20",
            "14",
            "10",
            "10",
            "20",
            "14",
            "0",
            "INSERT",
            "8",
            "0",
            "2",
            "B1",
            "10",
            "100",
            "20",
            "200",
            "0",
            "ENDSEC",
            "0",
            "EOF",
        });
    }

    private static string BuildDxfWithNestedInsertLayerInheritance()
    {
        return string.Join(Environment.NewLine, NestedInsertLayerInheritanceDxfLines);
    }
}
