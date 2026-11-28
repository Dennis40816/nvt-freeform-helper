// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerOperationBehaviorTests
{
    [Fact]
    public void SortArtifactRowsCommand_SortsByDisplayedColumnsAndTogglesDirection()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.CopperPillarDiameter = 10m;
        AddPoint(viewModel, "Zeta", 120m, -10m);
        AddPoint(viewModel, "Alpha", 100m, -20m);
        AddPoint(viewModel, "Beta", 10m, 30m);
        AddPoint(viewModel, "Gamma", 10m, 20m);
        viewModel.CustomEditor.CustomPathLabelText = "Sweep";
        viewModel.CustomEditor.CustomPathStartMachineX = 20m;
        viewModel.CustomEditor.CustomPathStartMachineY = 10m;
        viewModel.CustomEditor.CustomPathEndMachineX = 80m;
        viewModel.CustomEditor.CustomPathEndMachineY = 40m;
        viewModel.CustomEditor.CustomPathStepCount = 2m;
        viewModel.AddCustomPathCommand.Execute(null);
        var sourceOrder = viewModel.ArtifactRows.Select(static row => row.Key).ToArray();
        string[] numericOrder =
        [
            "aa-tl", "aa-bl", "custom-point-4", "custom-point-3", "custom-path-1", "custom-path-1-step-1",
            "custom-path-1-step-2", "custom-point-2", "aa-tr", "aa-br", "custom-point-1",
        ];

        AssertSort(viewModel, "Kind",
        [
            "custom-path-1", "aa-bl", "aa-br", "aa-tl", "aa-tr", "custom-point-2", "custom-point-3",
            "custom-point-4", "custom-point-1", "custom-path-1-step-1", "custom-path-1-step-2",
        ]);
        AssertSort(viewModel, "Pixel", numericOrder);
        AssertSort(viewModel, "Machine", numericOrder);
        AssertSort(viewModel, "Safe",
        [
            "aa-tl", "aa-bl", "custom-point-4", "custom-point-3", "custom-path-1", "custom-path-1-step-1",
            "custom-path-1-step-2", "aa-tr", "custom-point-1", "custom-point-2", "aa-br",
        ]);

        viewModel.SortArtifactRowsCommand.Execute("   ");

        Assert.Equal(sourceOrder.Reverse(), viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal("Sort: Source desc", viewModel.ArtifactSortStatusText);
    }

    [Fact]
    public void CountGuides_WithOneGuideAndInset_ShowCenteredCoordinatesAndCountSummary()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.MachineOriginX = 10m;
        viewModel.MachineOriginY = 20m;
        viewModel.HorizontalGuideCount = 1m;
        viewModel.VerticalGuideCount = 1m;
        viewModel.HorizontalGuideInset = 5m;
        viewModel.VerticalGuideInset = 10m;

        var horizontal = Assert.Single(viewModel.Snapshot.Lines,
            static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide);
        var vertical = Assert.Single(viewModel.Snapshot.Lines,
            static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide);

        Assert.Equal(45d, horizontal.StartMachineY, 6);
        Assert.Equal(45d, horizontal.EndMachineY, 6);
        Assert.Equal(2.5d, horizontal.StartWorld.Y, 6);
        Assert.Equal(60d, vertical.StartMachineX, 6);
        Assert.Equal(60d, vertical.EndMachineX, 6);
        Assert.Equal(5d, vertical.StartWorld.X, 6);
        Assert.Single(viewModel.HorizontalGuideRows);
        Assert.Single(viewModel.VerticalGuideRows);
        Assert.Equal("Guides: H 1 count, inset 5 / V 1 count, inset 10", viewModel.GuideSummaryText);
    }

    private static void AssertSort(CoordinatePlannerWorkspaceViewModel viewModel, string key, string[] expectedKeys)
    {
        viewModel.SortArtifactRowsCommand.Execute($" {key} ");

        Assert.Equal(expectedKeys, viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal($"Sort: {key} asc", viewModel.ArtifactSortStatusText);

        viewModel.SortArtifactRowsCommand.Execute(key);

        Assert.Equal(expectedKeys.Reverse(), viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal($"Sort: {key} desc", viewModel.ArtifactSortStatusText);

        viewModel.PixelWidth += 100m;

        Assert.Equal(expectedKeys.Reverse(), viewModel.ArtifactRows.Select(static row => row.Key));
        Assert.Equal($"Sort: {key} desc", viewModel.ArtifactSortStatusText);

        viewModel.SortArtifactRowsCommand.Execute(null);
        Assert.Equal("Sort: Source asc", viewModel.ArtifactSortStatusText);
    }

    private static void AddPoint(CoordinatePlannerWorkspaceViewModel viewModel, string label, decimal x, decimal y)
    {
        viewModel.CustomEditor.CustomPointLabelText = label;
        viewModel.CustomEditor.CustomPointMachineX = x;
        viewModel.CustomEditor.CustomPointMachineY = y;
        viewModel.AddCustomPointCommand.Execute(null);
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
                DefaultPixelWidth: 1000,
                DefaultPixelHeight: 500))
        {
            ShowBistRectangle = false,
            HorizontalGuideCount = 0m,
            VerticalGuideCount = 0m,
        };
    }
}
