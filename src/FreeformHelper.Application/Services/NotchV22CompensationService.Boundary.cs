using System.Collections.Frozen;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchV22CompensationService
{
    internal static FrozenSet<int> BuildBoundaryRegularIndices(
        RegularGrid grid,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var padsByRowCol = grid.Pads.ToDictionary(pad => (pad.Row, pad.Col));
        var activeRegularPadIdSet = NormalizeActiveRegularPadIdSet(grid, activeRegularPadIds);
        return grid.Pads
            .Where(regularPad => IsBoundarySeed(regularPad, grid, padsByRowCol, activeRegularPadIdSet))
            .Select(static regularPad => regularPad.Index)
            .ToFrozenSet();
    }

    private static HashSet<int> BuildBoundaryRegularIndicesForOverlaps(
        IEnumerable<RegularPad> overlappedPads,
        RegularGrid grid,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        var padsByRowCol = grid.Pads.ToDictionary(pad => (pad.Row, pad.Col));
        var activeRegularPadIdSet = NormalizeActiveRegularPadIdSet(grid, activeRegularPadIds);
        return FilterBoundaryRegularIndices(
            overlappedPads,
            grid,
            padsByRowCol,
            activeRegularPadIdSet);
    }

    private static HashSet<int> FilterBoundaryRegularIndices(
        IEnumerable<RegularPad> overlappedPads,
        IReadOnlySet<int> boundaryRegularIndices)
    {
        return overlappedPads
            .Where(regularPad => boundaryRegularIndices.Contains(regularPad.Index))
            .Select(static regularPad => regularPad.Index)
            .ToHashSet();
    }

    private static HashSet<int> FilterBoundaryRegularIndices(
        IEnumerable<RegularPad> overlappedPads,
        RegularGrid grid,
        IReadOnlyDictionary<(int Row, int Col), RegularPad> padsByRowCol,
        IReadOnlySet<int>? activeRegularPadIdSet)
    {
        return overlappedPads
            .Where(regularPad => IsBoundarySeed(regularPad, grid, padsByRowCol, activeRegularPadIdSet))
            .Select(static regularPad => regularPad.Index)
            .ToHashSet();
    }

    private static bool IsBoundarySeed(
        RegularPad regular,
        RegularGrid grid,
        IReadOnlyDictionary<(int Row, int Col), RegularPad> padsByRowCol,
        IReadOnlySet<int>? activeRegularPadIdSet)
    {
        if (regular.Row <= 0 ||
            regular.Col <= 0 ||
            regular.Row >= grid.Rows - 1 ||
            regular.Col >= grid.Cols - 1)
        {
            return true;
        }

        return IsNeighborInactive(regular.Row - 1, regular.Col, padsByRowCol, activeRegularPadIdSet) ||
               IsNeighborInactive(regular.Row + 1, regular.Col, padsByRowCol, activeRegularPadIdSet) ||
               IsNeighborInactive(regular.Row, regular.Col - 1, padsByRowCol, activeRegularPadIdSet) ||
               IsNeighborInactive(regular.Row, regular.Col + 1, padsByRowCol, activeRegularPadIdSet);
    }

    private static bool IsNeighborInactive(
        int row,
        int col,
        IReadOnlyDictionary<(int Row, int Col), RegularPad> padsByRowCol,
        IReadOnlySet<int>? activeRegularPadIdSet)
    {
        if (!padsByRowCol.TryGetValue((row, col), out var neighbor))
        {
            return true;
        }

        if (activeRegularPadIdSet is null)
        {
            return false;
        }

        return !activeRegularPadIdSet.Contains(neighbor.RegularPadId);
    }

    private static HashSet<int>? NormalizeActiveRegularPadIdSet(
        RegularGrid targetGrid,
        IReadOnlySet<int>? configuredActiveRegularIds)
    {
        if (configuredActiveRegularIds is null || configuredActiveRegularIds.Count == 0)
        {
            return null;
        }

        var mapped = targetGrid.Pads
            .Select(static pad => pad.RegularPadId)
            .Where(configuredActiveRegularIds.Contains)
            .ToHashSet();
        return mapped.Count > 0 ? mapped : null;
    }

    private sealed record OverlappedRegularInfo(RegularPad Pad, double OverlapArea);

    private sealed record StageAOverlapResult(
        double RawToRegularRatio,
        double OverlapAreaTotal,
        double OverlappedRegularAreaTotal,
        int OverlapRegularCount,
        IReadOnlyList<OverlappedRegularInfo> OverlappedRegulars);

    private sealed record StageCEvaluationResult(
        IReadOnlyList<Polygon2> ToFullSeedRegionPolygons,
        IReadOnlyList<Polygon2> ToFullCandidateRegionPolygons,
        IReadOnlyList<Polygon2> ToFullRegionPolygons,
        IReadOnlyList<NotchV22RegularDebugInfo> RegularDebugInfos,
        NotchV22StageCSubphaseTimings StageCBreakdown);
}
