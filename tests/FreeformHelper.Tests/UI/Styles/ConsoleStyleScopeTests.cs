using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Guards that the Core console styles stay inside the console panel. Core's button and scroll styles
/// use the same class names as the NFH styles, so a load at the application level would change every control.
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
        button.Classes.Add("actionIconButton");
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
