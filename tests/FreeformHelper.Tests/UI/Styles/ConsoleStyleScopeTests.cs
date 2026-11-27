using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Guards shared button roles across application and console scopes, while NFH keeps its own scroll styles.
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

    [AvaloniaFact]
    public void ScrollBarInsideAnNfhScrollViewerOutsideTheConsole_KeepsTheNfhThickness()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var scrollViewer = new ScrollViewer
        {
            Height = 100,
            Width = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
            Content = new Border { Height = 600, Width = 150 },
        };

        var window = ShowInWindow(scrollViewer);
        try
        {
            scrollViewer.ApplyTemplate();
            window.Dispatcher.RunJobs();

            var scrollBar = Assert.Single(
                scrollViewer.GetVisualDescendants().OfType<ScrollBar>(),
                static bar => bar.Orientation == Orientation.Vertical);
            Assert.Equal(ResourceDouble("ScrollBarThickness"), scrollBar.Width);
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
