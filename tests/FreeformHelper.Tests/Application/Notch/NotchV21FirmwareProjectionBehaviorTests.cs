// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV21FirmwareProjectionBehaviorTests
{
    [Theory]
    [InlineData(NotchComputationMode.CadAllocation, 1)]
    [InlineData(NotchComputationMode.LegacyRegularAnchor, 0)]
    public void Simulate_V21WithMixedVersionRows_AppliesOnlyV21AndKeepsSourceOrdinals(
        NotchComputationMode mode, int expectedActionCount)
    {
        var table = new NotchTable([
            new NotchTableRow(0, 20, 1, 102, new NotchV22Node(20, 50, 65535, 0, 65535, 0, 0), "V22"),
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 10, 0, 101,
                [10, 100, 150, 65535, 0, 0, 65535, 0, 0], "V21 gain"),
        ]);

        var result = Simulate(table, mode, targetBefore: 200);

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(150d, Assert.Single(result.Cells, cell => cell.DiffIndex == 10).AfterValue);
        Assert.Equal(200d, Assert.Single(result.Cells, cell => cell.DiffIndex == 20).AfterValue);
        Assert.Equal(expectedActionCount, result.Actions.Count);
        Assert.All(result.Actions, action =>
        {
            Assert.Equal(101, action.CadPadId);
            Assert.Equal([1], action.SourceRowNumbers);
            Assert.Equal(150, action.CombinePercent);
        });
    }

    [Fact]
    public void ExportAndSimulation_V21TwoLegsToSameDestination_KeepsBothContributionsAndOneComment()
    {
        using var culture = TestCulture.UseEnUs();
        var table = new NotchTable([
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 10, 0, 101,
                [10, 100, 100, 20, 1, 32, 20, 1, 32], "split"),
        ]);
        var text = NotchEdgeCaseHarness.ExportC(table);

        var result = Simulate(table, NotchComputationMode.CadAllocation, targetBefore: 0);

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(50d, Assert.Single(result.Cells, cell => cell.DiffIndex == 10).AfterValue);
        Assert.Equal(50d, Assert.Single(result.Cells, cell => cell.DiffIndex == 20).AfterValue);
        var nodeLine = Assert.Single(text.Split('\n'), line => line.Contains("// LEG split srcD=10", StringComparison.Ordinal));
        Assert.EndsWith("// LEG split srcD=10", nodeLine.TrimEnd('\r'), StringComparison.Ordinal);
        Assert.DoesNotContain(" | ", nodeLine, StringComparison.Ordinal);
    }

    private static NotchApplySimulationResult Simulate(NotchTable table, NotchComputationMode mode, double targetBefore)
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 20]);
        var projection = new DiffFrameGridProjectionResult(true, null, [
            new DiffFrameGridCellValue(0, 0, 0, 0, 0, 100),
            new DiffFrameGridCellValue(0, 1, 0, 1, 1, targetBefore),
        ]);
        return NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid, grid.Pads.Select(pad => pad.RegularPadId).ToHashSet(), [projection], table,
            NotchAlgorithmVersion.V21, 65535, ComputationMode: mode));
    }
}
