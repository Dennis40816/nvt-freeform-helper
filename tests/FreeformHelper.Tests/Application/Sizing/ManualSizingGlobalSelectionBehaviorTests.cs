// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingGlobalSelectionBehaviorTests
{
    [Fact]
    public void ApplySizing_GlobalScopes_AfterChannelShrink_UpdatesOnlyRemainingSelectedIndices()
    {
        var grid = CreateFixedGrid();
        grid.XChannels = 3;
        grid.YChannels = 3;
        ManualSizingService.EnsureSizingLists(grid);
        grid.XChannels = 2;
        grid.YChannels = 2;
        grid.ColumnOverrides = [false, false];
        grid.RowOverrides = [false, false];

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null,
            rows: [2, 0, 0, -1], cols: [2, 0, 0, -1], width: 20, height: 25);

        double[] expectedWidths = [20, 80];
        double[] expectedHeights = [25, 75];
        double[] originalWidths = [40, 60];
        double[] originalHeights = [30, 70];
        bool[] expectedFlags = [true, false];
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(expectedWidths, grid.ColumnWidths);
        Assert.Equal(expectedHeights, grid.RowHeights);
        Assert.Equal(expectedFlags, grid.ColumnOverrides);
        Assert.Equal(expectedFlags, grid.RowOverrides);
        Assert.Equal(originalWidths, result.Snapshot.ColumnWidths);
        Assert.Equal(originalHeights, result.Snapshot.RowHeights);
    }

    [Fact]
    public void ResetSizing_GlobalScopes_IgnoresRemovedIndicesAndPreservesUnselectedFixedSizes()
    {
        var grid = CreateFixedGrid();
        grid.ColumnWidths = [30, 50];
        grid.RowHeights = [20, 40];

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [2, 0, 0, -1], cols: [2, 0, 0, -1]);

        double[] expectedWidths = [50, 50];
        double[] expectedHeights = [60, 40];
        bool[] expectedFlags = [false, true];
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(expectedWidths, grid.ColumnWidths);
        Assert.Equal(expectedHeights, grid.RowHeights);
        Assert.Equal(expectedFlags, grid.ColumnOverrides);
        Assert.Equal(expectedFlags, grid.RowOverrides);
    }

    [Fact]
    public void ResetSizing_GlobalWidthAfterPanelShrink_ReturnsErrorAndRestoresAllSizing()
    {
        var grid = CreateFixedGrid();
        grid.ActiveAreaWidth = 50;

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0], cols: [0]);

        double[] expectedWidths = [40, 60];
        double[] expectedHeights = [30, 70];
        bool[] expectedFlags = [true, true];
        Assert.False(result.Success);
        Assert.Equal("Manual sizes exceed available total.", result.Error);
        Assert.Equal(expectedWidths, grid.ColumnWidths);
        Assert.Equal(expectedHeights, grid.RowHeights);
        Assert.Equal(expectedFlags, grid.ColumnOverrides);
        Assert.Equal(expectedFlags, grid.RowOverrides);
        Assert.Equal(expectedWidths, result.Snapshot.ColumnWidths);
        Assert.Equal(expectedHeights, result.Snapshot.RowHeights);
        Assert.Equal(WidthAdjustmentScope.ColumnGlobal, grid.WidthScope);
        Assert.Equal(HeightAdjustmentScope.RowGlobal, grid.HeightScope);
    }

    private static GridSettings CreateFixedGrid()
    {
        return new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 100,
            ColumnWidths = [40, 60],
            RowHeights = [30, 70],
            ColumnOverrides = [true, true],
            RowOverrides = [true, true],
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.RowGlobal,
        };
    }
}
