// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CanvasFocusViewportDpiTests
{
    [AvaloniaFact]
    public void TryGetFocusTarget_At150PercentScaling_ReturnsTheCenterInCanvasUnits()
    {
        AssertVisibleCenterAtScaling(1.5);
    }

    [AvaloniaFact]
    public void TryGetFocusTarget_At200PercentScaling_ReturnsTheCenterInCanvasUnits()
    {
        AssertVisibleCenterAtScaling(2);
    }

    private static void AssertVisibleCenterAtScaling(double scaling)
    {
        var surface = new PadCanvas { Width = 600, Height = 400 };
        Canvas.SetLeft(surface, -400);
        Canvas.SetTop(surface, 0);
        var container = new Canvas();
        container.Children.Add(surface);
        var host = new Window
        {
            Width = 800,
            Height = 600,
            Content = container,
            ShowActivated = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        var floating = new Window
        {
            Width = 200,
            Height = 400,
            ShowActivated = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        try
        {
            host.SetRenderScaling(scaling);
            host.Show();
            HeadlessUiPump.Drain(host);
            floating.SetRenderScaling(scaling);
            floating.Show();
            HeadlessUiPump.Drain(floating);

            var found = CanvasFocusViewportService.TryGetFocusTarget(surface, [floating], out var target);

            Assert.True(found);
            // The visible part of the canvas is its left 400 by 400 units, so its center is (200, 200).
            Assert.Equal(new Point(200, 200), target);
        }
        finally
        {
            floating.Close();
            host.Close();
        }
    }
}
