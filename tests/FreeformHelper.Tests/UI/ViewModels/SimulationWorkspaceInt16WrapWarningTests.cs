// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceInt16WrapWarningTests
{
    [Fact]
    public void V21Q7SumBeyondInt16_WrapsAndWarnsOnlyForTheDestinationPad()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 20]);
        var table = new NotchTable(
        [
            new NotchTableRow(
                NotchAlgorithmVersion.V21, 0, 10, 0, 100,
                [10, 100, 200, 20, 1, 128, 65535, 0, 0], "first unity Q7 term"),
            new NotchTableRow(
                NotchAlgorithmVersion.V21, 0, 11, 1, 101,
                [11, 100, 200, 20, 1, 128, 65535, 0, 0], "second unity Q7 term"),
        ]);
        var session = new SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        var vm = new SimulationWorkspaceViewModel(
            new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase()), session)
        {
            GlobalValue = 32767d,
        };

        vm.SelectRegularPad(2);
        vm.SelectedManualValue = 0d;
        vm.ApplySelectedOverrideCommand.Execute(null);

        var destination = Assert.Single(vm.SimulationOverlayItems, static cell => cell.RegularPadId == 2);
        Assert.Equal(-2d, destination.AfterValue);
        Assert.True(vm.HasDiffViolationPads);
        Assert.Equal(1, vm.DiffViolationPadCount);
        Assert.Equal("Potential mismatch pads: 1.", vm.DiffViolationSummaryText);
        Assert.Equal("REG 2", vm.DiffViolationPadListText);
    }
}
