using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class TerminalStartupPathTests
{
    private readonly ITestOutputHelper _output;

    public TerminalStartupPathTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_LogBurst_ParsesLatestTextOnce()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        AppLogStore.Instance.Clear();
        using var shell = new ShellViewModel();
        var window = new MainWindow();
        window.SetShellViewModel(shell);
        window.Width = 1280;
        window.Height = 900;

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            var view = window.GetVisualDescendants().OfType<FreeformHelperView>().Single();
            var editor = window.GetVisualDescendants().OfType<TextEditor>()
                .Single(control => string.Equals(control.Name, "ConsoleEditor", StringComparison.Ordinal));
            Assert.True(shell.IsConsoleExpanded);
            var parsesBeforeBurst = view.ConsoleLinkParseCount;

            for (var i = 0; i < 200; i++)
            {
                AppLogStore.Instance.Add(new AppLogEntry(
                    DateTimeOffset.UtcNow,
                    "INFO",
                    "terminal-test",
                    $"burst line {i:D3}"));
            }

            await FlushUiQueueAsync();

            var burstParseCount = view.ConsoleLinkParseCount - parsesBeforeBurst;
            _output.WriteLine($"200 log entries: {burstParseCount} full link parses");
            Assert.InRange(burstParseCount, 1, 2);
            Assert.Equal(shell.ConsoleText, editor.Text);
            Assert.Contains("burst line 199", editor.Text, StringComparison.Ordinal);

            Assert.True(editor.ExtentHeight > editor.ViewportHeight);
            editor.ScrollToHome();
            await FlushUiQueueAsync();
            Assert.True(editor.VerticalOffset < editor.ExtentHeight - editor.ViewportHeight - 1);
            AppLogStore.Instance.Add(new AppLogEntry(
                DateTimeOffset.UtcNow,
                "INFO",
                "terminal-test",
                "while reading older lines"));
            await FlushUiQueueAsync();
            Assert.Equal(0, editor.VerticalOffset);

            editor.ScrollToEnd();
            var scrollWait = Stopwatch.StartNew();
            do
            {
                await FlushUiQueueAsync();
            }
            while (editor.ExtentHeight - editor.VerticalOffset - editor.ViewportHeight > 1 &&
                   scrollWait.Elapsed < TimeSpan.FromSeconds(5));
            Assert.True(editor.ExtentHeight - editor.VerticalOffset - editor.ViewportHeight <= 1);

            AppLogStore.Instance.Add(new AppLogEntry(
                DateTimeOffset.UtcNow,
                "INFO",
                "terminal-test",
                "while following latest line"));
            scrollWait.Restart();
            do
            {
                await FlushUiQueueAsync();
            }
            while ((!editor.Text.Contains("while following latest line", StringComparison.Ordinal) ||
                    editor.ExtentHeight - editor.VerticalOffset - editor.ViewportHeight > 1) &&
                   scrollWait.Elapsed < TimeSpan.FromSeconds(5));
            Assert.True(editor.ExtentHeight - editor.VerticalOffset - editor.ViewportHeight <= 1);
            Assert.Contains("while following latest line", editor.Text, StringComparison.Ordinal);

            shell.ConsoleFontSize = 18;
            Assert.Equal(18, editor.FontSize);
            shell.ConsoleSearchText = "while following latest line";
            shell.IsConsoleFilterEnabled = true;
            Assert.Contains("while following latest line", editor.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("burst line 199", editor.Text, StringComparison.Ordinal);
            shell.IsConsoleFilterEnabled = false;
            Assert.Equal(shell.ConsoleText, editor.Text);
        }
        finally
        {
            window.Close();
            AppLogStore.Instance.Clear();
        }
    }

    [AvaloniaFact]
    public async Task ConsoleStartup_UsesAppLevelAvaloniaEditTheme()
    {
        var appAxamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FreeformHelper.UI", "App.axaml"));
        var xaml = await File.ReadAllTextAsync(appAxamlPath);

        Assert.Contains("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml", xaml, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_AfterStartupLogs_ShowsTextEditorText()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        AppLogStore.Instance.Clear();
        try
        {
            for (var i = 0; i < 8; i++)
            {
                AppLogStore.Instance.Add(new AppLogEntry(
                    DateTimeOffset.UtcNow,
                    "INFO",
                    "terminal-test",
                    $"startup line {i}"));
            }

            using var shell = new ShellViewModel();
            var window = new MainWindow();
            window.SetShellViewModel(shell);
            window.Width = 1280;
            window.Height = 900;

            try
            {
                window.Show();
                await FlushUiQueueAsync();

                shell.IsConsoleExpanded = true;
                await FlushUiQueueAsync();

                var editor = window.GetVisualDescendants()
                    .OfType<TextEditor>()
                    .FirstOrDefault(control => string.Equals(control.Name, "ConsoleEditor", StringComparison.Ordinal));

                Assert.Contains("startup line 0", shell.ConsoleText);
                Assert.NotNull(editor);
                Assert.True(editor!.IsVisible);
                Assert.Contains("startup line 0", editor.Text ?? string.Empty);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            AppLogStore.Instance.Clear();
        }
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_AfterStartupLogs_RendersVisibleGlyphs()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        AppLogStore.Instance.Clear();
        try
        {
            for (var i = 0; i < 24; i++)
            {
                AppLogStore.Instance.Add(new AppLogEntry(
                    DateTimeOffset.UtcNow,
                    "INFO",
                    "terminal-test",
                    $"render line {i}"));
            }

            using var shell = new ShellViewModel();
            var window = new MainWindow();
            window.SetShellViewModel(shell);
            window.Width = 1280;
            window.Height = 900;

            try
            {
                window.Show();
                await FlushUiQueueAsync();

                shell.IsConsoleExpanded = true;
                await FlushUiQueueAsync();

                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);

                using var locked = frame!.Lock();
                var visiblePixelCount = CountBrightPixelsInConsoleBody(
                    locked,
                    locked.RowBytes,
                    locked.Size.Width,
                    locked.Size.Height);

                Assert.True(
                    visiblePixelCount >= 120,
                    $"Expected visible terminal glyph pixels after startup expansion, but only counted {visiblePixelCount} bright pixels.");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            AppLogStore.Instance.Clear();
        }
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_AfterStartupLogs_UsesTextEditorOnly()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        AppLogStore.Instance.Clear();
        try
        {
            AppLogStore.Instance.Add(new AppLogEntry(
                DateTimeOffset.UtcNow,
                "INFO",
                "terminal-test",
                "foreground check"));

            using var shell = new ShellViewModel();
            var window = new MainWindow();
            window.SetShellViewModel(shell);
            window.Width = 1280;
            window.Height = 900;

            try
            {
                window.Show();
                await FlushUiQueueAsync();

                shell.IsConsoleExpanded = true;
                await FlushUiQueueAsync();

                var editor = window.GetVisualDescendants()
                    .OfType<TextEditor>()
                    .FirstOrDefault(control => string.Equals(control.Name, "ConsoleEditor", StringComparison.Ordinal));
                Assert.NotNull(editor);
                Assert.True(editor!.IsVisible);
                Assert.NotNull(editor.Foreground);
                Assert.NotNull(editor.TextArea);
                Assert.NotNull(editor.TextArea!.Foreground);
                Assert.DoesNotContain(
                    window.GetVisualDescendants(),
                    control => string.Equals(control.Name, "ConsoleTextBox", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            AppLogStore.Instance.Clear();
        }
    }

    [AvaloniaFact]
    public void HeadlessApp_InitializesApplicationStyles()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = Avalonia.Application.Current;
        Assert.NotNull(app);
        Assert.NotEmpty(app!.Styles);
    }

    [AvaloniaFact]
    public void HeadlessApp_ResolvesUiFontAliasesWithoutSystemFontFallback()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface("Inter"), out _));
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface("Segoe UI Variable Text"), out _));
        Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface("Segoe UI"), out _));
    }

    [Fact]
    public void ShellViewModel_DefaultsConsoleExpanded()
    {
        using var shell = new ShellViewModel();
        Assert.True(shell.IsConsoleExpanded);
    }

    private static async Task FlushUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static int CountBrightPixelsInConsoleBody(ILockedFramebuffer locked, int rowBytes, int width, int height)
    {
        var startY = Math.Max(0, height - 150);
        var endY = Math.Max(startY + 1, height - 20);
        var startX = 24;
        var endX = Math.Max(startX + 1, width - 24);
        var brightCount = 0;
        var bytes = new byte[rowBytes * height];
        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var offset = (y * rowBytes) + (x * 4);
                var blue = bytes[offset];
                var green = bytes[offset + 1];
                var red = bytes[offset + 2];
                var luma = (0.299 * red) + (0.587 * green) + (0.114 * blue);
                if (luma >= 175)
                {
                    brightCount++;
                }
            }
        }

        return brightCount;
    }
}
