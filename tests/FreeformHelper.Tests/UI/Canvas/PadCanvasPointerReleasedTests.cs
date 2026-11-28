using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a pointer release does on a <see cref="PadCanvas"/>: the end of a pan, the end of a box selection
/// and the selection change of a single click with its Shift, Ctrl and multi-selection variants.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasPointerReleasedTests
{
    private static readonly Point2 _emptySpace = new(25, 5);
    private static readonly Point2 _firstRegular = new(5, 5);
    private static readonly Point2 _secondRegular = new(15, 5);
    private static readonly Point2 _cadOnly = new(35, 5);

    [AvaloniaFact]
    public void MiddleRelease_EndsThePanAndKeepsTheSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);
        var window = WindowOf(canvas);

        window.MouseDown(ToWindow(canvas, _firstRegular), MouseButton.Middle);
        Assert.True(PadCanvasPrivateAccess.IsMiddlePanning(canvas));
        window.MouseUp(ToWindow(canvas, _firstRegular), MouseButton.Middle);

        Assert.False(PadCanvasPrivateAccess.IsMiddlePanning(canvas));
        Assert.Equal(0, PadCanvasPrivateAccess.LastPanRedrawTicks(canvas));
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 1);
    }

    [AvaloniaFact]
    public void BoxRelease_SelectsThePadsTheBoxTouchesAndEndsTheGesture()
    {
        var canvas = CreateCanvas();
        var changes = CollectSelectionChanges(canvas);

        Drag(canvas, new Point2(1, 1), new Point2(25, 9));

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        Assert.Empty(PadCanvasPrivateAccess.SelectedCadIds(canvas));
        Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
        Assert.Equal(0, PadCanvasPrivateAccess.LastBoxSelectionRedrawTicks(canvas));
        Assert.NotEmpty(changes);
    }

    [AvaloniaFact]
    public void BoxRelease_WithoutControl_ReplacesTheSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedCadIds(canvas).Add(20);

        Drag(canvas, new Point2(1, 1), new Point2(8, 9));

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        Assert.Empty(PadCanvasPrivateAccess.SelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void BoxRelease_WithControl_AddsToTheSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedCadIds(canvas).Add(20);

        Drag(canvas, new Point2(1, 1), new Point2(8, 9), RawInputModifiers.Control);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        AssertSame(PadCanvasPrivateAccess.SelectedCadIds(canvas), 20);
    }

    [AvaloniaFact]
    public void BoxRelease_AcrossCadAndRegularPads_SelectsBothKinds()
    {
        var canvas = CreateCanvas();

        Drag(canvas, new Point2(1, 1), new Point2(38, 9));

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        AssertSame(PadCanvasPrivateAccess.SelectedCadIds(canvas), 20);
    }

    [AvaloniaFact]
    public void BoxRelease_WithAlt_SelectsRegularPadsOnly()
    {
        var canvas = CreateCanvas();

        Drag(canvas, new Point2(1, 1), new Point2(38, 9), RawInputModifiers.Alt);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        Assert.Empty(PadCanvasPrivateAccess.SelectedCadIds(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_OnAnUnselectedPad_SelectsItAndMakesItTheAnchor()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);
        var changes = CollectSelectionChanges(canvas);

        Click(canvas, _firstRegular);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 0), PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
        Assert.NotEmpty(changes);
    }

    [AvaloniaFact]
    public void ClickRelease_OnACadPad_SelectsIt()
    {
        var canvas = CreateCanvas();

        Click(canvas, _cadOnly);

        AssertSame(PadCanvasPrivateAccess.SelectedCadIds(canvas), 20);
        Assert.Empty(PadCanvasPrivateAccess.SelectedRegIdx(canvas));
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Cad, 20), PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_OnEmptySpace_ClearsTheSelectionAndTheAnchor()
    {
        var canvas = CreateCanvas();
        Click(canvas, _firstRegular);

        Click(canvas, _emptySpace);

        Assert.Empty(PadCanvasPrivateAccess.SelectedRegIdx(canvas));
        Assert.Empty(PadCanvasPrivateAccess.SelectedCadIds(canvas));
        Assert.Null(PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_WithControl_TogglesThePadAndKeepsTheOthers()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);

        Click(canvas, _secondRegular, RawInputModifiers.Control);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);

        Click(canvas, _firstRegular, RawInputModifiers.Control);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 1);
    }

    [AvaloniaFact]
    public void ClickRelease_WithControlOnEmptySpace_KeepsTheSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);

        Click(canvas, _emptySpace, RawInputModifiers.Control);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
    }

    [AvaloniaFact]
    public void ClickRelease_WithShiftAndAnchor_SelectsEveryPadBetweenAnchorAndClick()
    {
        var canvas = CreateCanvas();
        Click(canvas, _firstRegular);

        Click(canvas, _cadOnly, RawInputModifiers.Shift);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        AssertSame(PadCanvasPrivateAccess.SelectedCadIds(canvas), 20);
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 0), PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_WithShiftAndNoAnchor_SelectsOnlyThePadAndMakesItTheAnchor()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);

        Click(canvas, _firstRegular, RawInputModifiers.Shift);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 0), PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_WithControlAndShift_TogglesLikeControl()
    {
        var canvas = CreateCanvas();
        Click(canvas, _firstRegular);

        Click(canvas, _secondRegular, RawInputModifiers.Control | RawInputModifiers.Shift);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 1), PadCanvasPrivateAccess.LastSingleSelectedPad(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_OnOneOfSeveralSelectedPads_KeepsTheSelectionAndSchedulesASingleSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);

        Click(canvas, _firstRegular);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        Assert.Equal(new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 0), PadCanvasPrivateAccess.PendingSingleHit(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
    }

    [AvaloniaFact]
    public void ClickRelease_OnTheOnlySelectedPad_SelectsItAtOnce()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);

        Click(canvas, _firstRegular);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        Assert.Null(PadCanvasPrivateAccess.PendingSingleHit(canvas));
    }

    [AvaloniaFact]
    public void Release_WithoutAGesture_ChangesNothing()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);
        var changes = CollectSelectionChanges(canvas);

        WindowOf(canvas).MouseUp(ToWindow(canvas, _firstRegular), MouseButton.Left);

        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 1);
        Assert.Empty(changes);
    }

    private static void AssertSame<T>(IEnumerable<T> actual, params T[] expected)
        where T : notnull
    {
        Assert.Equal(expected.Order(), actual.Order());
    }

    private static Window WindowOf(PadCanvas canvas) => (Window)TopLevel.GetTopLevel(canvas)!;

    // The headless input is posted in window coordinates.
    private static Point ToWindow(PadCanvas canvas, Point2 world) =>
        canvas.TranslatePoint(canvas.WorldToScreen(world), WindowOf(canvas))!.Value;

    private static List<int> CollectSelectionChanges(PadCanvas canvas)
    {
        var changes = new List<int>();
        canvas.SelectionChanged += (_, e) => changes.Add(e.SelectedCadPadIds.Length + e.SelectedRegularPadIndices.Length);
        return changes;
    }

    private static void Click(PadCanvas canvas, Point2 world, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var window = WindowOf(canvas);
        var screen = ToWindow(canvas, world);
        window.MouseDown(screen, MouseButton.Left, modifiers);
        window.MouseUp(screen, MouseButton.Left, modifiers);
    }

    private static void Drag(PadCanvas canvas, Point2 from, Point2 to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var window = WindowOf(canvas);
        var start = ToWindow(canvas, from);
        var end = ToWindow(canvas, to);
        window.MouseDown(start, MouseButton.Left, modifiers);
        window.MouseMove(end, modifiers);
        Assert.True(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
        window.MouseUp(end, MouseButton.Left, modifiers);
    }

    private static PadCanvas CreateCanvas()
    {
        var regularPads = new[]
        {
            new RegularPad(0, 0, 0, Rectangle(0, 0, 10, 10)),
            new RegularPad(0, 1, 1, Rectangle(10, 0, 20, 10)),
        };
        var cadPads = new[]
        {
            new CadPad(20, "Small", "L1", Rectangle(30, 0, 40, 10)),
        };
        var canvas = new PadCanvas { CadPads = cadPads, RegularPads = regularPads, Width = 200, Height = 120 };
        var window = new Window { Content = canvas, Width = 200, Height = 120 };
        window.Show();
        canvas.Measure(new Size(200, 120));
        canvas.Arrange(new Rect(0, 0, 200, 120));
        // The default view puts the pads above the canvas, where no window input reaches them. Zoom in and pan them down.
        PadCanvasPrivateAccess.Zoom(canvas) = 4;
        PadCanvasPrivateAccess.Pan(canvas) = new Vector(10, 100);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);
        return canvas;
    }

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(new[]
    {
        new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY),
    });
}
