using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceUseCaseTests
{
    private const int NullDiffValue = 65535;
    private static readonly int[] SimulationDiffIndices = { 10, 11 };

    [Fact]
    public void BuildScenarioSnapshot_WhenSourcesVary_UsesSingleResultAndAuditModel()
    {
        var session = BuildSession();
        var useCase = new SimulationWorkspaceUseCase(new NotchApplySimulationReviewUseCase());
        var csvPath = TestFiles.WriteTempCsv(
            """
            LogVer:"1.0",Xch:"2",Ych:"1",
            timestamp(mm:ss:nnn):"09:14:165",Frame index:"0",Break point index:"0",DiffData:
            100,50
            """);
        var csvDataset = useCase.ImportCsvSources(new[] { csvPath }, session.FwDiffGrid);
        var copperRequest = new CopperPillarSimulationRequest(
            CenterX: 0.5d,
            CenterY: 0.5d,
            Diameter: 0.5d,
            PeakValue: 400d);
        var scenarios = new[]
        {
            SimulationWorkspaceUseCase.BuildManualScenario(
                session,
                NotchAlgorithmVersion.V22,
                globalValue: 100d,
                new Dictionary<int, double>()),
            SimulationWorkspaceUseCase.BuildCsvScenario(
                csvDataset,
                NotchAlgorithmVersion.V22,
                NotchApplySimulationAggregationMode.SingleFrame,
                selectedFrameIndex: 0),
            SimulationWorkspaceUseCase.BuildCopperScenario(
                session,
                NotchAlgorithmVersion.V22,
                copperRequest),
            SimulationWorkspaceUseCase.BuildCopperScenario(
                session,
                NotchAlgorithmVersion.V22,
                copperRequest,
                SimulationScenarioKind.CopperPathSweep),
        };

        var snapshots = scenarios
            .Select(scenario => SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario))
            .ToArray();

        Assert.Equal(
            new[]
            {
                SimulationScenarioKind.Manual,
                SimulationScenarioKind.Csv,
                SimulationScenarioKind.Copper,
                SimulationScenarioKind.CopperPathSweep,
            },
            snapshots.Select(static snapshot => snapshot.Scenario.Kind));
        foreach (var snapshot in snapshots)
        {
            Assert.True(snapshot.Result.IsSupported);
            Assert.True(snapshot.SafetyAudit.HasCells);
            Assert.Equal(snapshot.Result.Cells.Count, snapshot.SafetyAudit.CellCount);
            Assert.Same(snapshot.ReviewSnapshot.Result, snapshot.Result);
            Assert.Same(snapshot.ReviewSnapshot.Dataset, snapshot.Dataset);
        }
    }

    [Fact]
    public void ReplayCopperPath_ProjectsEachStepThroughScenarioSnapshotAudit()
    {
        var session = BuildSession();

        var result = SimulationWorkspaceUseCase.ReplayCopperPath(
            session,
            NotchAlgorithmVersion.V22,
            new CopperPillarPathReplayRequest(
                new Point2(0.5d, 0.5d),
                new Point2(1.5d, 0.5d),
                StepCount: 2,
                Diameter: 0.5d,
                PeakValue: 1000d));

        Assert.Equal(2, result.Steps.Count);
        Assert.True(result.HasEmsViolations);
        Assert.All(result.Steps, step => Assert.NotEqual("unsupported", step.StatusText));
        Assert.Equal(result.Steps.Sum(static step => step.EmsViolationCount), result.TotalEmsViolationCount);
    }

    [Fact]
    public void BuildScenarioSnapshot_PropagatesLegacyCompatibilityModeToTheSharedFirmwareProjection()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(SimulationDiffIndices);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V21,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                values: [10, 100, 50, 11, 1, 128, NullDiffValue, 0, 0],
                comment: "legacy destination node"),
        });
        var session = new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            ComputationMode: NotchComputationMode.LegacyRegularAnchor);
        var scenario = SimulationWorkspaceUseCase.BuildManualScenario(
            session,
            NotchAlgorithmVersion.V21,
            globalValue: 100d,
            new Dictionary<int, double>());

        var snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario);

        Assert.Equal("V2.1 legacy firmware apply", snapshot.Result.ContractText);
        Assert.Empty(snapshot.Result.Actions);
        Assert.False(snapshot.SafetyAudit.GlobalFlowAudit.HasActionFlowModel);
        Assert.Collection(
            snapshot.Result.Cells,
            cell => Assert.Equal((10, 150d), (cell.DiffIndex, cell.AfterValue)),
            cell => Assert.Equal((11, 100d), (cell.DiffIndex, cell.AfterValue)));
    }

    private static SimulationWorkspaceSession BuildSession()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid(
            SimulationDiffIndices,
            icIndexSelector: static col => col);
        var table = new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 0,
                cadPadId: 313,
                v22Node: new NotchV22Node(10, 94, 11, 44, NullDiffValue, 0, 0),
                comment: "sample")
        });

        return new SimulationWorkspaceSession(
            grid,
            table,
            NullDiffValue,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection: new[]
            {
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
            },
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int>
            {
                [100] = 10,
                [101] = 11,
            },
            CadIcIndexByCadId: new Dictionary<int, int>
            {
                [100] = 0,
                [101] = 1,
            });
    }
}
