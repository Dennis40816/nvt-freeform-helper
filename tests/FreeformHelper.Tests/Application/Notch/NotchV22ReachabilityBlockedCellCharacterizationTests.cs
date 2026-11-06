using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Characterizes the blocked-cell behavior of the To Full reachability sampling
/// (40 x 40 cells per regular pad) through the public Compute entry point.
/// The regular grid is a single 10 x 10 pad, so one cell is 0.25 x 0.25 (area 0.0625).
/// </summary>
public sealed class NotchV22ReachabilityBlockedCellCharacterizationTests
{
    private const double CellArea = 0.0625;
    private const int TotalCells = 1600;

    [Fact]
    public void Compute_BlockerOnOppositeSide_SplitsFreeCellsByNearestOwner()
    {
        // Target columns 0..11, blocker columns 28..39, free columns 12..27.
        // Columns 12..19 are nearer to the target; columns 20..27 are nearer to the blocker.
        var target = CreateCadRect(0, 0, 3, 10, id: 1);
        var blocker = CreateCadRect(7, 0, 10, 10, id: 2);

        var (result, debug) = ComputeSingleRegular(target, blocker);

        Assert.Equal(480, debug.SourceCellCount);
        Assert.Equal(800, debug.BlockedCellCount);
        Assert.Equal(800, debug.ReachableCellCount);
        Assert.Equal(TotalCells, debug.TotalCellCount);
        Assert.Equal(800 * CellArea, debug.BlockedArea, 9);
        Assert.True(debug.HasDirectionalBlocker);
        Assert.Equal(1, debug.BlockerCandidateCount);
        AssertAppliedReachability(result, debug, overlapArea: 30.0, assignedFreeCells: 320);
        Assert.All(result.ToFullPolygons, polygon => Assert.True(polygon.Bounds.MaxX <= 5.0 + 1e-9));
    }

    [Fact]
    public void Compute_EquidistantFreeColumn_IsCountedAsBlocked()
    {
        // Target columns 0..11, blocker columns 27..39. Column 19 is equidistant (8 steps) from both owners.
        var target = CreateCadRect(0, 0, 3, 10, id: 1);
        var blocker = CreateCadRect(6.75, 0, 10, 10, id: 2);

        var (result, debug) = ComputeSingleRegular(target, blocker);

        Assert.Equal(480, debug.SourceCellCount);
        Assert.Equal(840, debug.BlockedCellCount);
        Assert.Equal(760, debug.ReachableCellCount);
        Assert.Equal(840 * CellArea, debug.BlockedArea, 9);
        AssertAppliedReachability(result, debug, overlapArea: 30.0, assignedFreeCells: 280);
    }

    [Fact]
    public void Compute_BlockerAboveTarget_MatchesHorizontalCellCounts()
    {
        var target = CreateCadRect(0, 0, 10, 3, id: 1);
        var blocker = CreateCadRect(0, 7, 10, 10, id: 2);

        var (result, debug) = ComputeSingleRegular(target, blocker);

        Assert.Equal(480, debug.SourceCellCount);
        Assert.Equal(800, debug.BlockedCellCount);
        Assert.Equal(800, debug.ReachableCellCount);
        AssertAppliedReachability(result, debug, overlapArea: 30.0, assignedFreeCells: 320);
        Assert.All(result.ToFullPolygons, polygon => Assert.True(polygon.Bounds.MaxY <= 5.0 + 1e-9));
    }

    [Fact]
    public void Compute_BlockerInOppositeCorner_UsesManhattanDistanceAroundTheBlock()
    {
        var target = CreateCadRect(0, 0, 4, 4, id: 1);
        var blocker = CreateCadRect(6, 6, 10, 10, id: 2);

        var (result, debug) = ComputeSingleRegular(target, blocker);

        Assert.Equal(256, debug.SourceCellCount);
        Assert.Equal(820, debug.BlockedCellCount);
        Assert.Equal(780, debug.ReachableCellCount);
        Assert.Equal(820 * CellArea, debug.BlockedArea, 9);
        AssertAppliedReachability(result, debug, overlapArea: 16.0, assignedFreeCells: 524);
    }

    [Fact]
    public void Compute_ContestedCellsOnly_HasNoSourceCellsAndKeepsOverlapArea()
    {
        // Identical polygons tie in every occupied cell, so no cell is owned by the target alone.
        var target = CreateCadRect(0, 0, 4, 10, id: 1);
        var twin = CreateCadRect(0, 0, 4, 10, id: 2);

        var (result, debug) = ComputeSingleRegular(target, twin);

        Assert.Equal(0, debug.SourceCellCount);
        Assert.Equal(640, debug.BlockedCellCount);
        Assert.Equal(0, debug.ReachableCellCount);
        Assert.Equal(TotalCells, debug.TotalCellCount);
        Assert.Equal(640 * CellArea, debug.BlockedArea, 9);
        Assert.Equal(debug.OverlapArea, debug.ReachableArea, 9);
        Assert.False(debug.IsToFullApplied);
        Assert.Empty(result.ToFullPolygons);
    }

