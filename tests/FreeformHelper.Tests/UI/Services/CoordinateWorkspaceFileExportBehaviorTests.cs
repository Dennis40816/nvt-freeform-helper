// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using SkiaSharp;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinateWorkspaceFileExportBehaviorTests
{
    [Fact]
    public async Task CsvCommandThroughTextWriterSavesAllCurrentRowsWithUtf8BomAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateWorkspace();
        string path = TestFiles.NewTempFilePath("coordinate-artifacts-", ".csv");
        viewModel.RequestSaveTextFileAsync = request => WriteTextAsync(path, request.Content);
        viewModel.CustomPointLabelText = "Probe, \"A\" 測試";
        viewModel.CustomPointMachineX = 25.25m;
        viewModel.CustomPointMachineY = 10.5m;
        viewModel.AddCustomPointCommand.Execute(null);
        viewModel.SelectedArtifactRow = viewModel.ArtifactRows[viewModel.ArtifactRows.Count - 1];
        string expected = CoordinateArtifactService.ExportCsv(viewModel.ArtifactRows);

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);

        Assert.True(File.Exists(path));
        Assert.True(TestFiles.ReadBytes(path).AsSpan().StartsWith("\uFEFF"u8));
        Assert.Equal(expected, TestFiles.ReadContent(path));
        Assert.Contains("Exported CSV", viewModel.StatusText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task PreviewImageWriterSavesRenderedPlannerAreaAsDecodablePngAsync()
    {
        using var culture = TestCulture.UseEnUs();
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new CoordinatePlannerWorkspaceView { DataContext = CreateWorkspace() };
        var window = new Window { Content = view, Width = 1400, Height = 1000 };
        string path = TestFiles.NewTempFilePath("coordinate-preview-", ".png");
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var preview = Assert.IsType<Grid>(view.FindControl<Grid>("CoordinatePreviewHost"));
            Assert.True(preview.Bounds.Width > 1 && preview.Bounds.Height > 1);
            var expectedSize = new PixelSize((int)Math.Ceiling(preview.Bounds.Width), (int)Math.Ceiling(preview.Bounds.Height));
            using var bitmap = new RenderTargetBitmap(expectedSize);
            bitmap.Render(preview);

            await PreviewImageWriter.SaveToFileAsync(bitmap, path);

            Assert.True(File.Exists(path));
            byte[] bytes = TestFiles.ReadBytes(path);
            using var png = new MemoryStream(bytes);
            using var codec = SKCodec.Create(png);
            Assert.NotNull(codec);
            Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
            using var decoded = SKBitmap.Decode(bytes);
            Assert.NotNull(decoded);
            Assert.Equal(expectedSize.Width, decoded.Width);
            Assert.Equal(expectedSize.Height, decoded.Height);
        }
        finally
        {
            window.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private static async Task<bool> WriteTextAsync(string path, string content)
    {
        await TextExportWriter.WriteAsync(path, content, includeBom: true, TestContext.Current.CancellationToken);
        return true;
    }

    private static CoordinatePlannerWorkspaceViewModel CreateWorkspace()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 30d, cellHeight: 20d),
                [TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 400d, 600d)],
                ["AA.drawing"], SourceRevision: 0,
                DefaultMachineWidth: 150d, DefaultMachineHeight: 200d,
                DefaultPixelWidth: 1500, DefaultPixelHeight: 2000));
    }
}
