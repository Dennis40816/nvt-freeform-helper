// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerArrayHandleBehaviorTests
{
    [Fact]
    public void BeginCustomArrayCornerEdit_HiddenArray_ShowsArrayAndSelectsExistingCorner()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        Assert.False(viewModel.ShowCustomArray);

        Assert.True(viewModel.BeginCustomArrayCornerEdit("array-br"));

        Assert.True(viewModel.ShowCustomArray);
        AssertSelectedCorner(viewModel, "array-br", 100m, 50m);
        Assert.Equal("BR · machine (100, 50) mm", viewModel.SelectedCustomArrayCornerSummaryText);
        Assert.Equal("4-point array BR selected.", viewModel.StatusText);
        var selectedRow = viewModel.SelectedArtifactRow;

        Assert.True(viewModel.BeginCustomArrayCornerEdit("array-br"));

        Assert.Same(selectedRow, viewModel.SelectedArtifactRow);
        AssertSelectedCorner(viewModel, "array-br", 100m, 50m);
        Assert.Equal("4-point array BR selected.", viewModel.StatusText);
    }

    [Fact]
    public void ArrayCornerEditing_NonCornerArtifactKey_RejectsAllEditPhasesWithoutChangingState()
    {
        var viewModel = CreateViewModel();
        var selectedRow = Assert.Single(viewModel.ArtifactRows, static row => row.Key == "aa-tl");
        viewModel.SelectedArtifactRow = selectedRow;
        var snapshot = viewModel.Snapshot;
        var selectedCorner = viewModel.SelectedCustomArrayCornerOption;
        var status = viewModel.StatusText;

        Assert.False(viewModel.BeginCustomArrayCornerEdit("aa-tl"));
        Assert.False(viewModel.MoveCustomArrayCornerToWorldPoint("aa-tl", new Point2(8d, 1d)));
        Assert.False(viewModel.CompleteCustomArrayCornerEdit("aa-tl"));

        Assert.False(viewModel.ShowCustomArray);
        Assert.Same(snapshot, viewModel.Snapshot);
        Assert.Same(selectedRow, viewModel.SelectedArtifactRow);
        Assert.Equal(selectedCorner, viewModel.SelectedCustomArrayCornerOption);
        Assert.Equal(0m, viewModel.CustomArrayTopLeftMachineX);
        Assert.Equal(0m, viewModel.CustomArrayTopLeftMachineY);
        Assert.Equal(status, viewModel.StatusText);
    }

    [Fact]
    public void MoveCustomArrayCornerToWorldPoint_ClampsToBoundsAndRoundsWithMachineOrigin()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = CreateViewModel();
        viewModel.MachineOriginX = 10m;
        viewModel.MachineOriginY = -5m;

        Assert.True(viewModel.MoveCustomArrayCornerToWorldPoint("array-tl", new Point2(-4d, 8d)));

        Assert.True(viewModel.ShowCustomArray);
        AssertSelectedCorner(viewModel, "array-tl", 10m, -5m);

        Assert.True(viewModel.MoveCustomArrayCornerToWorldPoint("array-br", new Point2(12d, -2d)));

        AssertSelectedCorner(viewModel, "array-br", 110m, 45m);
        Assert.Equal(110m, viewModel.CustomArrayBottomRightMachineX);
        Assert.Equal(45m, viewModel.CustomArrayBottomRightMachineY);

        Assert.True(viewModel.MoveCustomArrayCornerToWorldPoint("array-tl", new Point2(1.23456d, 3.76544d)));
        Assert.True(viewModel.CompleteCustomArrayCornerEdit("array-tl"));

        AssertSelectedCorner(viewModel, "array-tl", 22.346m, 7.346m);
        Assert.Equal(22.346m, viewModel.CustomArrayTopLeftMachineX);
        Assert.Equal(7.346m, viewModel.CustomArrayTopLeftMachineY);
        Assert.Equal(100m, viewModel.CustomArrayTopRightMachineX);
        Assert.Equal(0m, viewModel.CustomArrayTopRightMachineY);
        Assert.Equal("4-point array TL set to (22.346, 7.346) mm.", viewModel.StatusText);
    }

    private static void AssertSelectedCorner(
        CoordinatePlannerWorkspaceViewModel viewModel,
        string key,
        decimal expectedX,
        decimal expectedY)
    {
        Assert.Equal(key, viewModel.SelectedCustomArrayCornerOption.Key);
        Assert.Equal(expectedX, viewModel.SelectedCustomArrayCornerMachineX);
        Assert.Equal(expectedY, viewModel.SelectedCustomArrayCornerMachineY);
        var row = Assert.IsType<CoordinateArtifactRow>(viewModel.SelectedArtifactRow);
        Assert.Equal(key, row.Key);
        Assert.Equal((double)expectedX, row.MachineX, 6);
        Assert.Equal((double)expectedY, row.MachineY, 6);
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
                DefaultPixelHeight: 500));
    }
}
