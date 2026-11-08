// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfIndexAssignerScanDirectionTests
{
    [Fact]
    public void OrderPads_AllScanDirections_OrderCellsThenPositionsWithinEachCell()
    {
        var grid = RegularGridBuilder.BuildFromSettings(new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            AlignmentMode = GridAlignmentMode.FromPanelAa,
            WidthScope = WidthAdjustmentScope.ColumnGlobal,
            HeightScope = HeightAdjustmentScope.RowGlobal,
        });
        CadPad[] pads =
        [
            CreateCadPad(5, 1.25, 0.25), CreateCadPad(2, 0.25, 0.75), CreateCadPad(7, 1.25, 1.25),
            CreateCadPad(3, 0.75, 0.25), CreateCadPad(1, 0.25, 0.25), CreateCadPad(6, 0.25, 1.25),
            CreateCadPad(4, 0.75, 0.75),
        ];
        (ScanOrder Order, int[] ExpectedIds)[] cases =
        [
            (ScanOrder.LeftToRight_TopToBottom, [6, 7, 2, 1, 4, 3, 5]),
            (ScanOrder.RightToLeft_TopToBottom, [7, 6, 5, 4, 3, 2, 1]),
            (ScanOrder.LeftToRight_BottomToTop, [1, 2, 3, 4, 5, 6, 7]),
            (ScanOrder.RightToLeft_BottomToTop, [5, 3, 4, 1, 2, 7, 6]),
        ];

        foreach (var (order, expectedIds) in cases)
        {
            var ordered = DxfIndexAssigner.OrderPads(pads, grid, order);

            Assert.Equal(expectedIds, ordered.Select(static pad => pad.Id));
        }
    }

    [Fact]
    public void ResolveGridCell_EquallyLocalContainingCells_UsesTheNearestCentroid()
    {
        var grid = RegularGridBuilder.BuildFromSettings(new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            AlignmentMode = GridAlignmentMode.FromPanelAa,
            WidthScope = WidthAdjustmentScope.RowLocal,
            HeightScope = HeightAdjustmentScope.ColumnLocal,
            RowWidthOverrides = [[0.5, 1.5], [1.5, 0.5]],
            ColumnHeightOverrides = [[0.5, 1.5], [1.5, 0.5]],
        });

        Assert.Equal((1, 0), DxfIndexAssigner.ResolveGridCell(grid, new Point2(1, 1.25)));
        Assert.Equal((0, 1), DxfIndexAssigner.ResolveGridCell(grid, new Point2(1.25, 1)));
    }

    private static CadPad CreateCadPad(int id, double x, double y)
    {
        const double halfSize = 0.0625;
        return new CadPad(id, "CAD", "PAD", new Polygon2(
        [
            new Point2(x - halfSize, y - halfSize), new Point2(x + halfSize, y - halfSize),
            new Point2(x + halfSize, y + halfSize), new Point2(x - halfSize, y + halfSize),
        ]));
    }
}
