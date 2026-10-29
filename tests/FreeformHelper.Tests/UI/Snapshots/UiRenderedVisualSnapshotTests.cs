using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiRenderedVisualSnapshotTests
{
    private static readonly JsonSerializerOptions BaselineJsonOptions = new()
    {
        WriteIndented = true
    };

    [AvaloniaTheory]
    [InlineData("Light", ShellPage.Workspace)]
    [InlineData("Light", ShellPage.HowToUse)]
    [InlineData("Light", ShellPage.Dev)]
    [InlineData("Light", ShellPage.Simulation)]
    [InlineData("Light", ShellPage.Coordinate)]
    [InlineData("Dark", ShellPage.Workspace)]
    [InlineData("Dark", ShellPage.HowToUse)]
    [InlineData("Dark", ShellPage.Dev)]
    [InlineData("Dark", ShellPage.Simulation)]
    [InlineData("Dark", ShellPage.Coordinate)]
    public async Task MainWindowNavigation_PageRoundTrip_PreservesRenderedWorkspace(string theme, ShellPage page)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var app = Assert.IsType<App>(Avalonia.Application.Current);
        var previousTheme = app.RequestedThemeVariant;
        using var shell = new ShellViewModel { IsConsoleExpanded = false };
        shell.Simulation.RequestBuildWorkspaceAsync = null;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
        var window = new MainWindow { Width = 1440, Height = 900 };
        window.SetShellViewModel(shell);
        try
        {
            app.RequestedThemeVariant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
            window.Show();
            await FlushUiQueueAsync();
            var before = CaptureRenderedBuffer(window);

            await ShellViewModelNavigationTests.ShowPageAsync(shell, page);
            await FlushUiQueueAsync();
            shell.ShowWorkspaceCommand.Execute(null);
            await FlushUiQueueAsync();
            var after = CaptureRenderedBuffer(window);

            // The console header shows the line count of the process-wide log store, and the navigation under
            // test and other work in the same process add lines. That text is not workspace content.
            var console = FindConsoleRegion(window);
            MaskRegion(before, console);
            MaskRegion(after, console);

            Assert.Equal(
                ComputeAverageHashHex(before.Buffer, before.Width, before.Height, before.Stride, 16),
                ComputeAverageHashHex(after.Buffer, after.Width, after.Height, after.Stride, 16));
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = previousTheme;
        }
    }

    private const string ConsoleSurfaceKey = "MainWindow.ConsoleExpanded";

    [AvaloniaFact]
    public async Task RenderedUiSurfaces_MatchAdvancedVisualBaseline()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var mode = UiBaselineUpdateModeResolver.Resolve();
        var repoRoot = TestPaths.RepoRoot;
        var baselinePath = Path.Combine(repoRoot, "tests", "FreeformHelper.Tests", "Snapshots", "ui-rendered-visual-baseline.json");
        Assert.True(File.Exists(baselinePath), $"Rendered baseline file not found: {baselinePath}");

        var baseline = JsonSerializer.Deserialize<RenderedVisualBaseline>(File.ReadAllText(baselinePath));
        Assert.NotNull(baseline);
        Assert.NotNull(baseline!.Surfaces);
        Assert.NotEmpty(baseline.Surfaces);

        var updateCandidates = new Dictionary<string, string>(StringComparer.Ordinal);
        var fatalIssues = new List<string>();
        var hashMismatches = new List<string>();
        foreach (var item in baseline.Surfaces.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            using var consoleSurface = await ConsoleSurface.CreateAsync(item.Key);
            if (!TryCreateSurface(item.Key, consoleSurface.Shell, out var control))
            {
                fatalIssues.Add($"{item.Key}: no control factory defined");
                continue;
            }

            var rendered = await RenderControlToBgraBufferAsync(control, item.Value.Width, item.Value.Height);
            var actualHash = ComputeAverageHashHex(rendered.Buffer, rendered.Width, rendered.Height, rendered.Stride, 16);
            updateCandidates[item.Key] = actualHash;
            var expectedHash = item.Value.Hash.ToLowerInvariant();
            var distance = HammingDistanceHex(actualHash, expectedHash);

            if (distance > item.Value.MaxDistance)
            {
                hashMismatches.Add(
                    $"{item.Key}: hamming={distance} > max={item.Value.MaxDistance}, expected={expectedHash}, actual={actualHash}");
            }
        }

        if (mode == UiBaselineUpdateMode.Apply)
        {
            Assert.True(
                fatalIssues.Count == 0,
                "Cannot apply rendered baseline update because required controls are unavailable:\n" + string.Join('\n', fatalIssues));

            if (hashMismatches.Count > 0)
            {
                foreach (var surface in baseline.Surfaces.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    if (updateCandidates.TryGetValue(surface.Key, out var updatedHash))
                    {
                        surface.Value.Hash = updatedHash;
                    }
                }

                var updatedBaselineText = JsonSerializer.Serialize(baseline, BaselineJsonOptions);
                File.WriteAllText(baselinePath, updatedBaselineText + Environment.NewLine, new UTF8Encoding(false));
            }

            return;
        }

        if (mode == UiBaselineUpdateMode.DryRun)
        {
            Assert.True(
                fatalIssues.Count == 0 && hashMismatches.Count == 0,
                "Rendered visual baseline dry-run found changes. Use FH_UI_BASELINE_MODE=apply to write baseline.\n"
                + string.Join('\n', fatalIssues.Concat(hashMismatches)));
            return;
        }

        Assert.True(
            fatalIssues.Count == 0 && hashMismatches.Count == 0,
            "Rendered visual snapshot mismatch. Intentional UI changes should update tests/FreeformHelper.Tests/Snapshots/ui-rendered-visual-baseline.json.\n"
            + string.Join('\n', fatalIssues.Concat(hashMismatches)));
    }

    // The console surface shows a store of its own that holds one fixed line, so the test never touches the shared store.
    private sealed class ConsoleSurface : IDisposable
    {
        private readonly LogStore? _store;

        private ConsoleSurface(LogStore? store)
        {
            _store = store;
            Shell = store is null ? null : new ShellViewModel { ConsoleStore = store, IsConsoleExpanded = true };
        }

        public ShellViewModel? Shell { get; }

        public static async Task<ConsoleSurface> CreateAsync(string key)
        {
            var store = key == ConsoleSurfaceKey
                ? await ConsoleLogTestSupport.CreateSeededStoreAsync("test", "console snapshot line")
                : null;
            return new ConsoleSurface(store);
        }

        public void Dispose()
        {
            Shell?.Dispose();
            _store?.Dispose();
        }
    }

    private static bool TryCreateSurface(string key, ShellViewModel? consoleShell, out Control control)
    {
        switch (key)
        {
            case ConsoleSurfaceKey:
                var window = new MainWindow();
                window.SetShellViewModel(consoleShell!);
                control = window;
                return true;
            case "SettingsWindow.Default":
                var owner = new FreeformHelperViewModel();
                control = new SettingsWindow
                {
                    DataContext = new SettingsWindowViewModel(owner),
                };
                return true;
            case "HowToUseView.Default":
                control = new HowToUseView();
                return true;
            default:
                control = null!;
                return false;
        }
    }

    private static async Task<RenderedBuffer> RenderControlToBgraBufferAsync(Control control, int width, int height)
    {
        var ownsHost = control is not Window;
        var host = control as Window ?? new Window { Content = control };
        host.Width = width;
        host.Height = height;

        try
        {
            host.Show();
            await FlushUiQueueAsync();
            return CaptureRenderedBuffer(host);
        }
        finally
        {
            host.Close();
            if (ownsHost)
            {
                host.Content = null;
            }
        }
    }

    private static RenderedBuffer CaptureRenderedBuffer(Window host)
    {
        using var frame = host.CaptureRenderedFrame();
        Assert.NotNull(frame);

        using var locked = frame.Lock();
        var rowBytes = locked.RowBytes;
        var frameWidth = locked.Size.Width;
        var frameHeight = locked.Size.Height;
        var bytes = new byte[rowBytes * frameHeight];
        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);
        return new RenderedBuffer(bytes, frameWidth, frameHeight, rowBytes);
    }

    private static PixelRect FindConsoleRegion(Window window)
    {
        var border = window.GetVisualDescendants()
            .OfType<Border>()
            .Single(static candidate => candidate.IsEffectivelyVisible && candidate.Name == "ConsoleBorder");
        var origin = border.TranslatePoint(default, window);
        Assert.NotNull(origin);
        var scale = window.RenderScaling;
        return new PixelRect(
            (int)Math.Floor(origin.Value.X * scale),
            (int)Math.Floor(origin.Value.Y * scale),
            (int)Math.Ceiling(border.Bounds.Width * scale),
            (int)Math.Ceiling(border.Bounds.Height * scale));
    }

    private static void MaskRegion(RenderedBuffer rendered, PixelRect region)
    {
        var left = Math.Clamp(region.X, 0, rendered.Width);
        var right = Math.Clamp(region.X + region.Width, 0, rendered.Width);
        var top = Math.Clamp(region.Y, 0, rendered.Height);
        var bottom = Math.Clamp(region.Y + region.Height, 0, rendered.Height);
        for (var y = top; y < bottom; y++)
        {
            Array.Clear(rendered.Buffer, (y * rendered.Stride) + (left * 4), (right - left) * 4);
        }
    }

    private static async Task FlushUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static string ComputeAverageHashHex(byte[] bgra, int width, int height, int stride, int hashSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(hashSize, 1);

        var cellLuma = new double[hashSize * hashSize];
        var total = 0d;

        for (var y = 0; y < hashSize; y++)
        {
            var y0 = y * height / hashSize;
            var y1 = Math.Max(y0 + 1, (y + 1) * height / hashSize);

            for (var x = 0; x < hashSize; x++)
            {
                var x0 = x * width / hashSize;
                var x1 = Math.Max(x0 + 1, (x + 1) * width / hashSize);

                var sum = 0d;
                var count = 0;
                for (var py = y0; py < y1; py++)
                {
                    for (var px = x0; px < x1; px++)
                    {
                        var pixelOffset = (py * stride) + (px * 4);
                        var blue = bgra[pixelOffset];
                        var green = bgra[pixelOffset + 1];
                        var red = bgra[pixelOffset + 2];
                        var luma = (0.299 * red) + (0.587 * green) + (0.114 * blue);
                        sum += luma;
                        count++;
                    }
                }

                var index = (y * hashSize) + x;
                var value = sum / Math.Max(1, count);
                cellLuma[index] = value;
                total += value;
            }
        }

        var mean = total / cellLuma.Length;
        Span<byte> bytes = stackalloc byte[(cellLuma.Length + 7) / 8];
        for (var i = 0; i < cellLuma.Length; i++)
        {
            if (cellLuma[i] >= mean)
            {
                bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
            }
        }

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int HammingDistanceHex(string leftHex, string rightHex)
    {
        if (leftHex.Length != rightHex.Length)
        {
            return int.MaxValue;
        }

        var bytes = leftHex.Length / 2;
        var distance = 0;
        for (var i = 0; i < bytes; i++)
        {
            var leftByte = Convert.ToByte(leftHex.Substring(i * 2, 2), 16);
            var rightByte = Convert.ToByte(rightHex.Substring(i * 2, 2), 16);
            distance += System.Numerics.BitOperations.PopCount((uint)(leftByte ^ rightByte));
        }

        return distance;
    }

    private sealed class RenderedVisualBaseline
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("surfaces")]
        public Dictionary<string, RenderedSurface> Surfaces { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class RenderedSurface
    {
        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("maxDistance")]
        public int MaxDistance { get; set; } = 24;
    }

    private sealed record RenderedBuffer(byte[] Buffer, int Width, int Height, int Stride);
}
