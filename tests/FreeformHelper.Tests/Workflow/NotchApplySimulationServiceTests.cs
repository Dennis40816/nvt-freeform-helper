using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchApplySimulationServiceTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] DiffIndices3 = { 10, 11, 12 };
    private static readonly int[] DiffIndices4 = { 10, 11, 12, 13 };
    private static readonly int[] DiffIndices2 = { 10, 11 };
    private static readonly int[] DiffIndicesDuplicate = { 10, 10, 11 };
    private static readonly int[] ContinuationSourceRows = [1, 2];
    private static readonly int[] SuppressedDuplicateRegularPadIds = [1];
    private static readonly int[] V21CanonicalMainPayload = [10, 120, 120, 11, 1, 77, 12, 1, 26];
    private static readonly int[] V21CanonicalContinuationPayload = [10, 100, 100, 13, 1, 13, NullDiffValue, 0, 0];
    private static readonly int[] V21LegacySamplePayload = [10, 127, 94, 11, 1, 56, NullDiffValue, 0, 0];

    [Fact]
    public void Simulate_WhenV22RowExists_TransformsAnchorAndTargetUsingCombinePayload()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices3);
        var projection = CreateProjection(grid, 100d, 50d, 25d);
        var frames = new[] { projection };
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "TM81 sample")
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        Assert.False(result.DiffIdentityContract.HasDuplicateResolutions);
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 100d, 50d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 50d, 94d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((12, 25d, 25d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));

        var action = Assert.Single(result.Actions);
        Assert.Equal(94, action.CombinePercent);
        Assert.Equal(50, action.SourceRetainedPercent);
        Assert.Equal(100d, action.SourceBeforeValue);
        Assert.Equal(50d, action.SourceAfterValue);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((11, 44, 44d), (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
        Assert.True(result.Histograms.Before.HasBins);
        Assert.True(result.Heatmap.HasHotspots);
    }

    [Fact]
    public void Simulate_WhenV22ContinuationRowsExist_MergesAllLegsIntoSingleAnchorAction()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices4);
        var projection = CreateProjection(grid, 100d, 0d, 0d, 0d);
        var frames = new[] { projection };
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 120, 11, 60, 12, 20, 0),
                comment: "MAIN"),
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 100, 13, 10, NullDiffValue, 0, 1),
                comment: "CONT")
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue));

        Assert.True(result.IsSupported);
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 40d), (cell.DiffIndex, cell.AfterValue)),
            cell => Assert.Equal((11, 60d), (cell.DiffIndex, cell.AfterValue)),
            cell => Assert.Equal((12, 20d), (cell.DiffIndex, cell.AfterValue)),
            cell => Assert.Equal((13, 10d), (cell.DiffIndex, cell.AfterValue)));

        var action = Assert.Single(result.Actions);
        Assert.Equal(ContinuationSourceRows, action.SourceRowNumbers);
        Assert.Equal((0, 10, 0, 490), (action.IcIndex, action.AnchorDiffIndex, action.RegularPadId, action.CadPadId));
        Assert.Equal((120, 40, 100d, 40d),
            (action.CombinePercent, action.SourceRetainedPercent, action.SourceBeforeValue, action.SourceAfterValue));
        Assert.Equal(
            [(11, 60, 60d), (12, 20, 20d), (13, 10, 10d)],
            action.Legs.Select(static leg => (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue)).ToArray());
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Simulate_WhenCanonicalContinuationRowsAreProjected_PreservesEachFirmwareContract()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices4);
        var projection = CreateProjection(grid, 100d, 0d, 0d, 0d);
        var frames = new[] { projection };
        var v22Table = new NotchTable(new NotchTableRow[]
        {
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 120, 11, 60, 12, 20, 0),
                comment: "MAIN"),
            new(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                v22Node: new NotchV22Node(10, 100, 13, 10, NullDiffValue, 0, 1),
                comment: "CONT")
        });
        var v21Table = new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                values: V21CanonicalMainPayload,
                comment: "MAIN"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 490,
                values: V21CanonicalContinuationPayload,
                comment: "CONT")
        });

        var v22Result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            v22Table,
            NotchAlgorithmVersion.V22,
            NullDiffValue));
        var v21Result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            v21Table,
            NotchAlgorithmVersion.V21,
            NullDiffValue));

        Assert.True(v22Result.IsSupported);
        Assert.True(v21Result.IsSupported);
        Assert.Equal("V2.1 projected apply (v2.2 canonical)", v21Result.ContractText);

        Assert.Equal(
            [40d, 60d, 20d, 10d],
            v22Result.Cells.OrderBy(static cell => cell.DiffIndex).Select(static cell => cell.AfterValue).ToArray());
        Assert.Equal(
            [30d, 60d, 20d, 10d],
            v21Result.Cells.OrderBy(static cell => cell.DiffIndex).Select(static cell => cell.AfterValue).ToArray());
    }

    [Fact]
    public void Simulate_WhenMeanAggregationRequested_AveragesProjectedFramesBeforeApplyingRows()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices2);
        var projectionA = CreateProjection(grid, 10d, 20d);
        var projectionB = CreateProjection(grid, 30d, 40d);
        var frames = new[] { projectionA, projectionB };
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 111,
                v22Node: new NotchV22Node(10, 100, NullDiffValue, 0, NullDiffValue, 0, 0),
                comment: "NOOP")
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue,
            NotchApplySimulationAggregationMode.Mean));

        Assert.True(result.IsSupported);
        Assert.Equal(2, result.ConsumedFrameCount);
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((20d, 20d), (cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((30d, 30d), (cell.BeforeValue, cell.AfterValue)));
        Assert.True(Assert.Single(result.Actions).IsNoOp);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Simulate_WhenVersionIsV21_UsesProjectedCanonicalContract()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndices2);
        var projection = CreateProjection(grid, 100d, 50d);
        var frames = new[] { projection };
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: V21LegacySamplePayload,
                comment: "legacy sample")
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            table,
            NotchAlgorithmVersion.V21,
            NullDiffValue));

        Assert.True(result.IsSupported);
        Assert.Equal("V2.1 projected apply (v2.2 canonical)", result.ContractText);
        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((10, 100d, 50d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((11, 50d, 93d), (cell.DiffIndex, cell.BeforeValue, cell.AfterValue)));
        var action = Assert.Single(result.Actions);
        Assert.True(action.IsLegacyApproximation);
        Assert.Equal(94, action.CombinePercent);
        Assert.Equal(50, action.SourceRetainedPercent);
        Assert.Equal(43d, Assert.Single(action.Legs).DeltaValue);
        Assert.True(result.Histograms.Before.HasBins);
        Assert.True(result.Heatmap.HasHotspots);
    }

    [Fact]
    public void Simulate_WhenV21LegTargetsItsOwnSource_KeepsActionFlowAlignedWithFirmwareCells()
    {
        var grid = BuildLinearGrid(diffIndices: [10]);
        var projection = CreateProjection(grid, 100d);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: [10, 100, 100, 10, 2, 64, NullDiffValue, 0, 0],
                comment: "self subtract"),
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            NotchAlgorithmVersion.V21,
            NullDiffValue));
        var audit = SimulationSafetyAuditService.Analyze(result);

        Assert.Equal(100d, Assert.Single(result.Cells).AfterValue);
        var action = Assert.Single(result.Actions);
        Assert.Equal(150d, action.SourceAfterValue);
        var leg = Assert.Single(action.Legs);
        Assert.Equal((10, -50, -50d), (leg.TargetDiffIndex, leg.RatioPercent, leg.DeltaValue));
        Assert.True(audit.GlobalFlowAudit.IsBalanced);
        Assert.False(audit.HasNetFlowResiduals);
    }

    [Fact]
    public void Simulate_WhenV21ContinuationRowsClampIndependently_ReportsEffectiveRetainedPercent()
    {
        var grid = BuildLinearGrid(diffIndices: [10, 11]);
        var projection = CreateProjection(grid, 100d, 0d);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: [10, 100, 50, 11, 1, 128, NullDiffValue, 0, 0],
                comment: "main clamps retain to zero"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: [10, 100, 100, 11, 2, 128, NullDiffValue, 0, 0],
                comment: "continuation adds source back"),
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            NotchAlgorithmVersion.V21,
            NullDiffValue));
        var audit = SimulationSafetyAuditService.Analyze(result);

        Assert.Collection(
            result.Cells,
            cell => Assert.Equal(100d, cell.AfterValue),
            cell => Assert.Equal(0d, cell.AfterValue));
        var action = Assert.Single(result.Actions);
        Assert.Equal(100, action.SourceRetainedPercent);
        Assert.Equal(100d, action.SourceAfterValue);
        Assert.Empty(action.Legs);
        Assert.True(audit.GlobalFlowAudit.IsBalanced);
        Assert.False(audit.HasNetFlowResiduals);
    }

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void Simulate_WhenFrameIsOutsideInt16Domain_UsesFirmwareBaselineForCellsAndAudit(
        NotchAlgorithmVersion version)
    {
        var grid = BuildLinearGrid(diffIndices: [10, 11, 12]);
        var projection = CreateProjection(grid, 100.75d, 40_000d, double.NaN);
        var table = new NotchTable(grid.Pads.Select(pad => version == NotchAlgorithmVersion.V21
            ? new NotchTableRow(
                NotchAlgorithmVersion.V21,
                pad.IcIndex,
                pad.DiffIndex,
                pad.RegularPadId,
                cadPadId: null,
                values: [pad.DiffIndex, 100, 100, NullDiffValue, 0, 0, NullDiffValue, 0, 0],
                comment: "no-op")
            : new NotchTableRow(
                pad.IcIndex,
                pad.DiffIndex,
                pad.RegularPadId,
                cadPadId: null,
                v22Node: new NotchV22Node(pad.DiffIndex, 100, NullDiffValue, 0, NullDiffValue, 0, 0),
                comment: "no-op")).ToArray());

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            version,
            NullDiffValue));
        var audit = SimulationSafetyAuditService.Analyze(result);

        Assert.Collection(
            result.Cells,
            cell => Assert.Equal((100d, 100d), (cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal(((double)short.MaxValue, (double)short.MaxValue), (cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((0d, 0d), (cell.BeforeValue, cell.AfterValue)));
        Assert.All(result.Cells, static cell => Assert.Equal(0d, cell.DeltaValue));
        Assert.True(audit.GlobalFlowAudit.IsBalanced);
        Assert.False(audit.HasNetFlowResiduals);
        Assert.False(result.Heatmap.HasHotspots);
    }

    [Fact]
    public void Simulate_WhenV21ProjectionReferencesMissingDiffs_WritesExactDiagnostics()
    {
        var grid = BuildLinearGrid(diffIndices: [10]);
        var projection = CreateProjection(grid, 100d);
        var table = new NotchTable(new NotchTableRow[]
        {
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 1010,
                values: [10, 100, 200, 20, 1, 128, NullDiffValue, 0, 0],
                comment: "missing destination"),
            new(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 99,
                regularPadIndex: 99,
                cadPadId: 1099,
                values: [99, 100, 200, 10, 1, 128, NullDiffValue, 0, 0],
                comment: "missing source"),
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            NotchAlgorithmVersion.V21,
            NullDiffValue));

        Assert.Equal(
            [
                "Missing v2.1 firmware destination diff IC1/diff20 in projected grid.",
                "Missing v2.1 firmware source diff IC1/diff99 in projected grid.",
                "Missing anchor diff IC1/diff99 in projected grid.",
            ],
            result.Diagnostics);
        Assert.True(result.IsSupported);
    }

    [Fact]
    public void Simulate_WhenGridPadsAreUnordered_ReturnsCellsAndHeatmapInRowColumnOrder()
    {
        var orderedGrid = TestGeometryFactory.CreateRegularGrid(
            rows: 2,
            cols: 2,
            diffIndexSelector: static (row, col) => 10 + (row * 2) + col);
        var grid = new RegularGrid(
            orderedGrid.Rows,
            orderedGrid.Cols,
            orderedGrid.XEdges,
            orderedGrid.YEdges,
            [orderedGrid.Pads[3], orderedGrid.Pads[0], orderedGrid.Pads[2], orderedGrid.Pads[1]]);
        var projection = CreateProjection(grid, 10d, 20d, 30d, 40d);

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            new NotchTable(Array.Empty<NotchTableRow>()),
            NotchAlgorithmVersion.V22,
            NullDiffValue));

        var expectedOrder = new[]
        {
            (RegularPadId: 0, Row: 0, Col: 0, DiffIndex: 10),
            (RegularPadId: 1, Row: 0, Col: 1, DiffIndex: 11),
            (RegularPadId: 2, Row: 1, Col: 0, DiffIndex: 12),
            (RegularPadId: 3, Row: 1, Col: 1, DiffIndex: 13),
        };

        Assert.Equal([3, 0, 2, 1], grid.Pads.Select(static pad => pad.RegularPadId));
        Assert.Equal(
            expectedOrder,
            result.Cells.Select(static cell =>
                (cell.RegularPadId, Row: cell.RegularRow, Col: cell.RegularCol, cell.DiffIndex)));
        Assert.Equal(
            expectedOrder,
            result.Heatmap.Cells.Select(static cell =>
                (cell.RegularPadId, Row: cell.RegularRow, Col: cell.RegularCol, cell.DiffIndex)));
    }

    [Fact]
    public void Simulate_WhenActiveSurfaceContainsDuplicateDiffKey_DoesNotThrowAndWritesDiagnostics()
    {
        var grid = BuildLinearGrid(diffIndices: DiffIndicesDuplicate);
        var projection = CreateProjection(grid, 120d, 80d, 30d);
        var frames = new[] { projection };
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 777,
                v22Node: new NotchV22Node(10, 100, NullDiffValue, 0, NullDiffValue, 0, 0),
                comment: "NOOP")
        });

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            frames,
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue));

        Assert.True(result.IsSupported);
        Assert.Contains(
            result.Diagnostics,
            static diagnostic => diagnostic.Contains("merged REG 0, 1 by sum", StringComparison.Ordinal));
        Assert.True(result.DiffIdentityContract.HasDuplicateResolutions);
        var duplicate = Assert.Single(result.DiffIdentityContract.DuplicateResolutions);
        Assert.Equal((0, 10), (duplicate.IcIndex, duplicate.DiffIndex));
        Assert.Equal(0, duplicate.PrimaryRegularPadId);
        Assert.Equal(SuppressedDuplicateRegularPadIds, duplicate.SuppressedRegularPadIds);
        Assert.Equal(NotchApplySimulationDuplicateDiffResolutionStrategy.MergeSumActiveRegularPads, duplicate.Strategy);
        Assert.All(
            result.Cells.Where(static cell => cell.DiffIndex == 10),
            static cell => Assert.Equal((200d, 200d, 0d), (cell.BeforeValue, cell.AfterValue, cell.DeltaValue)));
        Assert.All(result.Cells, static cell => Assert.Equal(0d, cell.DeltaValue));
        Assert.False(result.Heatmap.HasHotspots);
        Assert.True(SimulationSafetyAuditService.Analyze(result).GlobalFlowAudit.IsBalanced);
    }

    private static RegularGrid BuildLinearGrid(int[] diffIndices)
    {
        return TestGeometryFactory.CreateLinearRegularGrid(diffIndices);
    }

    private static DiffFrameGridProjectionResult CreateProjection(RegularGrid grid, params double[] values)
    {
        Assert.Equal(grid.Pads.Count, values.Length);
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select((pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: values[index]))
            .ToList();
        return new DiffFrameGridProjectionResult(true, null, cells);
    }
}

