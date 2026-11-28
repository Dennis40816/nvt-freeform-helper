// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfIcResolverTests
{
    [Fact]
    public void ResolveWithoutGridReturnsFirstIc()
    {
        var pad = CreatePad();

        var actual = DxfIcResolver.Resolve(null, new GridSettings(), pad);

        Assert.Equal(0, actual);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    public void ResolveWithUnusableGridReturnsFirstIc(int columns, int edgeCount)
    {
        var grid = new RegularGrid(1, columns, Enumerable.Range(0, edgeCount).Select(static x => (double)x).ToArray(),
            new double[] { 0, 1 }, Array.Empty<RegularPad>());
        var pad = CreatePad();

        var actual = DxfIcResolver.Resolve(grid, new GridSettings(), pad);

        Assert.Equal(0, actual);
    }

    [Fact]
    public void ResolveUsesGridCellAndConfiguredIcColumns()
    {
        var settings = new GridSettings
        {
            XChannels = 4,
            YChannels = 1,
            CascadeNum = 2,
            PerIcXChannels = [1, 3],
            ActiveAreaWidth = 4,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        };
        var grid = RegularGridBuilder.BuildFromSettings(settings);
        var pad = new CadPad(101, "Output", "OUTPUT", grid.GetPad(0, 1).Polygon);

        var actual = DxfIcResolver.Resolve(grid, settings, pad);

        Assert.Equal(1, actual);
    }

    private static CadPad CreatePad() => new(101, "Output", "OUTPUT", new Polygon2(
        [new Point2(2, 0), new Point2(3, 0), new Point2(3, 1), new Point2(2, 1)]));
}
