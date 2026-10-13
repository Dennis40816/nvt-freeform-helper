using System.Runtime.InteropServices;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using Nvt.Core.Avalonia.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class TerminalStartupPathTests
{
    [AvaloniaFact]
    public async Task ConsoleExpanded_LogBurst_ShowsEveryLineOnce()
    {
        using var console = new ConsoleFixture();

        await console.AddBurstAsync();

        var texts = ConsoleLogTestSupport.RowTexts(console.Controller.Projection, console.SourceId);
        Assert.Equal(200, texts.Count);
        Assert.Equal("burst line 199", texts[^1]);
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_SearchText_ShowsOnlyMatchingLines()
    {
        using var console = new ConsoleFixture();
        await console.AddBurstAsync();

        console.Controller.SetSearchText("burst line 199");
        await ConsoleLogTestSupport.WaitForProjectionAsync(
            console.Controller,
            projection => ConsoleLogTestSupport.RowTexts(projection, console.SourceId).Count == 1);

        Assert.Equal(["burst line 199"], ConsoleLogTestSupport.RowTexts(console.Controller.Projection, console.SourceId));
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_ResetFilters_RestoresEveryLine()
    {
        using var console = new ConsoleFixture();
        await console.AddBurstAsync();
        console.Controller.SetSearchText("burst line 199");
        await ConsoleLogTestSupport.WaitForProjectionAsync(
            console.Controller,
            projection => ConsoleLogTestSupport.RowTexts(projection, console.SourceId).Count == 1);

        console.Controller.ResetFiltersCommand.Execute(null);
        await ConsoleLogTestSupport.WaitForProjectionAsync(
            console.Controller,
            projection => ConsoleLogTestSupport.RowTexts(projection, console.SourceId).Count == 200);

        Assert.Equal(200, ConsoleLogTestSupport.RowTexts(console.Controller.Projection, console.SourceId).Count);
    }

    [AvaloniaFact]
    public void ConsoleExpanded_ListView_IsComposedWithTheShellController()
    {
        using var console = new ConsoleFixture();

        Assert.Same(console.Controller, console.List.Controller);
        Assert.True(console.List.IsVisible);
    }

    [AvaloniaFact]
    public void ConsoleStartup_LoadsCoreConsoleResources()
    {
        HeadlessAppBootstrap.EnsureInitialized();

        Assert.True(Avalonia.Application.Current!.Styles.TryGetResource("Nvt.Console.MinimumWidth", null, out var minimumWidth));
        Assert.IsType<double>(minimumWidth);
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_AfterStartupLogs_ListShowsTheStartupLines()
    {
        using var console = new ConsoleFixture();

        for (var i = 0; i < 8; i++)
        {
            ConsoleLogTestSupport.Write(console.SourceId, $"startup line {i}");
        }

        await ConsoleLogTestSupport.WaitForProjectionAsync(
            console.Controller,
            projection => ConsoleLogTestSupport.RowTexts(projection, console.SourceId).Count == 8);
        console.Shell.IsConsoleExpanded = true;
        await FlushUiQueueAsync();

        Assert.True(console.List.IsVisible);
        Assert.Equal("startup line 0", ConsoleLogTestSupport.RowTexts(console.Controller.Projection, console.SourceId)[0]);
    }

    [AvaloniaFact]
    public async Task ConsoleExpanded_AfterStartupLogs_RendersVisibleGlyphs()
    {
        using var console = new ConsoleFixture();
        for (var i = 0; i < 24; i++)
        {
            ConsoleLogTestSupport.Write(console.SourceId, $"render line {i}");
        }

        await ConsoleLogTestSupport.WaitForProjectionAsync(
            console.Controller,
            projection => ConsoleLogTestSupport.RowTexts(projection, console.SourceId).Count == 24);
        console.Shell.IsConsoleExpanded = true;
        await FlushUiQueueAsync();

        using var frame = console.Window.CaptureRenderedFrame();
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

    [AvaloniaFact]
    public async Task ConsoleCollapsed_HidesTheListAndKeepsTheHeader()
    {
        using var console = new ConsoleFixture();

        console.Shell.IsConsoleExpanded = false;
        await FlushUiQueueAsync();

        Assert.False(console.List.IsEffectivelyVisible);
        Assert.NotEmpty(console.Window.GetVisualDescendants().OfType<ConsoleHeader>());
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

    private sealed class ConsoleFixture : IDisposable
    {
        public string SourceId { get; } = $"terminal-test-{Guid.NewGuid():N}";

        private readonly LoggingConfiguration? _previousConfiguration;

        public ShellViewModel Shell { get; }
        public MainWindow Window { get; }
        public ConsoleController Controller => Shell.ConsoleSession.Controller;
        public ConsoleListView List { get; }

        public ConsoleFixture()
        {
            HeadlessAppBootstrap.EnsureInitialized();
            _previousConfiguration = LogManager.Configuration;
            LogManager.Configuration = new LoggingConfiguration();
            Shell = new ShellViewModel();
            Window = new MainWindow { Width = 1280, Height = 900 };
            Window.SetShellViewModel(Shell);
            Window.Show();
            HeadlessUiPump.Drain(Window);
            List = Window.GetVisualDescendants().OfType<ConsoleListView>().First();
        }

        public async Task AddBurstAsync()
        {
            for (var i = 0; i < 200; i++)
            {
                ConsoleLogTestSupport.Write(SourceId, $"burst line {i}");
            }

            await ConsoleLogTestSupport.WaitForProjectionAsync(
                Controller,
                projection => ConsoleLogTestSupport.RowTexts(projection, SourceId).Count == 200);
        }

        public void Dispose()
        {
            Window.Close();
            Shell.Dispose();
            LogManager.Configuration = _previousConfiguration;
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
