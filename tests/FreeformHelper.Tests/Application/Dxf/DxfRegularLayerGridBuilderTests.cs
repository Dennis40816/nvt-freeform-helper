using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfRegularLayerGridBuilderTests
{
    [Fact]
    public void BuildFromLayer_AssignsRowMajorFromBottomLeft()
    {
        var pads = new[]
        {
            CreatePad(100, "D", 10, 0),
            CreatePad(101, "A", 0, 10),
            CreatePad(102, "C", 0, 0),
            CreatePad(103, "B", 10, 10),
        };
        var set = new CadPadSet(pads);
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
        };
        _ = new DxfRegularLayerGridBuilder();
        var grid = DxfRegularLayerGridBuilder.BuildFromLayer(set, settings);

        Assert.Equal(2, grid.Rows);
        Assert.Equal(2, grid.Cols);
        Assert.Equal(4, grid.Pads.Count);

        Assert.Equal(0, grid.GetPad(0, 0).Bounds.MinX, 6);
        Assert.Equal(0, grid.GetPad(0, 0).Bounds.MinY, 6);
        Assert.Equal(10, grid.GetPad(0, 1).Bounds.MinX, 6);
        Assert.Equal(0, grid.GetPad(0, 1).Bounds.MinY, 6);
        Assert.Equal(0, grid.GetPad(1, 0).Bounds.MinX, 6);
        Assert.Equal(10, grid.GetPad(1, 0).Bounds.MinY, 6);
        Assert.Equal(10, grid.GetPad(1, 1).Bounds.MinX, 6);
        Assert.Equal(10, grid.GetPad(1, 1).Bounds.MinY, 6);
    }

    [Fact]
    public void BuildFromLayer_ThrowsWhenCountMismatch()
    {
        var pads = new[]
        {
            CreatePad(1, "P1", 0, 0),
            CreatePad(2, "P2", 10, 0),
            CreatePad(3, "P3", 20, 0),
        };
        var set = new CadPadSet(pads);
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
        };

        var builder = new DxfRegularLayerGridBuilder();

        var ex = Assert.Throws<InvalidOperationException>(() => DxfRegularLayerGridBuilder.BuildFromLayer(set, settings));
        Assert.Contains("Expected 4", ex.Message);
    }

    [Fact]
    public void BuildFromLayer_SortsColumnsByXWithinEachRow()
    {
        var pads = new[]
        {
            CreatePad(1, "top-mid", 10, 102),
            CreatePad(2, "top-right", 20, 101),
            CreatePad(3, "top-left", 0, 100),
            CreatePad(4, "bottom-mid", 10, 2),
            CreatePad(5, "bottom-right", 20, 1),
            CreatePad(6, "bottom-left", 0, 0),
        };
        var set = new CadPadSet(pads);
        var settings = new GridSettings
        {
            XChannels = 3,
            YChannels = 2,
        };
        _ = new DxfRegularLayerGridBuilder();
        var grid = DxfRegularLayerGridBuilder.BuildFromLayer(set, settings);

        // Bottom row
        Assert.Equal(0, grid.GetPad(0, 0).Bounds.MinX, 6);
        Assert.Equal(10, grid.GetPad(0, 1).Bounds.MinX, 6);
        Assert.Equal(20, grid.GetPad(0, 2).Bounds.MinX, 6);
        // Top row
        Assert.Equal(0, grid.GetPad(1, 0).Bounds.MinX, 6);
        Assert.Equal(10, grid.GetPad(1, 1).Bounds.MinX, 6);
        Assert.Equal(20, grid.GetPad(1, 2).Bounds.MinX, 6);
    }

    [Fact]
    public void BuildFromLayer_DerivesEdgesFromLayerPadExtents()
    {
        var pads = new[]
        {
            CreatePadWithSize(1, "left", 0, 0, width: 4, height: 8),
            CreatePadWithSize(2, "middle", 10, 0, width: 6, height: 8),
            CreatePadWithSize(3, "right", 22, 0, width: 10, height: 8),
        };
        var set = new CadPadSet(pads);
        var settings = new GridSettings
        {
            XChannels = 3,
            YChannels = 1,
        };
        _ = new DxfRegularLayerGridBuilder();
        var grid = DxfRegularLayerGridBuilder.BuildFromLayer(set, settings);

        Assert.Equal(0, grid.XEdges[0], 6);
        Assert.Equal(7, grid.XEdges[1], 6);
        Assert.Equal(19, grid.XEdges[2], 6);
        Assert.Equal(32, grid.XEdges[3], 6);
        Assert.Equal(0, grid.YEdges[0], 6);
        Assert.Equal(8, grid.YEdges[1], 6);
    }

    private static CadPad CreatePad(int id, string name, double minX, double minY)
    {
        return CreatePadWithSize(id, name, minX, minY, width: 8, height: 8);
    }

    private static CadPad CreatePadWithSize(int id, string name, double minX, double minY, double width, double height)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(minX + width, minY),
            new Point2(minX + width, minY + height),
            new Point2(minX, minY + height),
        });
        return new CadPad(id, name, "REGULAR", polygon);
    }
}
