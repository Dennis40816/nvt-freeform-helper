// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceScenarioContractTests
{
    [Fact]
    public void BuildScenarioSnapshot_ForV22_ExposesOnlyTheSelectedVersionRows()
    {
        using var culture = TestCulture.UseEnUs();
        var v21Row = new NotchTableRow(
            NotchAlgorithmVersion.V21,
            icIndex: 0,
            diffIndex: 10,
            regularPadIndex: 0,
            cadPadId: 100,
            values: [10, 0, 0, 65535, 0, 0, 65535, 0, 0],
            comment: "V21 row from the same generated table");
        var v22Row = new NotchTableRow(
            icIndex: 0,
            diffIndex: 10,
            regularPadIndex: 0,
            cadPadId: 100,
            v22Node: new NotchV22Node(10, 75, 65535, 0, 65535, 0, 0),
            comment: "V22 row from the same generated table");
        var session = CreateSession() with { Table = new NotchTable(new[] { v21Row, v22Row }) };
        var scenario = SimulationWorkspaceUseCase.BuildManualScenario(
            session, NotchAlgorithmVersion.V22, globalValue: 100d, new Dictionary<int, double>());

        var snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario);

        Assert.Same(v22Row, Assert.Single(snapshot.FilteredTable.Rows));
        Assert.Same(snapshot.ReviewSnapshot.FilteredTable, snapshot.FilteredTable);
        Assert.Equal(2, session.Table.Rows.Count);
        Assert.True(snapshot.SimulationResult.IsSupported);
        Assert.Equal(75d, Assert.Single(snapshot.SimulationResult.Cells).AfterValue);
    }

    [Theory]
    [InlineData(SimulationScenarioKind.Manual)]
    [InlineData(SimulationScenarioKind.Csv)]
    public void BuildCopperScenario_ForANonCopperKind_RejectsTheRequestedKind(SimulationScenarioKind kind)
    {
        using var culture = TestCulture.UseEnUs();
        var session = CreateSession();
        var request = new CopperPillarSimulationRequest(0.5d, 0.5d, Diameter: 0.5d, PeakValue: 100d);

        var exception = Assert.Throws<ArgumentException>(() => SimulationWorkspaceUseCase.BuildCopperScenario(
            session, NotchAlgorithmVersion.V22, request, kind));

        Assert.Equal("kind", exception.ParamName);
    }

    private static SimulationWorkspaceSession CreateSession()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 1, cols: 1, diffIndexSelector: static (_, _) => 10);
        return new SimulationWorkspaceSession(
            grid,
            new NotchTable(Array.Empty<NotchTableRow>()),
            NullDiffValue: 65535,
            SourceRevision: 1,
            ActiveRegularPadIds: new HashSet<int> { 0 },
            CadPadsForCopperProjection: new[] { TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d) },
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10 },
            CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0 });
    }
}
