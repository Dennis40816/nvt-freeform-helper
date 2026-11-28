// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerWorkspaceEditingBehaviorTests
{
    private static readonly string[] _layerNames = ["AA.drawing"];
    private static readonly double[] _horizontalGuideYs = [0d, 12.5d, 25d];
    private static readonly double[] _verticalGuideXs = [20d, 80d];

    [Fact]
    public void DirectCornerEdits_RebuildRowsAndKeepTheSelectedCornerWhenAnotherCornerChanges()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        Assert.True(viewModel.BeginCustomArrayCornerEdit("array-tl"));

        viewModel.CustomArrayTopLeftMachineX = 12.5m;
        viewModel.CustomArrayTopLeftMachineY = 7.25m;

        var selected = Assert.IsType<CoordinateArtifactRow>(viewModel.SelectedArtifactRow);
        Assert.Equal("array-tl", selected.Key);
        Assert.Equal(12.5d, selected.MachineX, 6);
        Assert.Equal(7.25d, selected.MachineY, 6);
        Assert.Equal(12.5m, viewModel.SelectedCustomArrayCornerMachineX);
        Assert.Equal(7.25m, viewModel.SelectedCustomArrayCornerMachineY);
        Assert.Equal("TL · machine (12.5, 7.25) mm", viewModel.SelectedCustomArrayCornerSummaryText);

        viewModel.CustomArrayBottomRightMachineX = 83.75m;
        viewModel.CustomArrayBottomRightMachineY = 41.5m;

        var other = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "array-br");
        Assert.Equal(83.75d, other.MachineX, 6);
        Assert.Equal(41.5d, other.MachineY, 6);
        Assert.Equal("array-tl", viewModel.SelectedArtifactRow?.Key);
        Assert.Equal("array-tl", viewModel.SelectedCustomArrayCornerOption.Key);
        Assert.Equal("TL · machine (12.5, 7.25) mm", viewModel.SelectedCustomArrayCornerSummaryText);
    }

    [Fact]
    public void ExplicitGuideText_IgnoresInvalidTokensAndPlotsDistinctInRangePositions()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        var positions = viewModel.GuideGenerationOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.ExplicitPositions);
        viewModel.SelectedHorizontalGuideGenerationOption = positions;
        viewModel.SelectedVerticalGuideGenerationOption = positions;
        viewModel.HorizontalGuidePositionListText = "25; bad\t-5\r\n12.5,25 100";
        viewModel.VerticalGuidePositionListText = "80,20;invalid\n20 120";

        Assert.Equal(_horizontalGuideYs, viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY));
        Assert.Equal(_verticalGuideXs, viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX));
        Assert.True(viewModel.IsHorizontalGuideExplicitMode);
        Assert.True(viewModel.IsVerticalGuideExplicitMode);
        Assert.False(viewModel.IsHorizontalGuideCountMode);
        Assert.False(viewModel.IsVerticalGuidePitchMode);

        viewModel.HorizontalGuidePositionListText = " \t\r\n ";

        Assert.Empty(viewModel.HorizontalGuideRows);
        Assert.Equal(2, viewModel.VerticalGuideRows.Count);
        Assert.True(viewModel.IsHorizontalGuideExplicitMode);
    }

    [Fact]
    public void ReturningToRegularBounds_PublishesClearedLayerPreferenceAndRoundedPixelCounts()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        var preferences = new List<CoordinatePlannerWorkspaceViewModel.CoordinatePlannerWorkspacePreferences>();
        viewModel.WorkspacePreferencesChanged += preferences.Add;
        viewModel.PixelWidth = 1280.5m;
        viewModel.PixelHeight = 720.49m;
        Assert.Equal("AA.drawing", viewModel.BuildWorkspacePreferences().PreferredAaOutlineLayerName);

        viewModel.SelectedAaBoundsOption = viewModel.AaBoundsOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerBoundsMode.RegularGrid);

        Assert.Equal(3, preferences.Count);
        var current = viewModel.BuildWorkspacePreferences();
        Assert.Equal(current, preferences[^1]);
        Assert.Equal(1281, current.PixelWidth);
        Assert.Equal(720, current.PixelHeight);
        Assert.Equal(string.Empty, current.PreferredAaOutlineLayerName);
        Assert.Equal("Pixels: 1281 x 720", viewModel.PixelSummaryText);
        Assert.Equal("AA outline: regular grid bounds", viewModel.ActiveAreaSourceSummaryText);
        var corner = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tr");
        Assert.Equal(10d, corner.WorldX, 6);
        Assert.Equal(5d, corner.WorldY, 6);
        Assert.Equal(1281d, corner.PixelX, 6);
    }

    [Fact]
    public async Task ShowArtifactDetailCommand_WithoutHost_SelectsTheRequestedRowAsync()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedArtifactRow = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tl");
        var requestedRow = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-br");
        var snapshot = viewModel.Snapshot;

        await viewModel.ShowArtifactDetailCommand.ExecuteAsync(requestedRow);

        Assert.Same(requestedRow, viewModel.SelectedArtifactRow);
        Assert.Same(snapshot, viewModel.Snapshot);
        Assert.False(viewModel.ShowArtifactDetailCommand.IsRunning);
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                new[] { TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 300d, 400d) },
                _layerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
    }
}
