using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class DxfOverlapAnalyzer
{
    private static List<IndexPair> BuildOverlapCandidates(IReadOnlyList<CadPad> pads)
    {
        if (pads.Count < 2)
        {
            return new List<IndexPair>(0);
        }

        var totalPairs = PairCount(pads.Count);
        if (pads.Count < SpatialAccelerationMinPadCount)
        {
            return BuildFullCandidatePairs(pads.Count);
        }

        var cellSize = ComputeCellSize(pads);
        if (cellSize <= MinCellSize)
        {
            return BuildFullCandidatePairs(pads.Count);
        }

        var buckets = new Dictionary<long, List<int>>();
        for (var index = 0; index < pads.Count; index++)
        {
            var bounds = pads[index].Bounds;
            var minCol = ToCell(bounds.MinX, cellSize);
            var maxCol = ToCell(bounds.MaxX, cellSize);
            var minRow = ToCell(bounds.MinY, cellSize);
            var maxRow = ToCell(bounds.MaxY, cellSize);

            var colSpan = (long)maxCol - minCol + 1;
            var rowSpan = (long)maxRow - minRow + 1;
            var coveredCells = colSpan * rowSpan;
            if (coveredCells <= 0 || coveredCells > MaxSpatialCellsPerPad)
            {
                return BuildFullCandidatePairs(pads.Count);
            }

            for (var col = minCol; col <= maxCol; col++)
            {
                for (var row = minRow; row <= maxRow; row++)
                {
                    var cellKey = BuildCellKey(col, row);
                    if (!buckets.TryGetValue(cellKey, out var list))
                    {
                        list = new List<int>(4);
                        buckets[cellKey] = list;
                    }

                    list.Add(index);
                }
            }
        }

        var pairKeys = new HashSet<long>();
        foreach (var indices in buckets.Values)
        {
            if (indices.Count < 2)
            {
                continue;
            }

            for (var i = 0; i < indices.Count; i++)
            {
                for (var j = i + 1; j < indices.Count; j++)
                {
                    pairKeys.Add(BuildPairKey(indices[i], indices[j]));
                }
            }
        }

        if (pairKeys.Count == 0)
        {
            return new List<IndexPair>(0);
        }

        // If spatial candidate count is close to full pair count, skip extra overhead.
        if (pairKeys.Count >= totalPairs * 9 / 10)
        {
            return BuildFullCandidatePairs(pads.Count);
        }

        var keys = new List<long>(pairKeys);
        keys.Sort();
        var result = new List<IndexPair>(keys.Count);
        foreach (var key in keys)
        {
            var left = (int)(key >> 32);
            var right = (int)(key & uint.MaxValue);
            result.Add(new IndexPair(left, right));
        }

        return result;
    }

    private static List<IndexPair> BuildFullCandidatePairs(int count)
    {
        var totalPairs = PairCount(count);
        var pairs = new List<IndexPair>((int)Math.Min(totalPairs, int.MaxValue));
        for (var i = 0; i < count; i++)
        {
            for (var j = i + 1; j < count; j++)
            {
                pairs.Add(new IndexPair(i, j));
            }
        }

        return pairs;
    }

    private static double ComputeCellSize(IReadOnlyList<CadPad> pads)
    {
        var sizes = new List<double>(pads.Count);
        foreach (var pad in pads)
        {
            var bounds = pad.Bounds;
            var width = Math.Max(0.0, bounds.MaxX - bounds.MinX);
            var height = Math.Max(0.0, bounds.MaxY - bounds.MinY);
            var size = Math.Max(width, height);
            if (size > MinCellSize)
            {
                sizes.Add(size);
            }
        }

        if (sizes.Count == 0)
        {
            return MinCellSize;
        }

        sizes.Sort();
        return Math.Max(MinCellSize, sizes[sizes.Count / 2]);
    }

    private static int ToCell(double value, double cellSize)
    {
        return (int)Math.Floor(value / cellSize);
    }

    private static long BuildCellKey(int col, int row)
    {
        return ((long)(uint)col << 32) | (uint)row;
    }

    private readonly record struct IndexPair(int LeftIndex, int RightIndex);
}
