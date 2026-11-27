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
    private static readonly string[] _textButtonRoles = ["actionPrimary", "actionNeutral", "actionDanger", "actionGhost", "chipAction"];
    private static readonly string[] _textButtonStates = ["", ":pointerover", ":pressed", ":focus-visible"];

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
    public async Task TextToggleMarkDistinguishesCheckedFromUncheckedAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var on = new ToggleButton { Content = "On", IsChecked = true, Classes = { "actionGhost", "flat", "marked" } };
        var off = new ToggleButton { Content = "Off", Classes = { "actionGhost", "flat", "marked" } };

        var window = new Window { Width = 240, Height = 120, Content = new StackPanel { Children = { on, off } } };
        window.Classes.Add("reducedMotion");
        try
        {
            app.RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            var canvas = CanvasColor(window);
            var onFill = Assert.IsAssignableFrom<ISolidColorBrush>(on.Background).Color;
            var offFill = Assert.IsAssignableFrom<ISolidColorBrush>(off.Background).Color;
            var mark = Assert.IsAssignableFrom<ISolidColorBrush>(on.BorderBrush).Color;
            var onText = Assert.IsAssignableFrom<ISolidColorBrush>(on.Foreground).Color;
            Assert.Equal(0, onFill.A);
            Assert.Equal(0, offFill.A);
            Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(off.BorderBrush).Color.A);
            Assert.True(on.BorderThickness.Left > 0);
            Assert.True(Contrast(mark, canvas) >= 3.0, $"{theme}: checked mark contrast is below 3:1.");
            Assert.True(Contrast(onText, canvas) >= 4.5, $"{theme}: checked label contrast is below 4.5:1.");
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
            Classes = { "actionIconButton", "actionGhost" },
        };
        var plain = new Button
        {
            Content = new FontIcon { Glyph = NvtIcons.FitScreen, Classes = { "symbols" } },
            Classes = { "actionIconButton", "actionGhost" },
        };
        var window = new Window
        {
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { overlay, plain, toggle } },
        };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var backdrop = CanvasColor(window);
            window.Background = new SolidColorBrush(backdrop);
            // The fit button is the unnamed floating button; the two named ones show or hide side panels.
            // Without a view model it stays disabled, so the plain button covers the other states.
            var fit = Assert.Single(overlay.GetVisualDescendants().OfType<Button>(),
                static button => button.Classes.Contains("actionIconButton") && button.Content is FontIcon { Glyph: NvtIcons.FitScreen } && button.Name is null);
            foreach (var button in new Button[] { fit, plain, toggle })
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
                    // Avalonia applies the control opacity to every shape, not to the button as one layer.
                    var shownFill = CompositeOver(fill, button.Opacity, backdrop);
                    var ratio = Contrast(CompositeOver(foreground, glyph.Opacity * button.Opacity, shownFill), shownFill);
                    Assert.True(ratio >= 3d, $"{theme} {button.GetType().Name} {state}: glyph contrast {ratio:F2} is below 3:1.");
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task CanvasToolbarAtRestKeepsTheCheckedMarkVisibleAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var overlay = new CanvasOverlayControls();
        var window = new Window
        {
            Width = 900,
            Height = 200,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = overlay,
        };
        window.Classes.Add("reducedMotion");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var toolbar = Assert.Single(overlay.GetVisualDescendants().OfType<Border>(),
                static border => border.Classes.Contains("canvasQuickToolbar"));
            var toggle = overlay.GetVisualDescendants().OfType<ToggleButton>().First();
            toggle.IsChecked = true;
            await HeadlessUiPump.FlushQueueAsync();

            var canvas = CanvasColor(window);
            var fill = Assert.IsAssignableFrom<ISolidColorBrush>(toggle.Background).Color;
            var mark = CompositeOver(Assert.IsAssignableFrom<ISolidColorBrush>(toggle.BorderBrush).Color, toolbar.Opacity, canvas);
            var text = CompositeOver(Assert.IsAssignableFrom<ISolidColorBrush>(toggle.Foreground).Color, toolbar.Opacity, canvas);
            Assert.Equal(1, toolbar.Opacity);
            Assert.Equal(0, fill.A);
            Assert.True(toggle.BorderThickness.Left > 0);
            Assert.True(Contrast(mark, canvas) >= 3d, $"{theme}: checked mark on the canvas is {Contrast(mark, canvas):F2}:1.");
            Assert.True(Contrast(text, canvas) >= 4.5d, $"{theme}: checked label on the canvas is {Contrast(text, canvas):F2}:1.");
        }
        finally
        {
            window.Close();
        }
    }

    // NFH replaces the Core accent, so the role buttons with text are measured here with the NFH colours.
    [AvaloniaTheory]
    [InlineData("Dark")]
    [InlineData("Light")]
    public async Task RoleButtonTextKeepsContrastWithTheProductAccentAsync(string theme)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var buttons = _textButtonRoles.Select(static role => new Button { Content = role, Classes = { role } }).ToArray();
        var panel = new StackPanel();
        panel.Children.AddRange(buttons);
        var window = new Window
        {
            Width = 320,
            Height = 320,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = panel,
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var surface = Assert.IsAssignableFrom<ISolidColorBrush>(
                window.FindResource(window.ActualThemeVariant, "NfcSurfaceBrush"));
            window.Background = surface;
            var offenders = new List<string>();
            foreach (var button in buttons)
            {
                button.Transitions = null;
                foreach (var state in _textButtonStates)
                {
                    var pseudoClasses = (IPseudoClasses)button.Classes;
                    pseudoClasses.Set(":pointerover", state == ":pointerover");
                    pseudoClasses.Set(":pressed", state == ":pressed");
                    pseudoClasses.Set(":focus-visible", state == ":focus-visible");
                    await HeadlessUiPump.FlushQueueAsync();
                    var fill = button.Background is ISolidColorBrush brush
                        ? CompositeOver(brush.Color, brush.Opacity, surface.Color)
                        : surface.Color;
                    var text = CompositeOver(Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color, 1d, fill);
                    var ratio = Contrast(text, fill);
                    if (ratio < 4.5d) offenders.Add($"{button.Content}{state}={ratio:F2}");
                }
            }

            Assert.True(offenders.Count == 0, $"{theme} text below 4.5:1: {string.Join(", ", offenders)}");
        }
        finally
        {
            window.Close();
        }
    }

    // BrushCanvasBackground follows the application theme, so read the colour of the window theme.
    private static Color CanvasColor(Window window) =>
        Assert.IsType<Color>(window.FindResource(window.ActualThemeVariant, "ColorCanvasBackground"));

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
