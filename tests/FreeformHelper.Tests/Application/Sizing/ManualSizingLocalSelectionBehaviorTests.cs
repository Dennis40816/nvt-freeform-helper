// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingLocalSelectionBehaviorTests
{
    [Fact]
    public void ApplySizing_LocalScopes_IgnoresRemovedIndicesAndPreservesOtherRowsAndColumns()
    {
        var grid = CreateLocalGrid();
        grid.RowWidthOverrides = [[40, 60], [50, 50]];
        grid.RowWidthOverrideFlags = [[true, true], [false, false]];
        grid.ColumnHeightOverrides = [[50, 50], [20, 80]];
        grid.ColumnHeightOverrideFlags = [[false, false], [true, true]];

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null,
            rows: [2, 1, 1, -1], cols: [2, 0, 0, -1], width: 70, height: 30);

        double[] expectedSelectedSizes = [70, 30];
        double[] expectedOtherWidths = [40, 60];
        double[] expectedOtherHeights = [20, 80];
        double[] expectedGlobalSizes = [50, 50];
        bool[] expectedWidthFlags = [true, false];
        bool[] expectedHeightFlags = [false, true];
        bool[] expectedOtherFlags = [true, true];
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(expectedSelectedSizes, grid.RowWidthOverrides[1]);
        Assert.Equal(expectedSelectedSizes, grid.ColumnHeightOverrides[0]);
        Assert.Equal(expectedWidthFlags, grid.RowWidthOverrideFlags[1]);
        Assert.Equal(expectedHeightFlags, grid.ColumnHeightOverrideFlags[0]);
        Assert.Equal(expectedOtherWidths, grid.RowWidthOverrides[0]);
        Assert.Equal(expectedOtherHeights, grid.ColumnHeightOverrides[1]);
        Assert.Equal(expectedOtherFlags, grid.RowWidthOverrideFlags[0]);
        Assert.Equal(expectedOtherFlags, grid.ColumnHeightOverrideFlags[1]);
        Assert.Equal(expectedGlobalSizes, grid.ColumnWidths);
        Assert.Equal(expectedGlobalSizes, grid.RowHeights);
    }

    [Fact]
    public void ResetSizing_LocalScopes_IgnoresRemovedIndicesAndRedistributesOnlySelectedRowAndColumn()
    {
        var grid = CreateLocalGrid();
        grid.ActiveAreaWidth = 120;
        grid.ActiveAreaHeight = 90;
        grid.RowWidthOverrides = [[40, 60], [70, 30]];
        grid.RowWidthOverrideFlags = [[true, true], [true, true]];
        grid.ColumnHeightOverrides = [[60, 40], [20, 80]];
        grid.ColumnHeightOverrideFlags = [[true, true], [true, true]];

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [2, 1, 1, -1], cols: [2, 0, 0, -1]);

        double[] expectedSelectedWidths = [90, 30];
        double[] expectedSelectedHeights = [60, 30];
        double[] expectedOtherWidths = [40, 60];
        double[] expectedOtherHeights = [20, 80];
        double[] expectedGlobalSizes = [50, 50];
        bool[] expectedWidthFlags = [false, true];
        bool[] expectedHeightFlags = [true, false];
        bool[] expectedOtherFlags = [true, true];
        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(expectedSelectedWidths, grid.RowWidthOverrides[1]);
        Assert.Equal(expectedSelectedHeights, grid.ColumnHeightOverrides[0]);
        Assert.Equal(expectedWidthFlags, grid.RowWidthOverrideFlags[1]);
        Assert.Equal(expectedHeightFlags, grid.ColumnHeightOverrideFlags[0]);
        Assert.Equal(expectedOtherWidths, grid.RowWidthOverrides[0]);
        Assert.Equal(expectedOtherHeights, grid.ColumnHeightOverrides[1]);
        Assert.Equal(expectedOtherFlags, grid.RowWidthOverrideFlags[0]);
        Assert.Equal(expectedOtherFlags, grid.ColumnHeightOverrideFlags[1]);
        Assert.Equal(expectedGlobalSizes, grid.ColumnWidths);
        Assert.Equal(expectedGlobalSizes, grid.RowHeights);
    }

    private static GridSettings CreateLocalGrid()
    {
        return new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 100,
            ColumnWidths = [50, 50],
            RowHeights = [50, 50],
            WidthScope = WidthAdjustmentScope.RowLocal,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
        };
    }
}
