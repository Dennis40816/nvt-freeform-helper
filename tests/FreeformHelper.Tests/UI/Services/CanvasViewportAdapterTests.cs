using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasViewportAdapterTests
{
    [AvaloniaFact]
    public async Task Attach_QueuesInitialFit_AndHandlesFitRequests()
    {
        var canvas = new PadCanvas
        {
            Width = 320,
            Height = 180,
            FitBoundsOverride = new Rect2(0d, 0d, 40d, 20d),
        };
        canvas.Measure(new Size(320, 180));
        canvas.Arrange(new Rect(new Size(320, 180)));

        var viewportChangedCount = 0;
        var adapter = new CanvasViewportAdapter(canvas, () => viewportChangedCount++);
        var emitter = new FitRequestEmitter();

        adapter.Attach(handler => emitter.FitCanvasRequested += handler);
        await DrainUiQueueAsync();

        var initialSnapshot = canvas.GetViewFrameSnapshot();
        Assert.True(initialSnapshot.Zoom > 1.0);

        var initialRevision = initialSnapshot.Revision;
        emitter.Raise();
        await DrainUiQueueAsync();

        var afterExplicitFit = canvas.GetViewFrameSnapshot();
        Assert.True(afterExplicitFit.Revision > initialRevision);
        Assert.True(viewportChangedCount > 0);

        adapter.Detach(handler => emitter.FitCanvasRequested -= handler);
    }

    [AvaloniaFact]
    public async Task ToOverlayRect_UsesViewFrameWorldToScreenTransform()
    {
        var canvas = new PadCanvas
        {
            Width = 300,
            Height = 200,
            FitBoundsOverride = new Rect2(0d, 0d, 10d, 10d),
        };
        canvas.Measure(new Size(300, 200));
        canvas.Arrange(new Rect(new Size(300, 200)));
        canvas.FitToContent();
        await DrainUiQueueAsync();

        var adapter = new CanvasViewportAdapter(canvas, static () => { });
        var overlay = new Canvas();
        var frame = adapter.GetViewFrameSnapshot();

        var actual = adapter.ToOverlayRect(overlay, new Rect2(2d, 3d, 6d, 8d), frame);
        var expectedTopLeft = frame.WorldToScreen.Transform(new Point(2d, 8d));
        var expectedBottomRight = frame.WorldToScreen.Transform(new Point(6d, 3d));
        var expected = new Rect(expectedTopLeft, expectedBottomRight).Normalize();

        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }

    [AvaloniaFact]
    public void ApplyClipPolicy_SetsClipToBoundsForHosts()
    {
        var grid = new Grid { ClipToBounds = false };
        var overlay = new Canvas { ClipToBounds = false };

        CanvasViewportAdapter.ApplyClipPolicy(grid, overlay);

        Assert.True(grid.ClipToBounds);
        Assert.True(overlay.ClipToBounds);
    }

    private static async Task DrainUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private sealed class FitRequestEmitter
    {
        public event EventHandler? FitCanvasRequested;

        public void Raise()
        {
            FitCanvasRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
