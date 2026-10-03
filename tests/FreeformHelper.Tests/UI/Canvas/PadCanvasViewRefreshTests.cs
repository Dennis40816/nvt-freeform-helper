using System.Reflection;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.UI.Controls;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadCanvasViewRefreshTests
{
    [Fact]
    public void ComputeLowDetailMode_ReturnsTrue_WhenTransientNavigationIsActive()
    {
        var nowTicks = DateTime.UtcNow.Ticks;

        Assert.True(PadCanvas.ComputeLowDetailMode(
            isMiddlePanning: false,
            isSpacePanning: false,
            isBoxSelecting: false,
            zoom: 1.0,
            lowDetailZoomThreshold: 0.3,
            transientLowDetailUntilTicks: nowTicks + TimeSpan.FromMilliseconds(50).Ticks,
            nowTicks: nowTicks));

        Assert.False(PadCanvas.ComputeLowDetailMode(
            isMiddlePanning: false,
            isSpacePanning: false,
            isBoxSelecting: false,
            zoom: 1.0,
            lowDetailZoomThreshold: 0.3,
            transientLowDetailUntilTicks: nowTicks - TimeSpan.FromMilliseconds(1).Ticks,
            nowTicks: nowTicks));
    }

    [Fact]
    public void ShouldDeferSecondaryVisuals_ReturnsTrue_OnlyDuringActiveNavigationWindow()
    {
        var nowTicks = DateTime.UtcNow.Ticks;

        Assert.True(PadCanvas.ShouldDeferSecondaryVisuals(
            isMiddlePanning: true,
            isSpacePanning: false,
            isBoxSelecting: false,
            transientLowDetailUntilTicks: nowTicks - TimeSpan.FromMilliseconds(1).Ticks,
            nowTicks: nowTicks));
        Assert.True(PadCanvas.ShouldDeferSecondaryVisuals(
            isMiddlePanning: false,
            isSpacePanning: false,
            isBoxSelecting: false,
            transientLowDetailUntilTicks: nowTicks + TimeSpan.FromMilliseconds(50).Ticks,
            nowTicks: nowTicks));
        Assert.False(PadCanvas.ShouldDeferSecondaryVisuals(
            isMiddlePanning: false,
            isSpacePanning: false,
            isBoxSelecting: false,
            transientLowDetailUntilTicks: nowTicks - TimeSpan.FromMilliseconds(1).Ticks,
            nowTicks: nowTicks));
    }

    [Fact]
    public void ResolveTailVisualPolicy_RespectsAxisLabelToggle_WhileAllowingHoverDebugDefer()
    {
        var deferredPolicy = PadCanvas.ResolveTailVisualPolicy(
            secondaryVisualsDeferred: true,
            showAxisLabels: true);
        var immediatePolicy = PadCanvas.ResolveTailVisualPolicy(
            secondaryVisualsDeferred: false,
            showAxisLabels: true);
        var hiddenAxisPolicy = PadCanvas.ResolveTailVisualPolicy(
            secondaryVisualsDeferred: false,
            showAxisLabels: false);

        Assert.True(deferredPolicy.DrawAxisLabels);
        Assert.False(deferredPolicy.DrawHoverDebug);
        Assert.True(immediatePolicy.DrawAxisLabels);
        Assert.True(immediatePolicy.DrawHoverDebug);
        Assert.False(hiddenAxisPolicy.DrawAxisLabels);
        Assert.True(hiddenAxisPolicy.DrawHoverDebug);
    }

    [AvaloniaFact]
    public async Task RequestViewRefresh_CoalescesViewChangedNotifications()
    {
        var canvas = new PadCanvas();
        var requestViewRefreshMethod = typeof(PadCanvas).GetMethod("RequestViewRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(requestViewRefreshMethod);

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        requestViewRefreshMethod!.Invoke(canvas, [true]);
        requestViewRefreshMethod.Invoke(canvas, [true]);

        await DrainUiQueueAsync();

        Assert.Equal(1, viewChangedCount);
    }

    [AvaloniaFact]
    public async Task RequestVisualRefresh_DoesNotRaiseViewChanged()
    {
        var canvas = new PadCanvas();
        var requestVisualRefreshMethod = typeof(PadCanvas).GetMethod("RequestVisualRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(requestVisualRefreshMethod);

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        requestVisualRefreshMethod!.Invoke(canvas, [false]);
        requestVisualRefreshMethod.Invoke(canvas, [false]);

        await DrainUiQueueAsync();

        Assert.Equal(0, viewChangedCount);
    }

    [AvaloniaFact]
    public async Task MatchThresholdChange_QueuesVisualRefreshWithoutViewChanged()
    {
        var canvas = new PadCanvas();
        var refreshQueuedField = typeof(PadCanvas).GetField("_isViewRefreshQueued", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(refreshQueuedField);
        await DrainUiQueueAsync();

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        canvas.MatchThreshold += 0.01;

        Assert.True((bool)refreshQueuedField!.GetValue(canvas)!);
        await DrainUiQueueAsync();

        Assert.False((bool)refreshQueuedField.GetValue(canvas)!);
        Assert.Equal(0, viewChangedCount);
    }

    [AvaloniaFact]
    public async Task RequestViewRefresh_ResolvesLatestViewSnapshot_BeforeViewChanged()
    {
        var canvas = new PadCanvas
        {
            Width = 320,
            Height = 180,
        };
        canvas.Measure(new Size(320, 180));
        canvas.Arrange(new Rect(new Size(320, 180)));

        var initialSnapshot = canvas.GetViewFrameSnapshot();
        Assert.Equal(1.0, initialSnapshot.Zoom);

        var zoomField = typeof(PadCanvas).GetField("_zoom", BindingFlags.Instance | BindingFlags.NonPublic);
        var panField = typeof(PadCanvas).GetField("_pan", BindingFlags.Instance | BindingFlags.NonPublic);
        var invalidateViewFrameSnapshotMethod = typeof(PadCanvas).GetMethod("InvalidateViewFrameSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
        var requestViewRefreshMethod = typeof(PadCanvas).GetMethod("RequestViewRefresh", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(zoomField);
        Assert.NotNull(panField);
        Assert.NotNull(invalidateViewFrameSnapshotMethod);
        Assert.NotNull(requestViewRefreshMethod);

        zoomField!.SetValue(canvas, 2.0);
        panField!.SetValue(canvas, new Vector(20, 30));
        invalidateViewFrameSnapshotMethod!.Invoke(canvas, null);

        PadCanvasViewFrameSnapshot snapshotSeenByHandler = default;
        canvas.ViewChanged += (_, _) => snapshotSeenByHandler = canvas.GetViewFrameSnapshot();

        requestViewRefreshMethod!.Invoke(canvas, [true]);
        await DrainUiQueueAsync();

        Assert.True(snapshotSeenByHandler.Revision > initialSnapshot.Revision);
        Assert.Equal(2.0, snapshotSeenByHandler.Zoom);
        Assert.Equal(new Vector(20, 30), snapshotSeenByHandler.Pan);
        Assert.Equal(new Size(320, 180), snapshotSeenByHandler.BoundsSize);
    }

    [AvaloniaFact]
    public async Task FitToContent_WhenFitBoundsOverrideIsSet_UsesOverrideBoundsCenter()
    {
        var fitBounds = new Rect2(100d, 200d, 140d, 260d);
        var canvas = new PadCanvas
        {
            Width = 300,
            Height = 200,
            FitBoundsOverride = fitBounds,
        };
        canvas.Measure(new Size(300, 200));
        canvas.Arrange(new Rect(new Size(300, 200)));

        canvas.FitToContent();
        await DrainUiQueueAsync();

        var viewFrame = canvas.GetViewFrameSnapshot();
        var viewportCenterX = (viewFrame.WorldViewport.MinX + viewFrame.WorldViewport.MaxX) / 2d;
        var viewportCenterY = (viewFrame.WorldViewport.MinY + viewFrame.WorldViewport.MaxY) / 2d;
        var expectedCenterX = (fitBounds.MinX + fitBounds.MaxX) / 2d;
        var expectedCenterY = (fitBounds.MinY + fitBounds.MaxY) / 2d;
        Assert.Equal(expectedCenterX, viewportCenterX, 3);
        Assert.Equal(expectedCenterY, viewportCenterY, 3);
    }

    private static async Task DrainUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }
}
