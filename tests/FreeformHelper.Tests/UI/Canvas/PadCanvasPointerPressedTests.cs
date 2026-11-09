using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what a pointer press does on a <see cref="PadCanvas"/>: the right-click selection and context events,
/// the double click, the axis label click, and the start of a left-button gesture.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class PadCanvasPointerPressedTests
{
    private static readonly Point2 _emptySpace = new(100, 5);
    private static readonly Point2 _firstRegular = new(5, 5);
    private static readonly Point2 _cadOnly = new(35, 5);

    [AvaloniaFact]
    public void RightPress_OnEmptySpace_ClearsTheSelectionAndRaisesNoContext()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);
        var contexts = CollectContexts(canvas);

        var handled = Press(canvas, _emptySpace, MouseButton.Right);

        Assert.True(handled);
        Assert.Empty(PadCanvasPrivateAccess.SelectedRegIdx(canvas));
        Assert.Empty(contexts);
    }

    [AvaloniaFact]
    public void RightPress_OnUnselectedRegularPad_SelectsItAndRaisesTheRegularContext()
    {
        var canvas = CreateCanvas();
        var contexts = CollectContexts(canvas);

        var handled = Press(canvas, _firstRegular, MouseButton.Right);

        Assert.True(handled);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        AssertSame(contexts, "Regular:0");
    }

    [AvaloniaFact]
    public void RightPress_WithControlOnSelectedPad_TogglesItOffAndStillRaisesTheContext()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);
        var contexts = CollectContexts(canvas);

        var handled = Press(canvas, _firstRegular, MouseButton.Right, KeyModifiers.Control);

        Assert.True(handled);
        Assert.Empty(PadCanvasPrivateAccess.SelectedRegIdx(canvas));
        AssertSame(contexts, "Regular:0");
    }

    [AvaloniaFact]
    public void RightPress_OnSelectedPadWithoutControl_KeepsTheWholeSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);
        var contexts = CollectContexts(canvas);

        var handled = Press(canvas, _firstRegular, MouseButton.Right);

        Assert.True(handled);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0, 1);
        AssertSame(contexts, "Regular:0");
    }

    [AvaloniaFact]
    public void RightPress_OnCadPad_SelectsItAndRaisesTheCadContext()
    {
        var canvas = CreateCanvas();
        var contexts = CollectContexts(canvas);

        var handled = Press(canvas, _cadOnly, MouseButton.Right);

        Assert.True(handled);
        AssertSame(PadCanvasPrivateAccess.SelectedCadIds(canvas), 20);
        AssertSame(contexts, "Cad:20");
    }

    [AvaloniaFact]
    public void LeftDoubleClick_OnEmptySpace_ClearsTheSelectionWithoutStartingAGesture()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(0);

        var handled = Press(canvas, _emptySpace, MouseButton.Left, clickCount: 2);

        Assert.True(handled);
        Assert.Empty(PadCanvasPrivateAccess.SelectedRegIdx(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
    }

    [AvaloniaFact]
    public void LeftDoubleClick_OnRegularPad_SelectsItAndRaisesActivated()
    {
        var canvas = CreateCanvas();
        var activated = new List<int>();
        canvas.RegularPadActivated += (_, e) => activated.Add(e.Pad.Index);

        var handled = Press(canvas, _firstRegular, MouseButton.Left, clickCount: 2);

        Assert.True(handled);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 0);
        AssertSame(activated, 0);
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
    }

    [AvaloniaFact]
    public void LeftSingleClick_StartsTheGestureAndLeavesTheSelectionToTheRelease()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.SelectedRegIdx(canvas).Add(1);

        var handled = Press(canvas, _firstRegular, MouseButton.Left);

        Assert.True(handled);
        Assert.True(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 1);
    }

    [AvaloniaFact]
    public void LeftPress_OnAColumnLabel_SelectsTheColumnWithoutStartingAGesture()
    {
        var canvas = CreateCanvas();
        var labelScreen = canvas.WorldToScreen(new Point2(15, 5));
        PadCanvasPrivateAccess.AxisLabelHits(canvas).Add(new PadCanvas.AxisLabelHit(
            PadCanvas.AxisLabelKind.Column,
            1,
            new Rect(labelScreen.X - 4, labelScreen.Y - 4, 8, 8)));

        var handled = Press(canvas, new Point2(15, 5), MouseButton.Left);

        Assert.True(handled);
        AssertSame(PadCanvasPrivateAccess.SelectedRegIdx(canvas), 1);
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
    }

    [AvaloniaFact]
    public void LeftPress_DropsAPendingSingleSelection()
    {
        var canvas = CreateCanvas();
        PadCanvasPrivateAccess.PendingSingleHit(canvas) = new PadCanvas.HitResult(PadCanvas.HitKind.Regular, 1);

        Press(canvas, _emptySpace, MouseButton.Left);

        Assert.Null(PadCanvasPrivateAccess.PendingSingleHit(canvas));
    }

    private static void AssertSame<T>(IEnumerable<T> actual, params T[] expected)
        where T : notnull
    {
        Assert.Equal(expected.Order(), actual.Order());
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
        // Avalonia 12 resolves pointer positions through a TopLevel.
        var window = new Window { Content = canvas, Width = 200, Height = 120 };
        window.Show();
        canvas.Measure(new Size(200, 120));
        canvas.Arrange(new Rect(0, 0, 200, 120));
        return canvas;
    }

    private static List<string> CollectContexts(PadCanvas canvas)
    {
        var contexts = new List<string>();
        canvas.CadPadContextRequested += (_, e) => contexts.Add($"Cad:{e.Pad.Id}");
        canvas.RegularPadContextRequested += (_, e) => contexts.Add($"Regular:{e.Pad.Index}");
        return contexts;
    }

    private static bool Press(
        PadCanvas canvas,
        Point2 world,
        MouseButton button,
        KeyModifiers modifiers = KeyModifiers.None,
        int clickCount = 1)
    {
        var screen = canvas.WorldToScreen(world);
        var properties = button == MouseButton.Right
            ? new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed)
            : new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        using var pointer = new Pointer(1, PointerType.Mouse, isPrimary: true);
        var root = TopLevel.GetTopLevel(canvas)!;
        var rootPosition = canvas.TranslatePoint(screen, root)!.Value;
        var args = new PointerPressedEventArgs(canvas, pointer, root, rootPosition, 0, properties, modifiers, clickCount);
        canvas.RaiseEvent(args);
        return args.Handled;
    }

    private static Polygon2 Rectangle(double minX, double minY, double maxX, double maxY) => new(new[]
    {
        new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY),
    });
}
