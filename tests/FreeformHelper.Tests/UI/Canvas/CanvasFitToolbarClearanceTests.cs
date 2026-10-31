using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Guards the whole chain that keeps the column ruler away from the layer toggles at the top of the canvas:
/// the toolbar measurement, the binding to the canvas and the position of the fitted content on the screen.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class CanvasFitToolbarClearanceTests
{
    private const double Tolerance = 1e-6;

    [AvaloniaTheory]
    [InlineData(1)]
    [InlineData(3)]
    public void FreeformHelperView_FitToContent_ReservesMeasuredToolbarBand(int icCount)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var window = new MainWindow { Width = 1280, Height = 900 };
        window.SetShellViewModel(shell);
        window.Show();
        HeadlessUiPump.Drain(window);

        try
        {
            var canvas = window.GetVisualDescendants().OfType<PadCanvas>().Single();
            var overlay = window.GetVisualDescendants().OfType<CanvasOverlayControls>().Single();
            var toolbar = overlay.GetVisualDescendants().OfType<StackPanel>().Single(static panel => panel.Name == "CanvasLeftOverlay");

            // A tall, narrow grid, so the height of the canvas limits the zoom and the top margin shows on the screen.
            // With several ICs the first row also gets the IC group blocks above the column labels.
            var grid = TestGeometryFactory.CreateRegularGrid(
                rows: 20,
                cols: 3,
                icIndexSelector: (_, col) => col % icCount);
            canvas.RegularPads = grid.Pads;
            HeadlessUiPump.Drain(window);

            // The toolbar measurement reaches the canvas through the ElementName binding.
            var toolbarBottom = toolbar.TranslatePoint(new Point(0d, toolbar.Bounds.Height), canvas)!.Value.Y;
            Assert.True(overlay.TopToolbarInset > 0d);
            Assert.Equal(toolbarBottom, overlay.TopToolbarInset, 0.5d);
            Assert.Equal(overlay.TopToolbarInset, canvas.FitTopInset, Tolerance);

            var topWithBinding = FittedContentTop(window, canvas, grid.Pads);

            // The same fit without the inset puts the content higher by exactly the band of the toolbar.
            canvas.FitTopInset = 0d;
            var topWithoutInset = FittedContentTop(window, canvas, grid.Pads);

            // The unshifted fit already leaves room for the column labels and the IC group blocks above the content.
            // The shift moves that whole ruler band below the toolbar.
            Assert.True(topWithoutInset > 0d);
            Assert.Equal(overlay.TopToolbarInset, topWithBinding - topWithoutInset, 1e-3d);
        }
        finally
        {
            window.Close();
        }
    }

    private static double FittedContentTop(Window window, PadCanvas canvas, IReadOnlyList<RegularPad> pads)
    {
        canvas.FitToContent();
        HeadlessUiPump.Drain(window);

        var frame = canvas.GetViewFrameSnapshot();
        var worldTop = pads.Max(static pad => pad.Bounds.MaxY);
        var worldBottom = pads.Min(static pad => pad.Bounds.MinY);

        // The screen Y axis points down, so the world top is the smallest screen Y.
        var top = (-worldTop * frame.Zoom) + frame.Pan.Y;
        var bottom = (-worldBottom * frame.Zoom) + frame.Pan.Y;
        Assert.True(top >= 0d);
        Assert.True(bottom <= canvas.Bounds.Height + Tolerance);
        return top;
    }
}
