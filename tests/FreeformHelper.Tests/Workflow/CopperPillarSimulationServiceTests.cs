using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CopperPillarSimulationServiceTests
{
    [Fact]
    public void ProjectToRegularGrid_WhenCopperFullyInsidePad_AssignsPeakValue()
    {
        var grid = BuildGrid(rows: 1, cols: 1);

        var projection = CopperPillarSimulationService.ProjectToRegularGrid(
            grid,
            new CopperPillarSimulationRequest(0.5d, 0.5d, 0.5d, 400d));

        Assert.True(projection.IsCompatible);
        var cell = Assert.Single(projection.Cells);
        Assert.Equal(400d, cell.Value, precision: 6);
    }

    [Fact]
    public void ProjectToRegularGrid_WhenCopperStraddlesTwoPads_ConservesPeakByOverlapArea()
    {
        var grid = BuildGrid(rows: 1, cols: 2);

        var projection = CopperPillarSimulationService.ProjectToRegularGrid(
            grid,
            new CopperPillarSimulationRequest(1d, 0.5d, 1d, 400d));

        Assert.True(projection.IsCompatible);
        Assert.Equal(2, projection.Cells.Count);
        Assert.Equal(200d, projection.Cells[0].Value, precision: 6);
        Assert.Equal(200d, projection.Cells[1].Value, precision: 6);
        Assert.Equal(400d, projection.Cells.Sum(static cell => cell.Value), precision: 6);
    }

    [Fact]
    public void ProjectToRegularGrid_WhenCopperOutsideGrid_AssignsBaselineOnly()
    {
        var grid = BuildGrid(rows: 1, cols: 2);

        var projection = CopperPillarSimulationService.ProjectToRegularGrid(
            grid,
            new CopperPillarSimulationRequest(10d, 10d, 1d, 400d, BaselineValue: 3d));

        Assert.True(projection.IsCompatible);
        Assert.All(projection.Cells, static cell => Assert.Equal(3d, cell.Value, precision: 6));
    }

    [Fact]
    public void ProjectCadOutputToRegularGrid_UsesCadOverlapThenMapsToOutputFwDiff()
    {
        var grid = BuildGrid(rows: 1, cols: 2);
        var cadPads = new[]
        {
            TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
        };

        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            cadPads,
            new Dictionary<int, int> { [100] = 11 },
            new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(0.5d, 0.5d, 0.5d, 400d));

        Assert.True(projection.IsCompatible);
        Assert.Equal(0d, projection.Cells[0].Value, precision: 6);
        Assert.InRange(projection.Cells[1].Value, 399.99d, 400.01d);
    }

    [Fact]
    public void ProjectCadOutputToRegularGrid_WhenMappingMissing_DoesNotFallbackToRegularOverlap()
    {
        var grid = BuildGrid(rows: 1, cols: 1);

        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            Array.Empty<CadPad>(),
            new Dictionary<int, int>(),
            new Dictionary<int, int>(),
            new CopperPillarSimulationRequest(0.5d, 0.5d, 0.5d, 400d));

        Assert.False(projection.IsCompatible);
        Assert.Contains("CAD Output FW Diff", projection.Diagnostic, StringComparison.Ordinal);
        Assert.Empty(projection.Cells);
    }

    [Theory]
    [InlineData(400d)]
    [InlineData(360d)]
    public void ProjectCadOutputToRegularGrid_WhenCopperFullyInsidePad_PreservesPeakInFirmwareSimulation(double peak)
    {
        var grid = BuildGrid(rows: 1, cols: 1);
        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            new[] { TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d) },
            new Dictionary<int, int> { [100] = 10 },
            new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(0.5d, 0.5d, 0.5d, peak));

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            new NotchTable(Array.Empty<NotchTableRow>()),
            NotchAlgorithmVersion.V22,
            NullDiffValue: 65535));

        Assert.True(projection.IsCompatible);
        Assert.True(simulation.IsSupported);
        var projected = Assert.Single(projection.Cells);
        var simulated = Assert.Single(simulation.Cells);
        Assert.Equal((peak, peak, peak), (projected.Value, simulated.BeforeValue, simulated.AfterValue));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectCadOutputToRegularGrid_WhenCopperTangentToObliquePadEdge_PreservesPeakInFirmwareSimulation(bool reverseVertices)
    {
        var grid = BuildGrid(rows: 1, cols: 1);
        var vertices = new[]
        {
            new Point2(5.25d, -1.75d),
            new Point2(-2.25d, 2.25d),
            new Point2(-4.5d, 2.25d),
            new Point2(-4.5d, -4.5d),
            new Point2(5.25d, -4.5d),
        };
        if (reverseVertices)
        {
            Array.Reverse(vertices);
        }

        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            new[] { new CadPad(100, "CAD100", "SENSOR", new Polygon2(vertices)) },
            new Dictionary<int, int> { [100] = 10 },
            new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(0.5d, 0.5d, 0.5d, 400d));

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            new NotchTable(Array.Empty<NotchTableRow>()),
            NotchAlgorithmVersion.V22,
            NullDiffValue: 65535));

        Assert.True(projection.IsCompatible);
        Assert.True(simulation.IsSupported);
        var projected = Assert.Single(projection.Cells);
        var simulated = Assert.Single(simulation.Cells);
        Assert.Equal((400d, 400d, 400d), (projected.Value, simulated.BeforeValue, simulated.AfterValue));
    }

    [Theory]
    [InlineData(0.5d)]
    [InlineData(0.50000001d)]
    public void ProjectCadOutputToRegularGrid_WhenCopperPartiallyOverlapsPad_PreservesFirmwareBeforeAndAfter(double centerX)
    {
        var grid = BuildGrid(rows: 1, cols: 2);
        var projection = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid,
            new[] { TestGeometryFactory.CreateCadPad(100, "SENSOR", 0.5d, 0d, 1d, 1d) },
            new Dictionary<int, int> { [100] = 10 },
            new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(centerX, 0.5d, 0.5d, 400d));
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 100,
                v22Node: new NotchV22Node(10, 100, 11, 50, 65535, 0, 0),
                comment: "Partial copper overlap")
        });

        var simulation = NotchApplySimulationService.Simulate(new NotchApplySimulationRequest(
            grid,
            grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            new[] { projection },
            table,
            NotchAlgorithmVersion.V22,
            NullDiffValue: 65535));

        Assert.True(projection.IsCompatible);
        Assert.True(simulation.IsSupported);
        Assert.Collection(
            simulation.Cells,
            cell => Assert.Equal((199d, 100d), (cell.BeforeValue, cell.AfterValue)),
            cell => Assert.Equal((0d, 99d), (cell.BeforeValue, cell.AfterValue)));
    }

    private static RegularGrid BuildGrid(int rows, int cols)
    {
        return TestGeometryFactory.CreateRegularGrid(
            rows,
            cols,
            diffIndexSelector: static (_, col) => col + 10);
    }
}
