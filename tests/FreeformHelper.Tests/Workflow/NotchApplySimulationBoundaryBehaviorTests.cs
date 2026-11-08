// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchApplySimulationBoundaryBehaviorTests
{
    [Fact]
    public void Simulate_OutOfRangeFrameIndices_ReturnUnsupportedWithoutApplyingRows()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var request = CreateRequest(grid, CreateHalvingTable(), 100d);

        foreach (var index in new[] { -1, 1 })
        {
            var result = NotchApplySimulationService.Simulate(request with { SelectedFrameIndex = index });

            Assert.False(result.IsSupported);
            Assert.Equal(0, result.ConsumedFrameCount);
            Assert.Empty(result.Cells);
            Assert.Empty(result.Actions);
            Assert.Equal(
                [
                    $"Selected frame index {index} is out of range.",
                    "No compatible projected diff frames are available for simulation.",
                ],
                result.Diagnostics);
        }
    }

    [Fact]
    public void Simulate_SelectedIncompatibleFrame_ReturnsItsDiagnosticWithoutUsingAnotherFrame()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10]);
        var request = CreateRequest(grid, CreateHalvingTable(), 100d);
        var incompatible = DiffFrameGridProjectionResult.Incompatible("Frame dimensions 1x2 do not match grid 1x1.");

        var result = NotchApplySimulationService.Simulate(request with
        {
            FrameProjections = [request.FrameProjections[0], incompatible],
            SelectedFrameIndex = 1,
        });

        Assert.False(result.IsSupported);
        Assert.Equal(0, result.ConsumedFrameCount);
        Assert.Empty(result.Cells);
        Assert.Empty(result.Actions);
        Assert.Equal(
            [incompatible.Diagnostic!, "No compatible projected diff frames are available for simulation."],
            result.Diagnostics);
    }

    [Fact]
    public void Simulate_MeanWithIncompatibleFrameAndInactivePad_UsesOnlyCompatibleActiveValues()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var request = CreateRequest(grid, CreateHalvingTable(), 100d, 900d);

        var result = NotchApplySimulationService.Simulate(request with
        {
            ActiveRegularPadIds = new HashSet<int> { 0 },
            AggregationMode = NotchApplySimulationAggregationMode.Mean,
            FrameProjections =
            [
                request.FrameProjections[0],
                DiffFrameGridProjectionResult.Incompatible("Frame dimensions 1x3 do not match grid 1x2."),
                CreateProjection(grid, 300d, 700d),
            ],
        });

        Assert.True(result.IsSupported);
        Assert.Equal(2, result.ConsumedFrameCount);
        Assert.Equal("Ignored 1 incompatible projected frame(s) during mean aggregation.", Assert.Single(result.Diagnostics));
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 200d, 100d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 0d, 0d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
        var action = Assert.Single(result.Actions);
        Assert.Equal((200d, 100d), (action.SourceBeforeValue, action.SourceAfterValue));
        var hotspot = Assert.Single(result.Heatmap.Hotspots);
        Assert.Equal((0, -100d), (hotspot.RegularPadId, hotspot.DeltaValue));
    }

    [Fact]
    public void Simulate_V22MissingAnchor_SkipsItsTransfersAndStillAppliesTheAvailableSource()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 99, 9, 909, new NotchV22Node(99, 100, 11, 80, ushort.MaxValue, 0, 0), "missing source"),
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 100, 11, 25, ushort.MaxValue, 0, 0), "available source"),
        ]);

        var result = NotchApplySimulationService.Simulate(CreateRequest(grid, table, 100d, 20d));

        Assert.True(result.IsSupported);
        Assert.Equal("Missing anchor diff IC1/diff99 in projected grid.", Assert.Single(result.Diagnostics));
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 100d, 75d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 20d, 45d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
        var action = Assert.Single(result.Actions);
        Assert.Equal((10, 0, 101), (action.AnchorDiffIndex, action.RegularPadId, action.CadPadId));
        Assert.Equal([2], action.SourceRowNumbers);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((11, 25, 25d), (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
    }

    [Fact]
    public void Simulate_V22ClampedMainWithCancellingContinuations_KeepsFirmwareRoundingResidual()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 25, 11, 50, ushort.MaxValue, 0, 0), "main"),
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 100, 11, 50, ushort.MaxValue, 0, 1), "add continuation"),
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 100, 11, -100, ushort.MaxValue, 0, 1), "subtract continuation"),
        ]);

        var result = NotchApplySimulationService.Simulate(CreateRequest(grid, table, 3d, 10d));

        Assert.True(result.IsSupported);
        Assert.Equal(
            "Source retained percent below zero for IC1/diff10; clamped to zero.",
            Assert.Single(result.Diagnostics));
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 3d, 0d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 10d, 9d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
        var action = Assert.Single(result.Actions);
        Assert.Equal((25, 0, 3d, 0d),
            (action.CombinePercent, action.SourceRetainedPercent, action.SourceBeforeValue, action.SourceAfterValue));
        Assert.Equal([1, 2, 3], action.SourceRowNumbers);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((11, 0, -1d), (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
    }

    private static NotchTable CreateHalvingTable()
    {
        return new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 101, new NotchV22Node(10, 50, ushort.MaxValue, 0, ushort.MaxValue, 0, 0), "half"),
        ]);
    }

    private static NotchApplySimulationRequest CreateRequest(RegularGrid grid, NotchTable table, params double[] values)
    {
        return new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [CreateProjection(grid, values)],
            table,
            NotchAlgorithmVersion.V22,
            ushort.MaxValue);
    }

    private static DiffFrameGridProjectionResult CreateProjection(RegularGrid grid, params double[] values)
    {
        Assert.Equal(grid.Pads.Count, values.Length);
        var cells = grid.Pads.Select((pad, index) => new DiffFrameGridCellValue(
            pad.Row, pad.Col, pad.Row, pad.Col, pad.RegularPadId, values[index])).ToArray();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }
}
