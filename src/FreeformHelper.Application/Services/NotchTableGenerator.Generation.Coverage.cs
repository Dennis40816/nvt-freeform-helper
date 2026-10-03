using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{
    private static NotchToFullCoverageAudit BuildToFullCoverageAudit(
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>> candidatesByDiff,
        IReadOnlyList<NotchTableRow> canonicalRows,
        int nullValue)
    {
        if (candidatesByDiff.Count == 0)
        {
            return NotchToFullCoverageAudit.Empty;
        }

        var expectedTargetsByBucket = new Dictionary<(int IcIndex, int DiffIndex), Dictionary<int, HashSet<int>>>();
        foreach (var entry in candidatesByDiff)
        {
            Dictionary<int, HashSet<int>>? expectedTargets = null;
            foreach (var candidate in entry.Value)
            {
                foreach (var leg in candidate.Legs.Where(static leg => leg.HasToFullCoverage))
                {
                    expectedTargets ??= new Dictionary<int, HashSet<int>>();
                    if (!expectedTargets.TryGetValue(leg.TargetDiffIndex, out var cadPadIds))
                    {
                        cadPadIds = new HashSet<int>();
                        expectedTargets[leg.TargetDiffIndex] = cadPadIds;
                    }

                    cadPadIds.Add(candidate.CadPadId);
                }
            }

            if (expectedTargets is not null && expectedTargets.Count > 0)
            {
                expectedTargetsByBucket[entry.Key] = expectedTargets;
            }
        }

        if (expectedTargetsByBucket.Count == 0)
        {
            return NotchToFullCoverageAudit.Empty;
        }

        var emittedTargetsByBucket = canonicalRows
            .Where(static row => row.Version == NotchAlgorithmVersion.V22)
            .GroupBy(static row => (row.IcIndex, row.DiffIndex))
            .ToDictionary(
                static group => group.Key,
                group => group
                    .SelectMany(row => EnumerateEmittedTargetDiffs(row, nullValue))
                    .ToHashSet());

        var coveredTargetDiffCount = 0;
        var missingGaps = new List<NotchToFullCoverageGap>();
        foreach (var bucket in expectedTargetsByBucket
                     .OrderBy(static entry => entry.Key.IcIndex)
                     .ThenBy(static entry => entry.Key.DiffIndex))
        {
            emittedTargetsByBucket.TryGetValue(bucket.Key, out var emittedTargets);
            foreach (var target in bucket.Value.OrderBy(static entry => entry.Key))
            {
                if (emittedTargets is not null && emittedTargets.Contains(target.Key))
                {
                    coveredTargetDiffCount++;
                    continue;
                }

                missingGaps.Add(new NotchToFullCoverageGap(
                    bucket.Key.IcIndex,
                    bucket.Key.DiffIndex,
                    target.Key,
                    target.Value.OrderBy(static cadPadId => cadPadId).ToArray()));
            }
        }

        var expectedTargetDiffCount = expectedTargetsByBucket.Sum(static bucket => bucket.Value.Count);
        return new NotchToFullCoverageAudit(
            BucketCount: expectedTargetsByBucket.Count,
            ExpectedTargetDiffCount: expectedTargetDiffCount,
            CoveredTargetDiffCount: coveredTargetDiffCount,
            MissingGaps: missingGaps);
    }

    private static IEnumerable<int> EnumerateEmittedTargetDiffs(NotchTableRow row, int nullValue)
    {
        var node = row.V22Node ?? NotchV22Node.FromValues(row.Values);
        if (node.TargetDiffIndex1 != nullValue && node.TargetRatioPercent1 != 0)
        {
            yield return node.TargetDiffIndex1;
        }

        if (node.TargetDiffIndex2 != nullValue && node.TargetRatioPercent2 != 0)
        {
            yield return node.TargetDiffIndex2;
        }
    }
}
