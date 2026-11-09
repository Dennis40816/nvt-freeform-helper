// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerWorkspaceDisplayBehaviorTests
{
    private static readonly string[] _layerNames = ["AA.drawing", "SIG", "sig"];
    private static readonly int[] _allPadIds = [1, 2, 3];

    [Fact]
    public void SelectedLayerOption_FiltersCadByExactLayerAndCanRestoreAllPads()
    {
        var viewModel = CreateViewModel();
        var snapshot = viewModel.Snapshot;
        Assert.Equal(3, viewModel.CadPadsForCanvas.Count);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option => option.LayerName == "SIG");

        Assert.Equal(2, Assert.Single(viewModel.CadPadsForCanvas).Id);
        Assert.True(viewModel.ShowCad);
        Assert.Equal("Layer: SIG", viewModel.LayerSummaryText);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerCadLayerMode.None);

        Assert.Empty(viewModel.CadPadsForCanvas);
        Assert.False(viewModel.ShowCad);
        Assert.Equal("Layer: Regular only", viewModel.LayerSummaryText);

        viewModel.SelectedLayerOption = viewModel.LayerOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerCadLayerMode.AllVisible);

        Assert.Equal(_allPadIds, viewModel.CadPadsForCanvas.Select(static pad => pad.Id));
        Assert.True(viewModel.ShowCad);
        Assert.Equal("Layer: All active CAD", viewModel.LayerSummaryText);
        Assert.Same(snapshot, viewModel.Snapshot);
    }

    [Fact]
    public void CalibrationAndBistSummaries_FollowEditedCoordinatesAndDisabledRectangle()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        Assert.Equal("Safe = raw until copper dia is set.", viewModel.SafeCoordinatePolicyText);
        viewModel.MachineOriginX = 10.5m;
        viewModel.MachineOriginY = -2.25m;
        viewModel.MachineWidth = 80m;
        viewModel.MachineHeight = 40m;
        viewModel.PixelWidth = 800m;
        viewModel.PixelHeight = 400m;
        viewModel.CopperPillarDiameter = 2.5m;

        Assert.Equal("Machine: origin (10.5, -2.25) · size 80 x 40 mm", viewModel.MachineSummaryText);
        Assert.Equal("Pixels: 800 x 400", viewModel.PixelSummaryText);
        Assert.Equal("Copper pillar: 2.5 mm dia", viewModel.CopperSummaryText);
        Assert.Equal("Safe clamps by copper radius.", viewModel.SafeCoordinatePolicyText);
        Assert.Equal(
            "BIST rect px (200, 100) -> (600, 300) · machine (30.5, 7.75) -> (70.5, 27.75)",
            viewModel.BistRectangleSummaryText);
        var corner = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tl");
        Assert.Equal(11.75d, corner.SafeMachineX, 6);
        Assert.Equal(-1d, corner.SafeMachineY, 6);

        viewModel.ShowBistRectangle = false;

        Assert.False(viewModel.HasBistRectangle);
        Assert.Empty(viewModel.Snapshot.Rectangles);
        Assert.Equal("BIST center blank rect disabled.", viewModel.BistRectangleSummaryText);
        Assert.DoesNotContain(viewModel.ArtifactRows, static row => row.Key == "bist-center");
    }

    [Fact]
    public void CustomArraySummary_FollowsRoundedCountsAndHidingClearsSelectedArrayRow()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        Assert.Equal("4-point array disabled.", viewModel.CustomArraySummaryText);
        viewModel.CustomArrayColumnCount = 2.5m;
        viewModel.CustomArrayRowCount = 1.5m;
        viewModel.ShowCustomArray = true;

        Assert.Equal("4-point array: 3 x 2 dot(s), corners 4, plotted dots 6.", viewModel.CustomArraySummaryText);
        Assert.Equal(4, viewModel.CustomArrayCornerRows.Count);
        Assert.Equal(6, viewModel.CustomArrayPointRows.Count);
        viewModel.SelectedArtifactRow = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "array-tl");

        viewModel.ShowCustomArray = false;

        Assert.Equal("4-point array disabled.", viewModel.CustomArraySummaryText);
        Assert.Empty(viewModel.CustomArrayCornerRows);
        Assert.Empty(viewModel.CustomArrayPointRows);
        Assert.Null(viewModel.SelectedArtifactRow);
        Assert.DoesNotContain(viewModel.ArtifactRows, static row => row.Key.StartsWith("array-", StringComparison.Ordinal));
    }

    [Fact]
    public void BlankCustomLabels_UseFallbackNamesAndSummariesTrackPathOnlyThenPointAndClear()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        Assert.Equal("Custom artifacts disabled.", viewModel.CustomArtifactSummaryText);
        Assert.False(viewModel.ClearCustomArtifactsCommand.CanExecute(null));
        viewModel.CustomPathLabelText = " \t ";
        viewModel.CustomPathStartMachineX = 10m;
        viewModel.CustomPathStartMachineY = 5m;
        viewModel.CustomPathEndMachineX = 30m;
        viewModel.CustomPathEndMachineY = 15m;
        viewModel.CustomPathStepCount = 1m;

        viewModel.AddCustomPathCommand.Execute(null);

        Assert.True(viewModel.HasCustomArtifacts);
        Assert.True(viewModel.ClearCustomArtifactsCommand.CanExecute(null));
        Assert.Equal("Custom artifacts: 0 point(s), 1 path(s).", viewModel.CustomArtifactSummaryText);
        var path = Assert.Single(viewModel.CustomRecipeRows);
        Assert.Equal("Path", path.Label);
        Assert.Equal("Machine (10, 5) -> (30, 15) · 2 step(s)", path.DetailText);
        Assert.Equal("Custom path 'Path' added.", viewModel.StatusText);
        Assert.Equal(2, viewModel.Snapshot.Points.Count(static point => point.Kind == CoordinatePlannerPointKind.CustomPathStep));
        viewModel.CustomPointLabelText = " \r\n ";
        viewModel.CustomPointMachineX = 12.5m;
        viewModel.CustomPointMachineY = 7.25m;

        viewModel.AddCustomPointCommand.Execute(null);

        Assert.Equal("Custom artifacts: 1 point(s), 1 path(s).", viewModel.CustomArtifactSummaryText);
        var point = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "custom-point-1");
        Assert.Equal("Point", point.Label);
        Assert.Equal(12.5d, point.MachineX, 6);
        Assert.Equal(7.25d, point.MachineY, 6);
        Assert.Equal("Custom point 'Point' added.", viewModel.StatusText);

        viewModel.ClearCustomArtifactsCommand.Execute(null);

        Assert.False(viewModel.HasCustomArtifacts);
        Assert.False(viewModel.ClearCustomArtifactsCommand.CanExecute(null));
        Assert.Empty(viewModel.CustomRecipeRows);
        Assert.Equal("Custom artifacts disabled.", viewModel.CustomArtifactSummaryText);
        Assert.DoesNotContain(viewModel.ArtifactRows, static row => row.Key.StartsWith("custom-", StringComparison.Ordinal));
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel()
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new CoordinatePlannerWorkspaceUseCase(),
            new CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                new[]
                {
                    TestGeometryFactory.CreateCadPad(1, "AA.drawing", 100d, 200d, 300d, 400d),
                    TestGeometryFactory.CreateCadPad(2, "SIG", 10d, 20d, 30d, 40d),
                    TestGeometryFactory.CreateCadPad(3, "sig", 40d, 20d, 50d, 40d),
                },
                _layerNames,
                SourceRevision: 0,
                DefaultMachineWidth: 100d,
                DefaultMachineHeight: 50d,
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500));
    }
}
