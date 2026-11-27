using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using Xunit;
using static FreeformHelper.Tests.NotchHolePanelFixture;

namespace FreeformHelper.Tests;

/// <summary>
/// To Full is not limited to boundary regulars. On a panel where every CAD pad lies across several
/// regular pads and leaves a narrow gap, interior regulars need To Full as well: without it the rows of
/// interior pads disappear, together with the area they pass to neighbors, and a uniform input no longer
/// stays uniform.
/// </summary>
public sealed class NotchToFullInteriorUniformFieldTests
{
    private const int UniformInput = 400;

    [Fact]
    public void UniformInput_StaysUniform_WhenInteriorPadsStraddleRegularsAndLeaveGaps()
    {
        var panel = Build([], gridOrigin: StaggerForwardOrigin);
        // The virtual area cap and the target coverage guard are off, so that only the To Full rule is checked.
        var settings = CreateSettings(capsEnabled: false);
        settings.Notch.CompensationModel = NotchCompensationModel.CurrentGain;

        var table = new NotchTableGenerator().Generate(panel.Cad, panel.Grid, settings);

        var cells = panel.Grid.Pads
            .Select(static pad => new DiffFrameGridCellValue(pad.Row, pad.Col, pad.Row, pad.Col, pad.RegularPadId, UniformInput))
            .ToList();
        var simulated = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            panel.Grid,
            panel.Grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            [new DiffFrameGridProjectionResult(true, null, cells)],
            table,
            NotchAlgorithmVersion.V22,
            NullValue,
            ComputationMode: NotchComputationMode.CadAllocation));

        Assert.True(simulated.IsSupported);
        // Every CAD pad emits a row; an empty table would keep the input uniform without compensating anything.
        Assert.Equal(GridSize * GridSize, table.Rows.Select(static row => row.DiffIndex).Distinct().Count());
        Assert.Equal(GridSize * GridSize, simulated.Cells.Count);
        Assert.All(simulated.Cells, static cell => Assert.Equal(UniformInput, cell.AfterValue));
    }
}
