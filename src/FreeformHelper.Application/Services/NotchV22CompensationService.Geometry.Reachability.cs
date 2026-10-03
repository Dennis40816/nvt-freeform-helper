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

        var cellArea = cellWidth * cellHeight;
        var total = resolution * resolution;
        const int ContestedCell = -1;
        const int FreeCell = -2;
        var source = new bool[total];
        var cellOwners = new int[total];
        var allPads = new CadPad[blockerCandidates.Length + 1];
        allPads[0] = targetCad;
        Array.Copy(blockerCandidates, 0, allPads, 1, blockerCandidates.Length);
        const int TargetOwnerIndex = 0;
        var sourceCount = 0;
        var foreignOccupiedCount = 0;
        var areaTieEpsilon = Math.Max(AreaEpsilon, cellArea * 1e-6);
        for (var row = 0; row < resolution; row++)
        {
            var y0 = bounds.MinY + (row * cellHeight);
            var y1 = y0 + cellHeight;
            for (var col = 0; col < resolution; col++)
            {
                var idx = (row * resolution) + col;
                var x0 = bounds.MinX + (col * cellWidth);
                var x1 = x0 + cellWidth;
                var cellRect = new Rect2(x0, y0, x1, y1);
                var bestOwnerIndex = FreeCell;
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

                var cellOwner = !hasOwner
                    ? FreeCell
                    : isTie
                        ? ContestedCell
                        : bestOwnerIndex;
                cellOwners[idx] = cellOwner;

                if (cellOwner == TargetOwnerIndex)
                {
                    source[idx] = true;
                    sourceCount++;
                    continue;
                }

                if (cellOwner != FreeCell)
                {
                    foreignOccupiedCount++;
                }
            }
        }

        var sourcePolygons = sourceCount > 0
            ? BuildVisitedCellPolygons(source, bounds, resolution, cellWidth, cellHeight)
            : new List<Polygon2>(0);

        // Thin polygons can miss all sample points; preserve overlap-area lower bound.
        if (sourceCount == 0)
        {
            return new ToFullReachabilityResult(
                ReachableArea: Math.Max(0.0, overlapArea),
                ReachablePolygons: Array.Empty<Polygon2>(),
                SourcePolygons: sourcePolygons,
                SourceCellCount: 0,
                BlockedCellCount: foreignOccupiedCount,
                ReachableCellCount: 0,
                TotalCellCount: total,
                SourceArea: Math.Max(0.0, overlapArea),
                BlockedArea: foreignOccupiedCount * cellArea,
                BlockerCandidateCount: blockerCandidateCount);
        }

        // No foreign-occupied cells in this regular: expand to full regular bounds.
        if (foreignOccupiedCount == 0)
        {
            return new ToFullReachabilityResult(
                ReachableArea: Math.Max(overlapArea, regularPad.Area),
                ReachablePolygons: new[] { ToRectPolygon(bounds) },
                SourcePolygons: sourcePolygons,
                SourceCellCount: sourceCount,
                BlockedCellCount: 0,
                ReachableCellCount: total,
                TotalCellCount: total,
                SourceArea: Math.Max(0.0, overlapArea),
                BlockedArea: 0.0,
                BlockerCandidateCount: blockerCandidateCount);
        }

        var ownerDistances = new int[allPads.Length][];
        var queue = new Queue<(int OwnerIndex, int CellIndex)>(Math.Max(sourceCount, allPads.Length));
        for (var ownerIndex = 0; ownerIndex < allPads.Length; ownerIndex++)
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

        var currentReachable = new bool[total];
        var reachableCount = 0;
        var blockedCount = 0;
        var assignedFreeCount = 0;
        for (var cellIndex = 0; cellIndex < total; cellIndex++)
        {
            if (source[cellIndex])
            {
                currentReachable[cellIndex] = true;
                reachableCount++;
                continue;
            }

            if (cellOwners[cellIndex] != FreeCell)
            {
                blockedCount++;
                continue;
            }

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

            if (bestOwner == TargetOwnerIndex && !isTie)
            {
                currentReachable[cellIndex] = true;
                reachableCount++;
                assignedFreeCount++;
                continue;
            }

            blockedCount++;
        }

        var reachableArea = Math.Max(overlapArea, overlapArea + (assignedFreeCount * cellArea));
        var polygons = BuildVisitedCellPolygons(currentReachable, bounds, resolution, cellWidth, cellHeight);
        return new ToFullReachabilityResult(
            ReachableArea: reachableArea,
            ReachablePolygons: polygons,
            SourcePolygons: sourcePolygons,
            SourceCellCount: sourceCount,
            BlockedCellCount: blockedCount,
            ReachableCellCount: reachableCount,
            TotalCellCount: total,
            SourceArea: Math.Max(0.0, overlapArea),
            BlockedArea: blockedCount * cellArea,
            BlockerCandidateCount: blockerCandidateCount);

        void TryVisit(int ownerIndex, int currentDistance, int row, int col)
        {
            if (row < 0 || row >= resolution || col < 0 || col >= resolution)
            {
                return;
            }

            var neighbor = (row * resolution) + col;
            if (cellOwners[neighbor] != FreeCell)
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

    private static ToFullReachabilityResult ExpandToRegularBounds(
        ToFullReachabilityResult sourceCoverage,
        RegularPad regularPad,
        double overlapArea)
    {
        var totalCellCount = sourceCoverage.TotalCellCount;
        var reachableCellCount = totalCellCount > 0
            ? totalCellCount
            : sourceCoverage.ReachableCellCount;
        return new ToFullReachabilityResult(
            ReachableArea: Math.Max(overlapArea, regularPad.Area),
            ReachablePolygons: new[] { ToRectPolygon(regularPad.Bounds) },
            SourcePolygons: sourceCoverage.SourcePolygons,
            SourceCellCount: sourceCoverage.SourceCellCount,
            BlockedCellCount: 0,
            ReachableCellCount: reachableCellCount,
            TotalCellCount: totalCellCount,
            SourceArea: sourceCoverage.SourceArea,
            BlockedArea: 0.0,
            BlockerCandidateCount: 0);
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
}
