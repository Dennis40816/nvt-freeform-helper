// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CopperPillarSimulationBehaviorTests
{
    [Theory]
    [InlineData(0, true, true, 1d, "Regular grid has no pads.")]
    [InlineData(1, false, true, 1d, "Copper CAD projection requires CAD pads and CAD Output FW Diff mapping.")]
    [InlineData(1, true, false, 1d, "Copper CAD projection requires CAD pads and CAD Output FW Diff mapping.")]
    [InlineData(1, true, true, 0d, "Copper diameter must be greater than 0.")]
    [InlineData(1, true, true, -1d, "Copper diameter must be greater than 0.")]
    public void ProjectCadOutputToRegularGrid_UnusableInputs_ReturnsReasonAndNoCells(
        int rows, bool includeCad, bool includeMapping, double diameter, string expectedDiagnostic)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(rows, 1);
        CadPad[] cadPads = includeCad
            ? [TestGeometryFactory.CreateCadPad(100, "SENSOR", -2d, -2d, 2d, 2d)]
            : [];
        var outputDiffs = new Dictionary<int, int>();
        if (includeMapping)
        {
            outputDiffs[100] = 0;
        }

        var result = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid, cadPads, outputDiffs, new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(0d, 0d, diameter, 400d));

        Assert.False(result.IsCompatible);
        Assert.Equal(expectedDiagnostic, result.Diagnostic);
        Assert.Empty(result.Cells);
    }

    [Theory]
    [InlineData(true, true, 2, 2, 807d)]
    [InlineData(true, false, 2, 0, 1607d)]
    [InlineData(false, true, 0, 2, 1607d)]
    public void ProjectCadOutputToRegularGrid_IncompleteMappings_AccumulatesOnlyResolvedCopperAndReportsLosses(
        bool omitCadMappings, bool omitTargets, int missingMappings, int missingTargets, double expectedValue)
    {
        using var culture = TestCulture.UseEnUs();
        var grid = TestGeometryFactory.CreateRegularGrid(
            1, 2, diffIndexSelector: static (_, _) => 10, icIndexSelector: static (_, col) => col);
        var cadPads = Enumerable.Range(100, 6)
            .Select(id => TestGeometryFactory.CreateCadPad(id, "SENSOR", -2d, -2d, 2d, 2d))
            .ToList();
        cadPads.Add(TestGeometryFactory.CreateCadPad(106, "SENSOR", 10d, 10d, 11d, 11d));
        cadPads.Add(new CadPad(107, "CORNER", "SENSOR", new Polygon2(
        [
            new Point2(0.8d, 0.8d), new Point2(1.2d, 0.8d), new Point2(0.8d, 1.2d),
        ])));
        var outputDiffs = Enumerable.Range(100, 8).ToDictionary(static id => id, static _ => 10);
        var icIndices = Enumerable.Range(100, 8).ToDictionary(static id => id, static _ => 1);
        if (omitCadMappings)
        {
            outputDiffs.Remove(102);
            icIndices.Remove(103);
        }

        if (omitTargets)
        {
            outputDiffs[104] = 99;
            icIndices[105] = 2;
        }

        var result = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            grid, cadPads, outputDiffs, icIndices,
            new CopperPillarSimulationRequest(0d, 0d, 2d, 400d, BaselineValue: 7d));

        Assert.True(result.IsCompatible);
        Assert.Equal(
            $"Copper CAD projection touched 6 CAD pad(s); missing CAD output mapping={missingMappings}, " +
            $"missing target regular diff={missingTargets}.", result.Diagnostic);
        Assert.Collection(result.Cells,
            cell => Assert.Equal((0, 7d), (cell.RegularPadId, cell.Value)),
            cell => Assert.Equal((1, expectedValue), (cell.RegularPadId, cell.Value)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectCadOutputToRegularGrid_CircleCrossesConcaveCorner_ClipsCopperForEitherWinding(bool reverseVertices)
    {
        var vertices = new[]
        {
            new Point2(-2d, -2d), new Point2(2d, -2d), new Point2(2d, 0d),
            new Point2(0d, 0d), new Point2(0d, 2d), new Point2(-2d, 2d), new Point2(-2d, -2d),
        };
        if (reverseVertices)
        {
            Array.Reverse(vertices);
        }

        var result = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[] { new CadPad(100, "NOTCHED", "SENSOR", new Polygon2(vertices)) },
            new Dictionary<int, int> { [100] = 0 }, new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(-0.25d, -0.25d, 1d, 100d, CircleSegmentCount: 256));

        Assert.True(result.IsCompatible);
        Assert.Null(result.Diagnostic);
        // The missing quadrant cuts about 2.51% of the circle, even though its center is inside the pad.
        Assert.InRange(Assert.Single(result.Cells).Value, 97.48d, 97.52d);
    }
}
