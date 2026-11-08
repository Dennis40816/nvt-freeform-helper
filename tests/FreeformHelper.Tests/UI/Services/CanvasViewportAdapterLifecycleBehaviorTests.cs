// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasViewportAdapterLifecycleBehaviorTests
{
    [AvaloniaFact]
    public async Task AttachAndDetach_RepeatedCalls_KeepOneSubscriptionAndStopViewportNotificationsAsync()
    {
        var canvas = new PadCanvas { Width = 320, Height = 180, FitBoundsOverride = new Rect2(0, 0, 40, 20) };
        canvas.Measure(new Size(320, 180));
        canvas.Arrange(new Rect(new Size(320, 180)));
        var notifications = 0;
        var subscriptions = 0;
        var unsubscriptions = 0;
        EventHandler? fitRequested = null;
        Action<EventHandler> subscribe = handler =>
        {
            subscriptions++;
            fitRequested += handler;
        };
        Action<EventHandler> unsubscribe = handler =>
        {
            unsubscriptions++;
            fitRequested -= handler;
        };
        var adapter = new CanvasViewportAdapter(canvas, () => notifications++);

        adapter.Detach(unsubscribe);
        Assert.Equal(0, unsubscriptions);
        adapter.Attach(subscribe);
        adapter.Attach(subscribe);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(1, subscriptions);

        notifications = 0;
        canvas.ResetView();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(1, notifications);
        fitRequested?.Invoke(this, EventArgs.Empty);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(adapter.GetViewFrameSnapshot().Zoom > 1);

        adapter.Detach(unsubscribe);
        adapter.Detach(unsubscribe);
        Assert.Equal(1, unsubscriptions);
        notifications = 0;
        canvas.ResetView();
        await HeadlessUiPump.FlushQueueAsync();
        fitRequested?.Invoke(this, EventArgs.Empty);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal(0, notifications);
        Assert.Equal(1d, adapter.GetViewFrameSnapshot().Zoom);
    }
}
