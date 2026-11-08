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
public sealed class CanvasFocusViewportBehaviorTests
{
    [AvaloniaFact]
    public void TryGetFocusTarget_UsesWiderVisibleSide_AndPrefersLeftOnTie()
    {
        using var scene = new FocusScene();
        (int OffsetX, double ExpectedX)[] cases = [(100, 450), (350, 175), (200, 100)];

        foreach (var (offsetX, expectedX) in cases)
        {
            scene.SetCanvasOrigin(-offsetX, -50);
            var floating = scene.AddWindow();

            var found = CanvasFocusViewportService.TryGetFocusTarget(scene.Surface, [floating], out var target);

            Assert.True(found);
            Assert.Equal(new Point(expectedX, 200), target);
        }
    }

    [AvaloniaFact]
    public void TryGetFocusTarget_WithoutUsableVisibleSpace_ReturnsFalseAndDefaultTarget()
    {
        using var scene = new FocusScene();
        var hidden = scene.AddWindow();
        hidden.Hide();

        Assert.False(CanvasFocusViewportService.TryGetFocusTarget(scene.Surface, [null, hidden], out var noWindowTarget));
        Assert.Equal(default, noWindowTarget);

        (int X, int Y, int Width, int Height)[] occlusions =
        [
            (600, 0, 200, 200),
            (-200, 0, 200, 200),
            (0, 400, 200, 200),
            (0, -200, 200, 200),
            (0, 0, 600, 400),
            (1, 0, 598, 400),
        ];
        foreach (var (x, y, width, height) in occlusions)
        {
            scene.SetCanvasOrigin(-x, -y);
            var floating = scene.AddWindow(width, height);

            Assert.False(CanvasFocusViewportService.TryGetFocusTarget(scene.Surface, [floating], out var target));
            Assert.Equal(default, target);
        }
    }

    [AvaloniaFact]
    public void TryGetFocusTarget_PrioritizesActiveWindow_AndContinuesAfterNonOverlappingWindow()
    {
        using var scene = new FocusScene();
        var inactive = scene.AddWindow();
        var active = scene.AddWindow(400, 20);
        active.Activate();
        HeadlessUiPump.Drain(active);
        Assert.True(active.IsActive);
        Assert.False(inactive.IsActive);

        Assert.True(CanvasFocusViewportService.TryGetFocusTarget(scene.Surface, [inactive, active], out var fallback));
        Assert.Equal(new Point(380, 200), fallback);

        active.Height = 200;
        HeadlessUiPump.Drain(active);

        Assert.True(CanvasFocusViewportService.TryGetFocusTarget(scene.Surface, [inactive, active], out var preferred));
        Assert.Equal(new Point(480, 200), preferred);
    }

    private sealed class FocusScene : IDisposable
    {
        private readonly List<Window> _floatingWindows = new();

        public FocusScene()
        {
            Surface = new PadCanvas { Width = 600, Height = 400 };
            Canvas.SetLeft(Surface, 40);
            Canvas.SetTop(Surface, 30);
            var container = new Canvas();
            container.Children.Add(Surface);
            Host = new Window
            {
                Width = 800,
                Height = 600,
                Content = container,
                ShowActivated = false,
                WindowDecorations = WindowDecorations.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            Host.Show();
            HeadlessUiPump.Drain(Host);
        }

        public PadCanvas Surface { get; }
        public Window Host { get; }

        public void SetCanvasOrigin(double x, double y)
        {
            // Headless top levels share a screen origin; move the canvas to model relative window positions.
            Canvas.SetLeft(Surface, x);
            Canvas.SetTop(Surface, y);
            HeadlessUiPump.Drain(Host);
        }

        public Window AddWindow(int width = 200, int height = 200)
        {
            var window = new Window
            {
                Width = width,
                Height = height,
                ShowActivated = false,
                WindowDecorations = WindowDecorations.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            _floatingWindows.Add(window);
            window.Show();
            HeadlessUiPump.Drain(window);
            return window;
        }

        public void Dispose()
        {
            foreach (var window in _floatingWindows)
            {
                window.Close();
            }
            Host.Close();
        }
    }
}
