using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
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

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        PadCanvasPrivateAccess.RequestViewRefresh(canvas, true);
        PadCanvasPrivateAccess.RequestViewRefresh(canvas, true);

        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(1, viewChangedCount);
    }

    [AvaloniaFact]
    public async Task RequestVisualRefresh_DoesNotRaiseViewChanged()
    {
        var canvas = new PadCanvas();

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        PadCanvasPrivateAccess.RequestVisualRefresh(canvas, false);
        PadCanvasPrivateAccess.RequestVisualRefresh(canvas, false);

        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(0, viewChangedCount);
    }

    [AvaloniaFact]
    public async Task MatchThresholdChange_QueuesVisualRefreshWithoutViewChanged()
    {
        var canvas = new PadCanvas();
        await HeadlessUiPump.FlushQueueAsync();

        var viewChangedCount = 0;
        canvas.ViewChanged += (_, _) => viewChangedCount++;

        canvas.MatchThreshold += 0.01;

        Assert.True(PadCanvasPrivateAccess.IsViewRefreshQueued(canvas));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(PadCanvasPrivateAccess.IsViewRefreshQueued(canvas));
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

        PadCanvasPrivateAccess.Zoom(canvas) = 2.0;
        PadCanvasPrivateAccess.Pan(canvas) = new Vector(20, 30);
        PadCanvasPrivateAccess.InvalidateViewFrameSnapshot(canvas);

        PadCanvasViewFrameSnapshot snapshotSeenByHandler = default;
        canvas.ViewChanged += (_, _) => snapshotSeenByHandler = canvas.GetViewFrameSnapshot();

        PadCanvasPrivateAccess.RequestViewRefresh(canvas, true);
        await HeadlessUiPump.FlushQueueAsync();

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
        await HeadlessUiPump.FlushQueueAsync();

        var viewFrame = canvas.GetViewFrameSnapshot();
        var viewportCenterX = (viewFrame.WorldViewport.MinX + viewFrame.WorldViewport.MaxX) / 2d;
        var viewportCenterY = (viewFrame.WorldViewport.MinY + viewFrame.WorldViewport.MaxY) / 2d;
        var expectedCenterX = (fitBounds.MinX + fitBounds.MaxX) / 2d;
        var expectedCenterY = (fitBounds.MinY + fitBounds.MaxY) / 2d;
        Assert.Equal(expectedCenterX, viewportCenterX, 3);
        Assert.Equal(expectedCenterY, viewportCenterY, 3);
    }

    [AvaloniaFact]
    public async Task FitToContent_WhenFitTopInsetIsSet_LeavesThatBandFreeAtTheTop()
    {
        var withoutInset = await FitZoomAsync(fitTopInset: 0d);
        var withInset = await FitZoomAsync(fitTopInset: 40d);

        // Canvas 300 x 200, margin 20 on every side, bounds 100 x 100: the height limits the zoom.
        Assert.Equal(1.6d, withoutInset, 3);
        Assert.Equal(1.2d, withInset, 3);
    }

    private static async Task<double> FitZoomAsync(double fitTopInset)
    {
        var canvas = new PadCanvas
        {
            Width = 300,
            Height = 200,
            FitBoundsOverride = new Rect2(0d, 0d, 100d, 100d),
            FitTopInset = fitTopInset,
        };
        canvas.Measure(new Size(300, 200));
        canvas.Arrange(new Rect(new Size(300, 200)));

        canvas.FitToContent();
        await HeadlessUiPump.FlushQueueAsync();

        return canvas.GetViewFrameSnapshot().Zoom;
    }
}
