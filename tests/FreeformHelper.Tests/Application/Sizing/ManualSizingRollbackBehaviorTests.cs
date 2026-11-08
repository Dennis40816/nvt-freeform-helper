// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingRollbackBehaviorTests
{
    [Fact]
    public void ApplySizing_RowLocalWidthFailure_RestoresEarlierRowsAndDoesNotApplyHeight()
    {
        var grid = CreateGrid();
        grid.WidthScope = WidthAdjustmentScope.RowLocal;
        grid.RowWidthOverrides = [[50, 50], [20, 80]];
        grid.RowWidthOverrideFlags = [[false, false], [true, true]];
        var before = ManualSizingService.CaptureSnapshot(grid);

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0, 1], cols: [0], width: 30, height: 30);

        Assert.False(result.Success);
        Assert.Equal("No auto cells available to keep total size constant.", result.Error);
        AssertSizingEquals(before, grid);
    }

    [Fact]
    public void ApplySizing_GlobalHeightFailure_RestoresSuccessfulWidthChange()
    {
        var grid = CreateGrid();
        var before = ManualSizingService.CaptureSnapshot(grid);

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0], cols: [0], width: 30, height: 120);

        Assert.False(result.Success);
        Assert.Equal("Manual sizes exceed available total.", result.Error);
        AssertSizingEquals(before, grid);
    }

    [Fact]
    public void ApplySizing_ColumnLocalHeightFailure_RestoresWidthsAndEarlierColumns()
    {
        var grid = CreateGrid();
        grid.XChannels = 3;
        grid.ColumnWidths = [30, 30, 40];
        grid.HeightScope = HeightAdjustmentScope.ColumnLocal;
        grid.ColumnHeightOverrides = [[50, 50], [20, 80]];
        grid.ColumnHeightOverrideFlags = [[false, false], [true, true]];
        ManualSizingService.EnsureSizingLists(grid);
        var before = ManualSizingService.CaptureSnapshot(grid);

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0], cols: [0, 1], width: 20, height: 30);

        Assert.False(result.Success);
        Assert.Equal("No auto cells available to keep total size constant.", result.Error);
        AssertSizingEquals(before, grid);
    }

    [Fact]
    public void ResetSizing_ColumnLocalHeightFailure_RestoresWidthsAndEarlierColumns()
    {
        var grid = CreateGrid();
        grid.ColumnWidths = [40, 60];
        grid.ColumnOverrides = [true, true];
        grid.HeightScope = HeightAdjustmentScope.ColumnLocal;
        // A smaller panel leaves a retained fixed cell taller than the available height.
        grid.ColumnHeightOverrides = [[50, 40], [20, 120]];
        grid.ColumnHeightOverrideFlags = [[true, true], [true, true]];
        var before = ManualSizingService.CaptureSnapshot(grid);

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0], cols: [0, 1]);

        Assert.False(result.Success);
        Assert.Equal("Manual sizes exceed available total.", result.Error);
        AssertSizingEquals(before, grid);
    }

    private static GridSettings CreateGrid()
    {
        var grid = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 100,
            ActiveAreaHeight = 100,
            ColumnWidths = [50, 50],
            RowHeights = [50, 50],
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.RowGlobal,
        };
        ManualSizingService.EnsureSizingLists(grid);
        return grid;
    }

    private static void AssertSizingEquals(ManualSizingSnapshot expected, GridSettings actual)
    {
        Assert.Equal(expected.ColumnWidths, actual.ColumnWidths);
        Assert.Equal(expected.RowHeights, actual.RowHeights);
        Assert.Equal(expected.ColumnOverrides, actual.ColumnOverrides);
        Assert.Equal(expected.RowOverrides, actual.RowOverrides);
        Assert.Equal(expected.WidthScope, actual.WidthScope);
        Assert.Equal(expected.HeightScope, actual.HeightScope);
        Assert.Equal(expected.RowWidthOverrides.Count, actual.RowWidthOverrides.Count);
        Assert.Equal(expected.ColumnHeightOverrides.Count, actual.ColumnHeightOverrides.Count);
        for (var row = 0; row < expected.RowWidthOverrides.Count; row++)
        {
            Assert.Equal(expected.RowWidthOverrides[row], actual.RowWidthOverrides[row]);
            Assert.Equal(expected.RowWidthOverrideFlags[row], actual.RowWidthOverrideFlags[row]);
        }

        for (var col = 0; col < expected.ColumnHeightOverrides.Count; col++)
        {
            Assert.Equal(expected.ColumnHeightOverrides[col], actual.ColumnHeightOverrides[col]);
            Assert.Equal(expected.ColumnHeightOverrideFlags[col], actual.ColumnHeightOverrideFlags[col]);
        }
    }
}
