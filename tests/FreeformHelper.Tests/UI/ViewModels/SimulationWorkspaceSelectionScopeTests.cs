// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceSelectionScopeTests
{
    [Theory]
    [InlineData(NotchApplySimulationCanvasViewMode.Delta)]
    [InlineData(NotchApplySimulationCanvasViewMode.ChangedOnly)]
    public void RandomizeVisibleValues_InSignedView_UpdatesTheSelectedActivePadAndPreservesOtherScopes(
        NotchApplySimulationCanvasViewMode viewMode)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12],
            icIndexSelector: static col => col == 2 ? 1 : 0);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), NullDiffValue: 65535, SourceRevision: 1,
            ActiveRegularPadIds: new HashSet<int> { 0, 2 });
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = 1000d,
        };
        vm.SelectRegularPad(2);
        vm.SelectedManualValue = 2000d;
        vm.ApplySelectedOverrideCommand.Execute(null);
        vm.SelectRegularPad(0);
        vm.SelectedAreaOption = vm.AreaOptions.Single(option => option.IcIndex == 0);
        vm.SetCanvasViewMode(viewMode);
        Assert.True(vm.RandomizeVisibleValuesCommand.CanExecute(null));

        vm.RandomizeVisibleValuesCommand.Execute(null);

        Assert.Equal(0, vm.SelectedRegularPadId);
        Assert.True(vm.HasSelectedOverride);
        Assert.InRange(vm.SelectedManualValue, -120d, 120d);
        var selectedCell = Assert.Single(vm.SimulationOverlayItems, static cell => cell.RegularPadId == 0);
        Assert.Equal(vm.SelectedManualValue, selectedCell.BeforeValue);
        Assert.Equal("Randomized 1 regular pad(s) in IC 1.", vm.StatusText);
        vm.SelectRegularPad(1);
        Assert.False(vm.HasSelectedOverride);
        Assert.Equal("0", vm.SelectedBeforeText);
        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => !option.IcIndex.HasValue);
        vm.SelectRegularPad(2);
        Assert.True(vm.HasSelectedOverride);
        Assert.Equal(2000d, vm.SelectedManualValue);
        Assert.Equal("2000", vm.SelectedBeforeText);
    }

    [Fact]
    public void SelectRegularPad_WithIncomingAndOutgoingTransfers_OrdersImpactsByMagnitudeThenRole()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12]);
        var table = new NotchTable(
        [
            CreateTransferRow(10, 0, 101, 11, 20),
            CreateTransferRow(12, 2, null, 11, 30),
            CreateTransferRow(11, 1, 102, 10, 20),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, NullDiffValue: 65535, SourceRevision: 1,
            ActiveRegularPadIds: new HashSet<int> { 0, 1, 2 });
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = 100d,
        };

        vm.SelectRegularPad(1);

        Assert.Equal("100", vm.SelectedBeforeText);
        Assert.Equal("130", vm.SelectedAfterText);
        Assert.Equal("+30", vm.SelectedDeltaText);
        Assert.Equal("3 notch impact(s) affect this regular.", vm.SelectedImpactSummaryText);
        NotchApplySimulationImpactItemViewModel[] expected =
        [
            new("Target receive", "From FW Diff Idx 12 at 30%", "CAD - · anchor REG 2", "+30", "Rows: #2"),
            new("Anchor retain", "FW Diff Idx 11 keeps 80%", "Combine 100% · CAD 102", "-20", "Rows: #3"),
            new("Target receive", "From FW Diff Idx 10 at 20%", "CAD 101 · anchor REG 0", "+20", "Rows: #1"),
        ];
        Assert.Equal(expected, vm.SelectedImpactItems);
    }

    private static NotchTableRow CreateTransferRow(
        int sourceDiff, int regularPadId, int? cadPadId, int targetDiff, int ratioPercent)
    {
        return new NotchTableRow(
            icIndex: 0, diffIndex: sourceDiff, regularPadIndex: regularPadId, cadPadId: cadPadId,
            v22Node: new NotchV22Node(sourceDiff, 100, targetDiff, ratioPercent, 65535, 0, 0),
            comment: "selection impact sample");
    }
}
