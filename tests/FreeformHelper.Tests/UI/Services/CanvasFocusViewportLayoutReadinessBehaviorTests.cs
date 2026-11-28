// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasFocusViewportLayoutReadinessBehaviorTests
{
    [AvaloniaFact]
    public void TryGetFocusTarget_NarrowUnhostedCanvas_DefersFocusUntilUsableLayout()
    {
        var surface = new PadCanvas();
        var floating = new Window
        {
            Width = 200,
            Height = 200,
            ShowActivated = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        var host = new Window
        {
            Width = 800,
            Height = 600,
            ShowActivated = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        try
        {
            floating.Show();
            HeadlessUiPump.Drain(floating);
            (double Width, double Height)[] narrowLayouts = [(1, 400), (600, 1)];
            foreach (var (width, height) in narrowLayouts)
            {
                surface.Width = width;
                surface.Height = height;
                surface.Measure(new Size(width, height));
                surface.Arrange(new Rect(0, 0, width, height));
                Assert.Equal(new Size(width, height), surface.Bounds.Size);

                var found = CanvasFocusViewportService.TryGetFocusTarget(surface, [floating], out var target);

                Assert.False(found);
                Assert.Equal(default, target);
            }

            surface.Width = 600;
            surface.Height = 400;
            Canvas.SetLeft(surface, -100);
            Canvas.SetTop(surface, -50);
            var container = new Canvas();
            container.Children.Add(surface);
            host.Content = container;
            host.Show();
            HeadlessUiPump.Drain(host);

            Assert.True(CanvasFocusViewportService.TryGetFocusTarget(surface, [floating], out var readyTarget));
            Assert.Equal(new Point(450, 200), readyTarget);
        }
        finally
        {
            floating.Close();
            host.Close();
        }
    }
}
