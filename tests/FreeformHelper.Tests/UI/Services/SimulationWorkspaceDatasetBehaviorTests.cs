// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceDatasetBehaviorTests
{
    [Fact]
    public void BuildManualScenario_GridWithColumnsButNoRowsReportsIncompatibleFrameInsteadOfSimulating()
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 0, cols: 2);
        var session = new SimulationWorkspaceSession(
            grid, new NotchTable([]), NullDiffValue: 65535, SourceRevision: 1, ActiveRegularPadIds: new HashSet<int>());

        var scenario = SimulationWorkspaceUseCase.BuildManualScenario(
            session, NotchAlgorithmVersion.V22, globalValue: 100d, new Dictionary<int, double>());
        var snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario);

        const string diagnostic = "Frame dimensions 0x0 do not match grid 0x2.";
        Assert.Equal(0, scenario.Dataset.CompatibleFrameCount);
        Assert.Equal(diagnostic, Assert.Single(scenario.Dataset.Diagnostics));
        var file = Assert.Single(scenario.Dataset.Files);
        Assert.Equal(2, file.DeclaredCols);
        Assert.Equal(0, file.DeclaredRows);
        Assert.Equal(0, file.CompatibleFrameCount);
        var frame = Assert.Single(scenario.Dataset.Frames);
        Assert.Empty(frame.Frame.Rows);
        Assert.False(frame.Projection.IsCompatible);
        Assert.Equal(diagnostic, frame.Projection.Diagnostic);
        Assert.False(snapshot.SimulationResult.IsSupported);
        Assert.Equal(0, snapshot.SimulationResult.ConsumedFrameCount);
        Assert.Empty(snapshot.SimulationResult.Cells);
        Assert.False(snapshot.SafetyAudit.HasCells);
        Assert.Contains(diagnostic, snapshot.Diagnostics);
    }
}
