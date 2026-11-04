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

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasKeyboardAndWheelTests
{
    private const int ZoomPrecision = 6;
    private const double NudgeStep = 35.0;

    private static Window CreateWindow(Control content) => new() { Width = 240, Height = 180, Content = content };

    private static RegularPad Regular(int row, int col, int index, double minX, double minY, double maxX, double maxY)
    {
        return new RegularPad(
            row,
            col,
            index,
            new Polygon2([new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]));
    }

    private static CadPad Cad(int id, double minX, double minY, double maxX, double maxY)
    {
        return new CadPad(
            id,
            $"pad-{id}",
            "SIG",
            new Polygon2([new Point2(minX, minY), new Point2(maxX, minY), new Point2(maxX, maxY), new Point2(minX, maxY)]));
    }

    [AvaloniaFact]
    public void CtrlA_SelectsEveryCadAndRegularPad()
    {
        var canvas = new PadCanvas
        {
            Focusable = true,
            ShowCad = true,
            ShowRegular = true,
            CadPads = [Cad(11, 0, 0, 10, 10), Cad(12, 20, 0, 30, 10)],
            RegularPads = [Regular(0, 0, 0, 0, 0, 10, 10), Regular(0, 1, 1, 10, 0, 20, 10), Regular(0, 2, 2, 20, 0, 30, 10)],
        };
        PadCanvasSelectionChangedEventArgs? changed = null;
        canvas.SelectionChanged += (_, e) => changed = e;
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());

            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");

            Assert.NotNull(changed);
            Assert.Equal([11, 12], changed.SelectedCadPadIds.ToArray());
            Assert.Equal([0, 1, 2], changed.SelectedRegularPadIndices.ToArray());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AKeyWithoutControl_DoesNotSelectAnything()
    {
        var canvas = new PadCanvas
        {
            Focusable = true,
            ShowRegular = true,
            RegularPads = [Regular(0, 0, 0, 0, 0, 10, 10)],
        };
        var selectionChanges = 0;
        canvas.SelectionChanged += (_, _) => selectionChanges++;
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());

            window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, "a");

            Assert.Equal(0, selectionChanges);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FKey_FitsTheContentIntoTheView()
    {
        var canvas = new PadCanvas
        {
            Focusable = true,
            ShowRegular = true,
            RegularPads = [Regular(0, 0, 0, 0, 0, 100, 50), Regular(0, 1, 1, 100, 0, 200, 50)],
        };
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            HeadlessUiPump.Drain(window);
            Assert.True(canvas.Focus());
            canvas.ResetView();
            var before = canvas.GetViewFrameSnapshot();
            Assert.Equal(1.0, before.Zoom);

            window.KeyPress(Key.F, RawInputModifiers.None, PhysicalKey.F, "f");
            HeadlessUiPump.Drain(window);

            var after = canvas.GetViewFrameSnapshot();
            Assert.NotEqual(before.Zoom, after.Zoom);
            Assert.True(after.WorldViewport.MinX <= 0 && after.WorldViewport.MaxX >= 200);
            Assert.True(after.WorldViewport.MinY <= 0 && after.WorldViewport.MaxY >= 50);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(Key.Left, PhysicalKey.ArrowLeft, NudgeStep, 0.0)]
    [InlineData(Key.Right, PhysicalKey.ArrowRight, -NudgeStep, 0.0)]
    [InlineData(Key.Up, PhysicalKey.ArrowUp, 0.0, NudgeStep)]
    [InlineData(Key.Down, PhysicalKey.ArrowDown, 0.0, -NudgeStep)]
    public void ShiftArrow_NudgesThePanByOneStep(Key key, PhysicalKey physicalKey, double expectedPanX, double expectedPanY)
    {
        var canvas = new PadCanvas { Focusable = true };
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());
            canvas.ResetView();

            window.KeyPress(key, RawInputModifiers.Shift, physicalKey, null);

            Assert.Equal(new Vector(expectedPanX, expectedPanY), canvas.GetViewFrameSnapshot().Pan);
            Assert.Equal(1.0, canvas.GetViewFrameSnapshot().Zoom);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ArrowWithoutShift_DoesNotMoveTheView()
    {
        var canvas = new PadCanvas { Focusable = true };
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());
            canvas.ResetView();

            window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);

            Assert.Equal(default, canvas.GetViewFrameSnapshot().Pan);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(1.0, false, 1.15)]
    [InlineData(-1.0, false, 1.0 / 1.15)]
    [InlineData(1.0, true, 1.25)]
    [InlineData(-2.0, true, 1.0 / (1.25 * 1.25))]
    public void MouseWheel_WithWholeSteps_ZoomsAroundThePointer(double wheelY, bool withControl, double expectedZoom)
    {
        var canvas = new PadCanvas();
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            canvas.ResetView();
            var pivot = new Point(80, 60);
            var worldBefore = ToWorld(canvas.GetViewFrameSnapshot(), pivot);

            window.MouseWheel(pivot, new Vector(0, wheelY), withControl ? RawInputModifiers.Control : RawInputModifiers.None);

            var after = canvas.GetViewFrameSnapshot();
            Assert.Equal(expectedZoom, after.Zoom, ZoomPrecision);
            var screenAfter = ToScreen(after, worldBefore);
            Assert.Equal(pivot.X, screenAfter.X, ZoomPrecision);
            Assert.Equal(pivot.Y, screenAfter.Y, ZoomPrecision);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MouseWheel_WithAFractionalStep_PansInsteadOfZooming()
    {
        var canvas = new PadCanvas();
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            canvas.ResetView();

            window.MouseWheel(new Point(80, 60), new Vector(0, 0.5), RawInputModifiers.None);

            var after = canvas.GetViewFrameSnapshot();
            Assert.Equal(1.0, after.Zoom);
            Assert.Equal(0.0, after.Pan.X);
            Assert.True(after.Pan.Y > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MouseWheel_WithAHorizontalStep_PansInsteadOfZooming()
    {
        var canvas = new PadCanvas();
        var window = CreateWindow(canvas);
        try
        {
            window.Show();
            canvas.ResetView();

            window.MouseWheel(new Point(80, 60), new Vector(1, 0), RawInputModifiers.None);

            var after = canvas.GetViewFrameSnapshot();
            Assert.Equal(1.0, after.Zoom);
            Assert.True(after.Pan.X > 0);
            Assert.Equal(0.0, after.Pan.Y);
        }
        finally
        {
            window.Close();
        }
    }

    private static Point2 ToWorld(PadCanvasViewFrameSnapshot frame, Point screen)
    {
        return new Point2((screen.X - frame.Pan.X) / frame.Zoom, -(screen.Y - frame.Pan.Y) / frame.Zoom);
    }

    private static Point ToScreen(PadCanvasViewFrameSnapshot frame, Point2 world)
    {
        return new Point((world.X * frame.Zoom) + frame.Pan.X, (-world.Y * frame.Zoom) + frame.Pan.Y);
    }
}
