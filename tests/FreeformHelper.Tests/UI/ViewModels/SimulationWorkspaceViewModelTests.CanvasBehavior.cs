// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class SimulationWorkspaceViewModelTests
{
    [Fact]
    public void MoveCopperToWorldPoint_OutsideCopperMode_PreservesTheContactAndManualValues()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession())
        {
            GlobalValue = 100d,
        };
        viewModel.SelectRegularPad(0);
        var originalCenter = new Point2(viewModel.CopperCenterX, viewModel.CopperCenterY);

        viewModel.MoveCopperToWorldPoint(new Point2(0.25d, 0.75d));

        Assert.Equal(originalCenter.X, viewModel.CopperCenterX);
        Assert.Equal(originalCenter.Y, viewModel.CopperCenterY);
        Assert.Equal("100", viewModel.SelectedBeforeText);
        Assert.Equal("50", viewModel.SelectedAfterText);

        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(
            option => option.Value == SimulationInputSourceMode.Csv);
        viewModel.MoveCopperToWorldPoint(new Point2(1.5d, 0.25d));

        Assert.Equal(originalCenter.X, viewModel.CopperCenterX);
        Assert.Equal(originalCenter.Y, viewModel.CopperCenterY);
        Assert.True(viewModel.IsCsvSource);
    }

    [Fact]
    public void MoveCopperToWorldPoint_IgnoresSubToleranceJitterAndAcceptsEitherAxisMovement()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession())
        {
            CopperDiameter = 0.5d,
        };
        viewModel.SelectedInputSourceOption = viewModel.SourceOptions.Single(
            option => option.Value == SimulationInputSourceMode.Copper);
        viewModel.MoveCopperToWorldPoint(new Point2(0.5d, 0.5d));
        viewModel.SelectRegularPad(0);
        Assert.Equal("400", viewModel.SelectedBeforeText);

        viewModel.MoveCopperToWorldPoint(new Point2(0.5000005d, 0.5000005d));

        Assert.Equal(0.5d, viewModel.CopperCenterX);
        Assert.Equal(0.5d, viewModel.CopperCenterY);
        Assert.Equal("400", viewModel.SelectedBeforeText);

        viewModel.MoveCopperToWorldPoint(new Point2(1.5d, 0.5d));

        Assert.Equal("0", viewModel.SelectedBeforeText);
        viewModel.SelectRegularPad(1);
        Assert.Equal("400", viewModel.SelectedBeforeText);

        viewModel.MoveCopperToWorldPoint(new Point2(1.5d, 1.5d));

        Assert.Equal(1.5d, viewModel.CopperCenterX);
        Assert.Equal(1.5d, viewModel.CopperCenterY);
        Assert.Equal("0", viewModel.SelectedBeforeText);
    }

    [Fact]
    public void CanvasViewMethods_SwitchDisplayedValuesAndReturnDeltaViewsToBefore()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession())
        {
            GlobalValue = 100d,
        };

        viewModel.ToggleBeforeAfterCanvasView();

        Assert.Equal(NotchApplySimulationCanvasViewMode.After, viewModel.SelectedCanvasViewMode);
        Assert.Equal(50d, viewModel.SimulationOverlayItems.Single(item => item.RegularPadId == 0).DisplayValue);

        viewModel.ToggleBeforeAfterCanvasView();

        Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);
        Assert.Equal(100d, viewModel.SimulationOverlayItems.Single(item => item.RegularPadId == 0).DisplayValue);

        viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);

        Assert.Equal(NotchApplySimulationCanvasViewMode.Delta, viewModel.SelectedCanvasViewMode);
        Assert.Equal(-50d, viewModel.SimulationOverlayItems.Single(item => item.RegularPadId == 0).DisplayValue);

        viewModel.ToggleBeforeAfterCanvasView();

        Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);
        Assert.Equal(100d, viewModel.SimulationOverlayItems.Single(item => item.RegularPadId == 0).DisplayValue);

        viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.ChangedOnly);
        Assert.Equal(NotchApplySimulationCanvasViewMode.ChangedOnly, viewModel.SelectedCanvasViewMode);
        viewModel.ToggleBeforeAfterCanvasView();
        Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);
    }
}
