using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasHitTestTests
{
    [AvaloniaFact]
    public void TryHitRegular_FallsBackToLinearScan_WhenSpatialBucketMisses()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        // This point is inside row1-col0, but the spatial bucket (built from global edges)
        // may map to row1-col1 when per-row widths diverge.
        SetCanvasView(canvas, zoom: 1.0, panX: 0.0, panY: 0.0);
        var screenPoint = GetScreenPointForWorld(canvas, new Point2(15, 15));
        var hit = PadCanvasPrivateAccess.HitTestForHoverDebug(canvas, screenPoint, regularOnly: true);

        Assert.Equal(PadCanvas.HitKind.Regular, hit.kind);
        Assert.Equal(2, hit.idOrIndex);
    }

    [AvaloniaFact]
    public void EnsureRegularSpatialIndex_DisablesIndex_ForNonSeparableGrid()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        PadCanvasPrivateAccess.EnsureRegularSpatialIndex(canvas);

        Assert.Null(PadCanvasPrivateAccess.RegularIndex(canvas));
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_UsesLinearFallback_WhenIndexDisabled()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };

        PadCanvasPrivateAccess.SelectPadsInWorldRect(canvas, new Rect2(11, 11, 19, 19), false, true);

        var selected = ReadSelectedRegularIndices(canvas);
        Assert.Contains(2, selected);
        Assert.DoesNotContain(3, selected);
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_NonAdditive_ReplacesExistingSelection()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 30, maxX: 10, maxY: 40),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };
        canvas.SetSelection([], [0, 1]);

        PadCanvasPrivateAccess.SelectPadsInWorldRect(canvas, new Rect2(1, 31, 9, 39), false, true);

        Assert.Equal([2], ReadSelectedRegularIndices(canvas));
        Assert.Empty(ReadSelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void SelectPadsInWorldRect_Additive_PreservesExistingSelection()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 30, maxX: 10, maxY: 40),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = pads,
        };
        canvas.SetSelection([], [0]);

        PadCanvasPrivateAccess.SelectPadsInWorldRect(canvas, new Rect2(1, 31, 9, 39), true, true);

        Assert.Equal([0, 2], ReadSelectedRegularIndices(canvas));
        Assert.Empty(ReadSelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void EnsureRegularSpatialIndex_RebuildsAfterRegularPadsReplacement()
    {
        var separablePads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 20, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 10, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 10, minY: 10, maxX: 20, maxY: 20),
        };

        var nonSeparablePads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 0, minX: 0, minY: 0, maxX: 10, maxY: 10),
            CreateRectPad(row: 0, col: 1, index: 1, minX: 10, minY: 0, maxX: 30, maxY: 10),
            CreateRectPad(row: 1, col: 0, index: 2, minX: 0, minY: 10, maxX: 20, maxY: 20),
            CreateRectPad(row: 1, col: 1, index: 3, minX: 20, minY: 10, maxX: 30, maxY: 20),
        };

        var canvas = new PadCanvas
        {
            ShowRegular = true,
            RegularPads = separablePads,
        };

        PadCanvasPrivateAccess.EnsureRegularSpatialIndex(canvas);
        Assert.NotNull(PadCanvasPrivateAccess.RegularIndex(canvas));

        canvas.RegularPads = nonSeparablePads;
        PadCanvasPrivateAccess.EnsureRegularSpatialIndex(canvas);
        Assert.Null(PadCanvasPrivateAccess.RegularIndex(canvas));
    }

    [AvaloniaFact]
    public void HitTestForHoverDebug_RegularOnly_IgnoresShowRegularVisibility()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 7, minX: 0, minY: 0, maxX: 10, maxY: 10),
        };
        pads[0].DiffIndex = 42;

        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowRegular = false,
            RegularPads = pads,
        };
        canvas.Measure(new Avalonia.Size(100, 100));
        canvas.Arrange(new Avalonia.Rect(0, 0, 100, 100));
        SetCanvasView(canvas, zoom: 5.0, panX: 25.0, panY: 75.0);
        var screenPoint = GetScreenPointForWorld(canvas, pads[0].Centroid);

        var hit = PadCanvasPrivateAccess.HitTestForHoverDebug(canvas, screenPoint, regularOnly: true);

        Assert.Equal(PadCanvas.HitKind.Regular, hit.kind);
        Assert.Equal(7, hit.idOrIndex);
    }

    [AvaloniaFact]
    public void BuildHoverDebugSnapshot_RegularOnly_StillShowsDiff_WhenRegularIsHidden()
    {
        var pads = new[]
        {
            CreateRectPad(row: 0, col: 0, index: 7, minX: 0, minY: 0, maxX: 10, maxY: 10),
        };
        pads[0].DiffIndex = 42;

        var canvas = new PadCanvas
        {
            Width = 100,
            Height = 100,
            ShowRegular = false,
            RegularPads = pads,
        };
        canvas.Measure(new Avalonia.Size(100, 100));
        canvas.Arrange(new Avalonia.Rect(0, 0, 100, 100));
        SetCanvasView(canvas, zoom: 5.0, panX: 25.0, panY: 75.0);
        var screenPoint = GetScreenPointForWorld(canvas, pads[0].Centroid);

        var hit = PadCanvasPrivateAccess.HitTestForHoverDebug(canvas, screenPoint, regularOnly: true);
        PadCanvasPrivateAccess.HoverDebugHit(canvas) = hit;
        PadCanvasPrivateAccess.HoverDebugVisible(canvas) = true;
        PadCanvasPrivateAccess.HoverDebugRegularOnlyMode(canvas) = true;

        var snapshot = PadCanvasPrivateAccess.BuildHoverDebugSnapshot(canvas);
        Assert.NotNull(snapshot);
        var text = snapshot.Value.Text;

        Assert.Contains("REG 7", text, StringComparison.Ordinal);
        Assert.Contains("Diff 42", text, StringComparison.Ordinal);
    }

    private static void SetCanvasView(PadCanvas canvas, double zoom, double panX, double panY)
    {
        PadCanvasPrivateAccess.Zoom(canvas) = zoom;
        PadCanvasPrivateAccess.Pan(canvas) = new Vector(panX, panY);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);
    }

    private static Avalonia.Point GetScreenPointForWorld(PadCanvas canvas, Point2 worldPoint)
    {
        return canvas.WorldToScreen(worldPoint);
    }

    private static List<int> ReadSelectedRegularIndices(PadCanvas canvas)
    {
        return PadCanvasPrivateAccess.SelectedRegIdx(canvas).ToList();
    }

    private static List<int> ReadSelectedCadIds(PadCanvas canvas)
    {
        return PadCanvasPrivateAccess.SelectedCadIds(canvas).ToList();
    }

    private static RegularPad CreateRectPad(int row, int col, int index, double minX, double minY, double maxX, double maxY)
    {
        var polygon = new Polygon2(
            new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY),
            });
        return new RegularPad(row, col, index, polygon);
    }
}