    [Fact]
    public void Compute_NoForeignCellsInRegular_ExpandsToWholeRegular()
    {
        var target = CreateCadRect(0, 0, 3, 10, id: 1);
        var grid = CreateSingleRegularGrid();

        var result = NotchV22CompensationService.Compute(
            target,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: [target]);

        var debug = Assert.Single(result.RegularDebugInfos);
        Assert.Equal(480, debug.SourceCellCount);
        Assert.Equal(0, debug.BlockedCellCount);
        Assert.Equal(TotalCells, debug.ReachableCellCount);
        Assert.Equal(0.0, debug.BlockedArea, 9);
        Assert.False(debug.HasDirectionalBlocker);
        Assert.Equal(100.0, debug.ReachableArea, 9);
        var polygon = Assert.Single(result.ToFullPolygons);
        Assert.Equal(100.0, polygon.Area(), 9);
    }

    [Fact]
    public void Compute_SameInputsTwice_ProducesIdenticalResults()
    {
        var target = CreateCadRect(0, 0, 4, 4, id: 1);
        var blocker = CreateCadRect(6, 6, 10, 10, id: 2);

        var (first, firstDebug) = ComputeSingleRegular(target, blocker);
        var (second, secondDebug) = ComputeSingleRegular(target, blocker);

        // A record compares its list members by reference, so each list is compared by content below
        // and the records are compared with those members emptied.
        Assert.Equal(
            first with
            {
                ToFullSeedPolygons = [],
                ToFullCandidatePolygons = [],
                ToFullPolygons = [],
                RegularDebugInfos = []
            },
            second with
            {
                ToFullSeedPolygons = [],
                ToFullCandidatePolygons = [],
                ToFullPolygons = [],
                RegularDebugInfos = []
            });
        Assert.Equal(Vertices(first.ToFullSeedPolygons), Vertices(second.ToFullSeedPolygons));
        Assert.Equal(Vertices(first.ToFullCandidatePolygons), Vertices(second.ToFullCandidatePolygons));
        Assert.Equal(Vertices(first.ToFullPolygons), Vertices(second.ToFullPolygons));
        Assert.NotEmpty(first.ToFullPolygons);

        Assert.Equal(
            firstDebug with { OwnerCadPadIds = [], ToFullRuleTrace = [] },
            secondDebug with { OwnerCadPadIds = [], ToFullRuleTrace = [] });
        Assert.Equal(firstDebug.OwnerCadPadIds, secondDebug.OwnerCadPadIds);
        Assert.Equal(firstDebug.ToFullRuleTrace, secondDebug.ToFullRuleTrace);

        var (_, firstTracedDebug) = ComputeSingleRegular(target, blocker, enableToFullRuleTrace: true);
        var (_, secondTracedDebug) = ComputeSingleRegular(target, blocker, enableToFullRuleTrace: true);

        Assert.NotEmpty(firstTracedDebug.ToFullRuleTrace);
        Assert.Equal(firstTracedDebug.ToFullRuleTrace, secondTracedDebug.ToFullRuleTrace);
    }

    private static Point2[][] Vertices(IReadOnlyList<Polygon2> polygons)
    {
        return [.. polygons.Select(static polygon => polygon.Vertices.ToArray())];
    }

    private static void AssertAppliedReachability(
        NotchV22CompensationResult result,
        NotchV22RegularDebugInfo debug,
        double overlapArea,
        int assignedFreeCells)
    {
        var expectedReachableArea = overlapArea + (assignedFreeCells * CellArea);
        Assert.True(debug.IsToFullApplied);
        Assert.Equal(overlapArea, debug.OverlapArea, 9);
        Assert.Equal(overlapArea, debug.SourceArea, 9);
        Assert.Equal(expectedReachableArea, debug.ReachableArea, 9);
        Assert.Equal(
            debug.ReachableCellCount * CellArea,
            result.ToFullPolygons.Sum(static polygon => polygon.Area()),
            9);
    }

    private static (NotchV22CompensationResult Result, NotchV22RegularDebugInfo Debug) ComputeSingleRegular(
        CadPad target,
        CadPad blocker,
        bool enableToFullRuleTrace = false)
    {
        var result = NotchV22CompensationService.Compute(
            target,
            CreateSingleRegularGrid(),
            enableToRegular: true,
            enableToFull: true,
            allCadPads: [target, blocker],
            enableToFullRuleTrace: enableToFullRuleTrace);

        return (result, Assert.Single(result.RegularDebugInfos));
    }

    private static RegularGrid CreateSingleRegularGrid()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pad = new RegularPad(0, 0, 0, polygon)
        {
            DiffIndex = 0,
            IcIndex = 0,
        };

        return new RegularGrid(1, 1, [0.0, 10.0], [0.0, 10.0], [pad]);
    }

    private static CadPad CreateCadRect(double minX, double minY, double maxX, double maxY, int id)
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
}
