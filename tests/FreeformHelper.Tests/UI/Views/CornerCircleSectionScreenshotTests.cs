// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views.WorkspaceSections;
using Nvt.Core.Avalonia.Panels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CornerCircleSectionScreenshotTests
{
    private const int SamplePixelWidth = 320;
    private const int SamplePixelHeight = 180;

    [AvaloniaFact]
    public async Task CaptureTheSectionInBothThemesAndAnExportSampleAsync()
    {
        // NFH_CORNER_CIRCLE_SHOTS keeps the files for review. Without it the test captures into a temp folder and deletes it.
        string? requested = ScreenshotOutputDirectory.Resolve("NFH_CORNER_CIRCLE_SHOTS");
        DirectoryInfo? scratch = requested is null ? TestFiles.CreateTempDirectory() : null;
        string output = requested ?? scratch!.FullName;
        Directory.CreateDirectory(output);
        HeadlessAppBootstrap.EnsureInitialized();
        var app = global::Avalonia.Application.Current!;
        var previousTheme = app.RequestedThemeVariant;
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            await CaptureSectionAsync(output, ThemeVariant.Dark, "corner-circles-dark.png");
            await CaptureSectionAsync(output, ThemeVariant.Light, "corner-circles-light.png");
            string samplePath = Path.Combine(output, "corner-circles-export-sample.png");
            await CornerCircleImageEncoder.SaveToFileAsync(BuildSample(), samplePath, TestContext.Current.CancellationToken);

            using var decoded = SkiaSharp.SKBitmap.Decode(samplePath);
            Assert.Equal(SamplePixelWidth, decoded.Width);
            Assert.Equal(SamplePixelHeight, decoded.Height);
        }
        finally
        {
            scratch?.Delete(recursive: true);
            app.RequestedThemeVariant = previousTheme;
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static async Task CaptureSectionAsync(string output, ThemeVariant theme, string fileName)
    {
        global::Avalonia.Application.Current!.RequestedThemeVariant = theme;
        var viewModel = CreateViewModel();
        viewModel.CornerCircles.Radius = 30m;
        viewModel.CornerCircles.IsEnabled = true;
        var window = new Window
        {
            Width = 420,
            Height = 1150,
            Content = new CoordinatePlannerWorkspaceControlsPaneView { DataContext = viewModel },
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var section = window.GetVisualDescendants().OfType<CoordinatePlannerCornerCircleSectionView>().Single();
            var panel = section.GetVisualDescendants().OfType<CollapsiblePanel>().First();
            panel.IsExpanded = true;
            await HeadlessUiPump.FlushQueueAsync();

            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(output, fileName), new PngBitmapEncoderOptions());
        }
        finally
        {
            window.Close();
        }
    }

    private static CornerCircleImage BuildSample()
    {
        var area = new Polygon2([
            new Point2(0, 0),
            new Point2(SamplePixelWidth, 0),
            new Point2(SamplePixelWidth, SamplePixelHeight),
            new Point2(0, SamplePixelHeight),
        ]);
        var layout = CornerCircleLayoutCalculator.Calculate(new CornerCircleLayoutRequest(area, 30d));
        return CornerCircleImageRenderer.Render(SamplePixelWidth, SamplePixelHeight, layout, drawCenterRectangle: true);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: SamplePixelWidth,
                DefaultPixelHeight: SamplePixelHeight));
    }
}
