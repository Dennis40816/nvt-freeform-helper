using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasGestureResetTests
{
    [AvaloniaFact]
    public void NormalRelease_PreservesHeldSpaceAcrossTwoPanGestures()
    {
        var canvas = new PadCanvas { Focusable = true };
        var window = CreateWindow(canvas);
        var captureLosses = 0;
        canvas.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => captureLosses++, handledEventsToo: true);
        var selected = PadCanvasPrivateAccess.SelectedCadIds(canvas);
        selected.Add(17);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");

            for (var gesture = 1; gesture <= 2; gesture++)
            {
                window.MouseDown(new Point(40, 40), MouseButton.Left);
                Assert.True(PadCanvasPrivateAccess.IsSpacePanning(canvas));
                Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
                window.MouseMove(new Point(60, 60));
                Assert.Equal(new Vector(20 * gesture, 20 * gesture), canvas.GetViewFrameSnapshot().Pan);
                Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
                window.MouseUp(new Point(60, 60), MouseButton.Left);
                Assert.False(PadCanvasPrivateAccess.IsSpacePanning(canvas));
                Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
                Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
            }

            Assert.Equal(2, captureLosses);
            Assert.Single(selected);
            Assert.Contains(17, selected);
            Assert.True(PadCanvasPrivateAccess.IsSpacePressed(canvas));
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.False(PadCanvasPrivateAccess.IsSpacePressed(canvas));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CaptureLost_CancelsMiddlePanBeforeNextMove()
    {
        var canvas = new PadCanvas { Focusable = true };
        var window = CreateWindow(canvas);
        IPointer? pointer = null;
        canvas.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);
        try
        {
            window.Show();
            window.MouseDown(new Point(40, 40), MouseButton.Middle);
            Assert.True(PadCanvasPrivateAccess.IsMiddlePanning(canvas));
            window.MouseMove(new Point(60, 60));
            var pan = canvas.GetViewFrameSnapshot().Pan;
            Assert.NotEqual(default, pan);

            Assert.NotNull(pointer);
            pointer.Capture(null);
            AssertGestureIsReset(canvas);
            window.MouseMove(new Point(100, 100));
            Assert.Equal(pan, canvas.GetViewFrameSnapshot().Pan);
            Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
            window.MouseUp(new Point(100, 100), MouseButton.Middle);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CaptureLost_DismissesBoxWithoutApplyingSelection()
    {
        var canvas = new PadCanvas { Focusable = true };
        var window = CreateWindow(canvas);
        IPointer? pointer = null;
        canvas.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);
        var selected = PadCanvasPrivateAccess.SelectedCadIds(canvas);
        selected.Add(17);
        try
        {
            window.Show();
            window.MouseDown(new Point(40, 40), MouseButton.Left);
            window.MouseMove(new Point(80, 80));
            Assert.True(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
            await DrainUiQueueAsync();
            Assert.False(PadCanvasPrivateAccess.IsViewRefreshQueued(canvas));

            Assert.NotNull(pointer);
            pointer.Capture(null);
            AssertGestureIsReset(canvas);
            Assert.True(PadCanvasPrivateAccess.IsViewRefreshQueued(canvas));
            await DrainUiQueueAsync();
            var pan = canvas.GetViewFrameSnapshot().Pan;
            window.MouseMove(new Point(120, 120));
            window.MouseUp(new Point(120, 120), MouseButton.Left);
            Assert.Equal(pan, canvas.GetViewFrameSnapshot().Pan);
            Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));
            Assert.Single(selected);
            Assert.Contains(17, selected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LostFocus_ClearsSpacePanAndSpaceKeyBeforeNextGesture()
    {
        var canvas = new PadCanvas { Focusable = true };
        IPointer? pointer = null;
        canvas.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);
        var other = new Button { Content = "Other" };
        var panel = new DockPanel();
        DockPanel.SetDock(other, Dock.Bottom);
        panel.Children.Add(other);
        panel.Children.Add(canvas);
        var window = CreateWindow(panel);
        try
        {
            window.Show();
            Assert.True(canvas.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.MouseDown(new Point(40, 40), MouseButton.Left);
            Assert.True(PadCanvasPrivateAccess.IsSpacePanning(canvas));
            Assert.True(other.Focus());
            AssertGestureIsReset(canvas);

            var pan = canvas.GetViewFrameSnapshot().Pan;
            window.MouseMove(new Point(100, 100));
            window.MouseUp(new Point(100, 100), MouseButton.Left);
            Assert.Equal(pan, canvas.GetViewFrameSnapshot().Pan);
            Assert.NotNull(pointer);
            Assert.Null(pointer.Captured);
            Assert.True(canvas.Focus());
            window.MouseDown(new Point(140, 40), MouseButton.Left);
            Assert.False(PadCanvasPrivateAccess.IsSpacePanning(canvas));
            Assert.True(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
            window.MouseUp(new Point(140, 40), MouseButton.Left);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(Control content) => new() { Width = 240, Height = 180, Content = content };

    private static void AssertGestureIsReset(PadCanvas canvas)
    {
        Assert.False(PadCanvasPrivateAccess.IsMiddlePanning(canvas));
        Assert.False(PadCanvasPrivateAccess.IsSpacePanning(canvas));
        Assert.False(PadCanvasPrivateAccess.IsSpacePressed(canvas));
        Assert.False(PadCanvasPrivateAccess.IsLeftPointerDown(canvas));
        Assert.False(PadCanvasPrivateAccess.IsBoxSelecting(canvas));

        Assert.Equal(0, PadCanvasPrivateAccess.LastPanRedrawTicks(canvas));
        Assert.Equal(0, PadCanvasPrivateAccess.LastBoxSelectionRedrawTicks(canvas));
    }

    private static async Task DrainUiQueueAsync()
    {
        await HeadlessUiPump.YieldAsync(DispatcherPriority.Render);
        await HeadlessUiPump.YieldAsync(DispatcherPriority.Background);
    }
}
