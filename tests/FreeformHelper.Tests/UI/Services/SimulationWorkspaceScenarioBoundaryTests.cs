// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationWorkspaceScenarioBoundaryTests
{
    [Theory]
    [InlineData(false, 0.5d, "Copper CAD projection requires CAD pads and CAD Output FW Diff mapping.")]
    [InlineData(true, 0d, "Copper diameter must be greater than 0.")]
    public void CopperScenario_InvalidProjection_PreservesDiagnosticAndProducesNoSimulationCells(
        bool hasMapping, double diameter, string expectedDiagnostic)
    {
        using var culture = TestCulture.UseEnUs();
        var session = CreateSession();
        if (!hasMapping)
        {
            session = session with { CadOutputFwDiffIndexByCadId = new Dictionary<int, int>() };
        }

        var scenario = SimulationWorkspaceUseCase.BuildCopperScenario(
            session,
            NotchAlgorithmVersion.V22,
            new CopperPillarSimulationRequest(0.5d, 0.5d, diameter, PeakValue: 600d));
        var snapshot = SimulationWorkspaceUseCase.BuildScenarioSnapshot(session, scenario);

        Assert.Equal(0, scenario.Dataset.CompatibleFrameCount);
        Assert.Equal(expectedDiagnostic, Assert.Single(scenario.Dataset.Diagnostics));
        var file = Assert.Single(scenario.Dataset.Files);
        Assert.Equal(0, file.CompatibleFrameCount);
        Assert.Equal(expectedDiagnostic, Assert.Single(file.Diagnostics));
        var frame = Assert.Single(scenario.Dataset.Frames);
        Assert.False(frame.Projection.IsCompatible);
        Assert.Equal(expectedDiagnostic, frame.Projection.Diagnostic);
        Assert.Empty(frame.Projection.Cells);
        Assert.False(snapshot.SimulationResult.IsSupported);
        Assert.Equal(0, snapshot.SimulationResult.ConsumedFrameCount);
        Assert.Empty(snapshot.SimulationResult.Cells);
        Assert.False(snapshot.SafetyAudit.HasCells);
        Assert.Contains(expectedDiagnostic, snapshot.Diagnostics);
        Assert.Contains("No compatible projected diff frames are available for simulation.", snapshot.Diagnostics);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ReplayCopperPath_AtMostOneRequestedStep_AuditsTheStartPosition(int requestedSteps)
    {
        var session = CreateSession();

        var result = SimulationWorkspaceUseCase.ReplayCopperPath(
            session,
            NotchAlgorithmVersion.V22,
            new CopperPillarPathReplayRequest(
                new Point2(0.5d, 0.5d),
                new Point2(1.5d, 0.5d),
                requestedSteps,
                Diameter: 0.5d,
                PeakValue: 600d));

        var step = Assert.Single(result.Steps);
        Assert.Equal(0, step.StepIndex);
        Assert.Equal((0.5d, 0.5d), (step.CenterX, step.CenterY));
        Assert.True(step.IsSupported);
        Assert.Equal(600d, step.MaxAfterValue);
        Assert.Equal(0, step.WorstRegularPadId);
        Assert.Equal(0, step.WorstIcIndex);
        Assert.Equal(10, step.WorstDiffIndex);
        Assert.Equal(1, step.EmsViolationCount);
        Assert.Equal("EMS risk", step.StatusText);
        Assert.Equal(1, result.TotalEmsViolationCount);
        Assert.Same(step, result.WorstStep);
    }

    private static SimulationWorkspaceSession CreateSession()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(
            rows: 1,
            cols: 2,
            diffIndexSelector: static (_, col) => 10 + col);
        return new SimulationWorkspaceSession(
            grid,
            new NotchTable(Array.Empty<NotchTableRow>()),
            NullDiffValue: 65535,
            SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet(),
            CadPadsForCopperProjection:
            [
                TestGeometryFactory.CreateCadPad(100, "SENSOR", 0d, 0d, 1d, 1d),
                TestGeometryFactory.CreateCadPad(101, "SENSOR", 1d, 0d, 2d, 1d),
            ],
            CadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [100] = 10, [101] = 11 },
            CadIcIndexByCadId: new Dictionary<int, int> { [100] = 0, [101] = 0 });
    }
}
