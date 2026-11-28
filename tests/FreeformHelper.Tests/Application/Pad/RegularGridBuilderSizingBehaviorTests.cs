// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class RegularGridBuilderSizingBehaviorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void BuildFromSettings_OverrideFlagsKeepOriginalGridSizing(bool localScopes, bool hasOverrides)
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 40,
            ActiveAreaHeight = 20,
            ColumnWidths = [4, 6],
            RowHeights = [3, 7],
            WidthScope = localScopes ? WidthAdjustmentScope.RowLocal : WidthAdjustmentScope.ColumnGlobal,
            HeightScope = localScopes ? HeightAdjustmentScope.ColumnLocal : HeightAdjustmentScope.RowGlobal,
            RowWidthOverrides = hasOverrides ? [[6, 4], [3, 7]] : [[0, -1], [-1, 0]],
            ColumnHeightOverrides = hasOverrides ? [[7, 3], [4, 6]] : [[-1, 0], [0, -1]],
            RowWidthOverrideFlags = [[false, false], [false, false]],
            ColumnHeightOverrideFlags = [[false, false], [false, false]],
        };
        var unflagged = RegularGridBuilder.BuildFromSettings(settings);
        settings.RowWidthOverrideFlags = [[true, true], [true, true]];
        settings.ColumnHeightOverrideFlags = [[true, true], [true, true]];

        var flagged = RegularGridBuilder.BuildFromSettings(settings);

        Assert.Equal(unflagged.Pads.Select(pad => pad.Bounds), flagged.Pads.Select(pad => pad.Bounds));
        Assert.Equal(unflagged.XEdges, flagged.XEdges);
        Assert.Equal(unflagged.YEdges, flagged.YEdges);
        var firstWidth = localScopes && hasOverrides ? 24d : 16d;
        var firstHeight = localScopes && hasOverrides ? 14d : 6d;
        Assert.Equal(firstWidth, flagged.GetPad(0, 0).Bounds.Width, 8);
        Assert.Equal(firstHeight, flagged.GetPad(0, 0).Bounds.Height, 8);
        Assert.Equal(localScopes && hasOverrides ? 12d : 16d, flagged.GetPad(1, 0).Bounds.Width, 8);
        Assert.Equal(localScopes && hasOverrides ? 8d : 6d, flagged.GetPad(0, 1).Bounds.Height, 8);
    }

    private static readonly double[] _expected1 = [0d, 2d, 8d, 12d];
    private static readonly double[] _expected2 = [0d, 4d, 6d, 12d];
    private static readonly double[] _expected3 = [2d, 6d, 4d];
    private static readonly double[] _expected4 = [4d, 2d, 6d];

    [Fact]
    public void BuildFromSettings_ZeroActiveAreaDimension_RejectsGridWithoutCad()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 0,
            ActiveAreaHeight = 10,
        };

        var widthError = Assert.Throws<InvalidOperationException>(() => RegularGridBuilder.BuildFromSettings(settings));
        settings.ActiveAreaWidth = 10;
        settings.ActiveAreaHeight = 0;
        var heightError = Assert.Throws<InvalidOperationException>(() => RegularGridBuilder.BuildFromSettings(settings));

        Assert.Equal("Active area size must be > 0 to build grid without CAD.", widthError.Message);
        Assert.Equal(widthError.Message, heightError.Message);
    }

    [Fact]
    public void BuildFromSettings_PartialSizing_FillsLocalOverridesWithoutFlagsAndFallsBackForMismatchedGlobalSizes()
    {
        var settings = new GridSettings
        {
            XChannels = 3,
            YChannels = 3,
            ActiveAreaWidth = 12,
            ActiveAreaHeight = 12,
            WidthScope = WidthAdjustmentScope.RowLocal,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            ColumnWidths = [0, -1, 0],
            RowHeights = [-1, 0, 0],
            RowWidthOverrides = [[1, 3, 0]],
            ColumnHeightOverrides = [[0, 1, 3]],
        };

        var grid = RegularGridBuilder.BuildFromSettings(settings);

        Assert.Equal(_expected1, grid.XEdges);
        Assert.Equal(_expected2, grid.YEdges);
        Assert.Equal(_expected3, Enumerable.Range(0, 3).Select(col => grid.GetPad(0, col).Bounds.Width));
        Assert.Equal(_expected4, Enumerable.Range(0, 3).Select(row => grid.GetPad(row, 0).Bounds.Height));
        Assert.All(grid.Pads.Where(pad => pad.Row > 0), pad => Assert.Equal(4d, pad.Bounds.Width));
        Assert.All(grid.Pads.Where(pad => pad.Col > 0), pad => Assert.Equal(4d, pad.Bounds.Height));
        Assert.Equal(12d, grid.Bounds.Width);
        Assert.Equal(12d, grid.Bounds.Height);

        settings.WidthScope = WidthAdjustmentScope.ColumnGlobal;
        settings.HeightScope = HeightAdjustmentScope.RowGlobal;
        settings.ColumnWidths = [1, 3];
        settings.RowHeights = [1];

        var globalGrid = RegularGridBuilder.BuildFromSettings(settings);

        Assert.All(globalGrid.Pads, pad => Assert.Equal(4d, pad.Bounds.Width));
        Assert.All(globalGrid.Pads, pad => Assert.Equal(4d, pad.Bounds.Height));
    }
}
