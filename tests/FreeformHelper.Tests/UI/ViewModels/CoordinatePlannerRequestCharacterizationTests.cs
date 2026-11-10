// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerRequestCharacterizationTests
{
    [Fact]
    public void EditedCountsRoundAwayFromZeroAndClampNegativeValues()
    {
        var viewModel = CreateViewModel();

        viewModel.PixelWidth = 2.5m;
        viewModel.PixelHeight = -0.5m;
        viewModel.HorizontalGuideCount = 2.5m;
        viewModel.VerticalGuideCount = -0.5m;
        viewModel.CustomArrayColumnCount = 2.5m;
        viewModel.CustomArrayRowCount = 1.5m;
        viewModel.ShowCustomArray = true;

        var preferences = viewModel.BuildWorkspacePreferences();
        Assert.Equal(3, preferences.PixelWidth);
        Assert.Equal(1, preferences.PixelHeight);
        Assert.Equal("Pixels: 3 x 1", viewModel.PixelSummaryText);
        Assert.Equal("Guides: H 3 count, inset 0 / V 0 count, inset 0", viewModel.GuideSummaryText);
        Assert.Equal([0d, 25d, 50d], viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY));
        Assert.Empty(viewModel.VerticalGuideRows);
        Assert.Equal(6, viewModel.CustomArrayPointRows.Count);
        var corner = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-br");
        Assert.Equal(3d, corner.PixelX);
        Assert.Equal(1d, corner.PixelY);

        viewModel.CustomArrayColumnCount = -0.5m;

        Assert.Empty(viewModel.CustomArrayPointRows);
    }

    [Fact]
    public void EditedGeometryKeepsNegativeOriginsAndClampsSpansAndCopperDiameter()
    {
        var viewModel = CreateViewModel();

        viewModel.MachineOriginX = -2.5m;
        viewModel.MachineOriginY = -1.5m;
        viewModel.MachineWidth = -3m;
        viewModel.MachineHeight = 0m;
        viewModel.CopperPillarDiameter = -2.5m;

        var corner = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-br");
        Assert.Equal(-2.499d, corner.MachineX, 6);
        Assert.Equal(-1.499d, corner.MachineY, 6);
        Assert.Equal(corner.MachineX, corner.SafeMachineX);
        Assert.Equal(corner.MachineY, corner.SafeMachineY);
        Assert.Equal(0d, viewModel.Snapshot.CopperPillarRadiusMachine);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PixelCastOverflowsBeforeClampingAndLeavesThePreviousRows(bool positive)
    {
        var viewModel = CreateViewModel();
        var rows = viewModel.ArtifactRows;
        var value = positive ? decimal.MaxValue : decimal.MinValue;

        Assert.Throws<OverflowException>(() => viewModel.PixelWidth = value);

        Assert.Same(rows, viewModel.ArtifactRows);
        Assert.Throws<OverflowException>(() => viewModel.BuildWorkspacePreferences());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GuideCountCastOverflowsBeforeClampingAndLeavesThePreviousRows(bool positive)
    {
        var viewModel = CreateViewModel();
        var rows = viewModel.ArtifactRows;
        var value = positive ? decimal.MaxValue : decimal.MinValue;

        Assert.Throws<OverflowException>(() => viewModel.HorizontalGuideCount = value);

        Assert.Same(rows, viewModel.ArtifactRows);
    }

    [Fact]
    public void ExplicitGuideTextPreservesPermissiveParsingAndSummaryCounts()
    {
        var viewModel = CreateViewModel();
        var explicitMode = viewModel.GuideGenerationOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.ExplicitPositions);

        viewModel.SelectedHorizontalGuideGenerationOption = explicitMode;
        viewModel.SelectedVerticalGuideGenerationOption = explicitMode;
        viewModel.HorizontalGuidePositionListText = "25;bad\t-5\r\n12.5,25 100 1e1";
        viewModel.VerticalGuidePositionListText = "80,20;invalid\n20 120";

        Assert.Equal([0d, 10d, 12.5d, 25d], viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY));
        Assert.Equal([20d, 80d], viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX));
        Assert.Equal("Guides: H 6 explicit, inset 0 / V 4 explicit, inset 0", viewModel.GuideSummaryText);

        viewModel.HorizontalGuidePositionListText = "bad ; + . 1x";

        Assert.Empty(viewModel.HorizontalGuideRows);
        Assert.Equal("Guides: H 0 explicit, inset 0 / V 4 explicit, inset 0", viewModel.GuideSummaryText);
    }

    [Fact]
    public void PitchGuidesKeepTheirModeSummaryAndInsetCoordinates()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedHorizontalGuideGenerationOption = viewModel.GuideGenerationOptions.Single(static option =>
            option.Mode == CoordinatePlannerWorkspaceViewModel.CoordinatePlannerGuideGenerationMode.Pitch);
        viewModel.HorizontalGuidePitch = 20m;
        viewModel.HorizontalGuideInset = 5m;
        viewModel.VerticalGuideInset = -2m;

        Assert.Equal([5d, 25d, 45d], viewModel.Snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY));
        Assert.Equal("Guides: H pitch 20 mm, inset 5 / V 5 count, inset 0", viewModel.GuideSummaryText);

        viewModel.HorizontalGuidePitch = -0.5m;

        Assert.Empty(viewModel.HorizontalGuideRows);
        Assert.Equal("Guides: H pitch 0 mm, inset 5 / V 5 count, inset 0", viewModel.GuideSummaryText);
    }

    [Fact]
    public void DefaultConstructionProducesOrderedRowsAndCoordinates()
    {
        var viewModel = CreateViewModel();

        AssertDefaultRows(viewModel);
        Assert.False(viewModel.ShowCustomArray);
        Assert.True(viewModel.ShowBistRectangle);
        Assert.Equal("Guides: H 5 count, inset 0 / V 5 count, inset 0", viewModel.GuideSummaryText);
    }

    [Fact]
    public void FirstRefreshKeepsDefaultRowsKeysOrderAndCoordinates()
    {
        var viewModel = CreateViewModel();
        var initialRows = viewModel.ArtifactRows.ToArray();

        viewModel.HorizontalGuidePitch = 11m;

        AssertDefaultRows(viewModel);
        Assert.Equal(initialRows, viewModel.ArtifactRows);
    }

    [Theory]
    [InlineData(-1d, 0d, -2, 0)]
    [InlineData(0.0004d, 0.0002d, 0, -1)]
    public void DefaultConstructionClampsRequestValuesWithoutChangingEditorValues(
        double width, double height, int pixelWidth, int pixelHeight)
    {
        var viewModel = CreateViewModel(width, height, pixelWidth, pixelHeight);

        Assert.Equal((decimal)width, viewModel.MachineWidth);
        Assert.Equal((decimal)height, viewModel.MachineHeight);
        Assert.Equal((decimal)pixelWidth, viewModel.PixelWidth);
        Assert.Equal((decimal)pixelHeight, viewModel.PixelHeight);
        var corner = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-br");
        Assert.Equal(0.001d, corner.MachineX);
        Assert.Equal(0.001d, corner.MachineY);
        Assert.Equal(1d, corner.PixelX);
        Assert.Equal(1d, corner.PixelY);
        Assert.Equal(19, viewModel.ArtifactRows.Count);
    }

    [Fact]
    public void CustomPointsAndPathsSurviveRequestRebuildWithTheirCoordinatesAndOrder()
    {
        var viewModel = CreateViewModel();
        viewModel.CustomEditor.CustomPointLabelText = "Probe";
        viewModel.CustomEditor.CustomPointMachineX = 25m;
        viewModel.CustomEditor.CustomPointMachineY = 10m;
        viewModel.AddCustomPointCommand.Execute(null);
        viewModel.CustomEditor.CustomPathLabelText = "Sweep";
        viewModel.CustomEditor.CustomPathStartMachineX = 10m;
        viewModel.CustomEditor.CustomPathStartMachineY = 5m;
        viewModel.CustomEditor.CustomPathEndMachineX = 90m;
        viewModel.CustomEditor.CustomPathEndMachineY = 45m;
        viewModel.CustomEditor.CustomPathStepCount = 2.5m;
        viewModel.AddCustomPathCommand.Execute(null);

        viewModel.PixelWidth = 200m;

        var rows = viewModel.ArtifactRows.Where(static row => row.Key.StartsWith("custom-", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["custom-point-1", "custom-path-1", "custom-path-1-step-1",
            "custom-path-1-step-2", "custom-path-1-step-3"], rows.Select(static row => row.Key));
        Assert.Equal([25d, 10d, 10d, 50d, 90d], rows.Select(static row => row.MachineX));
        Assert.Equal([10d, 5d, 5d, 25d, 45d], rows.Select(static row => row.MachineY));
        Assert.Equal([50d, 20d, 20d, 100d, 180d], rows.Select(static row => row.PixelX));
        Assert.Equal(90d, rows[1].MachineEndX);
        Assert.Equal(45d, rows[1].MachineEndY);
        Assert.Equal("Probe", rows[0].Label);
        Assert.Equal("Sweep", rows[1].Label);
    }

    private static void AssertDefaultRows(CoordinatePlannerWorkspaceViewModel viewModel)
    {
        (string Key, double X, double Y, double? EndX, double? EndY)[] expected =
        [
            ("aa-tl", 0d, 0d, null, null), ("aa-tr", 100d, 0d, null, null),
            ("aa-br", 100d, 50d, null, null), ("aa-bl", 0d, 50d, null, null),
            ("bist-center", 25d, 12.5d, 75d, 37.5d),
            ("bist-tl", 25d, 12.5d, null, null), ("bist-tr", 75d, 12.5d, null, null),
            ("bist-br", 75d, 37.5d, null, null), ("bist-bl", 25d, 37.5d, null, null),
            ("h-1", 0d, 0d, 100d, 0d), ("h-2", 0d, 12.5d, 100d, 12.5d),
            ("h-3", 0d, 25d, 100d, 25d), ("h-4", 0d, 37.5d, 100d, 37.5d),
            ("h-5", 0d, 50d, 100d, 50d), ("v-1", 0d, 0d, 0d, 50d),
            ("v-2", 25d, 0d, 25d, 50d), ("v-3", 50d, 0d, 50d, 50d),
            ("v-4", 75d, 0d, 75d, 50d), ("v-5", 100d, 0d, 100d, 50d),
        ];
        Assert.Equal(expected, viewModel.ArtifactRows.Select(static row =>
            (row.Key, row.MachineX, row.MachineY, row.MachineEndX, row.MachineEndY)));
        Assert.Equal(Enumerable.Range(0, expected.Length), viewModel.ArtifactRows.Select(static row => row.SortOrder));
        Assert.Equal(expected.Select(static row => (row.X * 10d, row.Y * 10d, row.EndX * 10d, row.EndY * 10d)),
            viewModel.ArtifactRows.Select(static row => (row.PixelX, row.PixelY, row.PixelEndX, row.PixelEndY)));
        Assert.Equal(expected.Select(static row => (row.X / 10d, 5d - (row.Y / 10d))),
            viewModel.ArtifactRows.Select(static row => (row.WorldX, row.WorldY)));
        Assert.Equal(expected.Select(static row => (row.X, row.Y, row.EndX, row.EndY)),
            viewModel.ArtifactRows.Select(static row =>
                (row.SafeMachineX, row.SafeMachineY, row.SafeMachineEndX, row.SafeMachineEndY)));
    }

    private static CoordinatePlannerWorkspaceViewModel CreateViewModel(
        double width = 100d, double height = 50d, int pixelWidth = 1000, int pixelHeight = 500)
    {
        return new CoordinatePlannerWorkspaceViewModel(
            new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceUseCase(),
            new FreeformHelper.UI.Services.CoordinatePlannerWorkspaceSession(
                TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
                Array.Empty<CadPad>(),
                Array.Empty<string>(),
                SourceRevision: 0,
                DefaultMachineWidth: width,
                DefaultMachineHeight: height,
                DefaultPixelWidth: pixelWidth,
                DefaultPixelHeight: pixelHeight));
    }
}
