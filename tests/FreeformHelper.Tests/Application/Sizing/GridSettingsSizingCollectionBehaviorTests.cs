// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class GridSettingsSizingCollectionBehaviorTests
{
    [Fact]
    public void ReplaceSizingCollections_NullInputs_ClearManualSizingAndRestoreUniformGrid()
    {
        var settings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 10,
            WidthScope = WidthAdjustmentScope.RowLocal,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            ColumnWidths = [5, 15],
            RowHeights = [2, 8],
            ColumnOverrides = [true, true],
            RowOverrides = [true, true],
            RowWidthOverrides = [[3, 17], [12, 8]],
            RowWidthOverrideFlags = [[true, true], [true, true]],
            ColumnHeightOverrides = [[4, 6], [7, 3]],
            ColumnHeightOverrideFlags = [[true, true], [true, true]],
        };
        var manualGrid = RegularGridBuilder.BuildFromSettings(settings);
        Assert.Equal(3d, manualGrid.GetPad(0, 0).Bounds.Width);
        Assert.Equal(4d, manualGrid.GetPad(0, 0).Bounds.Height);

        settings.ReplaceSizingCollections(
            columnWidths: null,
            rowHeights: null,
            columnOverrides: null,
            rowOverrides: null,
            rowWidthOverrides: null,
            rowWidthOverrideFlags: null,
            columnHeightOverrides: null,
            columnHeightOverrideFlags: null);
        var uniformGrid = RegularGridBuilder.BuildFromSettings(settings);

        Assert.Empty(settings.ColumnWidths);
        Assert.Empty(settings.RowHeights);
        Assert.Empty(settings.ColumnOverrides);
        Assert.Empty(settings.RowOverrides);
        Assert.Empty(settings.RowWidthOverrides);
        Assert.Empty(settings.RowWidthOverrideFlags);
        Assert.Empty(settings.ColumnHeightOverrides);
        Assert.Empty(settings.ColumnHeightOverrideFlags);
        Assert.Equal(4, uniformGrid.Pads.Count);
        Assert.All(uniformGrid.Pads, pad =>
        {
            Assert.Equal(10d, pad.Bounds.Width);
            Assert.Equal(5d, pad.Bounds.Height);
        });
        Assert.Equal(20d, uniformGrid.Bounds.Width);
        Assert.Equal(10d, uniformGrid.Bounds.Height);
    }
}
