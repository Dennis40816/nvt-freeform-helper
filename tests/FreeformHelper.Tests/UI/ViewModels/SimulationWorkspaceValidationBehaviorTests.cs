// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceValidationBehaviorTests
{
    [Fact]
    public void SelectedFlow_WithIncomingAndOutgoingLegs_SumsOnlyActionsFromTheSelectedIc()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();

        vm.SelectRegularPad(0);

        Assert.Equal("60", vm.SelectedAfterText);
        Assert.Equal("NetFlow -40 · source -50 · target +10", vm.SelectedNetFlowText);
        Assert.Equal("EMS OK: 420 margin", vm.SelectedEmsStatusText);
        Assert.True(vm.HasSelectedFlowLegs);
        Assert.Equal("2 source/target leg(s) touch this selected FW diff.", vm.SelectedFlowLegSummaryText);
        Assert.Collection(vm.SelectedFlowLegs,
            leg => Assert.Equal(new SimulationSelectedFlowLegViewModel(
                "Source retain", "FW Diff 10 keeps 50%", "Combine 100% · CAD 313", "-50", "Rows: #1"), leg),
            leg => Assert.Equal(new SimulationSelectedFlowLegViewModel(
                "Target receive", "From FW Diff 11 at 10%", "Anchor REG 1 · CAD -", "+10", "Rows: #2"), leg));

        vm.SelectRegularPad(1);

        Assert.Equal("120", vm.SelectedAfterText);
        Assert.Equal("NetFlow +20 · source -10 · target +30", vm.SelectedNetFlowText);
        Assert.Collection(vm.SelectedFlowLegs,
            leg => Assert.Equal(new SimulationSelectedFlowLegViewModel(
                "Target receive", "From FW Diff 10 at 30%", "Anchor REG 0 · CAD 313", "+30", "Rows: #1"), leg),
            leg => Assert.Equal(new SimulationSelectedFlowLegViewModel(
                "Source retain", "FW Diff 11 keeps 90%", "Combine 100% · CAD -", "-10", "Rows: #2"), leg));

        vm.SelectRegularPad(4);

        Assert.Equal("180", vm.SelectedAfterText);
        Assert.Equal("NetFlow +80 · source 0 · target +80", vm.SelectedNetFlowText);
        Assert.Equal(new SimulationSelectedFlowLegViewModel(
            "Target receive", "From FW Diff 10 at 80%", "Anchor REG 3 · CAD 99", "+80", "Rows: #3"),
            Assert.Single(vm.SelectedFlowLegs));
    }

    [Fact]
    public void SelectedFlow_WhenUntouchedOrFilteredOut_ReplacesPreviousLegsWithTheAppropriateEmptyState()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.SelectRegularPad(0);
        Assert.NotEmpty(vm.SelectedFlowLegs);

        vm.SelectRegularPad(5);

        Assert.Equal("100", vm.SelectedAfterText);
        Assert.Equal("EMS OK: 380 margin", vm.SelectedEmsStatusText);
        Assert.Equal("NetFlow 0 · source 0 · target 0", vm.SelectedNetFlowText);
        Assert.Equal("No notch source/target leg touches this selected FW diff.", vm.SelectedFlowLegSummaryText);
        Assert.Empty(vm.SelectedFlowLegs);
        Assert.False(vm.HasSelectedFlowLegs);

        vm.SelectRegularPad(0);
        vm.SelectedAreaOption = vm.AreaOptions.Single(option => option.IcIndex == 1);

        Assert.Equal(-1, vm.SelectedRegularPadId);
        Assert.Equal("-", vm.SelectedEmsStatusText);
        Assert.Equal("-", vm.SelectedNetFlowText);
        Assert.Equal("Select a regular pad to inspect source/target legs.", vm.SelectedFlowLegSummaryText);
        Assert.Empty(vm.SelectedFlowLegs);
        Assert.False(vm.HasSelectedFlowLegs);
    }

    [Fact]
    public void SelectRiskDiff_AlreadyInTheMatchingArea_FocusesTheNewPadAndItsEmsExcess()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.GlobalValue = 1000d;
        var area = vm.AreaOptions.Single(option => option.IcIndex == 1);
        vm.SelectedAreaOption = area;
        vm.SelectRegularPad(3);
        var risk = vm.SimulationHighRiskDiffs.Single(item => item.RegularPadId == 4);

        vm.SelectSimulationRiskDiffCommand.Execute(risk);

        Assert.Equal(area, vm.SelectedAreaOption);
        Assert.Equal(4, vm.SelectedRegularPadId);
        Assert.Equal("1800", vm.SelectedAfterText);
        Assert.Equal("EMS risk: +1320 over cap", vm.SelectedEmsStatusText);
        Assert.Equal("NetFlow +800 · source 0 · target +800", vm.SelectedNetFlowText);
        Assert.Equal("Focused REG 4 · IC 2 · FW Diff 11 from Simulation safety risk list.", vm.StatusText);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12, 10, 11, 12],
            icIndexSelector: static col => col / 3);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 313, new NotchV22Node(10, 100, 11, 30, 12, 20, 0), "two targets"),
            new NotchTableRow(0, 11, 1, null, new NotchV22Node(11, 100, 10, 10, 65535, 0, 0), "return flow"),
            new NotchTableRow(1, 10, 3, 99, new NotchV22Node(10, 100, 11, 80, 65535, 0, 0), "other IC"),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = 100d,
        };
    }
}
