using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22CompensationServiceTests
{
    private static readonly double[] Grid10 = [10.0];
    private static readonly double[] Grid10x2 = [10.0, 10.0];
    private static readonly double[] Grid10x20 = [10.0, 20.0];
    private static readonly double[] Grid10x3 = [10.0, 10.0, 10.0];

    [Fact]
    public void BuildAllocations_UsesAwayFromZeroAtV21ThresholdHalfStep()
    {
        var grid = TestInfrastructure.TestGeometryFactory.CreateRegularGrid(rows: 1, cols: 1);
        var cad = TestInfrastructure.TestGeometryFactory.CreateCadPad(800, "AA", 0, 0, 256, 1);

        var allocation = Assert.Single(NotchAllocationService.BuildAllocations(cad, grid));

        Assert.Equal(1.0 / 256.0, allocation.Ratio, 12);
        Assert.Equal(1, allocation.Q7);
    }

    [Fact]
    public void HasQ7PositiveAllocationInIc_MatchesBuildAllocationsAcrossIcAndQ7Boundary()
    {
        var grid = TestInfrastructure.TestGeometryFactory.CreateRegularGrid(
            rows: 1,
            cols: 2,
            cellWidth: 10,
            cellHeight: 10);
        grid.Pads[0].IcIndex = 0;
        grid.Pads[1].IcIndex = 1;
        var crossIc = TestInfrastructure.TestGeometryFactory.CreateCadPad(801, "AA", 5, 0, 15, 10);
        var q7Zero = TestInfrastructure.TestGeometryFactory.CreateCadPad(802, "AA", 0, 0, 2570, 10);

        var crossIcAllocations = NotchAllocationService.BuildAllocations(crossIc, grid);
        var q7ZeroAllocations = NotchAllocationService.BuildAllocations(q7Zero, grid);

        Assert.Equal([0, 1], crossIcAllocations.Select(static allocation => allocation.Pad.IcIndex).Order().ToArray());
        Assert.True(NotchAllocationService.HasQ7PositiveAllocationInIc(crossIc, grid, icIndex: 0));
        Assert.True(NotchAllocationService.HasQ7PositiveAllocationInIc(crossIc, grid, icIndex: 1));
        Assert.Empty(q7ZeroAllocations);
        Assert.False(NotchAllocationService.HasQ7PositiveAllocationInIc(q7Zero, grid, icIndex: 0));
        Assert.False(NotchAllocationService.HasQ7PositiveAllocationInIc(q7Zero, grid, icIndex: 1));
    }

    [Fact]
    public void Compute_UsesSumOfOverlapOverRegularArea_ForUndoNf()
    {
        var grid = CreateGrid(Grid10x20, Grid10); // areas: 100, 200
        var cad = CreateCadRect(0, 0, 30, 10); // overlaps full two regular pads
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: false);

        Assert.Equal(2.0, result.ToRegularRatio, 6); // 100/100 + 200/200
        Assert.Equal(1.0, result.ToFullRatio, 6); // disabled
        Assert.Equal(2.0, result.CombinedRatio, 6);
        Assert.Empty(result.ToFullPolygons);
    }

    [Fact]
    public void Compute_WithPrecomputedAllocations_MatchesDirectGeometryPath()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        var cad = CreateCadRect(12, 0, 18, 20, id: 900);
        var allocations = NotchAllocationService.BuildAllocations(cad, grid);

        var direct = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true);
        var contextual = NotchV22CompensationService.Compute(
            NotchV22CompensationService.CreateContext(
                cad,
                grid,
                enableToRegular: true,
                enableToFull: true));
        var precomputed = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            precomputedAllocations: allocations);

        Assert.Equal(direct.ToRegularRatio, contextual.ToRegularRatio);
        Assert.Equal(direct.ToFullRatio, contextual.ToFullRatio);
        Assert.Equal(direct.CombinedRatio, contextual.CombinedRatio);
        Assert.Equal(
            direct.RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)),
            contextual.RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)));
        Assert.Equal(direct.ToRegularRatio, precomputed.ToRegularRatio, 6);
        Assert.Equal(direct.ToFullRatio, precomputed.ToFullRatio, 6);
        Assert.Equal(direct.CombinedRatio, precomputed.CombinedRatio, 6);
        Assert.Equal(direct.Stage3Area, precomputed.Stage3Area, 6);
        Assert.Equal(
            direct.RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)),
            precomputed.RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)));
    }

    [Fact]
    public void Compute_WithExplicitEmptyPrecomputedAllocations_DoesNotFallBackToGeometry()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10);

        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            precomputedAllocations: Array.Empty<NotchAllocation>());

        Assert.Equal(0.0, result.ToRegularRatio);
        Assert.Equal(0.0, result.CombinedRatio);
        Assert.Equal(0.0, result.OverlapAreaTotal);
        Assert.Equal(0, result.OverlapRegularCount);
        Assert.Empty(result.RegularDebugInfos);
    }

    [Fact]
    public void Compute_WithSharedBoundaryQueryContext_IsStableUnderParallelReuse()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        var cadPads = new[]
        {
            CreateCadRect(1, 1, 9, 9, id: 1001),
            CreateCadRect(11, 1, 19, 9, id: 1002),
            CreateCadRect(1, 11, 9, 19, id: 1003),
            CreateCadRect(11, 11, 19, 19, id: 1004),
        };
        var sharedBoundaryQueryContext = NotchV22CompensationService.CreateBoundaryQueryContext(
            cadPads,
            strictOverlapRatio: 0.001);
        var sequential = cadPads
            .Select(cad => NotchV22CompensationService.Compute(
                cad,
                grid,
                enableToRegular: true,
                enableToFull: true,
                allCadPads: cadPads,
                sharedBoundaryQueryContext: sharedBoundaryQueryContext))
            .ToArray();
        var parallel = new NotchV22CompensationResult[cadPads.Length];

        Parallel.For(0, cadPads.Length, index =>
        {
            parallel[index] = NotchV22CompensationService.Compute(
                cadPads[index],
                grid,
                enableToRegular: true,
                enableToFull: true,
                allCadPads: cadPads,
                sharedBoundaryQueryContext: sharedBoundaryQueryContext);
        });

        for (var index = 0; index < cadPads.Length; index++)
        {
            Assert.Equal(sequential[index].ToRegularRatio, parallel[index].ToRegularRatio, 6);
            Assert.Equal(sequential[index].ToFullRatio, parallel[index].ToFullRatio, 6);
            Assert.Equal(sequential[index].CombinedRatio, parallel[index].CombinedRatio, 6);
            Assert.Equal(
                sequential[index].RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)),
                parallel[index].RegularDebugInfos.Select(static info => (info.RegularPadId, info.ReachableArea, info.ToFullRuleCode, info.IsToFullApplied)));
        }
    }

    [Fact]
    public void Compute_WithSharedBoundaryQueryContext_DoesNotReadFallbackCandidatePool()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 1);
        var blocker = CreateCadRect(5, 0, 10, 10, id: 2);
        var candidatePool = new[] { cad, blocker };
        var sharedBoundaryQueryContext = NotchV22CompensationService.CreateBoundaryQueryContext(
            candidatePool,
            strictOverlapRatio: 0.001);

        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: new ThrowOnReadCadPool(),
            sharedBoundaryQueryContext: sharedBoundaryQueryContext);

        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal(0.5, result.ToRegularRatio, 6);
        Assert.Equal(1.0, result.ToFullRatio, 6);
        Assert.Equal(0.5, result.CombinedRatio, 6);
        Assert.Equal([1, 2], debug.OwnerCadPadIds);
        Assert.Equal(1, debug.BlockerCandidateCount);
    }

    [Fact]
    public void Compute_WithoutSharedBoundaryQueryContext_AddsCurrentCadWhenFallbackPoolOmitsIt()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 1);
        var blocker = CreateCadRect(5, 0, 10, 10, id: 2);

        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: [blocker]);

        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal(0.5, result.ToRegularRatio, 6);
        Assert.Equal(1.0, result.ToFullRatio, 6);
        Assert.Equal(0.5, result.CombinedRatio, 6);
        Assert.Equal([1, 2], debug.OwnerCadPadIds);
        Assert.Equal(1, debug.BlockerCandidateCount);
    }

    [Fact]
    public void Compute_WithoutSharedBoundaryQueryContext_NormalizesNullEmptyAndSameIdPools()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 1);
        var sameIdCad = CreateCadRect(20, 20, 30, 30, id: 1);
        IReadOnlyList<CadPad>?[] fallbackPools =
        [
            null,
            Array.Empty<CadPad>(),
            [cad],
        ];

        var snapshots = fallbackPools
            .Select(pool => NotchV22CompensationService.Compute(
                cad,
                grid,
                enableToRegular: true,
                enableToFull: true,
                allCadPads: pool))
            .Select(static result =>
            {
                var debug = Assert.Single(result.RegularDebugInfos);
                return (
                    result.ToRegularRatio,
                    result.ToFullRatio,
                    result.CombinedRatio,
                    OwnerCadPadIds: string.Join(',', debug.OwnerCadPadIds),
                    debug.BlockerCandidateCount);
            });

        Assert.All(snapshots, static snapshot => Assert.Equal((0.5, 2.0, 1.0, "1", 0), snapshot));

        var sameIdResult = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: [sameIdCad]);
        Assert.Empty(Assert.Single(sameIdResult.RegularDebugInfos).OwnerCadPadIds);
    }

    [Fact]
    public void Compute_WhenTimingReporterProvided_ReportsStageElapsedMs()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        var cad = CreateCadRect(12, 0, 18, 20, id: 901);
        NotchV22CompensationStageTimings? timings = null;

        _ = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            timingReporter: reported => timings = reported);

        var reportedTimings = Assert.IsType<NotchV22CompensationStageTimings>(timings);
        Assert.True(reportedTimings.StageAElapsedMs >= 0);
        Assert.True(reportedTimings.StageBElapsedMs >= 0);
        Assert.True(reportedTimings.StageCElapsedMs >= 0);
        Assert.True(reportedTimings.StageDElapsedMs >= 0);
        Assert.True(reportedTimings.TotalElapsedMs >= 0);
        var stageCBreakdown = Assert.IsType<NotchV22StageCSubphaseTimings>(reportedTimings.StageCBreakdown);
        Assert.True(stageCBreakdown.OwnerAndBlockerLookupElapsedMs >= 0);
        Assert.True(stageCBreakdown.ExactOccupiedAreaElapsedMs >= 0);
        Assert.True(stageCBreakdown.SourceCoverageElapsedMs >= 0);
        Assert.True(stageCBreakdown.ReachabilityElapsedMs >= 0);
        Assert.True(stageCBreakdown.RuleDecisionElapsedMs >= 0);
        Assert.True(stageCBreakdown.PreviewAndDebugElapsedMs >= 0);
        Assert.True(stageCBreakdown.TotalElapsedMs >= 0);
    }

    [Fact]
    public void Compute_ToFullEnabled_UsesAreaBasedUpperBound()
    {
        var grid = CreateGrid(Grid10, Grid10); // area 100
        var cad = CreateCadRect(0, 0, 5, 10); // area 50
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(0.5, result.ToRegularRatio, 6); // 50/100
        Assert.Equal(2.0, result.ToFullRatio, 6); // 100/50
        Assert.Equal(1.0, result.CombinedRatio, 6); // 0.5 * 2.0
        var toFull = Assert.Single(result.ToFullPolygons);
        Assert.Equal(100.0, toFull.Area(), 6);
    }

    [Fact]
    public void Compute_ToFullEnabled_CompletelyBlockedByOtherCads_StaysAtOneHundredPercent()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(4, 4, 6, 6, id: 1); // area 4

        var blockers = new[]
        {
            cad,
            CreateCadRect(0, 0, 4, 10, id: 2),
            CreateCadRect(6, 0, 10, 10, id: 3),
            CreateCadRect(4, 0, 6, 4, id: 4),
            CreateCadRect(4, 6, 6, 10, id: 5),
        };
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true, allCadPads: blockers);

        Assert.Equal(0.04, result.ToRegularRatio, 6); // 4/100
        Assert.Equal(1.0, result.ToFullRatio, 6);
        Assert.Equal(0.04, result.CombinedRatio, 6);
    }

    [Fact]
    public void Compute_ToFullEnabled_OnlyCountsBoundaryPadsThatActuallyOverlap()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        var cad = CreateCadRect(0, 0, 5, 10); // overlaps only one boundary regular pad by 50%
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(0.5, result.ToRegularRatio, 6); // 50/100
        Assert.Equal(2.0, result.ToFullRatio, 6); // one overlapped boundary regular area 100 / cad area 50
        Assert.Equal(1.0, result.CombinedRatio, 6);
        var toFull = Assert.Single(result.ToFullPolygons);
        Assert.Equal(100.0, toFull.Area(), 6);
    }

    [Fact]
    public void Compute_ToFullEnabled_MultiOwnerRegular_UsesConnectedSafeFillWithoutCrossingForeignOwner()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 3, 3, 7, id: 10); // area 12
        var blocker = CreateCadRect(5, 0, 10, 10, id: 11); // blocks right-side expansion
        var allCadPads = new[] { cad, blocker };
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true, allCadPads: allCadPads);

        Assert.Equal(0.12, result.ToRegularRatio, 6); // 12/100
        Assert.True(result.ToFullRatio > 1.0);
        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.True(debug.IsToFullApplied);
        Assert.Equal("EXPAND_SHARED_REACHABLE", debug.ToFullRuleCode);
        Assert.NotEmpty(result.ToFullPolygons);
        Assert.All(
            result.ToFullPolygons,
            polygon => Assert.True(polygon.Bounds.MaxX <= blocker.Bounds.MinX + 1e-6));
    }

    [Fact]
    public void Compute_ToFullEnabled_WithManyCadPads_KeepsSameSharedReachableBehavior()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 200);
        var blocker = CreateCadRect(6, 0, 10, 10, id: 201);
        var allCadPads = new List<CadPad> { cad, blocker };
        for (var i = 0; i < 80; i++)
        {
            var offset = 200 + (i * 20);
            allCadPads.Add(CreateCadRect(offset, 200, offset + 5, 205, id: 300 + i));
        }
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true, allCadPads: allCadPads);

        Assert.Equal(0.5, result.ToRegularRatio, 6);
        Assert.True(result.ToFullRatio > 1.0);
        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal("EXPAND_SHARED_REACHABLE", debug.ToFullRuleCode);
        Assert.True(debug.IsToFullApplied);
    }

    [Fact]
    public void Compute_ToFullEnabled_ThinOverlap_IsNotDroppedBySampling()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 4.98, 5, 5.02, id: 12); // very thin strip, area 0.2
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.True(result.ToFullRatio > 100.0);
        var expanded = Assert.Single(result.ToFullPolygons);
        Assert.Equal(100.0, expanded.Area(), 1);
        Assert.Single(result.RegularDebugInfos);
        Assert.True(result.RegularDebugInfos[0].SourceArea > 0.0);
        Assert.True(result.RegularDebugInfos[0].ReachableArea >= result.RegularDebugInfos[0].SourceArea);
    }

    [Fact]
    public void Compute_WhenBoundaryVirtualAreaCapEnabled_LimitsStage3Gain()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 2, 10, id: 120); // area 20, full ToFull would expand to 100
        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            enableBoundaryVirtualAreaCap: true,
            boundaryVirtualAreaCapRatio: 1.0);

        Assert.Equal(0.2, result.ToRegularRatio, 6);
        Assert.Equal(2.0, result.ToFullRatio, 6); // stage3 effective area is 20 inside + 20 capped virtual
        Assert.Equal(0.4, result.CombinedRatio, 6);
        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal(100.0, debug.ReachableArea, 6);
        Assert.Equal(40.0, debug.Stage3EffectiveArea, 6);
    }

    [Fact]
    public void Compute_ToFullEnabled_SeedUsesPolygonOverlap_NotBoundingBox()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = new CadPad(
            id: 13,
            layer: "C13",
            name: "PAD13",
            polygon: new Polygon2(new[]
            {
                new Point2(0, 0),
                new Point2(10, 0),
                new Point2(0, 10),
            }));
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        var seedArea = result.ToFullSeedPolygons.Sum(polygon => polygon.Area());
        Assert.InRange(seedArea, 40.0, 80.0); // triangle overlap ~50, should not inflate to bbox area 100.
    }

    [Fact]
    public void Compute_ToFullEnabled_WhenRegularIsAlreadyFilledByAdjacentCadPads_DisablesExpansion()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 9.96, 10, id: 70);
        var neighbor = CreateCadRect(9.96, 0, 10, 10, id: 71);
        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: new[] { cad, neighbor });

        Assert.Equal(0.996, result.ToRegularRatio, 6);
        Assert.Equal(1.0, result.ToFullRatio, 6);
        Assert.False(result.IsToFullEnabled);
        Assert.Empty(result.ToFullPolygons);

        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.False(debug.IsToFullApplied);
        Assert.Equal("NO_EXPANSION_NEEDED", debug.ToFullRuleCode);
    }

    [Fact]
    public void Compute_ToFullEnabled_CenterRegularPad_ExpandsWhenSoleOwnerHasClearPath()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        var cad = CreateCadRect(12, 12, 18, 18, id: 20); // fully inside center regular (row=1,col=1)
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(0.36, result.ToRegularRatio, 6); // 36 / 100
        Assert.Equal(100.0 / 36.0, result.ToFullRatio, 6);
        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.False(debug.IsBoundaryRegular);
        Assert.True(debug.IsToFullApplied);
        Assert.Equal("EXPAND_CLEAR_PATH", debug.ToFullRuleCode);
    }

    [Fact]
    public void Compute_ToFullEnabled_BoundaryCad_AlsoExpandsItsOverlappedInteriorRegulars()
    {
        var grid = CreateGrid(
            Grid10,
            Grid10x3);
        var cad = CreateCadRect(0, 0, 5, 20, id: 30); // overlaps top boundary + middle interior regular by 50% each
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(1.0, result.ToRegularRatio, 6); // 50/100 + 50/100
        Assert.Equal(2.0, result.ToFullRatio, 6); // both overlapped regulars expand to full area
        Assert.Equal(2.0, result.CombinedRatio, 6);
        var expanded = Assert.Single(result.ToFullPolygons);
        Assert.Equal(200.0, expanded.Area(), 3);
    }

    [Fact]
    public void Compute_ToFullEnabled_ConnectedInteriorRegularAlsoExpands_WhenSafeFillExists()
    {
        var grid = CreateGrid(
            Grid10x3,
            Grid10x3);
        // Overlaps top-middle (boundary) + center-middle (interior), each by 60%.
        var cad = CreateCadRect(12, 0, 18, 20, id: 31);
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true);

        Assert.Equal(1.2, result.ToRegularRatio, 6); // 60/100 + 60/100
        Assert.Equal(5.0 / 3.0, result.ToFullRatio, 6); // both regulars expand to full area: stage3 area=200, cad area=120
        Assert.Equal(2.0, result.CombinedRatio, 6);

        Assert.Equal(2, result.RegularDebugInfos.Count);
        var boundary = Assert.Single(result.RegularDebugInfos, info => info.Row == 0 && info.Col == 1);
        var interior = Assert.Single(result.RegularDebugInfos, info => info.Row == 1 && info.Col == 1);

        Assert.True(boundary.IsBoundaryRegular);
        Assert.True(boundary.IsToFullApplied);
        Assert.False(interior.IsBoundaryRegular);
        Assert.True(interior.IsToFullApplied);
        Assert.Equal("EXPAND_CLEAR_PATH", interior.ToFullRuleCode);
    }

    [Fact]
    public void Compute_ToFullEnabled_MixedBoundaryCandidates_UseClearAndSharedPathsWithoutOverlap()
    {
        var grid = CreateGrid(
            Grid10x2,
            Grid10);
        var cad = CreateCadRect(0, 0, 15, 5, id: 40); // overlaps reg0=50, reg1=25
        var blocker = CreateCadRect(12, 0, 20, 5, id: 41); // overlaps reg1 only => make reg1 multi-owner
        var allCadPads = new[] { cad, blocker };
        var result = NotchV22CompensationService.Compute(cad, grid, enableToRegular: true, enableToFull: true, allCadPads: allCadPads);
        var blockerResult = NotchV22CompensationService.Compute(blocker, grid, enableToRegular: true, enableToFull: true, allCadPads: allCadPads);

        Assert.Equal(0.75, result.ToRegularRatio, 6); // (50+25)/100
        Assert.True(result.ToFullRatio > 1.0);

        Assert.Equal(2, result.RegularDebugInfos.Count);
        var reg0 = Assert.Single(result.RegularDebugInfos, info => info.Col == 0);
        var reg1 = Assert.Single(result.RegularDebugInfos, info => info.Col == 1);

        Assert.True(reg0.IsToFullBoundaryCandidate);
        Assert.True(reg0.IsToFullApplied);
        Assert.Equal("EXPAND_CLEAR_PATH", reg0.ToFullRuleCode);

        Assert.True(reg1.IsToFullBoundaryCandidate);
        Assert.True(reg1.IsToFullApplied);
        Assert.Equal("EXPAND_SHARED_REACHABLE", reg1.ToFullRuleCode);
        Assert.True(ComputeRectangleIntersectionArea(result.ToFullPolygons, blockerResult.ToFullPolygons) <= 1e-6);
    }

    [Fact]
    public void Compute_ToFullRuleEngineToggle_KeepsLegacyGateResultEquivalent()
    {
        var grid = CreateGrid(
            Grid10x2,
            Grid10);
        var cad = CreateCadRect(0, 0, 15, 5, id: 50);
        var blocker = CreateCadRect(12, 0, 20, 5, id: 51);
        var allPads = new[] { cad, blocker };
        var enginePath = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: allPads,
            enableToFullRuleEngine: true,
            enableToFullRuleTrace: false);
        var legacyPath = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: allPads,
            enableToFullRuleEngine: false,
            enableToFullRuleTrace: false);

        Assert.Equal(enginePath.ToRegularRatio, legacyPath.ToRegularRatio, 6);
        Assert.Equal(enginePath.ToFullRatio, legacyPath.ToFullRatio, 6);
        Assert.Equal(enginePath.CombinedRatio, legacyPath.CombinedRatio, 6);
        Assert.Equal(
            enginePath.RegularDebugInfos.Select(static info => info.ToFullRuleCode),
            legacyPath.RegularDebugInfos.Select(static info => info.ToFullRuleCode));
        Assert.All(enginePath.RegularDebugInfos, static info => Assert.Empty(info.ToFullRuleTrace));
        Assert.All(legacyPath.RegularDebugInfos, static info => Assert.Empty(info.ToFullRuleTrace));
    }

    [Fact]
    public void Compute_ToFullRuleTraceEnabled_EmitsRuleTraceEntries()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 60);
        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            enableToFullRuleEngine: true,
            enableToFullRuleTrace: true);

        var debug = Assert.Single(result.RegularDebugInfos);
        var first = Assert.Single(debug.ToFullRuleTrace, static trace => trace.Rule == "switch.enableToFull");
        Assert.True(first.Passed);
    }

    [Fact]
    public void Compute_ToFullRuleTraceEnabledWithLegacyPath_ReportsLegacyTraceStep()
    {
        var grid = CreateGrid(Grid10, Grid10);
        var cad = CreateCadRect(0, 0, 5, 10, id: 61);
        var result = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            enableToFullRuleEngine: false,
            enableToFullRuleTrace: true);

        var debug = Assert.Single(result.RegularDebugInfos);
        var legacyTrace = Assert.Single(debug.ToFullRuleTrace);
        Assert.Equal("legacy.inlineGate", legacyTrace.Rule);
        Assert.Contains(debug.ToFullRuleCode, legacyTrace.Detail);
    }

    private static RegularGrid CreateGrid(double[] widths, double[] heights)
    {
        var xEdges = BuildEdges(widths);
        var yEdges = BuildEdges(heights);
        var rows = heights.Length;
        var cols = widths.Length;

        var pads = new List<RegularPad>(rows * cols);
        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var x0 = xEdges[col];
                var x1 = xEdges[col + 1];
                var y0 = yEdges[row];
                var y1 = yEdges[row + 1];
                var polygon = new Polygon2(new[]
                {
                    new Point2(x0, y0),
                    new Point2(x1, y0),
                    new Point2(x1, y1),
                    new Point2(x0, y1),
                });
                pads.Add(new RegularPad(row, col, index, polygon)
                {
                    DiffIndex = index,
                    IcIndex = 0
                });
                index++;
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static double[] BuildEdges(double[] sizes)
    {
        var edges = new double[sizes.Length + 1];
        for (var i = 0; i < sizes.Length; i++)
        {
            edges[i + 1] = edges[i] + sizes[i];
        }

        return edges;
    }

    private static CadPad CreateCadRect(double minX, double minY, double maxX, double maxY, int id = 1)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(minX, minY),
            new Point2(maxX, minY),
            new Point2(maxX, maxY),
            new Point2(minX, maxY),
        });

        return new CadPad(id, $"C{id}", "PAD", polygon);
    }

    private static double ComputeRectangleIntersectionArea(
        IReadOnlyList<Polygon2> a,
        IReadOnlyList<Polygon2> b)
    {
        var area = 0.0;
        foreach (var left in a)
        {
            var leftBounds = left.Bounds;
            foreach (var right in b)
            {
                var rightBounds = right.Bounds;
                var overlapWidth = Math.Min(leftBounds.MaxX, rightBounds.MaxX) - Math.Max(leftBounds.MinX, rightBounds.MinX);
                var overlapHeight = Math.Min(leftBounds.MaxY, rightBounds.MaxY) - Math.Max(leftBounds.MinY, rightBounds.MinY);
                if (overlapWidth <= 0 || overlapHeight <= 0)
                {
                    continue;
                }

                area += overlapWidth * overlapHeight;
            }
        }

        return area;
    }

    private sealed class ThrowOnReadCadPool : IReadOnlyList<CadPad>
    {
        public int Count => throw UnexpectedRead();

        public CadPad this[int index] => throw UnexpectedRead();

        public IEnumerator<CadPad> GetEnumerator() => throw UnexpectedRead();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => throw UnexpectedRead();

        private static InvalidOperationException UnexpectedRead() => new("Fallback candidate pool was read.");
    }
}
