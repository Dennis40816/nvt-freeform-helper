// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinatePlannerCommandTests
{
    private static readonly string[] _activeAreaLayerNames = ["AA.drawing"];

    [Fact]
    public void SyncFromWorkspaceCommandRestoresDefaultsAndReplacesArtifactRowsFromChangedSource()
    {
        var sourcePads = new List<CadPad>
        {
            TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 300d, 400d),
        };
        var viewModel = CreateViewModel(sourcePads);
        viewModel.MachineOriginX = 12m;
        viewModel.MachineOriginY = 8m;
        viewModel.MachineWidth = 40m;
        viewModel.MachineHeight = 20m;
        viewModel.PixelWidth = 200m;
        viewModel.PixelHeight = 100m;

        viewModel.SyncFromWorkspaceCommand.Execute(null);

        AssertWorkspaceDefaults(viewModel);
        Assert.True(viewModel.ShowCustomArray);
        AssertArtifactPoint(viewModel, "aa-tl", new Point2(100d, 400d), new Point2(0d, 0d));
        AssertArtifactPoint(viewModel, "array-br", new Point2(300d, 200d), new Point2(100d, 50d));
        Assert.Equal("Coordinate planner synced from current workspace.", viewModel.StatusText);
        var previousRows = viewModel.ArtifactRows;
        var previousCorner = Assert.Single(previousRows, static row => row.Key == "aa-tl");
        sourcePads[0] = TestGeometryFactory.CreateCadPad(2, "AA.drawing", 10d, 20d, 40d, 60d);

        viewModel.SyncFromWorkspaceCommand.Execute(null);

        Assert.Equal(previousRows.Count, viewModel.ArtifactRows.Count);
        Assert.DoesNotContain(previousCorner, viewModel.ArtifactRows);
        AssertArtifactPoint(viewModel, "aa-tl", new Point2(10d, 60d), new Point2(0d, 0d));
        AssertArtifactPoint(viewModel, "array-br", new Point2(40d, 20d), new Point2(100d, 50d));
        Assert.Equal("Coordinate planner synced from current workspace.", viewModel.StatusText);
    }

    [Fact]
    public void FitCanvasCommandRaisesOneRequestWithViewModelSender()
    {
        var viewModel = CreateViewModel();
        var senders = new List<object?>();
        viewModel.FitCanvasRequested += (sender, _) => senders.Add(sender);

        viewModel.FitCanvasCommand.Execute(null);

        Assert.Same(viewModel, Assert.Single(senders));
    }

    [Fact]
    public async Task ExportPreviewPngCommandUpdatesStatusAsHostResultChangesAndHostDetachesAsync()
    {
        var viewModel = CreateViewModel();
        var exportRequests = 0;
        var exportResult = true;
        viewModel.RequestExportPreviewPngAsync = () =>
        {
            exportRequests++;
            return Task.FromResult(exportResult);
        };

        await viewModel.ExportPreviewPngCommand.ExecuteAsync(null);

        Assert.Equal(1, exportRequests);
        Assert.Equal("Coordinate preview exported.", viewModel.StatusText);
        exportResult = false;

        await viewModel.ExportPreviewPngCommand.ExecuteAsync(null);

        Assert.Equal(2, exportRequests);
        Assert.Equal("Coordinate preview export cancelled.", viewModel.StatusText);
        viewModel.RequestExportPreviewPngAsync = null;

        await viewModel.ExportPreviewPngCommand.ExecuteAsync(null);

        Assert.Equal(2, exportRequests);
        Assert.Equal("Coordinate image export is unavailable in the current host.", viewModel.StatusText);
    }

    [Fact]
    public async Task ShowArtifactDetailCommandIgnoresNullBeforeAndAfterValidRequestAsync()
    {
        var viewModel = CreateViewModel();
        var row = viewModel.ArtifactRows.Single(static artifact => artifact.Key == "aa-tl");
        var requests = new List<CoordinateArtifactRow>();
        viewModel.RequestArtifactDetailAsync = requestedRow =>
        {
            requests.Add(requestedRow);
            return Task.CompletedTask;
        };

        await viewModel.ShowArtifactDetailCommand.ExecuteAsync(null);

        Assert.Empty(requests);
        Assert.Null(viewModel.SelectedArtifactRow);

        await viewModel.ShowArtifactDetailCommand.ExecuteAsync(row);

        Assert.Same(row, Assert.Single(requests));
        Assert.Same(row, viewModel.SelectedArtifactRow);

        await viewModel.ShowArtifactDetailCommand.ExecuteAsync(null);

        Assert.Same(row, Assert.Single(requests));
        Assert.Same(row, viewModel.SelectedArtifactRow);
    }

    [AvaloniaFact]
    public async Task ArtifactDetailWindowShowsArtifactRowAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateViewModel();
        var row = viewModel.ArtifactRows.Single(static artifact => artifact.Key == "aa-br");
        var window = new CoordinateArtifactDetailWindow
        {
            DataContext = row,
        };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(window.IsVisible);
            var text = window.GetVisualDescendants().OfType<TextBlock>().Select(static block => block.Text).ToArray();
            Assert.Contains(row.Label, text);
            Assert.Contains(row.KindRecipeText, text);
            Assert.Contains(row.MachineText, text);
            Assert.Contains(row.SourceText, text);
        }
        finally
        {
            window.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel(IReadOnlyList<CadPad>? cadPads = null)
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                cadPads ?? Array.Empty<CadPad>(),
                cadPads is null ? Array.Empty<string>() : _activeAreaLayerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
    }

    private static void AssertWorkspaceDefaults(CoordinatePlannerWorkspaceViewModel viewModel)
    {
        Assert.Equal(0m, viewModel.MachineOriginX);
        Assert.Equal(0m, viewModel.MachineOriginY);
        Assert.Equal(100m, viewModel.MachineWidth);
        Assert.Equal(50m, viewModel.MachineHeight);
        Assert.Equal(1000m, viewModel.PixelWidth);
        Assert.Equal(500m, viewModel.PixelHeight);
    }

    private static void AssertArtifactPoint(
        CoordinatePlannerWorkspaceViewModel viewModel,
        string key,
        Point2 expectedWorld,
        Point2 expectedMachine)
    {
        var row = Assert.Single(viewModel.ArtifactRows, artifact => artifact.Key == key);
        Assert.Equal(expectedWorld.X, row.WorldX, 6);
        Assert.Equal(expectedWorld.Y, row.WorldY, 6);
        Assert.Equal(expectedMachine.X, row.MachineX, 6);
        Assert.Equal(expectedMachine.Y, row.MachineY, 6);
    }
}
