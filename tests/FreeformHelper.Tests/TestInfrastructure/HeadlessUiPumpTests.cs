// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Xunit;

namespace FreeformHelper.Tests.TestInfrastructure;

[Collection("HeadlessUiSerial")]
public sealed class HeadlessUiPumpTests
{
    [AvaloniaFact]
    public void Drain_NestedJobsAndLayoutCallback_CompletesResultingLayout()
    {
        var border = new Border { Width = 100, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        var window = new Window { Width = 400, Height = 200, Content = border };
        try
        {
            window.Show();
            HeadlessUiPump.Drain(window);
            var callback = new LayoutCallback(border);
            border.LayoutUpdated += callback.QueueWidthChange;
            window.Dispatcher.Post(
                () => window.Dispatcher.Post(() => border.Width = 200, DispatcherPriority.SystemIdle),
                DispatcherPriority.Background);

            HeadlessUiPump.Drain(window);

            Assert.Equal(300, border.Bounds.Width);
            Assert.True(border.IsMeasureValid);
            Assert.True(border.IsArrangeValid);
            Assert.False(window.Dispatcher.HasJobsWithPriority(DispatcherPriority.SystemIdle));
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class LayoutCallback(Border border)
    {
        public void QueueWidthChange(object? sender, EventArgs args)
        {
            border.LayoutUpdated -= QueueWidthChange;
            border.Dispatcher.Post(() => border.Width = 300, DispatcherPriority.SystemIdle);
        }
    }
}
