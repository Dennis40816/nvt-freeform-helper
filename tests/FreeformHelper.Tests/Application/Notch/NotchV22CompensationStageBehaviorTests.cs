// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22CompensationStageBehaviorTests
{
    [Fact]
    public void Compute_PrecomputedZeroOverlap_DoesNotCreateATargetOrChangeCompensation()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(101, "signal", 0, 0, 5, 10);
        var positive = Assert.Single(NotchAllocationService.BuildAllocations(cad, grid));
        // A caller can supply dense allocation evidence, including a regular with no overlap.
        NotchAllocation[] allocations = [new(grid.Pads[1], Ratio: 0.0, Q7: 0), positive];

        var result = NotchV22CompensationService.Compute(
            cad, grid, enableToRegular: true, enableToFull: true, precomputedAllocations: allocations);
        var resolved = NotchV22ResolvedResultService.Build(cad, result, strictOverlapRatio: 0.001);

        Assert.Equal(1, result.OverlapRegularCount);
        Assert.Equal(50.0, result.OverlapAreaTotal, 6);
        Assert.Equal(100.0, result.OverlappedRegularAreaTotal, 6);
        Assert.Equal(0.5, result.ToRegularRatio, 6);
        Assert.Equal(2.0, result.ToFullRatio, 6);
        Assert.Equal(1.0, result.CombinedRatio, 6);
        Assert.Equal(grid.Pads[0].RegularPadId, Assert.Single(result.RegularDebugInfos).RegularPadId);
        Assert.Equal(grid.Pads[0].DiffIndex, Assert.Single(resolved.TargetAllocation.Targets).DiffIndex);
        Assert.Equal(100.0, Assert.Single(result.ToFullCandidatePolygons).Area(), 6);
    }

    [Fact]
    public void Compute_MissingOrNonFiniteVirtualAreaCap_UsesOneInsideAreaAsTheCap()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(102, "signal", 0, 0, 2, 10);
        double?[] configuredRatios = [null, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

        foreach (var configuredRatio in configuredRatios)
        {
            var normalized = NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(configuredRatio);
            var result = NotchV22CompensationService.Compute(
                cad, grid, enableToRegular: true, enableToFull: true,
                enableBoundaryVirtualAreaCap: true, boundaryVirtualAreaCapRatio: configuredRatio ?? normalized);

            Assert.Equal(1.0, normalized);
            Assert.Equal(40.0, result.Stage3Area, 6);
            Assert.Equal(2.0, result.ToFullRatio, 6);
            Assert.Equal(0.4, result.CombinedRatio, 6);
            var debug = Assert.Single(result.RegularDebugInfos);
            Assert.True(debug.IsToFullApplied);
            Assert.Equal(100.0, debug.ReachableArea, 6);
            Assert.Equal(40.0, debug.Stage3EffectiveArea, 6);
        }
    }

    [Fact]
    public void Compute_VirtualAreaCapOutsideSupportedRange_ClampsGainWithoutChangingReachability()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(103, "signal", 0, 0, 0.5, 10);
        (double Ratio, double NormalizedRatio, double Stage3Area)[] cases = [(-0.5, 0.0, 5.0), (12.0, 10.0, 55.0)];

        foreach (var (ratio, normalizedRatio, stage3Area) in cases)
        {
            var result = NotchV22CompensationService.Compute(
                cad, grid, enableToRegular: true, enableToFull: true,
                enableBoundaryVirtualAreaCap: true, boundaryVirtualAreaCapRatio: ratio);

            Assert.Equal(normalizedRatio, NotchV22CompensationService.NormalizeBoundaryVirtualAreaCapRatio(ratio));
            Assert.Equal(stage3Area, result.Stage3Area, 6);
            Assert.Equal(stage3Area / 5.0, result.ToFullRatio, 6);
            Assert.Equal(stage3Area / 100.0, result.CombinedRatio, 6);
            var debug = Assert.Single(result.RegularDebugInfos);
            Assert.True(debug.IsToFullApplied);
            Assert.Equal(100.0, debug.ReachableArea, 6);
            Assert.Equal(stage3Area, debug.Stage3EffectiveArea, 6);
            Assert.Equal(5.0, Assert.Single(result.ToFullSeedPolygons).Area(), 6);
            Assert.Equal(100.0, Assert.Single(result.ToFullCandidatePolygons).Area(), 6);
        }
    }
}
