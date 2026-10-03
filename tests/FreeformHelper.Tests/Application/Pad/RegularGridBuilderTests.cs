using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularGridBuilderTests
{
    [Fact]
    public void BuildFromSettings_UsesActiveArea()
    {
        var settings = new GridSettings
        {
            XChannels = 4,
            YChannels = 2,
            ActiveAreaWidth = 40,
            ActiveAreaHeight = 10
        };

        var builder = new RegularGridBuilder();
        var grid = builder.BuildFromSettings(settings);

        Assert.Equal(2, grid.Rows);
        Assert.Equal(4, grid.Cols);
        Assert.Equal(8, grid.Pads.Count);
        Assert.Equal(40, grid.Bounds.Width, 6);
        Assert.Equal(10, grid.Bounds.Height, 6);
    }

    [Fact]
    public void BuildFromCadBounds_UsesCadExtents()
    {
        var pad = new CadPad(0, "P1", "PAD",
            new Polygon2(new[]
            {
                new Point2(0, 0),
                new Point2(10, 0),
                new Point2(10, 5),
                new Point2(0, 5),
            }));
        var cad = new CadPadSet(new[] { pad });

        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            BoundsPaddingRatio = 0
        };

        var builder = new RegularGridBuilder();
        var grid = builder.BuildFromCadBounds(cad, settings);

        Assert.Equal(1, grid.Rows);
        Assert.Equal(2, grid.Cols);
        Assert.Equal(2, grid.Pads.Count);
        Assert.Equal(10, grid.Bounds.Width, 6);
        Assert.Equal(5, grid.Bounds.Height, 6);
    }

    [Fact]
    public void Build_WithRowLocalWidths_UsesPerRowOverrides()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 10,
            WidthScope = WidthAdjustmentScope.RowLocal,
            RowWidthOverrides = new List<List<double>>
            {
                new() { 5, 15 },
                new() { 10, 10 }
            },
            RowWidthOverrideFlags = new List<List<bool>>
            {
                new() { true, true },
                new() { true, true }
            }
        };

        var builder = new RegularGridBuilder();
        var grid = builder.BuildFromSettings(settings);

        Assert.Equal(5, grid.GetPad(0, 0).Bounds.Width, 6);
        Assert.Equal(15, grid.GetPad(0, 1).Bounds.Width, 6);
        Assert.Equal(10, grid.GetPad(1, 0).Bounds.Width, 6);
        Assert.Equal(10, grid.GetPad(1, 1).Bounds.Width, 6);
    }

    [Fact]
    public void Build_WithColumnLocalHeights_UsesPerColumnOverrides()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 20,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            ColumnHeightOverrides = new List<List<double>>
            {
                new() { 5, 15 },
                new() { 10, 10 }
            },
            ColumnHeightOverrideFlags = new List<List<bool>>
            {
                new() { true, true },
                new() { true, true }
            }
        };

        var builder = new RegularGridBuilder();
        var grid = builder.BuildFromSettings(settings);

        Assert.Equal(5, grid.GetPad(0, 0).Bounds.Height, 6);
        Assert.Equal(15, grid.GetPad(1, 0).Bounds.Height, 6);
        Assert.Equal(10, grid.GetPad(0, 1).Bounds.Height, 6);
        Assert.Equal(10, grid.GetPad(1, 1).Bounds.Height, 6);
    }
}
