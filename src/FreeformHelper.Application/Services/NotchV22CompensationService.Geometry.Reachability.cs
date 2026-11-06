using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchV22CompensationService
{
    private static ToFullReachabilityResult ComputeReachabilityInRegular(
        CadPad targetCad,
        RegularPad regularPad,
        CadPad[] blockerCandidates,
        double overlapArea)
    {
        var blockerCandidateCount = blockerCandidates.Length;
        var bounds = regularPad.Bounds;
        var resolution = Math.Max(8, ReachabilityResolution);
        var cellWidth = bounds.Width / resolution;
        var cellHeight = bounds.Height / resolution;
        if (cellWidth <= AreaEpsilon || cellHeight <= AreaEpsilon)
        {
            return BuildDegenerateCellReachability(overlapArea, blockerCandidateCount);
        }

        var cellArea = cellWidth * cellHeight;
        var total = resolution * resolution;
        var allPads = new CadPad[blockerCandidates.Length + 1];
        allPads[0] = targetCad;
        Array.Copy(blockerCandidates, 0, allPads, 1, blockerCandidates.Length);
        var grid = new ReachabilityGridSpec(bounds, resolution, cellWidth, cellHeight, cellArea);
        var ownership = ClassifyCellOwners(allPads, grid);
        var sourceCount = ownership.SourceCount;
        var foreignOccupiedCount = ownership.ForeignOccupiedCount;

        var sourcePolygons = sourceCount > 0
            ? BuildVisitedCellPolygons(ownership.Source, bounds, resolution, cellWidth, cellHeight)
            : new List<Polygon2>(0);

        // Thin polygons can miss all sample points; preserve overlap-area lower bound.
        if (sourceCount == 0)
        {
            return BuildNoSourceCellReachability(overlapArea, blockerCandidateCount, ownership, grid, sourcePolygons);
        }

        // No foreign-occupied cells in this regular: expand to full regular bounds.
        if (foreignOccupiedCount == 0)
        {
            return BuildUnobstructedReachability(regularPad, overlapArea, blockerCandidateCount, ownership, grid, sourcePolygons);
        }

        var ownerDistances = ComputeOwnerDistances(allPads.Length, ownership, resolution);
        var reachable = ClassifyReachableCells(ownership, ownerDistances);

        var reachableArea = Math.Max(overlapArea, overlapArea + (reachable.AssignedFreeCount * cellArea));
        var polygons = BuildVisitedCellPolygons(reachable.CurrentReachable, bounds, resolution, cellWidth, cellHeight);
        return new ToFullReachabilityResult(
            ReachableArea: reachableArea,
            ReachablePolygons: polygons,
            SourcePolygons: sourcePolygons,
            SourceCellCount: sourceCount,
            BlockedCellCount: reachable.BlockedCount,
            ReachableCellCount: reachable.ReachableCount,
            TotalCellCount: total,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: reachable.BlockedCount * cellArea,
            BlockerCandidateCount: blockerCandidateCount);
    }

    private static ToFullReachabilityResult BuildDegenerateCellReachability(double overlapArea, int blockerCandidateCount)
    {
        return new ToFullReachabilityResult(
            ReachableArea: Math.Max(0.0, overlapArea),
            ReachablePolygons: Array.Empty<Polygon2>(),
            SourcePolygons: Array.Empty<Polygon2>(),
            SourceCellCount: 0,
            BlockedCellCount: 0,
            ReachableCellCount: 0,
            TotalCellCount: 0,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: 0.0,
            BlockerCandidateCount: blockerCandidateCount);
    }

    private static ToFullReachabilityResult BuildNoSourceCellReachability(
        double overlapArea,
        int blockerCandidateCount,
        ReachabilityCellOwnership ownership,
        ReachabilityGridSpec grid,
        IReadOnlyList<Polygon2> sourcePolygons)
    {
        return new ToFullReachabilityResult(
            ReachableArea: Math.Max(0.0, overlapArea),
            ReachablePolygons: Array.Empty<Polygon2>(),
            SourcePolygons: sourcePolygons,
            SourceCellCount: 0,
            BlockedCellCount: ownership.ForeignOccupiedCount,
            ReachableCellCount: 0,
            TotalCellCount: grid.Resolution * grid.Resolution,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: ownership.ForeignOccupiedCount * grid.CellArea,
            BlockerCandidateCount: blockerCandidateCount);
    }

    private static ToFullReachabilityResult BuildUnobstructedReachability(
        RegularPad regularPad,
        double overlapArea,
        int blockerCandidateCount,
        ReachabilityCellOwnership ownership,
        ReachabilityGridSpec grid,
        IReadOnlyList<Polygon2> sourcePolygons)
    {
        var total = grid.Resolution * grid.Resolution;
        return new ToFullReachabilityResult(
            ReachableArea: Math.Max(overlapArea, regularPad.Area),
            ReachablePolygons: new[] { ToRectPolygon(grid.Bounds) },
            SourcePolygons: sourcePolygons,
            SourceCellCount: ownership.SourceCount,
            BlockedCellCount: 0,
            ReachableCellCount: total,
            TotalCellCount: total,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: 0.0,
            BlockerCandidateCount: blockerCandidateCount);
    }

    private static ReachabilityCellOwnership ClassifyCellOwners(CadPad[] allPads, ReachabilityGridSpec grid)
    {
        var resolution = grid.Resolution;
        var source = new bool[resolution * resolution];
        var cellOwners = new int[resolution * resolution];
        var sourceCount = 0;
        var foreignOccupiedCount = 0;
        var areaTieEpsilon = Math.Max(AreaEpsilon, grid.CellArea * 1e-6);
        for (var row = 0; row < resolution; row++)
        {
            var y0 = grid.Bounds.MinY + (row * grid.CellHeight);
            var y1 = y0 + grid.CellHeight;
            for (var col = 0; col < resolution; col++)
            {
                var idx = (row * resolution) + col;
                var x0 = grid.Bounds.MinX + (col * grid.CellWidth);
                var x1 = x0 + grid.CellWidth;
                var cellOwner = ResolveCellOwner(allPads, new Rect2(x0, y0, x1, y1), areaTieEpsilon);
                cellOwners[idx] = cellOwner;

                if (cellOwner == ReachabilityTargetOwnerIndex)
                {
                    source[idx] = true;
                    sourceCount++;
                    continue;
                }

                if (cellOwner != ReachabilityFreeCell)
                {
                    foreignOccupiedCount++;
                }
            }
        }

        return new ReachabilityCellOwnership(source, cellOwners, sourceCount, foreignOccupiedCount);
    }

    private static int ResolveCellOwner(CadPad[] allPads, Rect2 cellRect, double areaTieEpsilon)
    {
        var bestOwnerIndex = ReachabilityFreeCell;
        var bestOwnerArea = 0.0;
        var hasOwner = false;
        var isTie = false;

        for (var ownerIndex = 0; ownerIndex < allPads.Length; ownerIndex++)
        {
            var ownerPad = allPads[ownerIndex];
            if (!ownerPad.Bounds.Intersects(cellRect))
            {
                continue;
            }

            var ownerArea = Polygon2.IntersectionAreaWithRect(ownerPad.Polygon, cellRect);
            if (ownerArea <= AreaEpsilon)
            {
                continue;
            }

            hasOwner = true;
            if (ownerArea > bestOwnerArea + areaTieEpsilon)
            {
                bestOwnerIndex = ownerIndex;
                bestOwnerArea = ownerArea;
                isTie = false;
                continue;
            }

            if (Math.Abs(ownerArea - bestOwnerArea) <= areaTieEpsilon)
            {
                isTie = true;
            }
        }

        return !hasOwner
            ? ReachabilityFreeCell
            : isTie
                ? ReachabilityContestedCell
                : bestOwnerIndex;
    }

    private static int[][] ComputeOwnerDistances(int ownerCount, ReachabilityCellOwnership ownership, int resolution)
    {
        var cellOwners = ownership.CellOwners;
        var total = resolution * resolution;
        var ownerDistances = new int[ownerCount][];
        var queue = new Queue<(int OwnerIndex, int CellIndex)>(Math.Max(ownership.SourceCount, ownerCount));
        for (var ownerIndex = 0; ownerIndex < ownerCount; ownerIndex++)
        {
            var distances = Enumerable.Repeat(-1, total).ToArray();
            ownerDistances[ownerIndex] = distances;
            for (var cellIndex = 0; cellIndex < total; cellIndex++)
            {
                if (cellOwners[cellIndex] != ownerIndex)
                {
                    continue;
                }

                distances[cellIndex] = 0;
                queue.Enqueue((ownerIndex, cellIndex));
            }
        }

        while (queue.Count > 0)
        {
            var (ownerIndex, idx) = queue.Dequeue();
            var row = idx / resolution;
            var col = idx % resolution;
            var currentDistance = ownerDistances[ownerIndex][idx];
            TryVisit(ownerIndex, currentDistance, row - 1, col);
            TryVisit(ownerIndex, currentDistance, row + 1, col);
            TryVisit(ownerIndex, currentDistance, row, col - 1);
            TryVisit(ownerIndex, currentDistance, row, col + 1);
        }

        return ownerDistances;

        void TryVisit(int ownerIndex, int currentDistance, int row, int col)
        {
            if (row < 0 || row >= resolution || col < 0 || col >= resolution)
            {
                return;
            }

            var neighbor = (row * resolution) + col;
            if (cellOwners[neighbor] != ReachabilityFreeCell)
            {
                return;
            }

            var distances = ownerDistances[ownerIndex];
            if (distances[neighbor] >= 0)
            {
                return;
            }

            distances[neighbor] = currentDistance + 1;
            queue.Enqueue((ownerIndex, neighbor));
        }
    }

    private static ReachableCellClassification ClassifyReachableCells(
        ReachabilityCellOwnership ownership,
        int[][] ownerDistances)
    {
        var total = ownership.Source.Length;
        var currentReachable = new bool[total];
        var reachableCount = 0;
        var blockedCount = 0;
        var assignedFreeCount = 0;
        for (var cellIndex = 0; cellIndex < total; cellIndex++)
        {
            if (ownership.Source[cellIndex])
            {
                currentReachable[cellIndex] = true;
                reachableCount++;
                continue;
            }

            if (ownership.CellOwners[cellIndex] != ReachabilityFreeCell)
            {
                blockedCount++;
                continue;
            }

            if (IsUniquelyNearestToTarget(ownerDistances, cellIndex))
            {
                currentReachable[cellIndex] = true;
                reachableCount++;
                assignedFreeCount++;
                continue;
            }

            blockedCount++;
        }

        return new ReachableCellClassification(currentReachable, reachableCount, blockedCount, assignedFreeCount);
    }

    private static bool IsUniquelyNearestToTarget(int[][] ownerDistances, int cellIndex)
    {
        var bestOwner = -1;
        var bestDistance = int.MaxValue;
        var isTie = false;
        for (var ownerIndex = 0; ownerIndex < ownerDistances.Length; ownerIndex++)
        {
            var distance = ownerDistances[ownerIndex][cellIndex];
            if (distance < 0)
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestOwner = ownerIndex;
                isTie = false;
            }
            else if (distance == bestDistance)
            {
                isTie = true;
            }
        }

        return bestOwner == ReachabilityTargetOwnerIndex && !isTie;
    }

    private static ToFullReachabilityResult ComputeSourceCoverageInRegular(
        CadPad targetCad,
        RegularPad regularPad,
        double overlapArea)
    {
        var normalizedOverlapArea = Math.Max(0.0, overlapArea);
        var bounds = regularPad.Bounds;
        if (normalizedOverlapArea <= AreaEpsilon)
        {
            return ToFullReachabilityResult.Disabled(normalizedOverlapArea);
        }

        if (normalizedOverlapArea >= regularPad.Area - AreaEpsilon)
        {
            var polygon = ToRectPolygon(bounds);
            return new ToFullReachabilityResult(
                ReachableArea: normalizedOverlapArea,
                ReachablePolygons: new[] { polygon },
                SourcePolygons: new[] { polygon },
                SourceCellCount: ReachabilityResolution * ReachabilityResolution,
                BlockedCellCount: 0,
                ReachableCellCount: ReachabilityResolution * ReachabilityResolution,
                TotalCellCount: ReachabilityResolution * ReachabilityResolution,
                SourceArea: normalizedOverlapArea,
                BlockedArea: 0.0,
                BlockerCandidateCount: 0);
        }

        var overlapPolygons = BuildCadOverlapPolygons(targetCad.Polygon, bounds);
        if (overlapPolygons.Length > 0)
        {
            return new ToFullReachabilityResult(
                ReachableArea: normalizedOverlapArea,
                ReachablePolygons: overlapPolygons,
                SourcePolygons: overlapPolygons,
                SourceCellCount: 0,
                BlockedCellCount: 0,
                ReachableCellCount: 0,
                TotalCellCount: 0,
                SourceArea: normalizedOverlapArea,
                BlockedArea: 0.0,
                BlockerCandidateCount: 0);
        }

        return ComputeSampledSourceCoverageInRegular(targetCad, regularPad, normalizedOverlapArea);
    }

    private static ToFullReachabilityResult ComputeSampledSourceCoverageInRegular(
        CadPad targetCad,
        RegularPad regularPad,
        double overlapArea)
    {
        var bounds = regularPad.Bounds;
        var resolution = Math.Max(8, ReachabilityResolution);
        var cellWidth = bounds.Width / resolution;
        var cellHeight = bounds.Height / resolution;
        if (cellWidth <= AreaEpsilon || cellHeight <= AreaEpsilon)
        {
            return ToFullReachabilityResult.Disabled(overlapArea);
        }

        var source = new bool[resolution * resolution];
        var sourceCount = 0;
        for (var row = 0; row < resolution; row++)
        {
            var y0 = bounds.MinY + (row * cellHeight);
            var y1 = y0 + cellHeight;
            for (var col = 0; col < resolution; col++)
            {
                var x0 = bounds.MinX + (col * cellWidth);
                var x1 = x0 + cellWidth;
                var cellRect = new Rect2(x0, y0, x1, y1);
                if (Polygon2.IntersectionAreaWithRect(targetCad.Polygon, cellRect) <= AreaEpsilon)
                {
                    continue;
                }

                source[(row * resolution) + col] = true;
                sourceCount++;
            }
        }

        var sourcePolygons = sourceCount > 0
            ? BuildVisitedCellPolygons(source, bounds, resolution, cellWidth, cellHeight)
            : new List<Polygon2>(0);
        return new ToFullReachabilityResult(
            ReachableArea: Math.Max(0.0, overlapArea),
            ReachablePolygons: sourcePolygons,
            SourcePolygons: sourcePolygons,
            SourceCellCount: sourceCount,
            BlockedCellCount: 0,
            ReachableCellCount: sourceCount,
            TotalCellCount: resolution * resolution,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: 0.0,
            BlockerCandidateCount: 0);
    }

    private static Polygon2 ToRectPolygon(Rect2 rect)
    {
        return new Polygon2(new[]
        {
            new Point2(rect.MinX, rect.MinY),
            new Point2(rect.MaxX, rect.MinY),
            new Point2(rect.MaxX, rect.MaxY),
            new Point2(rect.MinX, rect.MaxY),
        });
    }

    private static List<Polygon2> BuildVisitedCellPolygons(
        bool[] visited,
        Rect2 bounds,
        int resolution,
        double cellWidth,
        double cellHeight)
    {
        var polygons = new List<Polygon2>();
        for (var row = 0; row < resolution; row++)
        {
            var col = 0;
            while (col < resolution)
            {
                if (!visited[(row * resolution) + col])
                {
                    col++;
                    continue;
                }

                var start = col;
                while (col < resolution && visited[(row * resolution) + col])
                {
                    col++;
                }

                var end = col;
                var x0 = bounds.MinX + (start * cellWidth);
                var x1 = bounds.MinX + (end * cellWidth);
                var y0 = bounds.MinY + (row * cellHeight);
                var y1 = bounds.MinY + ((row + 1) * cellHeight);
                polygons.Add(new Polygon2(new[]
                {
                    new Point2(x0, y0),
                    new Point2(x1, y0),
                    new Point2(x1, y1),
                    new Point2(x0, y1),
                }));
            }
        }

        return polygons;
    }

    private sealed record ToFullReachabilityResult(
        double ReachableArea,
        IReadOnlyList<Polygon2> ReachablePolygons,
        IReadOnlyList<Polygon2> SourcePolygons,
        int SourceCellCount,
        int BlockedCellCount,
        int ReachableCellCount,
        int TotalCellCount,
        double SourceArea,
        double BlockedArea,
        int BlockerCandidateCount)
    {
        public static ToFullReachabilityResult Disabled(double overlapArea)
        {
            return new ToFullReachabilityResult(
                ReachableArea: Math.Max(0.0, overlapArea),
                ReachablePolygons: Array.Empty<Polygon2>(),
                SourcePolygons: Array.Empty<Polygon2>(),
                SourceCellCount: 0,
                BlockedCellCount: 0,
                ReachableCellCount: 0,
                TotalCellCount: 0,
                SourceArea: Math.Max(0.0, overlapArea),
                BlockedArea: 0.0,
                BlockerCandidateCount: 0);
        }
    }

    private const int ReachabilityContestedCell = -1;
    private const int ReachabilityFreeCell = -2;
    private const int ReachabilityTargetOwnerIndex = 0;

    private readonly record struct ReachabilityGridSpec(
        Rect2 Bounds,
        int Resolution,
        double CellWidth,
        double CellHeight,
        double CellArea);

    private readonly record struct ReachabilityCellOwnership(
        bool[] Source,
        int[] CellOwners,
        int SourceCount,
        int ForeignOccupiedCount);

    private readonly record struct ReachableCellClassification(
        bool[] CurrentReachable,
        int ReachableCount,
        int BlockedCount,
        int AssignedFreeCount);
}
