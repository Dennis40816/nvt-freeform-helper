// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FreeformDetectorBehaviorCoverageTests
{
    [Theory]
    [InlineData(1, 2, 0.5, true, false, FreeformType.XWay)]
    [InlineData(2, 1, 0.5, true, true, FreeformType.YWay)]
    [InlineData(2, 2, 0.5, true, false, FreeformType.XYWay)]
    [InlineData(2, 2, 0.5, false, true, FreeformType.XWay)]
    [InlineData(3, 2, 0.5, false, false, FreeformType.YWay)]
    [InlineData(1, 2, 0.6, false, true, FreeformType.None)]
    public void DetectAssignments_WithoutOverlapEvidence_UsesLegacySpansAndIgnoresIneligibleMatches(
        int rows, int cols, double threshold, bool enableXy, bool useEmptyEvidence, FreeformType expected)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows + 1, cols + 2);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, cols, rows),
            TestGeometryFactory.CreateCadPad(200, "PAD", cols, 0, cols + 1, 1),
        ]);
        foreach (var pad in grid.Pads)
        {
            pad.Freeform = FreeformType.XYWay;
            pad.MatchScore = 1;
            if (pad.Row < rows && pad.Col < cols)
            {
                pad.MatchedCadPadId = 100;
            }
        }

        grid.GetPad(0, cols).MatchedCadPadId = 200;
        var zeroScore = grid.GetPad(0, cols + 1);
        zeroScore.MatchedCadPadId = 100;
        zeroScore.MatchScore = 0;
        var settings = new MatchingSettings { FreeformAxisThreshold = threshold, EnableAutoDetectXy = enableXy };

        var assignments = FreeformDetector.DetectAssignments(
            cad, grid, settings, useEmptyEvidence ? PadMatchResult.Empty : null);
        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.Equal(expected == FreeformType.None ? 0 : rows * cols, assignments.Count);
        Assert.All(grid.Pads, pad => Assert.Equal(
            pad.Row < rows && pad.Col < cols ? expected : FreeformType.None, pad.Freeform));
    }

    [Theory]
    [InlineData(1.0, 0.9, 2.0, 1.3, FreeformType.YWay)]
    [InlineData(1.0, 1.7, 2.0, 2.1, FreeformType.YWay)]
    [InlineData(1.7, 1.0, 2.1, 2.0, FreeformType.XWay)]
    public void DetectAssignments_EdgeSpecialization_PreservesSingleSidedSpillBelowTheAxisThreshold(
        double minX, double minY, double maxX, double maxY, FreeformType expected)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 3, cols: 3);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", minX, minY, maxX, maxY)]);
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings { FreeformAxisThreshold = 0.3 };

        Assert.Empty(FreeformDetector.DetectAssignments(cad, grid, settings, matches));
        settings.EnableFreeformEdgeSpecialization = true;
        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);
        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.Equal(2, assignments.Count);
        Assert.All(matches.Links, link => Assert.Equal(expected, assignments[link.RegularPadId]));
        Assert.Contains(matches.Links, link => link.RegularCoverage < settings.FreeformAxisThreshold);
        Assert.All(grid.Pads.Where(pad => !assignments.ContainsKey(pad.RegularPadId)),
            pad => Assert.Equal(FreeformType.None, pad.Freeform));
    }

    [Theory]
    [InlineData(false, FreeformType.None)]
    [InlineData(true, FreeformType.XYWay)]
    public void DetectAssignments_CornerSpill_RequiresXyDetectionForBoundarySpecialization(
        bool enableXy, FreeformType expected)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 3, cols: 3);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0.9, 0.9, 1.3, 1.3)]);
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings
        {
            FreeformAxisThreshold = 0.3,
            EnableFreeformEdgeSpecialization = true,
            EnableAutoDetectXy = enableXy,
        };

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);
        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.Equal(enableXy ? 4 : 0, assignments.Count);
        Assert.All(matches.Links, link => Assert.Equal(expected, grid.Pads[link.RegularPadId].Freeform));
    }

    [Theory]
    [InlineData(3, 0.9, 1.0, 2.1, 2.0)]
    [InlineData(3, 1.0, 0.9, 2.0, 2.1)]
    [InlineData(5, 1.9, 2.0, 2.3, 3.0)]
    public void DetectAssignments_EdgeSpecialization_RejectsTwoSidedJitterAndInteriorSpill(
        int gridSize, double minX, double minY, double maxX, double maxY)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(gridSize, gridSize);
        var surroundingPads = grid.Pads.Select(pad => new CadPad(1000 + pad.RegularPadId, "Cell", "PAD", pad.Polygon));
        var spill = TestGeometryFactory.CreateCadPad(100, "PAD", minX, minY, maxX, maxY);
        var cad = new CadPadSet(surroundingPads.Append(spill).ToArray());
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings
        {
            FreeformAxisThreshold = 0.3,
            EnableFreeformEdgeSpecialization = true,
            EnableAutoDetectXy = true,
        };

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);

        Assert.Equal(grid.Pads.Count, matches.RegularToCad.Count);
        Assert.True(matches.CadToRegular[spill.Id].Count >= 2);
        Assert.Empty(assignments);
    }
}
