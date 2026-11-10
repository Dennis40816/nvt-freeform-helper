// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Pins spatial hit and selection results through the existing canvas seams.</summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasSpatialCharacterizationTests
{
    [AvaloniaTheory]
    [InlineData(5, 5, 10)]
    [InlineData(10, 5, 11)]
    [InlineData(5, 10, 12)]
    [InlineData(10, 10, 13)]
    [InlineData(20, 5, 11)]
    [InlineData(5, 20, 12)]
    [InlineData(20, 20, 13)]
    [InlineData(0, 0, 10)]
    [InlineData(-0.0000001, 5, -1)]
    [InlineData(20.0000001, 5, -1)]
    [InlineData(5, -0.0000001, -1)]
    [InlineData(5, 20.0000001, -1)]
    public void RegularHitAtInteriorAndOuterEdgesPreservesTheChosenPad(double x, double y, int expectedIndex)
    {
        var canvas = CreateCanvas(CreateGrid());

        var hit = Hit(canvas, new Point2(x, y), regularOnly: true);

        Assert.Equal(expectedIndex < 0 ? PadCanvas.HitKind.None : PadCanvas.HitKind.Regular, hit.kind);
        Assert.Equal(expectedIndex < 0 ? 0 : expectedIndex, hit.idOrIndex);
    }

    [AvaloniaTheory]
    [InlineData(10, 10, 4)]
    [InlineData(20, 20, 1)]
    [InlineData(20.0000001, 20, 0)]
    public void RegularSelectionAtAnEdgeIncludesEveryTouchingPad(double x, double y, int expectedCount)
    {
        var canvas = CreateCanvas(CreateGrid());

        var selection = Select(canvas, new Rect2(x, y, x + 0.1, y + 0.1), regularOnly: true);

        int[] expected = expectedCount == 4 ? [10, 11, 12, 13] : expectedCount == 1 ? [13] : [];
        Assert.Equal(expected, selection.RegularIndices);
        Assert.Empty(selection.CadIds);
        Assert.True(canvas.GetSelectionPerfSnapshot().UsedRegularIndex);
    }

    [AvaloniaTheory]
    [InlineData(3, 0, 3, 20, 3, 5)]
    [InlineData(0, 4, 20, 4, 5, 4)]
    [InlineData(3, 4, 3, 4, 3, 4)]
    public void CadWithZeroAreaBoundsHasNoHitOrSelection(
        double minX, double minY, double maxX, double maxY, double x, double y)
    {
        var pad = new CadPad(21, "CAD-21", "L1", CreatePolygon(minX, minY, maxX, maxY));
        var canvas = CreateCanvas([], [pad]);

        var hit = Hit(canvas, new Point2(x, y), regularOnly: false);
        var selection = Select(canvas, new Rect2(x - 0.1, y - 0.1, x + 0.1, y + 0.1), regularOnly: false);

        Assert.Equal(PadCanvas.HitKind.None, hit.kind);
        Assert.Empty(selection.CadIds);
        Assert.Empty(selection.RegularIndices);
        Assert.True(canvas.GetSelectionPerfSnapshot().UsedCadIndex);

        var outside = Select(canvas, new Rect2(maxX + 0.0000001, y, maxX + 0.1, y + 0.1), regularOnly: false);
        Assert.Empty(outside.CadIds);
    }

    [AvaloniaTheory]
    [InlineData(0, true, 11)]
    [InlineData(0.000000999, true, 11)]
    [InlineData(0.000001001, false, 10)]
    public void RegularGridSeparabilityTolerancePreservesIndexedAndFallbackResults(
        double boundaryOffset, bool expectedIndexUsed, int expectedHitIndex)
    {
        var canvas = CreateCanvas(CreateGrid(boundaryOffset));

        // Indexed hits choose the right cell; a scan keeps the first equal-area pad.
        var sharedEdgeHit = Hit(canvas, new Point2(10, 5), regularOnly: true);
        var shiftedRowHit = Hit(canvas, new Point2(10, 15), regularOnly: true);
        var selection = Select(canvas, new Rect2(10, 5, 10.1, 5.1), regularOnly: true);

        Assert.Equal(PadCanvas.HitKind.Regular, sharedEdgeHit.kind);
        Assert.Equal(expectedHitIndex, sharedEdgeHit.idOrIndex);
        Assert.Equal(PadCanvas.HitKind.Regular, shiftedRowHit.kind);
        Assert.Equal(boundaryOffset == 0 ? 13 : 12, shiftedRowHit.idOrIndex);
        Assert.Equal([10, 11], selection.RegularIndices);
        Assert.Equal(expectedIndexUsed, canvas.GetSelectionPerfSnapshot().UsedRegularIndex);
    }

    [AvaloniaFact]
    public void MixedQueryOnCadBucketAndRegularCellBoundariesPreservesHitPriorityAndSelectionIds()
    {
        var canvas = CreateCanvas(CreateGrid(),
            [
                new CadPad(21, "CAD-21", "L1", CreatePolygon(0, 0, 20, 20)),
                new CadPad(22, "CAD-22", "L1", CreatePolygon(9, 9, 11, 11)),
            ]);
        var world = new Point2(10, 10);

        var mixedHit = Hit(canvas, world, regularOnly: false);
        var regularHit = Hit(canvas, world, regularOnly: true);
        var selection = Select(canvas, new Rect2(10, 10, 10.1, 10.1), regularOnly: false);

        Assert.Equal(PadCanvas.HitKind.Cad, mixedHit.kind);
        Assert.Equal(22, mixedHit.idOrIndex);
        Assert.Equal(PadCanvas.HitKind.Regular, regularHit.kind);
        Assert.Equal(13, regularHit.idOrIndex);
        Assert.Equal([21, 22], selection.CadIds);
        Assert.Equal([10, 11, 12, 13], selection.RegularIndices);
        Assert.True(canvas.GetSelectionPerfSnapshot().UsedCadIndex);
        Assert.True(canvas.GetSelectionPerfSnapshot().UsedRegularIndex);
    }

    private static PadCanvas CreateCanvas(IReadOnlyList<RegularPad> regularPads, IReadOnlyList<CadPad>? cadPads = null)
    {
        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowCad = true,
            ShowRegular = true,
            CadPads = cadPads ?? [],
            RegularPads = regularPads,
        };
        canvas.Measure(new Size(100, 100));
        canvas.Arrange(new Rect(0, 0, 100, 100));
        PadCanvasPrivateAccess.Zoom(canvas) = 1.0;
        PadCanvasPrivateAccess.Pan(canvas) = new Vector(0, 0);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);
        return canvas;
    }

    private static PadCanvas.HitResult Hit(PadCanvas canvas, Point2 world, bool regularOnly)
    {
        return PadCanvasPrivateAccess.HitTestForHoverDebug(canvas, canvas.WorldToScreen(world), regularOnly);
    }

    private static (int[] CadIds, int[] RegularIndices) Select(PadCanvas canvas, Rect2 rect, bool regularOnly)
    {
        PadCanvasPrivateAccess.SelectPadsInWorldRect(canvas, rect, additive: false, regularOnly: regularOnly);
        return (PadCanvasPrivateAccess.SelectedCadIds(canvas).Order().ToArray(),
            PadCanvasPrivateAccess.SelectedRegIdx(canvas).Order().ToArray());
    }

    private static RegularPad[] CreateGrid(double boundaryOffset = 0)
    {
        return
        [
            new RegularPad(0, 0, 10, CreatePolygon(0, 0, 10, 10)),
            new RegularPad(0, 1, 11, CreatePolygon(10, 0, 20, 10)),
            new RegularPad(1, 0, 12, CreatePolygon(0, 10, 10 + boundaryOffset, 20)),
            new RegularPad(1, 1, 13, CreatePolygon(10 + boundaryOffset, 10, 20, 20)),
        ];
    }

    private static Polygon2 CreatePolygon(double minX, double minY, double maxX, double maxY)
    {
        return new Polygon2(
            [
                new Point2(minX, minY), new Point2(maxX, minY),
                new Point2(maxX, maxY), new Point2(minX, maxY),
            ]);
    }
}
