using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Guards shared button roles and scroll styles across application and console scopes.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class ConsoleStyleScopeTests
{
    [AvaloniaFact]
    public void CanvasOverlayButtonOutsideTheConsole_KeepsTheNfhSizeAndTemplate()
    {
        HeadlessAppBootstrap.EnsureInitialized();

        // The same role classes as the fit button of CanvasOverlayControls.
        var button = new Button { Content = "x" };
        button.Classes.Add("viewportOverlayAction");

        var window = ShowInWindow(button);
        try
        {
            button.ApplyTemplate();

            Assert.Equal(ResourceDouble("IconButtonSize"), button.Width);
            Assert.Equal(ResourceDouble("IconButtonSize"), button.MinHeight);

            // The Core role template has a Grid with the borders RoleBorder and PrimaryPressedBorder.
            // The NFH template is one Border around the content.
            var borders = button.GetVisualDescendants().OfType<Border>().ToList();
            Assert.Single(borders);
            Assert.DoesNotContain(borders, static border => border.Name is "RoleBorder" or "PrimaryPressedBorder");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark", "")]
    [InlineData("Dark", ":pointerover")]
    [InlineData("Dark", ":pressed")]
    [InlineData("Dark", ":disabled")]
    [InlineData("Dark", ":focus-visible")]
    [InlineData("Light", "")]
    [InlineData("Light", ":pointerover")]
    [InlineData("Light", ":pressed")]
    [InlineData("Light", ":disabled")]
    [InlineData("Light", ":focus-visible")]
    public void CoreGhostRoleHasTheSameLookInsideTheRealConsole(string theme, string state)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var button = new Button { Content = "Display" };
        button.Classes.Add("actionGhost");
        button.Classes.Add("reducedMotion");
        var console = new ConsolePanel();
        // Set the theme before the first show and switch off motion for the whole tree: a brush
        // transition that is still running would be compared instead of the final role colour.
        var root = new StackPanel { Children = { button, console } };
        root.Classes.Add("reducedMotion");
        var window = new Window
        {
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = root,
        };
        window.Show();
        window.Dispatcher.RunJobs();
        try
        {
            var display = Assert.Single(console.GetVisualDescendants().OfType<Button>(), static control => control.Name == "Display");
            // Core gives consoleAction a transparent disabled fill; compare plain ghost roles in the same real header scope.
            var scoped = new Button { Content = "Display" };
            scoped.Classes.Add("actionGhost");
            scoped.Classes.Add("reducedMotion");
            Assert.IsType<StackPanel>(display.Parent).Children.Add(scoped);
            // Attach and template the probe first: a state set before that is reset again.
            window.Dispatcher.RunJobs();
            SetState(button, state);
            SetState(scoped, state);
            SetState(display, state);
            window.Dispatcher.RunJobs();
            Assert.Equal(32d, button.Height);
            Assert.Equal(button.Height, scoped.Height);
            Assert.Equal(button.CornerRadius, scoped.CornerRadius);
            Assert.Equal(button.Padding, scoped.Padding);
            Assert.Equal(button.Background, scoped.Background);
            Assert.Equal(button.BorderBrush, scoped.BorderBrush);
            Assert.Equal(button.Foreground, scoped.Foreground);
            Assert.Equal(button.Height, display.Height);
            Assert.Equal(button.CornerRadius, display.CornerRadius);
            Assert.Equal(button.Padding, display.Padding);
            Assert.Equal(button.Foreground, display.Foreground);
            if (state == ":disabled")
            {
                Assert.Contains("consoleAction", display.Classes);
                Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(display.Background).Color.A);
                Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(display.BorderBrush).Color.A);
            }
            else
            {
                Assert.Equal(button.Background, display.Background);
                Assert.Equal(button.BorderBrush, display.BorderBrush);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("", false)]
    [InlineData(":pointerover", false)]
    [InlineData(":pressed", false)]
    [InlineData(":focus-visible", false)]
    [InlineData(":disabled", false)]
    [InlineData("", true)]
    [InlineData(":pointerover", true)]
    [InlineData(":pressed", true)]
    [InlineData(":focus-visible", true)]
    [InlineData(":disabled", true)]
    public void CoreButtonIs32WhileTheRealCanvasToggleKeepsItsShape(string state, bool isChecked)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var action = new Button { Content = "General action" };
        action.Classes.Add("actionNeutral");
        var overlay = new CanvasOverlayControls();
        var window = ShowInWindow(new StackPanel { Children = { action, overlay } });
        try
        {
            var toggle = overlay.GetVisualDescendants().OfType<ToggleButton>()
                .Where(static control => control.Classes.Contains("canvasQuickToggle")).ToArray()[0];
            var radius = toggle.CornerRadius;
            toggle.IsChecked = isChecked;
            SetState(toggle, state);
            window.Dispatcher.RunJobs();
            Assert.Equal(32d, action.Height);
            Assert.Equal(32d, action.Bounds.Height);
            Assert.Equal(ResourceDouble("ProductControlMinHeight"), toggle.MinHeight);
            Assert.Equal(ResourceDouble("ProductControlMinHeight"), toggle.Bounds.Height);
            Assert.Equal(new global::Avalonia.Thickness(10, 6), toggle.Padding);
            Assert.Equal(default(global::Avalonia.Thickness), toggle.BorderThickness);
            Assert.Equal(radius, toggle.CornerRadius);
            Assert.True(toggle.CornerRadius.TopLeft < toggle.MinHeight / 2);
            Assert.DoesNotContain("actionNeutral", toggle.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    private static void SetState(Button button, string state)
    {
        button.IsEnabled = state != ":disabled";
        if (state.Length > 0 && state != ":disabled")
        {
            ((IPseudoClasses)button.Classes).Set(state, true);
        }
    }

    [AvaloniaTheory]
    [InlineData("Dark", Orientation.Vertical)]
    [InlineData("Dark", Orientation.Horizontal)]
    [InlineData("Light", Orientation.Vertical)]
    [InlineData("Light", Orientation.Horizontal)]
    public void ScrollBarsInsideAndOutsideTheConsoleUseTheCoreLook(string theme, Orientation orientation)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var outside = CreateScrollProbe();
        var inside = CreateScrollProbe();
        var console = new ConsolePanel();
        var consoleRoot = Assert.IsType<Grid>(Assert.IsType<Border>(console.Content).Child);
        consoleRoot.Children.Add(inside);
        var window = new Window
        {
            Width = 480,
            Height = 320,
            RequestedThemeVariant = theme == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { outside, console } },
        };
        window.Show();
        try
        {
            window.Dispatcher.RunJobs();
            AssertCoreScrollLook(outside, orientation);
            AssertCoreScrollLook(inside, orientation);
        }
        finally
        {
            window.Close();
        }
    }

    private static ScrollViewer CreateScrollProbe() => new()
    {
        Height = 100,
        Width = 200,
        VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
        Content = new Border { Height = 600, Width = 600 },
    };

    private static void AssertCoreScrollLook(ScrollViewer scroll, Orientation orientation)
    {
        var bar = Assert.Single(scroll.GetVisualDescendants().OfType<ScrollBar>(), bar => bar.Orientation == orientation);
        var thumb = Assert.Single(bar.GetVisualDescendants().OfType<Thumb>());
        Assert.Equal(14d, orientation == Orientation.Vertical ? bar.Bounds.Width : bar.Bounds.Height);
        Assert.Equal(6d, orientation == Orientation.Vertical ? thumb.Bounds.Width : thumb.Bounds.Height);
        Assert.False(scroll.AllowAutoHide);
        Assert.False(bar.AllowAutoHide);
        Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(bar.Background).Color);
        Assert.True(bar.TryFindResource("NfcTextDisabledBrush", bar.ActualThemeVariant, out var expectedThumb));
        Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(expectedThumb).Color,
            Assert.IsAssignableFrom<ISolidColorBrush>(thumb.Background).Color);
    }

    [AvaloniaTheory]
    [InlineData(20d)]
    [InlineData(600d)]
    public void ViewportBoundContentFitsWithAndWithoutVerticalOverflow(double contentHeight)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var content = new Border { Height = contentHeight };
        content.Classes.Add("viewportBoundContent");
        var scroll = new ScrollViewer
        {
            Width = 200,
            Height = 100,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = content,
        };
        scroll.Classes.Add("viewportBoundScroll");
        var window = ShowInWindow(scroll);
        try
        {
            Assert.Equal(HorizontalAlignment.Stretch, scroll.HorizontalContentAlignment);
            Assert.Equal(default(global::Avalonia.Thickness), scroll.Padding);
            Assert.True(content.Bounds.Width > 0);
            Assert.Equal(scroll.Viewport.Width, content.Bounds.Width, precision: 3);
            Assert.True(content.Bounds.Width <= scroll.Bounds.Width);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProductButtonClassesWinOverTheDefaultButtonLook()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var header = new Button { Content = "Safe" };
        header.Classes.Add("workspaceTableHeaderButton");
        var row = new Button { Content = "Row" };
        row.Classes.Add("workspaceDataRow");
        var activeRow = new Button { Content = "Row" };
        activeRow.Classes.Add("workspaceDataRow");
        activeRow.Classes.Add("workspaceDataRowActive");
        var window = ShowInWindow(new StackPanel { Children = { header, row, activeRow } });
        try
        {
            Assert.Equal(0, Assert.IsAssignableFrom<ISolidColorBrush>(header.Background).Color.A);
            Assert.Equal(HorizontalAlignment.Left, header.HorizontalContentAlignment);
            Assert.Equal(HorizontalAlignment.Stretch, row.HorizontalContentAlignment);
            Assert.NotEqual(
                Assert.IsAssignableFrom<ISolidColorBrush>(row.Background).Color,
                Assert.IsAssignableFrom<ISolidColorBrush>(activeRow.Background).Color);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CanvasQuickToggleShowsDisabledAndKeyboardFocusStates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var disabled = new ToggleButton { Content = "To Full Preview", IsEnabled = false };
        disabled.Classes.Add("canvasQuickToggle");
        var focused = new ToggleButton { Content = "CAD layer" };
        focused.Classes.Add("canvasQuickToggle");
        var window = ShowInWindow(new StackPanel { Children = { disabled, focused } });
        try
        {
            focused.Focus(NavigationMethod.Tab);
            window.Dispatcher.RunJobs();
            Assert.Equal(ThemeColor(disabled, "NfcTextMutedBrush"),
                Assert.IsAssignableFrom<ISolidColorBrush>(disabled.Foreground).Color);
            Assert.Equal(ThemeColor(focused, "NfcSelectionSurfaceBrush"),
                Assert.IsAssignableFrom<ISolidColorBrush>(focused.Background).Color);
        }
        finally
        {
            window.Close();
        }
    }

    private static Color ThemeColor(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var brush));
        return Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    }

    [AvaloniaFact]
    public void CoreRoleButtonsUseTheInterFont()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var button = new Button { Content = "Match overlaps" };
        button.Classes.Add("actionNeutral");
        var window = ShowInWindow(button);
        try
        {
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(button.FontFamily), out var glyphTypeface));
            Assert.Equal("Inter", glyphTypeface.FamilyName);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window ShowInWindow(Control content)
    {
        var window = new Window { Content = new StackPanel { Children = { content } } };
        window.Show();
        window.Dispatcher.RunJobs();
        return window;
    }

    private static double ResourceDouble(string key)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value));
        return Assert.IsType<double>(value);
    }
}
