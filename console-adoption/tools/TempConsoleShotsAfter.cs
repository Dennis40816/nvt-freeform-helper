using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Logging;
using Avalonia.VisualTree;
using Nvt.Core.Avalonia.LogConsole;
using Nvt.Core.LogConsole;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

// Temporary: writes console screenshots for the adoption plan. Never committed.
[Collection("HeadlessUiSerial")]
public sealed class TempConsoleShots
{
    [AvaloniaTheory]
    [InlineData("Light", false)]
    [InlineData("Light", true)]
    [InlineData("Dark", false)]
    [InlineData("Dark", true)]
    public async Task Capture_ConsoleState(string theme, bool expanded)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = Assert.IsType<App>(Avalonia.Application.Current);
        var previousTheme = app.RequestedThemeVariant;
        var outputDir = Environment.GetEnvironmentVariable("FH_SHOT_DIR");
        Assert.False(string.IsNullOrEmpty(outputDir));
        Directory.CreateDirectory(outputDir!);

        NLog.LogManager.Configuration = new NLog.Config.LoggingConfiguration();
        using var store = new LogStore();
        using var shell = new ShellViewModel { ConsoleStore = store };
        shell.Simulation.RequestBuildWorkspaceAsync = null;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
        shell.IsConsoleExpanded = expanded;

        var window = new MainWindow { Width = 1440, Height = 900 };
        window.SetShellViewModel(shell);
        try
        {
            app.RequestedThemeVariant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
            window.Show();
            await FlushAsync();

            var day = new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.FromHours(8));
            store.Add(LogLevel.Info, "FreeformHelper.UI.App", "Project loaded: C:/work/sample_board/project_demo.json", day.AddSeconds(1));
            store.Add(LogLevel.Info, "FreeformHelper.UI.Workflow", "Step 1 finished: 4808 CAD pads, 5176 regular pads", day.AddSeconds(2));
            store.Add(LogLevel.Warn, "FreeformHelper.UI.Notch", "Pad 4808 overlaps 3 regular pads, see C:/work/export/notch_review.csv", day.AddSeconds(3));
            store.Add(LogLevel.Error, "FreeformHelper.UI.Export", "Cannot write C:/work/export/notch_v2.2.c: file is in use", day.AddSeconds(4));
            store.Add(LogLevel.Info, "FreeformHelper.UI.RuntimeQuery", "query status handled in 3 ms", day.AddSeconds(5));
            store.Add(LogLevel.Info, "FreeformHelper.UI.Docs", "See https://github.com/Dennis40816/nvt-freeform-helper for help", day.AddSeconds(6));
            using (var published = await store.CaptureLatestAsync())
            {
            }

            await ConsoleLogTestSupport.WaitForProjectionAsync(
                shell.ConsoleSession.Controller,
                projection => ConsoleLogTestSupport.RowTexts(projection).Count == 6);
            await FlushAsync();
            var panel = window.GetVisualDescendants().OfType<ConsolePanel>().FirstOrDefault();
            Console.WriteLine($"PANEL {theme} expanded={expanded} bounds={panel?.Bounds}");
            await FlushAsync();
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var name = $"console-after-{theme.ToLowerInvariant()}-{(expanded ? "expanded" : "collapsed")}.png";
            frame!.Save(Path.Combine(outputDir!, name), new PngBitmapEncoderOptions());
        }
        finally
        {
            app.RequestedThemeVariant = previousTheme;
            window.Close();
        }
    }

    private static async Task FlushAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }
}
