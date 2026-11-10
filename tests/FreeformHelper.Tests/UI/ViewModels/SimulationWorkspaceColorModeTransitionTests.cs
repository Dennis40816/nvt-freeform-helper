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

    [Theory]
    [InlineData(NotchApplySimulationCanvasColorMode.Auto, "AUTO color scale", "-15", "+15", "40", "40")]
    [InlineData(NotchApplySimulationCanvasColorMode.Threshold, "TH color scale", "-7.5", "+7.5", "0", "TH 7.5")]
    public void AreaSwitch_WithDifferentIcRanges_UpdatesLegendWithoutChangingSimulatedValues(
        NotchApplySimulationCanvasColorMode colorMode,
        string title,
        string negativeStart,
        string negativeEnd,
        string positiveStart,
        string positiveEnd)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace(new NotchTable([]));
        vm.GlobalValue = -15d;
        vm.ColorThresholdValue = 7.5d;
        vm.SelectRegularPad(1);
        vm.SelectedManualValue = 40d;
        vm.ApplySelectedOverrideCommand.Execute(null);
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(option => option.Value == colorMode);
        var originalCells = vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells.ToArray();
        var originalOverlay = vm.SimulationOverlayItems.ToArray();

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 0);

        AssertLegend(vm, title, negativeStart, "0", negativeEnd);
        var negativeCell = Assert.Single(vm.SimulationOverlayItems);
        Assert.Equal(0, negativeCell.RegularPadId);
        Assert.Equal((-15d, -15d, 0d), (negativeCell.BeforeValue, negativeCell.AfterValue, negativeCell.DeltaValue));
        Assert.Equal(originalCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 1);

        AssertLegend(vm, title, positiveStart, string.Empty, positiveEnd);
        var positiveCell = Assert.Single(vm.SimulationOverlayItems);
        Assert.Equal(1, positiveCell.RegularPadId);
        Assert.Equal((40d, 40d, 0d), (positiveCell.BeforeValue, positiveCell.AfterValue, positiveCell.DeltaValue));
        Assert.Equal(originalCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => !option.IcIndex.HasValue);

        Assert.Equal(originalOverlay, vm.SimulationOverlayItems);
        Assert.Equal(originalCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);
    }

    [Fact]
    public async Task ViewAndSourceRefresh_WithPopulatedCells_UpdatesLegendWithoutChangingSourceResultsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable([
            new NotchTableRow(
                icIndex: 0, diffIndex: 10, regularPadIndex: 0, cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, 65535, 0, 0), comment: "sample"),
        ]);
        var vm = CreateWorkspace(table);
        vm.GlobalValue = 100d;
        vm.ColorThresholdValue = 7.5d;
        var manualCells = vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells.ToArray();
        Assert.Collection(manualCells,
            cell => Assert.Equal((100d, 50d, -50d), (cell.BeforeValue, cell.AfterValue, cell.DeltaValue)),
            cell => Assert.Equal((100d, 100d, 0d), (cell.BeforeValue, cell.AfterValue, cell.DeltaValue)));
        AssertLegend(vm, "AUTO color scale", "100", string.Empty, "100");

        vm.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.After);

        AssertLegend(vm, "AUTO color scale", "50", string.Empty, "100");
        Assert.Equal(manualCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Threshold);
        AssertLegend(vm, "TH color scale", "0", string.Empty, "TH 7.5");
        Assert.Equal(manualCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);

        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>([csvPath]);

        await vm.ImportCsvAsync();

        Assert.True(vm.HasCsvData);
        Assert.True(vm.IsCsvSource);
        AssertLegend(vm, "TH color scale", "0", string.Empty, "TH 7.5");
        var csvCells = vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells.ToArray();
        Assert.Collection(csvCells,
            cell => Assert.Equal((100d, 50d, -50d), (cell.BeforeValue, cell.AfterValue, cell.DeltaValue)),
            cell => Assert.Equal((50d, 50d, 0d), (cell.BeforeValue, cell.AfterValue, cell.DeltaValue)));
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Auto);
        AssertLegend(vm, "AUTO color scale", "50", string.Empty, "50");

        vm.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);

        AssertLegend(vm, "AUTO color scale", "-50", "0", "+50");
        Assert.Equal(csvCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Threshold);
        AssertLegend(vm, "TH color scale", "-7.5", "0", "+7.5");
        Assert.Equal(csvCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);

        vm.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.After);
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(
            static option => option.Value == NotchApplySimulationCanvasColorMode.Auto);
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(
            static option => option.Value == SimulationInputSourceMode.Manual);
        AssertLegend(vm, "AUTO color scale", "50", string.Empty, "100");
        Assert.Equal(manualCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);

        vm.SelectedInputSourceOption = vm.SourceOptions.Single(
            static option => option.Value == SimulationInputSourceMode.Csv);
        AssertLegend(vm, "AUTO color scale", "50", string.Empty, "50");
        Assert.Equal(csvCells, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace(NotchTable table)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11], icIndexSelector: static col => col);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session);
    }

    private static void AssertLegend(
        SimulationWorkspaceViewModel vm, string title, string start, string center, string end)
    {
        Assert.Equal(title, vm.ColorLegendTitleText);
        Assert.Equal(start, vm.ColorLegendStartText);
        Assert.Equal(center, vm.ColorLegendCenterText);
        Assert.Equal(end, vm.ColorLegendEndText);
        Assert.Equal(center.Length > 0, vm.ShowLegendCenterLabel);
        Assert.Equal(center.Length == 0, vm.ShowSequentialLegend);
    }
}
