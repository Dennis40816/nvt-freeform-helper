using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.Styling;
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
    public void ActionIconButtonOutsideTheConsole_KeepsTheNfhSize()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var button = new Button { Content = "x" };
        button.Classes.Add("actionIconButton");

        var window = ShowInWindow(button);
        try
        {
            Assert.Equal(ResourceDouble("IconButtonSize"), button.Width);
            Assert.Equal(ResourceDouble("IconButtonSize"), button.MinHeight);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ScrollBarOutsideTheConsole_KeepsTheNfhThickness()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var scrollBar = new ScrollBar { Orientation = Orientation.Vertical };

        var window = ShowInWindow(scrollBar);
        try
        {
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
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static double ResourceDouble(string key)
    {
        Assert.True(Application.Current!.TryGetResource(key, ThemeVariant.Dark, out var value));
        return Assert.IsType<double>(value);
    }
}
