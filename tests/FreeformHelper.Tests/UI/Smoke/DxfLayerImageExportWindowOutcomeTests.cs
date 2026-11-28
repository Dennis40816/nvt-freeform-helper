// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.CadExport;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfLayerImageExportWindowOutcomeTests
{
    [AvaloniaFact]
    public async Task ExportButtonsWriteOnlyTheChosenLayerAsBmpWithRequestedPixelsAndPaddingAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            string path = TestFiles.NewTempFilePath("layer-window-", ".bmp");
            var viewModel = await LoadDxfAsync(owner, path);
            var export = viewModel.ExportDxfLayerImageCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            var window = Assert.Single(owner.OwnedWindows.OfType<LayerImageExportWindow>());
            var layerList = Assert.IsType<ListBox>(window.FindControl<ListBox>("LayerList"));
            var firstLayer = Assert.Single(layerList.GetVisualDescendants().OfType<CheckBox>(),
                box => AutomationProperties.GetName(box) == "L1");
            firstLayer.IsChecked = false;
            window.FindControl<HidePanelBlock>("AdvancedOptions")!.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();
            SetNumber(window, "Pixels X", 64m);
            SetNumber(window, "Pixels Y", 40m);
            SetNumber(window, "Padding X", 4m);
            SetNumber(window, "Padding Y", 3m);
            SetNumber(window, "Line width in pixels", 2m);
            Assert.IsType<LayerImageExportViewModel>(window.DataContext).PreviewSettings.UseDarkTheme = false;

            await ClickAsync(window, window.FindControl<Button>("ExportButton")!);
            var settings = Assert.Single(window.OwnedWindows.OfType<LayerImageExportSettingsDialog>());
            settings.FindControl<ComboBox>("FormatComboBox")!.SelectedValue = DxfLayerImageFormat.Bmp;
            await ClickAsync(settings, settings.FindControl<Button>("SaveButton")!);
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(settings.IsVisible);
            Assert.False(window.IsVisible);
            Assert.Equal("Layer image exported (1 file, BMP).", viewModel.StatusText);
            Assert.Equal(new byte[] { 0x42, 0x4D }, TestFiles.ReadBytes(path)[..2]);
            using var bitmap = SKBitmap.Decode(path);
            Assert.NotNull(bitmap);
            Assert.Equal(72, bitmap.Width);
            Assert.Equal(46, bitmap.Height);
            // L2 has two separated outlines; L1 alone has no interior vertical edges.
            Assert.True(HasDarkPixel(bitmap, 23, 23, 28), "right edge of the first L2 outline");
            Assert.True(HasDarkPixel(bitmap, 23, 41, 47), "left edge of the second L2 outline");
            Assert.False(HasDarkPixel(bitmap, 23, 31, 38), "nothing between the L2 outlines");
            Assert.Equal(SKColors.White, bitmap.GetPixel(0, 0));
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    [AvaloniaFact]
    public async Task CancelButtonCharacterizationWritesNothingAndKeepsPreviousStatusAndLayerChoicesAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var owner = new Window();
        try
        {
            owner.Show();
            string path = TestFiles.NewTempFilePath("layer-window-cancel-", ".png");
            var viewModel = await LoadDxfAsync(owner, path);
            string previousStatus = viewModel.StatusText;
            var export = viewModel.ExportDxfLayerImageCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            var window = Assert.Single(owner.OwnedWindows.OfType<LayerImageExportWindow>());
            var dialog = Assert.IsType<LayerImageExportViewModel>(window.DataContext);
            dialog.VisibleRows[0].IsChecked = false;
            SetNumber(window, "Pixels X", 64m);

            await ClickAsync(window, Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                button => button.Content as string == "Cancel"));
            await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.False(window.IsVisible);
            Assert.False(File.Exists(path));
            Assert.Equal(previousStatus, viewModel.StatusText);
            var reopenedExport = viewModel.ExportDxfLayerImageCommand.ExecuteAsync(null);
            await HeadlessUiPump.FlushQueueAsync();
            var reopened = Assert.Single(owner.OwnedWindows.OfType<LayerImageExportWindow>());
            var reopenedModel = Assert.IsType<LayerImageExportViewModel>(reopened.DataContext);
            Assert.Equal(2, reopenedModel.VisibleRows.Count);
            Assert.All(reopenedModel.VisibleRows, row => Assert.True(row.IsChecked));
            await ClickAsync(reopened, Assert.Single(reopened.GetVisualDescendants().OfType<Button>(),
                button => button.Content as string == "Cancel"));
            await reopenedExport.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            Assert.False(File.Exists(path));
        }
        finally
        {
            CloseWindows(owner);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private static async Task<FreeformHelperViewModel> LoadDxfAsync(Window owner, string outputPath)
    {
        string dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("L1", 0, 0, 30, 10), ("L2", 0, 0, 10, 10), ("L2", 20, 0, 30, 10));
        var viewModel = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath),
            OpenDxfLayerImageExportAsync = context => LayerImageExportWindowHost.ShowAsync(owner, context),
        };
        await viewModel.OpenDxfCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static bool HasDarkPixel(SKBitmap bitmap, int y, int fromX, int toX)
    {
        for (var x = fromX; x <= toX; x++)
        {
            if (bitmap.GetPixel(x, y).Red < 128)
            {
                return true;
            }
        }

        return false;
    }
    private static void SetNumber(Window window, string name, decimal value)
    {
        var input = Assert.Single(window.GetVisualDescendants().OfType<NumberScrubber>(),
            control => AutomationProperties.GetName(control) == name);
        input.Value = value;
    }

    private static async Task ClickAsync(Window window, Control target)
    {
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(target.IsEffectivelyEnabled);
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(point);
        window.MouseDown(point.Value, MouseButton.Left);
        window.MouseUp(point.Value, MouseButton.Left);
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static void CloseWindows(Window window)
    {
        foreach (var child in window.OwnedWindows.ToArray())
        {
            CloseWindows(child);
        }

        window.Close();
    }
}
