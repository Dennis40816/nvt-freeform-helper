// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FreeformUnequalAxisBehaviorTests
{
    [Theory]
    [InlineData(true, FreeformType.XWay)]
    [InlineData(false, FreeformType.YWay)]
    public void DetectAssignments_XyEnabledWithWeakSecondarySpill_TagsOnlyMajorCellsOnTheDominantAxis(
        bool horizontal, FreeformType expected)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 2, cols: 2);
        var cad = new CadPadSet(
        [
            TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, horizontal ? 2 : 1.25, horizontal ? 1.25 : 2),
        ]);
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings
        {
            FreeformAxisThreshold = 0.3,
            EnableAutoDetectXy = true,
            EnableFreeformEdgeSpecialization = false,
        };

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);
        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.Equal(4, matches.Links.Count);
        Assert.Equal(2, assignments.Count);
        Assert.All(grid.Pads, pad => Assert.Equal(
            (horizontal ? pad.Row == 0 : pad.Col == 0) ? expected : FreeformType.None,
            pad.Freeform));
    }
}
