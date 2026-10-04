using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkflowSteps;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class HeadlessUiSmokeTests
{
    private const int SimulationNullDiffValue = 65535;
    private static readonly double[] SimulationXEdges = { 0d, 1d, 2d };
    private static readonly double[] SimulationYEdges = { 0d, 1d };

    [AvaloniaFact]
    public void FreeformHelperView_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new FreeformHelperView
        {
            DataContext = new FreeformHelperViewModel()
        };

        var size = new Size(1200, 800);
        view.Measure(size);
        view.Arrange(new Rect(size));

        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public void FreeformHelperView_WithLayerToggles_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        vm.LayerToggles.Clear();
        vm.LayerToggles.Add(new FreeformHelperViewModel.LayerToggle("L1", isSelected: true));

        var view = new FreeformHelperView
        {
            DataContext = vm
        };

        var size = new Size(1200, 800);
        view.Measure(size);
        view.Arrange(new Rect(size));

        Assert.NotNull(view);
    }

    [AvaloniaFact]
    public async Task SharedTooltipStyle_CanOpen_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var target = new Button
        {
            Content = "Tooltip target",
        };
        ToolTip.SetTip(target, "Tooltip text");

        var window = new Window
        {
            Width = 240,
            Height = 120,
            Content = target,
        };

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            ToolTip.SetIsOpen(target, true);
            await FlushUiQueueAsync();

            Assert.True(ToolTip.GetIsOpen(target));
        }
        finally
        {
            ToolTip.SetIsOpen(target, false);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SharedTooltipStyle_StringTooltipUsesTooltipForegroundTextBlock_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var target = new Button
        {
            Content = "Tooltip target",
        };
        ToolTip.SetTip(target, "Tooltip text");

        var normalizedTip = Assert.IsType<TextBlock>(ToolTip.GetTip(target));
        Assert.Equal("Tooltip text", normalizedTip.Text);
        var normalizedForeground = Assert.IsAssignableFrom<ISolidColorBrush>(normalizedTip.Foreground);
        Assert.Equal(Colors.Black, normalizedForeground.Color);

        var window = new Window
        {
            Width = 240,
            Height = 120,
            Content = target,
        };

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            ToolTip.SetIsOpen(target, true);
            await FlushUiQueueAsync();

            Assert.True(ToolTip.GetIsOpen(target));
        }
        finally
        {
            ToolTip.SetIsOpen(target, false);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RightWorkflowStep3View_TargetCapTooltipUsesCurrentNotchAndEmsCaps()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = new FreeformHelperViewModel
        {
            IsStep3Expanded = true,
            TargetCoverageCapPercent = 128m,
        };
        viewModel.ApplySimulationSafetyOverview(
            SimulationSafetyAuditResult.Empty(afterCap: 512d),
            isStale: false,
            buildFailureText: null);
        var view = new RightWorkflowStep3View
        {
            DataContext = viewModel,
        };
        var window = new Window
        {
            Width = 480,
            Height = 900,
            Content = view,
        };

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            var target = Assert.Single(
                view.GetVisualDescendants().OfType<NumberScrubber>(),
                static scrubber => scrubber is { Value: 128m, Maximum: 255m });
            var tip = Assert.IsType<TextBlock>(ToolTip.GetTip(target));
            Assert.Equal(
                "At uniform 400, target cap 128% maps to After 512; compare with EMS cap 512.",
                tip.Text);

            ToolTip.SetIsOpen(target, true);
            await FlushUiQueueAsync();

            Assert.True(ToolTip.GetIsOpen(target));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task FreeformHelperView_CadLoadOverlay_TogglesSpinnerHostVisibility()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var vm = new FreeformHelperViewModel();
        var fakeHost = new FakeCadLoadSpinnerHost();
        var originalFactory = FreeformHelperView.CadLoadSpinnerHostFactory;
        FreeformHelperView.CadLoadSpinnerHostFactory = () => fakeHost;
        var view = new FreeformHelperView
        {
            DataContext = vm
        };
        var window = new Window
        {
            Width = 1200,
            Height = 800,
            Content = view,
        };

        try
        {
            window.Show();
            await FlushUiQueueAsync();
            await WaitForSpinnerIdleAsync(vm);

            Assert.NotNull(view.CadLoadSpinnerHostForTest);
            Assert.Equal(1, fakeHost.WarmupCallCount);
            var initialShowCount = fakeHost.ShowCallCount;
            var initialHideCount = fakeHost.HideCallCount;

            vm.BeginCadLoadCanvasOverlayScope();
            await FlushUiQueueAsync();

            Assert.Equal(initialShowCount + 1, fakeHost.ShowCallCount);
            Assert.Equal(initialHideCount, fakeHost.HideCallCount);
            Assert.Same(window, fakeHost.LastOwner);

            vm.EndCadLoadCanvasOverlayScope();
            await FlushUiQueueAsync();

            Assert.Equal(initialShowCount + 1, fakeHost.ShowCallCount);
            Assert.Equal(initialHideCount + 1, fakeHost.HideCallCount);

            vm.BeginCadLoadCanvasOverlayScope();
            await FlushUiQueueAsync();

            Assert.Equal(initialShowCount + 2, fakeHost.ShowCallCount);
            Assert.Equal(initialHideCount + 1, fakeHost.HideCallCount);

            vm.EndCadLoadCanvasOverlayScope();
            await FlushUiQueueAsync();

            Assert.Equal(initialShowCount + 2, fakeHost.ShowCallCount);
            Assert.Equal(initialHideCount + 2, fakeHost.HideCallCount);
        }
        finally
        {
            FreeformHelperView.CadLoadSpinnerHostFactory = originalFactory;
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CadLoadSpinnerWindow_RendersVisibleSpinner_AndAnimates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var spinnerWindow = new CadLoadSpinnerWindow();

        try
        {
            spinnerWindow.Show();
            await FlushUiQueueAsync();

            var spinner = spinnerWindow.FindControl<LoadingSpinner>("PART_LoadingSpinner");
            Assert.NotNull(spinner);
            var spinnerFlow = spinner!.FindControl<Avalonia.Controls.Shapes.Ellipse>("SpinnerFlow");
            Assert.NotNull(spinnerFlow);

            using (var frame = spinnerWindow.CaptureRenderedFrame())
            {
                Assert.NotNull(frame);
                using var locked = frame.Lock();
                var visibleSpinnerPixelCount = CountVisibleSpinnerPixels(
                    locked,
                    locked.RowBytes,
                    locked.Size.Width,
                    locked.Size.Height);
                Assert.True(
                    visibleSpinnerPixelCount >= 24,
                    $"Expected visible spinner pixels, but only counted {visibleSpinnerPixelCount}.");
            }

            var dashOffsetBefore = spinnerFlow!.StrokeDashOffset;
            await Task.Delay(120);
            await FlushUiQueueAsync();

            Assert.NotEqual(dashOffsetBefore, spinnerFlow.StrokeDashOffset);
        }
        finally
        {
            spinnerWindow.Close();
        }
    }

    [AvaloniaFact]
    public void MainWindow_WithExpandedConsole_Can_Layout_Headless()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel
        {
            IsConsoleExpanded = true,
        };

        var window = new MainWindow();
        HeadlessSessionGuardAttribute.CloseAtTestEnd(window);
        window.SetShellViewModel(shell);

        var size = new Size(1280, 900);
        window.Measure(size);
        window.Arrange(new Rect(size));

        Assert.NotNull(window);
    }

    [AvaloniaFact]
    public async Task MainWindow_WithExpandedConsole_RendersVisibleConsoleText()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel
        {
            IsConsoleExpanded = true,
        };
        shell.ConsoleText = "08:00:00 | INFO  | test | terminal visible line";
        shell.ConsoleRenderedLineCount = 1;
        shell.ConsoleSourceLineCount = 1;

        var window = new MainWindow();
        window.SetShellViewModel(shell);
        window.Width = 1280;
        window.Height = 900;

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var locked = frame.Lock();
            var visiblePixelCount = CountBrightPixelsInConsoleBody(
                locked,
                locked.RowBytes,
                locked.Size.Width,
                locked.Size.Height);

            Assert.True(
                visiblePixelCount >= 120,
                $"Expected visible terminal glyph pixels in console body, but only counted {visiblePixelCount} bright pixels.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task MainWindow_WithSimulationWorkspace_RendersVisibleConsoleText()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel
        {
            IsConsoleExpanded = true,
        };
        shell.ConsoleText = "09:30:00 | INFO  | simulation | shared terminal visible line";
        shell.ConsoleRenderedLineCount = 1;
        shell.ConsoleSourceLineCount = 1;

        var grid = BuildSimulationGrid();
        var workspaceViewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()),
            new SimulationWorkspaceSession(
                grid,
                BuildSimulationTable(),
                SimulationNullDiffValue,
                SourceRevision: shell.FreeformHelper.SimulationWorkspaceSourceRevision,
                ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet()));

        await shell.ShowSimulationAsync(workspaceViewModel);

        var window = new MainWindow();
        window.SetShellViewModel(shell);
        window.Width = 1280;
        window.Height = 900;

        try
        {
            window.Show();
            await FlushUiQueueAsync();

            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var locked = frame.Lock();
            var visiblePixelCount = CountBrightPixelsInConsoleBody(
                locked,
                locked.RowBytes,
                locked.Size.Width,
                locked.Size.Height);

            Assert.True(
                visiblePixelCount >= 120,
                $"Expected visible terminal glyph pixels in simulation console body, but only counted {visiblePixelCount} bright pixels.");
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task FlushUiQueueAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }

    private static async Task WaitForSpinnerIdleAsync(FreeformHelperViewModel viewModel)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await FlushUiQueueAsync();
            if (!viewModel.IsModalLoadingSpinnerVisible && !viewModel.IsCadLoadCanvasOverlayVisible)
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.False(viewModel.IsModalLoadingSpinnerVisible);
        Assert.False(viewModel.IsCadLoadCanvasOverlayVisible);
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

    private static int CountVisibleSpinnerPixels(ILockedFramebuffer locked, int rowBytes, int width, int height)
    {
        var visibleCount = 0;
        var bytes = new byte[rowBytes * height];
        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * rowBytes) + (x * 4);
                var alpha = bytes[offset + 3];
                var blue = bytes[offset];
                var green = bytes[offset + 1];
                var red = bytes[offset + 2];
                var luma = (0.299 * red) + (0.587 * green) + (0.114 * blue);
                if (alpha >= 32 && luma >= 40)
                {
                    visibleCount++;
                }
            }
        }

        return visibleCount;
    }

    private sealed class FakeCadLoadSpinnerHost : ICadLoadSpinnerHost
    {
        public int WarmupCallCount { get; private set; }
        public int ShowCallCount { get; private set; }
        public int HideCallCount { get; private set; }
        public int DisposeCallCount { get; private set; }
        public Window? LastOwner { get; private set; }

        public void Warmup()
        {
            WarmupCallCount++;
        }

        public void Show(Window owner)
        {
            ShowCallCount++;
            LastOwner = owner;
        }

        public void Hide()
        {
            HideCallCount++;
        }

        public void Dispose()
        {
            DisposeCallCount++;
        }
    }

    private static NotchTable BuildSimulationTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, SimulationNullDiffValue, 0, 0),
                comment: "sample")
        });
    }

    private static RegularGrid BuildSimulationGrid()
    {
        var pads = new List<RegularPad>
        {
            CreateSimulationPad(row: 0, col: 0, index: 0, diffIndex: 10),
            CreateSimulationPad(row: 0, col: 1, index: 1, diffIndex: 11),
        };
        return new RegularGrid(1, 2, SimulationXEdges, SimulationYEdges, pads);
    }

    private static RegularPad CreateSimulationPad(int row, int col, int index, int diffIndex)
    {
        var pad = new RegularPad(
            row,
            col,
            index,
            new Polygon2(new[]
            {
                new Point2(col, row),
                new Point2(col + 1, row),
                new Point2(col + 1, row + 1),
                new Point2(col, row + 1),
            }))
        {
            IcIndex = 0,
            DiffIndex = diffIndex,
        };

        return pad;
    }
}
