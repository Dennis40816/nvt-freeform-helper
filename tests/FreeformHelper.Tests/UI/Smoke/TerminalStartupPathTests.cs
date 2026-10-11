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
    public void ConsoleExpanded_LogBurst_ParsesLatestTextOnce()
    {
        using var console = new ConsoleFixture();
        var parsesBeforeBurst = console.View.ConsoleLinkParseCount;

        console.AddBurst();
        console.Drain();

        var burstParseCount = console.View.ConsoleLinkParseCount - parsesBeforeBurst;
        _output.WriteLine($"200 log entries: {burstParseCount} full link parses");
        Assert.InRange(burstParseCount, 1, 2);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_LogBurst_ShowsLatestShellText()
    {
        using var console = new ConsoleFixture();

        console.AddBurst();
        console.Drain();

        Assert.True(console.Shell.IsConsoleExpanded);
        Assert.Equal(console.Shell.ConsoleText, console.Editor.Text);
        Assert.Contains("burst line 199", console.Editor.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_ReadingOlderLines_NewLineKeepsOffset()
    {
        using var console = CreateConsoleWithBurst();
        console.Editor.ScrollToHome();
        console.Drain();

        console.AddLine("while reading older lines");
        console.Drain();

        Assert.True(console.Editor.ExtentHeight > console.Editor.ViewportHeight);
        Assert.Equal(0, console.Editor.VerticalOffset);
        Assert.Contains("while reading older lines", console.Editor.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_FollowingLatestLine_NewLineScrollsToEnd()
    {
        using var console = CreateConsoleWithBurst();
        console.Editor.ScrollToHome();
        console.Drain();
        console.Editor.ScrollToEnd();
        console.Drain();

        console.AddLine("while following latest line");
        console.Drain();

        Assert.True(console.Editor.ExtentHeight > console.Editor.ViewportHeight);
        Assert.True(console.Editor.ExtentHeight - console.Editor.VerticalOffset - console.Editor.ViewportHeight <= 1);
        Assert.Contains("while following latest line", console.Editor.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_FontSizeChange_UpdatesEditor()
    {
        using var console = new ConsoleFixture();

        console.Shell.ConsoleFontSize = 18;
        console.Drain();

        Assert.Equal(18, console.Editor.FontSize);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_FilterEnabled_ShowsOnlyMatchingLines()
    {
        using var console = CreateConsoleWithBurst();
        console.AddLine("while following latest line");
        console.Drain();
        console.Shell.ConsoleSearchText = "while following latest line";

        console.Shell.IsConsoleFilterEnabled = true;
        console.Drain();

        Assert.Contains("while following latest line", console.Editor.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("burst line 199", console.Editor.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_FilterDisabled_RestoresShellText()
    {
        using var console = CreateConsoleWithBurst();
        console.AddLine("while following latest line");
        console.Drain();
        console.Shell.ConsoleSearchText = "while following latest line";
        console.Shell.IsConsoleFilterEnabled = true;
        console.Drain();

        console.Shell.IsConsoleFilterEnabled = false;
        console.Drain();

        Assert.Equal(console.Shell.ConsoleText, console.Editor.Text);
        Assert.Contains("burst line 199", console.Editor.Text, StringComparison.Ordinal);
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
    public void HeadlessApp_ResolvesEmbeddedFontTokensWithoutSystemFontFallback()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        foreach (var key in new[] { "FontFamilyUi", "FontFamilyCode" })
        {
            Assert.True(Avalonia.Application.Current!.Resources.TryGetResource(key, null, out var resource));
            var family = Assert.IsType<FontFamily>(resource);
            Assert.True(FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface));
            Assert.True(typeface.PlatformTypeface.TryGetStream(out var stream));
            stream.Dispose();
        }
    }

    [Fact]
    public void ShellViewModel_DefaultsConsoleExpanded()
    {
        using var shell = new ShellViewModel();
        Assert.True(shell.IsConsoleExpanded);
    }

    private static ConsoleFixture CreateConsoleWithBurst()
    {
        var console = new ConsoleFixture();
        console.AddBurst();
        console.Drain();
        return console;
    }

    private sealed class ConsoleFixture : IDisposable
    {
        private readonly MainWindow _window;

        public ShellViewModel Shell { get; }
        public FreeformHelperView View { get; }
        public TextEditor Editor { get; }

        public ConsoleFixture()
        {
            HeadlessAppBootstrap.EnsureInitialized();
            AppLogStore.Instance.Clear();
            Shell = new ShellViewModel();
            _window = new MainWindow { Width = 1280, Height = 900 };
            _window.SetShellViewModel(Shell);
            _window.Show();
            Drain();
            View = _window.GetVisualDescendants().OfType<FreeformHelperView>().Single();
            Editor = _window.GetVisualDescendants().OfType<TextEditor>()
                .Single(control => string.Equals(control.Name, "ConsoleEditor", StringComparison.Ordinal));
        }

        public void AddBurst()
        {
            for (var i = 0; i < 200; i++)
            {
                AddLine($"burst line {i:D3}");
            }
        }

        public void AddLine(string text)
        {
            _window.Dispatcher.VerifyAccess();
            AppLogStore.Instance.Add(new AppLogEntry(
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                "INFO",
                "terminal-test",
                text));
        }

        public void Drain() => HeadlessUiPump.Drain(_window);

        public void Dispose()
        {
            _window.Close();
            Shell.Dispose();
            AppLogStore.Instance.Clear();
        }
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
