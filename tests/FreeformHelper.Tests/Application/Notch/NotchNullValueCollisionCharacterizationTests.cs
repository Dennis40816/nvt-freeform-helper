using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the v2.2 export and the C# simulation do when the reserved "no target" value
/// (<c>NullValue</c>) is also a real firmware diff index (issue #64). <c>NullValue</c> is valid for
/// any value in [0, 65535] and nothing checks it against the diff indices of the grid. A target
/// that has the same index as <c>NullValue</c> is written as <c>NHC_DIFF_NONE</c>, so the firmware
/// skips it while the source still gives up its share. These tests describe the current result,
/// not the wanted one. A fix that rejects the collision with its IC and diff changes the expected
/// values here and must say so in its pull request.
/// </summary>
public sealed class NotchNullValueCollisionCharacterizationTests
{
    private const int CollidingValue = 0;

    [Fact]
    public void Export_TargetWithTheNullValueIndex_IsWrittenAsNoTarget()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([1, CollidingValue]);
        Assert.Contains(grid.Pads, static pad => pad.DiffIndex == CollidingValue);

        var text = NotchTableExporter.ExportAsCInitializer(
            CreateCollidingTable(),
            CreateCadSet(),
            grid,
            CreateSettings(),
            NotchExportProfile.Debug);

        Assert.Contains("#define NHC_DIFF_NONE            (0x0000u)", text, StringComparison.Ordinal);
        Assert.Contains(
            "{     1, 100, NHC_DIFF_NONE,   20, NHC_DIFF_NONE,    0, NHC_FLAG_NONE }",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Simulation_TargetWithTheNullValueIndex_LosesTheMovedShare()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([1, CollidingValue]);
        var cells = grid.Pads
            .OrderBy(static pad => pad.Row)
            .ThenBy(static pad => pad.Col)
            .Select(static (pad, index) => new DiffFrameGridCellValue(
                FrameRow: pad.Row,
                FrameCol: pad.Col,
                RegularRow: pad.Row,
                RegularCol: pad.Col,
                RegularPadId: pad.RegularPadId,
                Value: index == 0 ? 100d : 0d))
            .ToList();
        var projection = new DiffFrameGridProjectionResult(true, null, cells);

        var result = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            CreateCollidingTable(),
            NotchAlgorithmVersion.V22,
            CollidingValue,
            ComputationMode: NotchComputationMode.CadAllocation));

        Assert.True(result.IsSupported);
        Assert.Empty(result.Diagnostics);
        var after = result.Cells
            .OrderBy(static cell => cell.RegularCol)
            .Select(static cell => cell.AfterValue)
            .ToArray();

        // The source (diff 1) keeps 80 of 100. The 20 moved to diff 0 never arrives: 100 -> 80 in total.
        Assert.Equal([80d, 0d], after);
    }

    private static NotchTable CreateCollidingTable()
    {
        return new NotchTable([
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 1,
                regularPadIndex: 0,
                cadPadId: 1,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 1,
                    CombinePercent: 100,
                    TargetDiffIndex1: CollidingValue,
                    TargetRatioPercent1: 20,
                    TargetDiffIndex2: CollidingValue,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "null-collision"),
        ]);
    }

    private static CadPadSet CreateCadSet()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        return new CadPadSet(new[] { new CadPad(1, "L", "P", polygon) });
    }

    private static ProjectSettings CreateSettings()
    {
        return new ProjectSettings
        {
            Notch = new NotchSettings
            {
                NullValue = CollidingValue,
                ComputationMode = NotchComputationMode.CadAllocation,
            },
        };
    }
}
