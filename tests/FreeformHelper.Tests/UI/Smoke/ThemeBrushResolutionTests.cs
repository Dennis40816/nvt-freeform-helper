// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Icons;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ThemeBrushResolutionTests
{
    private static readonly string[] _viewportStates = ["", ":pointerover", ":pressed", ":checked", ":disabled", ":focus-visible"];

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task NotchPreviewUsesCoreSurfaceAcrossAttachmentAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var canvas = new NotchPreviewCanvas();
        var window = new Window { Width = 240, Height = 120 };
        try
        {
            app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            Assert.True(app.TryGetResource("NfcSurfaceBrush", app.RequestedThemeVariant, out var resource));
            var expected = Assert.IsAssignableFrom<ISolidColorBrush>(resource);
            canvas.Measure(new global::Avalonia.Size(240, 120));
            canvas.Arrange(new global::Avalonia.Rect(0, 0, 240, 120));
            AssertBackground(canvas, expected.Color);

            window.Content = canvas;
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            AssertBackground(canvas, expected.Color);

            window.Content = null;
            await HeadlessUiPump.FlushQueueAsync();
            AssertBackground(canvas, expected.Color);
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task CoordinateOverlayUsesCoreMutedBrushAcrossAttachmentAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var owner = new Border();
        var window = new Window { Width = 240, Height = 120 };
        try
        {
            app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            Assert.True(app.TryGetResource("NfcTextMutedBrush", app.RequestedThemeVariant, out var resource));
            var expected = Assert.IsAssignableFrom<ISolidColorBrush>(resource);

            var style = CoordinatePlannerOverlayStyle.Resolve(owner);
            Assert.Same(expected, style.MutedBrush);

            window.Content = owner;
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(expected, CoordinatePlannerOverlayStyle.Resolve(owner).MutedBrush);

            window.Content = null;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Same(expected, CoordinatePlannerOverlayStyle.Resolve(owner).MutedBrush);
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task TextToggleFillDistinguishesCheckedFromUncheckedAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var on = new ToggleButton { Content = "On", IsChecked = true };
        var off = new ToggleButton { Content = "Off" };
        foreach (var toggle in new[] { on, off })
        {
            toggle.Classes.Add("canvasQuickToggle");
        }

        var window = new Window { Width = 240, Height = 120, Content = new StackPanel { Children = { on, off } } };
        try
        {
            app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            // The canvas toolbar has no border or check mark: the fill is the only on/off signal.
            var onFill = Assert.IsAssignableFrom<ISolidColorBrush>(on.Background).Color;
            var offFill = Assert.IsAssignableFrom<ISolidColorBrush>(off.Background).Color;
            var onText = Assert.IsAssignableFrom<ISolidColorBrush>(on.Foreground).Color;
            var fillContrast = Contrast(onFill, offFill);
            var textContrast = Contrast(onText, onFill);
            Assert.True(fillContrast >= 3.0, $"{theme}: checked/unchecked fill contrast {fillContrast:F2} is below 3:1.");
            Assert.True(textContrast >= 4.5, $"{theme}: checked label contrast {textContrast:F2} is below 4.5:1.");
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task ViewportOverlayGlyphContrastIncludesControlOpacityAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var overlay = new CanvasOverlayControls();
        var toggle = new ToggleButton
        {
            Content = new FontIcon { Glyph = NvtIcons.FitScreen, Classes = { "symbols" } },
            Classes = { "viewportOverlayAction" },
        };
        var window = new Window
        {
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { overlay, toggle } },
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var backdrop = Assert.IsAssignableFrom<ISolidColorBrush>(
                window.FindResource(window.ActualThemeVariant, "BrushCanvasBackground"));
            window.Background = backdrop;
            // The fit button is the unnamed floating button; the two named ones show or hide side panels.
            var fit = Assert.Single(overlay.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("viewportOverlayAction") && button.Name is null);
            foreach (var button in new Button[] { fit, toggle })
            {
                foreach (var state in _viewportStates)
                {
                    if (state == ":checked" && button is not ToggleButton) continue;
                    button.IsEnabled = state != ":disabled";
                    if (button is ToggleButton stateToggle) stateToggle.IsChecked = state == ":checked";
                    var pseudoClasses = (IPseudoClasses)button.Classes;
                    pseudoClasses.Set(":pointerover", state == ":pointerover");
                    pseudoClasses.Set(":pressed", state == ":pressed");
                    pseudoClasses.Set(":focus-visible", state == ":focus-visible");
                    await HeadlessUiPump.FlushQueueAsync();
                    var glyph = Assert.IsType<FontIcon>(button.Content);
                    var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(glyph.Foreground).Color;
                    var fill = Assert.IsAssignableFrom<ISolidColorBrush>(button.Background).Color;
                    var glyphOnFill = CompositeOver(foreground, glyph.Opacity, fill);
                    var ratio = Contrast(CompositeOver(glyphOnFill, button.Opacity, backdrop.Color),
                        CompositeOver(fill, button.Opacity, backdrop.Color));
                    Assert.True(ratio >= 3d, $"{theme} {button.GetType().Name} {state}: glyph contrast {ratio:F2} is below 3:1.");
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    private static Color CompositeOver(Color color, double opacity, Color backdrop)
    {
        var foregroundAlpha = color.A / 255d * opacity;
        var backgroundAlpha = backdrop.A / 255d * (1d - foregroundAlpha);
        var alpha = foregroundAlpha + backgroundAlpha;
        if (alpha == 0d) return Colors.Transparent;
        static byte Channel(byte value, byte background, double sourceWeight, double backgroundWeight, double totalWeight) =>
            (byte)Math.Round((value * sourceWeight + background * backgroundWeight) / totalWeight, MidpointRounding.AwayFromZero);

        return Color.FromArgb((byte)Math.Round(alpha * 255d, MidpointRounding.AwayFromZero),
            Channel(color.R, backdrop.R, foregroundAlpha, backgroundAlpha, alpha),
            Channel(color.G, backdrop.G, foregroundAlpha, backgroundAlpha, alpha),
            Channel(color.B, backdrop.B, foregroundAlpha, backgroundAlpha, alpha));
    }

    private static double Contrast(Color first, Color second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static void AssertBackground(NotchPreviewCanvas canvas, Color expected)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            canvas.Render(context);
        }

        var background = Assert.IsType<GeometryDrawing>(Assert.Single(drawing.Children));
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(background.Brush).Color);
    }
}
