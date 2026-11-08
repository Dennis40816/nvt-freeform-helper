// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class SimulationWorkspaceViewModelTests
{
    [Fact]
    public void DuplicateDiffSummary_WhenTwoKeysAreShared_IdentifiesMergedPadsAndRemainingGroup()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 10, 20, 20]);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), NullDiffValue, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);

        viewModel.ApplyManualPresetCommand.Execute("100");

        Assert.True(viewModel.HasDuplicateDiffResolutions);
        Assert.Equal(2, viewModel.DuplicateDiffResolutionCount);
        Assert.Equal("merge-sum-active-regular-pads", viewModel.DuplicateDiffResolutionStrategyText);
        Assert.Equal(
            "Duplicate FW Diff Idx keys: 2 (strategy: merge-sum-active-regular-pads).",
            viewModel.DuplicateDiffResolutionSummaryText);
        Assert.Equal("IC1/FW Diff Idx 10: merge REG 0, 1 (+1)", viewModel.DuplicateDiffResolutionSampleText);
        viewModel.SelectRegularPad(0);
        Assert.Equal("200", viewModel.SelectedBeforeText);
        viewModel.SelectRegularPad(1);
        Assert.Equal("200", viewModel.SelectedBeforeText);
    }

    [Fact]
    public void SelectedRegularInspector_WhenSelectionAndBaselineChange_UpdatesImpactPercentAndEmptyState()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession())
        {
            GlobalValue = 100d,
        };

        viewModel.SelectRegularPad(1);

        Assert.Equal("REG 1 · IC 2 · FW Diff Idx 11 · CAD Output FW Diff Idx 11", viewModel.SelectedRegularHeaderText);
        Assert.Equal("R0, C1", viewModel.SelectedRegularLocationText);
        Assert.Equal("0%", viewModel.SelectedPercentChangeText);
        Assert.Equal("No notch rows affect this regular under the current simulation version.", viewModel.SelectedImpactSummaryText);

        viewModel.SelectRegularPad(0);

        Assert.Equal("100", viewModel.SelectedBeforeText);
        Assert.Equal("50", viewModel.SelectedAfterText);
        Assert.Equal("-50", viewModel.SelectedDeltaText);
        Assert.Equal("-50%", viewModel.SelectedPercentChangeText);
        Assert.Equal("1 notch impact(s) affect this regular.", viewModel.SelectedImpactSummaryText);
        Assert.True(viewModel.ShowSelectedOverrideEditor);
        viewModel.GlobalValue = 0d;
        Assert.Equal("-", viewModel.SelectedPercentChangeText);

        viewModel.SelectRegularPad(-1);

        Assert.False(viewModel.HasSelectedRegular);
        Assert.False(viewModel.ShowSelectedOverrideEditor);
        Assert.Equal("No regular selected.", viewModel.SelectedRegularHeaderText);
        Assert.Equal("-", viewModel.SelectedRegularLocationText);
        Assert.Equal("-", viewModel.SelectedBeforeText);
        Assert.Equal("-", viewModel.SelectedAfterText);
        Assert.Equal("-", viewModel.SelectedDeltaText);
        Assert.Equal("-", viewModel.SelectedPercentChangeText);
        Assert.Equal("Select a regular pad to inspect notch impact.", viewModel.SelectedImpactSummaryText);
    }

    [Fact]
    public void ThresholdLegend_WhenSwitchingBetweenAbsoluteAndDeltaViews_UsesThresholdAndSignedBounds()
    {
        using var culture = TestCulture.UseEnUs();
        var viewModel = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), BuildSession())
        {
            GlobalValue = 100d,
            ColorThresholdValue = 7.5d,
        };
        viewModel.SelectedCanvasColorOption = viewModel.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Threshold);

        Assert.Equal("TH color scale", viewModel.ColorLegendTitleText);
        Assert.True(viewModel.ShowSequentialLegend);
        Assert.Equal("0", viewModel.ColorLegendStartText);
        Assert.Equal(string.Empty, viewModel.ColorLegendCenterText);
        Assert.Equal("TH 7.5", viewModel.ColorLegendEndText);
        Assert.StartsWith("TH mode:", viewModel.ColorLegendMeaningText, StringComparison.Ordinal);

        viewModel.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);

        Assert.True(viewModel.ShowLegendCenterLabel);
        Assert.False(viewModel.ShowSequentialLegend);
        Assert.Equal("-7.5", viewModel.ColorLegendStartText);
        Assert.Equal("0", viewModel.ColorLegendCenterText);
        Assert.Equal("+7.5", viewModel.ColorLegendEndText);
        Assert.StartsWith("TH signed scale:", viewModel.ColorLegendMeaningText, StringComparison.Ordinal);

        viewModel.ToggleBeforeAfterCanvasView();
        Assert.Equal(NotchApplySimulationCanvasViewMode.Before, viewModel.SelectedCanvasViewMode);
        Assert.True(viewModel.ShowSequentialLegend);
        viewModel.ToggleBeforeAfterCanvasView();
        Assert.Equal(NotchApplySimulationCanvasViewMode.After, viewModel.SelectedCanvasViewMode);
        Assert.Equal("TH 7.5", viewModel.ColorLegendEndText);
    }
}
