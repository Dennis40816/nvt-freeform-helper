// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22CompensationReachabilityBehaviorTests
{
    [Fact]
    public void Compute_ForeignStripSeparatesFreeRegion_ExpandsOnlyOnTheSourceSide()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var target = TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 2, 10);
        var wall = TestGeometryFactory.CreateCadPad(2, "PAD", 4, 0, 6, 10);

        var result = NotchV22CompensationService.Compute(
            target, grid, enableToRegular: true, enableToFull: true, allCadPads: [target, wall]);

        // The gap from x=2 to x=4 is shared equally. Free space beyond the wall cannot reach the target.
        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal(20.0, debug.SourceArea, 9);
        Assert.Equal(30.0, debug.ReachableArea, 9);
        Assert.Equal(70.0, debug.BlockedArea, 9);
        Assert.True(debug.HasDirectionalBlocker);
        Assert.True(debug.IsToFullApplied);
        Assert.Equal("EXPAND_SHARED_REACHABLE", debug.ToFullRuleCode);
        Assert.Equal(new Rect2(0, 0, 3, 10), Assert.Single(result.ToFullPolygons).Bounds);
        Assert.Equal(30.0, result.Stage3Area, 9);
        Assert.Equal(1.5, result.ToFullRatio, 9);
        Assert.Equal(0.3, result.CombinedRatio, 9);
    }
}
