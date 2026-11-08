// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class FreeformOverlapOwnershipBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetectAssignments_CompetingAxes_PrefersGreaterCadCoverageRegardlessOfInputOrder(bool verticalFirst)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 2, cols: 2);
        var horizontal = TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 2, 1);
        var vertical = TestGeometryFactory.CreateCadPad(200, "PAD", 0, 0, 1, 1.5);
        var cad = new CadPadSet(verticalFirst ? [vertical, horizontal] : [horizontal, vertical]);
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings { FreeformAxisThreshold = 0.3 };

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);
        FreeformDetector.ApplyAssignments(grid, assignments);

        Assert.Equal(3, assignments.Count);
        Assert.Equal(FreeformType.YWay, grid.GetPad(0, 0).Freeform);
        Assert.Equal(FreeformType.XWay, grid.GetPad(0, 1).Freeform);
        Assert.Equal(FreeformType.YWay, grid.GetPad(1, 0).Freeform);
        Assert.Equal(FreeformType.None, grid.GetPad(1, 1).Freeform);
    }

    [Fact]
    public void DetectAssignments_ThinCopperWithLowRegularCoverage_KeepsMajorCadLinks()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(rows: 1, cols: 3);
        var cad = new CadPadSet([TestGeometryFactory.CreateCadPad(100, "PAD", 0, 0, 3, 0.2)]);
        var matches = PadMatcher.Match(cad, grid);
        var settings = new MatchingSettings { FreeformAxisThreshold = 0.3 };

        var assignments = FreeformDetector.DetectAssignments(cad, grid, settings, matches);

        Assert.Equal(3, matches.Links.Count);
        Assert.All(matches.Links, link =>
        {
            Assert.Equal(0.2, link.RegularCoverage, 9);
            Assert.Equal(1d / 3d, link.CadCoverage, 9);
        });
        Assert.Equal(3, assignments.Count);
        Assert.All(grid.Pads, pad => Assert.Equal(FreeformType.XWay, assignments[pad.RegularPadId]));
    }
}
