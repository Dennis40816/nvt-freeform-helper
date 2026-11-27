// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Checks that the CurrentGain target coverage guard and the final v2.2 MAIN/CONT nodes retain
/// the same anchor coverage (issue #64, inventory finding NOTCH_OUT-D01). MAIN excludes the
/// continuation legs from its combine; continuation rows add only to their targets.
/// </summary>
public sealed class NotchGuardVersusFinalNodeCharacterizationTests
{
    // One 10 by 10 cell holds the source. The CAD pad of 12 by 12 sticks out into three neighbors.
    // The boundary virtual area cap (ratio 1.0, set in the settings below) turns the overlaps of
    // 20, 20 and 4 percent into legs of 40, 40 and 8 percent.
    private const double CellSize = 10d;
    private const double CadSize = 12d;

    private static readonly double[] _beforeValues = [100d, 0d, 0d, 0d];
    private static readonly int[] _expectedMainValues = [0, 180, 1, 40, 2, 40, 0];
    private static readonly int[] _expectedContValues = [0, 100, 3, 8, 65535, 0, 1];
    private static readonly double[] _expectedAfterValues = [100d, 40d, 40d, 8d];

    [Fact]
    public void Generate_CurrentGainWithGuardCapAboveGuardBudget_RetainsTheGuardBudgetOnTheAnchor()
    {
        // The guard budgets the anchor at 188 - (40 + 40 + 8) = 100, which is below the cap of 104.
        var (grid, table) = Generate(targetCoverageCapPercent: 104);

        // One source with three legs: a MAIN row with two legs and a CONT row with one.
        Assert.Equal(2, table.Rows.Count);
        var main = table.Rows[0];
        var cont = table.Rows[1];
        Assert.Equal(_expectedMainValues, main.Values);
        Assert.Equal(_expectedContValues, cont.Values);
        Assert.All(table.Rows, static row => Assert.DoesNotContain("TG=", row.Comment, StringComparison.Ordinal));

        // Restore the source-wide combine used by the guard before subtracting every leg.
        var fullCombine = main.Values[1] + cont.Values[3];
        var legSum = main.Values[3] + main.Values[5] + cont.Values[3];
        var guardRetained = fullCombine - legSum;
        Assert.Equal(100, guardRetained);

        // Firmware subtracts MAIN legs only; MAIN has already excluded the CONT leg.
        var simulated = Simulate(grid, table);
        Assert.Equal(_expectedAfterValues, simulated);
        Assert.Equal((double)guardRetained, simulated[0]);
        Assert.InRange(simulated[0], 0d, 104d);
        Assert.Equal(188d, simulated.Sum());
    }

    [Fact]
    public void Generate_CurrentGainWithLowerGuardCap_ScalesTheSameSource()
    {
        // The same source with a cap below the guard budget: the guard is live for this shape.
        var (_, table) = Generate(targetCoverageCapPercent: 90);

        Assert.Equal(2, table.Rows.Count);
        Assert.All(table.Rows, static row => Assert.Contains("TG=", row.Comment, StringComparison.Ordinal));
        Assert.Equal(170, table.Rows[0].Values[1]);
    }

    private static (RegularGrid Grid, NotchTable Table) Generate(int targetCoverageCapPercent)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 2,
            cols: 2,
            cellWidth: CellSize,
            cellHeight: CellSize,
            diffIndexSelector: static (row, col) => (row * 2) + col,
            icIndexSelector: static (_, _) => 0);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, CadSize, CadSize)]);
        var source = grid.Pads[0];
        source.Freeform = FreeformType.XWay;
        source.MatchedCadPadId = 1;
        source.MatchScore = 1.0;

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.CurrentGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnableTargetCoverageGuard = true,
                EnableBoundaryVirtualAreaCap = true,
                BoundaryVirtualAreaCapRatio = 1.0,
                NullValue = 65535,
                TargetCoverageCapPercent = targetCoverageCapPercent,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };

        return (grid, new NotchTableGenerator().Generate(cad, grid, settings));
    }

    private static double[] Simulate(RegularGrid grid, NotchTable table)
    {
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select((pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: _beforeValues[index]))
            .ToList();
        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [new DiffFrameGridProjectionResult(true, null, cells)],
            table,
            NotchAlgorithmVersion.V22,
            ushort.MaxValue,
            ComputationMode: NotchComputationMode.CadAllocation));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        return result.Cells
            .OrderBy(static cell => cell.RegularRow)
            .ThenBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();
    }
}
