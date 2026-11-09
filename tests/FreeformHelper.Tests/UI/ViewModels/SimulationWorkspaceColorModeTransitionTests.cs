// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceColorModeTransitionTests
{
    [Fact]
    public void ColorModeSwitch_WithNegativeBeforeValues_UpdatesLegendWithoutChangingSimulatedValues()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), 65535, SourceRevision: 1,
            ActiveRegularPadIds: new HashSet<int> { 0 });
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = -15d,
            ColorThresholdValue = 7.5d,
        };
        vm.SelectRegularPad(0);
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Threshold);
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.Equal(NotchApplySimulationCanvasViewMode.Before, vm.SelectedCanvasViewMode);
        Assert.True(vm.ShowLegendCenterLabel);
        Assert.Equal("Color: TH scale (7.5)", vm.ColorModeSummaryText);
        Assert.Equal("-7.5", vm.ColorLegendStartText);
        Assert.Equal("+7.5", vm.ColorLegendEndText);

        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Auto);

        Assert.Equal("Color: AUTO scale", vm.ColorModeSummaryText);
        Assert.Equal("AUTO color scale", vm.ColorLegendTitleText);
        Assert.Equal("-15", vm.ColorLegendStartText);
        Assert.Equal("0", vm.ColorLegendCenterText);
        Assert.Equal("+15", vm.ColorLegendEndText);
        Assert.True(vm.ShowLegendCenterLabel);
        Assert.False(vm.ShowSequentialLegend);
        Assert.Equal("-15", vm.SelectedBeforeText);
        Assert.Equal("-15", vm.SelectedAfterText);
        Assert.Equal(-15d, Assert.Single(vm.SimulationOverlayItems).DisplayValue);
        Assert.Contains(nameof(vm.ColorModeSummaryText), changedProperties);
        Assert.Contains(nameof(vm.ColorLegendStartText), changedProperties);
        Assert.Contains(nameof(vm.ColorLegendEndText), changedProperties);
    }
}
