// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ManualSizingOperationBehaviorTests
{
    private static readonly bool[] _flagsFalseFalse = [false, false];
    private static readonly bool[] _flagsFalseFalseFalse = [false, false, false];
    private static readonly bool[] _flagsFalseFalseTrue = [false, false, true];
    private static readonly bool[] _flagsFalseTrue = [false, true];
    private static readonly bool[] _flagsTrueFalseTrue = [true, false, true];
    private static readonly bool[] _flagsTrueTrue = [true, true];
    private static readonly bool[] _flagsTrueTrueTrue = [true, true, true];
    private static readonly double[] _sizes10And30 = [10.0, 30.0];
    private static readonly double[] _sizes20And30And50 = [20.0, 30.0, 50.0];
    private static readonly double[] _sizes20And50And50 = [20.0, 50.0, 50.0];
    private static readonly double[] _sizes20And80 = [20.0, 80.0];
    private static readonly double[] _sizes30And30 = [30.0, 30.0];
    private static readonly double[] _sizes30And70 = [30.0, 70.0];
    private static readonly double[] _sizes40And60 = [40.0, 60.0];

    [Fact]
    public void ApplySizing_EmptyOrZeroDimensions_PreservesManualSizesAndOverrides()
    {
        (double? Width, double? Height)[] requests = [(null, null), (0, null), (null, 0), (0, 0)];

        foreach (var (width, height) in requests)
        {
            var grid = CreateFixedGrid();

            var result = ManualSizingService.ApplySizing(
                grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0], cols: [0], width: width, height: height);

            Assert.True(result.Success);
            Assert.Null(result.Error);
            Assert.Equal(_sizes40And60, grid.ColumnWidths);
            Assert.Equal(_sizes30And70, grid.RowHeights);
            Assert.Equal(_flagsTrueTrue, grid.ColumnOverrides);
            Assert.Equal(_flagsTrueTrue, grid.RowOverrides);
            Assert.Equal(_sizes40And60, result.Snapshot.ColumnWidths);
            Assert.Equal(_sizes30And70, result.Snapshot.RowHeights);
        }
    }

    [Fact]
    public void ApplySizing_CadBounds_UsesPaddedDimensionsAndReturnsAnUndoSnapshot()
    {
        var grid = new GridSettings
        {
            XChannels = 3,
            YChannels = 2,
            ActiveAreaWidth = 500,
            ActiveAreaHeight = 400,
            BoundsPaddingRatio = 0.25,
            ColumnWidths = [20, 30, 50],
            RowHeights = [10, 30],
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.RowGlobal,
        };
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "Copper", 10, 20, 110, 60)]);

        var result = ManualSizingService.ApplySizing(
            grid, GridAlignmentMode.FromCadBounds, cad, rows: [1], cols: [2], width: 60, height: 30);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(36, grid.ColumnWidths[0], 6);
        Assert.Equal(54, grid.ColumnWidths[1], 6);
        Assert.Equal(60, grid.ColumnWidths[2], 6);
        Assert.Equal(_sizes30And30, grid.RowHeights);
        Assert.Equal(_flagsFalseFalseTrue, grid.ColumnOverrides);
        Assert.Equal(_flagsFalseTrue, grid.RowOverrides);

        ManualSizingService.RestoreSnapshot(grid, result.Snapshot);

        Assert.Equal(_sizes20And30And50, grid.ColumnWidths);
        Assert.Equal(_sizes10And30, grid.RowHeights);
        Assert.Equal(_flagsFalseFalseFalse, grid.ColumnOverrides);
        Assert.Equal(_flagsFalseFalse, grid.RowOverrides);
    }

    [Fact]
    public void ResetSizing_GlobalHeight_RedistributesOnlyTheReleasedRow()
    {
        var grid = CreateFixedGrid();
        grid.YChannels = 3;
        grid.ActiveAreaHeight = 120;
        grid.RowHeights = [20, 30, 50];
        grid.RowOverrides = [true, true, true];

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [1], cols: []);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(_sizes20And50And50, grid.RowHeights);
        Assert.Equal(_flagsTrueFalseTrue, grid.RowOverrides);
        Assert.Equal(_sizes40And60, grid.ColumnWidths);
        Assert.Equal(_flagsTrueTrue, grid.ColumnOverrides);
        Assert.Equal(_sizes20And30And50, result.Snapshot.RowHeights);
        Assert.Equal(_flagsTrueTrueTrue, result.Snapshot.RowOverrides);
    }

    [Fact]
    public void ResetSizing_RowLocalWidthFailureAfterPanelShrink_RestoresEarlierRowsAndRetainsHeights()
    {
        var grid = CreateFixedGrid();
        grid.ActiveAreaWidth = 60;
        grid.WidthScope = WidthAdjustmentScope.RowLocal;
        grid.HeightScope = HeightAdjustmentScope.ColumnLocal;
        grid.RowWidthOverrides = [[40, 60], [20, 80]];
        grid.RowWidthOverrideFlags = [[true, true], [true, true]];
        grid.ColumnHeightOverrides = [[40, 60], [30, 70]];
        grid.ColumnHeightOverrideFlags = [[true, true], [true, true]];

        var result = ManualSizingService.ResetSizing(
            grid, GridAlignmentMode.FromPanelAa, cad: null, rows: [0, 1], cols: [0]);

        Assert.False(result.Success);
        Assert.Equal("Manual sizes exceed available total.", result.Error);
        Assert.Equal(_sizes40And60, grid.RowWidthOverrides[0]);
        Assert.Equal(_sizes20And80, grid.RowWidthOverrides[1]);
        Assert.Equal(_flagsTrueTrue, grid.RowWidthOverrideFlags[0]);
        Assert.Equal(_flagsTrueTrue, grid.RowWidthOverrideFlags[1]);
        Assert.Equal(_sizes40And60, grid.ColumnHeightOverrides[0]);
        Assert.Equal(_sizes30And70, grid.ColumnHeightOverrides[1]);
        Assert.Equal(_flagsTrueTrue, grid.ColumnHeightOverrideFlags[0]);
        Assert.Equal(_flagsTrueTrue, grid.ColumnHeightOverrideFlags[1]);
        Assert.Equal(_sizes40And60, grid.ColumnWidths);
        Assert.Equal(_sizes30And70, grid.RowHeights);
        Assert.Equal(_flagsTrueTrue, grid.ColumnOverrides);
        Assert.Equal(_flagsTrueTrue, grid.RowOverrides);
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
