// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinatePlannerWorkspaceViewCharacterizationTests
{
    [AvaloniaFact]
    public async Task HostCallbacksAreAssignedWhileAttachedAndClearedWhenDataContextIsRemovedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateViewModel();
        var view = new CoordinatePlannerWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.NotNull(viewModel.RequestArtifactDetailAsync);
            Assert.NotNull(viewModel.RequestExportPreviewPngAsync);
            Assert.NotNull(viewModel.RequestSetClipboardTextAsync);
            Assert.NotNull(viewModel.RequestSaveTextFileAsync);

            view.DataContext = null;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Null(viewModel.RequestArtifactDetailAsync);
            Assert.Null(viewModel.RequestExportPreviewPngAsync);
            Assert.Null(viewModel.RequestSetClipboardTextAsync);
            Assert.Null(viewModel.RequestSaveTextFileAsync);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task OverlayDrawsSnapshotAndClearsBothCanvasesWhenDataContextIsRemovedAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var view = new CoordinatePlannerWorkspaceView { DataContext = CreateViewModel() };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var shapes = Assert.IsType<Canvas>(view.FindControl<Canvas>("CoordinateOverlayCanvas"));
            var labels = Assert.IsType<Canvas>(view.FindControl<Canvas>("CoordinateOverlayLabelCanvas"));

            Assert.NotEmpty(shapes.Children);
            Assert.NotEmpty(labels.Children);

            view.DataContext = null;
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Empty(shapes.Children);
            Assert.Empty(labels.Children);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ArrayCornerDragUpdatesMachineCoordinatesAndCompletesEditOnReleaseAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateViewModel();
        viewModel.CopperPillarDiameter = 0m;
        viewModel.CustomArrayColumnCount = 0m;
        viewModel.CustomArrayRowCount = 0m;
        viewModel.CustomArrayTopLeftMachineX = 20m;
        viewModel.CustomArrayTopLeftMachineY = 10m;
        viewModel.ShowCustomArray = true;
        var view = new CoordinatePlannerWorkspaceView { DataContext = viewModel };
        var window = CreateWindow(view);
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            var overlay = Assert.IsType<Canvas>(view.FindControl<Canvas>("CoordinateOverlayCanvas"));
            var handle = Assert.Single(overlay.Children.OfType<Ellipse>(),
                static ellipse => ToolTip.GetTip(ellipse) is "Array TL" or TextBlock { Text: "Array TL" });
            Assert.NotNull(handle.Cursor);
            Assert.Equal(20m, viewModel.CustomArrayTopLeftMachineX);
            Assert.Equal(10m, viewModel.CustomArrayTopLeftMachineY);
            var canvas = Assert.IsType<PadCanvas>(view.FindControl<PadCanvas>("CoordinatePadCanvas"));
            var start = handle.TranslatePoint(new Point(handle.Bounds.Width / 2d, handle.Bounds.Height / 2d), window)!.Value;
            var end = canvas.TranslatePoint(canvas.WorldToScreen(new Point2(4d, 2d)), window)!.Value;

            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal("array-tl", viewModel.SelectedCustomArrayCornerOption.Key);
            Assert.Equal(40m, viewModel.CustomArrayTopLeftMachineX);
            Assert.Equal(30m, viewModel.CustomArrayTopLeftMachineY);

            window.MouseUp(end, MouseButton.Left);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.Equal(40m, viewModel.SelectedCustomArrayCornerMachineX);
            Assert.Equal(30m, viewModel.SelectedCustomArrayCornerMachineY);
            Assert.Equal("array-tl", Assert.IsType<CoordinateArtifactRow>(viewModel.SelectedArtifactRow).Key);
            Assert.Equal("4-point array TL set to (40, 30) mm.", viewModel.StatusText);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window CreateWindow(Control content) => new() { Content = content, Width = 1400, Height = 900 };

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel() => new(
        new CoordinatePlannerWorkspaceUseCase(),
        new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
            [], [], SourceRevision: 0,
            DefaultMachineWidth: 100d, DefaultMachineHeight: 50d,
            DefaultPixelWidth: 1000, DefaultPixelHeight: 500));
}
