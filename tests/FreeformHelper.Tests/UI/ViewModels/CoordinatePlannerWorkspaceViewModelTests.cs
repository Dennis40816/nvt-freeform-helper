using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerWorkspaceViewModelTests
{
    private static readonly string[] CoordinateLayerNames = ["AA.drawing", "SIG"];

    [Fact]
    public void Constructor_WhenAaDrawingExists_DefaultsToAaDrawingBounds()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                new[]
                {
                    BuildCadPad(1, "AA.drawing", 100d, 200d, 300d, 400d),
                    BuildCadPad(2, "SIG", 10d, 20d, 30d, 40d),
                },
                CoordinateLayerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        Assert.Equal(
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerBoundsMode.LayerBounds,
            viewModel.SelectedAaBoundsOption.Mode);
        Assert.Equal("AA.drawing", viewModel.SelectedAaBoundsOption.LayerName);
        Assert.Contains("AA.drawing", viewModel.ActiveAreaSourceSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UseActiveAreaSafeCornersCommand_LoadsSafeAaCornersIntoCustomArray()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        viewModel.CopperPillarDiameter = 10m;
        viewModel.UseActiveAreaSafeCornersCommand.Execute(null);

        Assert.True(viewModel.ShowCustomArray);
        Assert.Equal(5m, viewModel.CustomArrayTopLeftMachineX);
        Assert.Equal(5m, viewModel.CustomArrayTopLeftMachineY);
        Assert.Equal(95m, viewModel.CustomArrayTopRightMachineX);
        Assert.Equal(5m, viewModel.CustomArrayTopRightMachineY);
        Assert.Equal(95m, viewModel.CustomArrayBottomRightMachineX);
        Assert.Equal(45m, viewModel.CustomArrayBottomRightMachineY);
        Assert.Equal(5m, viewModel.CustomArrayBottomLeftMachineX);
        Assert.Equal(45m, viewModel.CustomArrayBottomLeftMachineY);
    }

    [Fact]
    public void MoveCustomArrayCornerToWorldPoint_UpdatesSelectedHandleAndArtifactRow()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        var moved = viewModel.MoveCustomArrayCornerToWorldPoint("array-tr", new Point2(2.5d, 3.75d));
        viewModel.CompleteCustomArrayCornerEdit("array-tr");

        Assert.True(moved);
        Assert.True(viewModel.ShowCustomArray);
        Assert.Equal("array-tr", viewModel.SelectedCustomArrayCornerOption.Key);
        Assert.Equal(25m, viewModel.CustomArrayTopRightMachineX);
        Assert.Equal(12.5m, viewModel.CustomArrayTopRightMachineY);
        Assert.Equal(25m, viewModel.SelectedCustomArrayCornerMachineX);
        Assert.Equal(12.5m, viewModel.SelectedCustomArrayCornerMachineY);
        Assert.Equal("array-tr", viewModel.SelectedArtifactRow?.Key);
        var row = Assert.Single(viewModel.ArtifactRows, static artifact => artifact.Key == "array-tr");
        Assert.Equal(25d, row.MachineX, 6);
        Assert.Equal(12.5d, row.MachineY, 6);
        Assert.Contains("TR", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedCustomArrayCornerInspector_UpdatesActiveCornerOnly()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        viewModel.ShowCustomArray = true;
        viewModel.SelectedCustomArrayCornerOption = viewModel.CustomArrayCornerOptions.Single(static option => option.Key == "array-bl");

        viewModel.SelectedCustomArrayCornerMachineX = 9m;
        viewModel.SelectedCustomArrayCornerMachineY = 41m;

        Assert.Equal(9m, viewModel.CustomArrayBottomLeftMachineX);
        Assert.Equal(41m, viewModel.CustomArrayBottomLeftMachineY);
        Assert.Equal(100m, viewModel.CustomArrayTopRightMachineX);
        Assert.Equal("array-bl", viewModel.SelectedArtifactRow?.Key);
        Assert.Contains("BL", viewModel.SelectedCustomArrayCornerSummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WhenPreferredAaLayerHasNoCadPads_UsesRegularGridFallbackDiagnostics()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                new[]
                {
                    BuildCadPad(2, "SIG", 10d, 20d, 30d, 40d),
                },
                CoordinateLayerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500,
                PreferredActiveAreaOutlineLayerName: "AA.drawing"));

        Assert.Equal(
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerBoundsMode.LayerBounds,
            viewModel.SelectedAaBoundsOption.Mode);
        Assert.Equal("AA.drawing", viewModel.SelectedAaBoundsOption.LayerName);
        Assert.Contains("regular grid bounds", viewModel.ActiveAreaSourceSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AA.drawing", viewModel.ActiveAreaSourceSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_BuildsFlatArtifactTableWithEssentialsOverlay()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        Assert.Equal(
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerOverlayMode.Essentials,
            viewModel.SelectedOverlayModeOption.Mode);
        Assert.Equal(
            CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideBasisMode.ActiveArea,
            viewModel.SelectedGuideBasisOption.Mode);
        Assert.False(viewModel.HighlightUnmatched);
        Assert.False(viewModel.HighlightFreeform);
        Assert.False(viewModel.ShowRegularAxisLabels);
        Assert.False(viewModel.ShowCalibrationDetails);
        Assert.Contains(viewModel.ArtifactRows, static row => row.Key == "aa-tl");
        Assert.Contains(viewModel.ArtifactRows, static row => row.Key == "bist-center");
        Assert.Equal(19, viewModel.ArtifactRows.Count);
    }

    [Fact]
    public void SelectedGuideBasisOption_WhenRegularGrid_RebuildsGuideRowsFromRegularBounds()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                new[]
                {
                    BuildCadPad(1, "AA.drawing", 100d, 200d, 300d, 400d),
                },
                CoordinateLayerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        var regularGuideOption = viewModel.GuideBasisOptions.Single(
            option => option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideBasisMode.RegularGrid);

        viewModel.SelectedGuideBasisOption = regularGuideOption;

        Assert.Contains("regular grid", viewModel.GuideSourceSummaryText, StringComparison.OrdinalIgnoreCase);
        var firstHorizontalGuide = Assert.Single(viewModel.Snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(0d, firstHorizontalGuide.StartWorld.X, 6);
        Assert.Equal(5d, firstHorizontalGuide.StartWorld.Y, 6);
        Assert.Equal(10d, firstHorizontalGuide.EndWorld.X, 6);
        Assert.Equal(5d, firstHorizontalGuide.EndWorld.Y, 6);
        Assert.Equal(0d, viewModel.PreviewBounds.MinX, 6);
        Assert.Equal(0d, viewModel.PreviewBounds.MinY, 6);
        Assert.Equal(300d, viewModel.PreviewBounds.MaxX, 6);
        Assert.Equal(400d, viewModel.PreviewBounds.MaxY, 6);
    }

    [Fact]
    public void SortArtifactRowsCommand_SortsFlatArtifactTable()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        viewModel.SortArtifactRowsCommand.Execute("Label");

        Assert.Equal(
            viewModel.ArtifactRows.OrderBy(static row => row.Label, StringComparer.Ordinal).Select(static row => row.Key),
            viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Contains("Label", viewModel.ArtifactSortStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShowArtifactDetailCommand_SelectsRowAndRequestsDialog()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        CoordinateArtifactRow? requestedRow = null;
        viewModel.ArtifactDetailRequested += row => requestedRow = row;
        var row = viewModel.ArtifactRows[0];

        viewModel.ShowArtifactDetailCommand.Execute(row);

        Assert.Same(row, viewModel.SelectedArtifactRow);
        Assert.Same(row, requestedRow);
    }

    [Fact]
    public async Task CopyArtifactsCommand_CopiesSelectedRowWhenAvailable()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        var row = viewModel.ArtifactRows.Single(static artifact => artifact.Key == "aa-tl");
        var copiedText = string.Empty;
        viewModel.SelectedArtifactRow = row;
        viewModel.RequestSetClipboardTextAsync = text =>
        {
            copiedText = text;
            return Task.FromResult(true);
        };

        await viewModel.CopySelectedArtifactCommand.ExecuteAsync(null);

        Assert.Contains("AA TL\tPoint\tAA corners", copiedText, StringComparison.Ordinal);
        Assert.DoesNotContain("AA TR\tPoint\tAA corners", copiedText, StringComparison.Ordinal);
        Assert.Contains("Copied 1 selected coordinate row.", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopyAllArtifactsCommand_CopiesCurrentSortedRows()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        var copiedText = string.Empty;
        viewModel.RequestSetClipboardTextAsync = text =>
        {
            copiedText = text;
            return Task.FromResult(true);
        };

        await viewModel.CopyAllArtifactsCommand.ExecuteAsync(null);

        Assert.Contains("AA TL\tPoint\tAA corners", copiedText, StringComparison.Ordinal);
        Assert.Contains("AA TR\tPoint\tAA corners", copiedText, StringComparison.Ordinal);
        Assert.Contains("Copied 19 coordinate row(s).", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedGuideGenerationOptions_RebuildGuidesFromPitchAndExplicitPositions()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        var pitchOption = viewModel.GuideGenerationOptions.Single(
            option => option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.Pitch);
        var explicitOption = viewModel.GuideGenerationOptions.Single(
            option => option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.ExplicitPositions);

        viewModel.SelectedHorizontalGuideGenerationOption = pitchOption;
        viewModel.HorizontalGuidePitch = 20m;
        viewModel.HorizontalGuideInset = 5m;
        viewModel.SelectedVerticalGuideGenerationOption = explicitOption;
        viewModel.VerticalGuideInset = 10m;
        viewModel.VerticalGuidePositionListText = "90, 10, 50, 200";

        var horizontalMachineY = viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY)
            .ToArray();
        var verticalMachineX = viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX)
            .ToArray();

        Assert.Equal([5d, 25d, 45d], horizontalMachineY);
        Assert.Equal([10d, 50d, 90d], verticalMachineX);
        Assert.Contains("pitch", viewModel.GuideSummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit", viewModel.GuideSummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CustomPointAndPathCommands_AddRowsToArtifactTable()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));

        viewModel.CustomPointLabelText = "Probe A";
        viewModel.CustomPointMachineX = 25m;
        viewModel.CustomPointMachineY = 10m;
        viewModel.AddCustomPointCommand.Execute(null);
        viewModel.CustomPathLabelText = "Edge sweep";
        viewModel.CustomPathStartMachineX = 0m;
        viewModel.CustomPathStartMachineY = 5m;
        viewModel.CustomPathEndMachineX = 100m;
        viewModel.CustomPathEndMachineY = 5m;
        viewModel.CustomPathStepCount = 3m;
        viewModel.AddCustomPathCommand.Execute(null);

        Assert.True(viewModel.HasCustomArtifacts);
        Assert.Equal(2, viewModel.CustomRecipeRows.Count);
        Assert.Contains(viewModel.ArtifactRows, static row => row.Key == "custom-point-1" && row.Recipe == "Custom point");
        Assert.Contains(viewModel.ArtifactRows, static row => row.Key == "custom-path-1" && row.Kind == "Path");
        Assert.Equal(3, viewModel.ArtifactRows.Count(static row => row.Kind == "Step" && row.Recipe == "Custom path"));

        viewModel.ClearCustomArtifactsCommand.Execute(null);

        Assert.False(viewModel.HasCustomArtifacts);
        Assert.DoesNotContain(viewModel.ArtifactRows, static row => row.Key.StartsWith("custom-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportArtifactsCommands_RequestCsvAndJsonFiles()
    {
        var viewModel = new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                BuildGrid(),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
        var requests = new List<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerTextExportRequest>();
        viewModel.RequestSaveTextFileAsync = request =>
        {
            requests.Add(request);
            return Task.FromResult(true);
        };

        await viewModel.ExportArtifactsCsvCommand.ExecuteAsync(null);
        await viewModel.ExportArtifactsJsonCommand.ExecuteAsync(null);

        Assert.Equal(2, requests.Count);
        Assert.Equal("coordinate_artifacts.csv", requests[0].SuggestedFileName);
        Assert.Equal("csv", requests[0].DefaultExtension);
        Assert.Contains("key,label,kind,recipe", requests[0].Content, StringComparison.Ordinal);
        Assert.Equal("coordinate_artifacts.json", requests[1].SuggestedFileName);
        Assert.Equal("json", requests[1].DefaultExtension);
        Assert.Contains("\"rows\": [", requests[1].Content, StringComparison.Ordinal);
    }

    private static RegularGrid BuildGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d);
    }

    private static CadPad BuildCadPad(int id, string layer, double minX, double minY, double maxX, double maxY)
    {
        return TestGeometryFactory.CreateCadPad(id, layer, minX, minY, maxX, maxY);
    }
}
