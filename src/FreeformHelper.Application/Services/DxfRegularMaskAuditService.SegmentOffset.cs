using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static partial class DxfRegularMaskAuditService
{
    private const double SegmentOffsetDominanceThreshold = 0.60;

    private static void ApplySegmentOffsetSignals(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        if (decisions.Count == 0)
        {
            return;
        }

        var segmentRows = BuildSegmentRows(
            decisions,
            orderedCadPads,
            regularPadById,
            cadIcIndexByCadId);
        foreach (var rowGroup in segmentRows
                     .GroupBy(static sample => (sample.IcIndex, sample.RowIndex))
                     .OrderBy(static group => group.Key.IcIndex)
                     .ThenBy(static group => group.Key.RowIndex))
        {
            var orderedSamples = rowGroup
                .OrderBy(static sample => sample.OrderedCadIndex)
                .ToList();
            var segmentStart = 0;
            var segmentIndex = 0;
            while (segmentStart < orderedSamples.Count)
            {
                var segmentEnd = segmentStart;
                while (segmentEnd + 1 < orderedSamples.Count &&
                       orderedSamples[segmentEnd + 1].OrderedCadIndex == orderedSamples[segmentEnd].OrderedCadIndex + 1)
                {
                    segmentEnd++;
                }

                ApplySegmentMetadata(
                    decisions,
                    orderedSamples,
                    segmentStart,
                    segmentEnd,
                    segmentIndex);
                AnalyzeSegmentOffset(
                    decisions,
                    orderedSamples,
                    segmentStart,
                    segmentEnd);
                segmentStart = segmentEnd + 1;
                segmentIndex++;
            }
        }
    }

    private static void ApplySegmentMetadata(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyList<SegmentRowSample> orderedSamples,
        int segmentStartInclusive,
        int segmentEndInclusive,
        int segmentIndex)
    {
        var memberCount = segmentEndInclusive - segmentStartInclusive + 1;
        for (var i = segmentStartInclusive; i <= segmentEndInclusive; i++)
        {
            var sample = orderedSamples[i];
            if (!decisions.TryGetValue(sample.CadPadId, out var decision))
            {
                continue;
            }

            decisions[sample.CadPadId] = decision with
            {
                IcIndex = sample.IcIndex,
                RowIndex = sample.RowIndex,
                SegmentIndex = segmentIndex,
                SegmentMemberCount = memberCount,
            };
        }
    }

    private static List<SegmentRowSample> BuildSegmentRows(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        var rows = new List<SegmentRowSample>(orderedCadPads.Count);
        for (var index = 0; index < orderedCadPads.Count; index++)
        {
            var cadPad = orderedCadPads[index];
            if (!decisions.TryGetValue(cadPad.Id, out var decision))
            {
                continue;
            }

            if (!cadIcIndexByCadId.TryGetValue(cadPad.Id, out var icIndex))
            {
                continue;
            }

            if (!TryResolveSegmentRow(decision, regularPadById, out var rowIndex))
            {
                continue;
            }

            rows.Add(new SegmentRowSample(
                CadPadId: cadPad.Id,
                OrderedCadIndex: decision.OrderedCadIndex,
                IcIndex: icIndex,
                RowIndex: rowIndex));
        }

        return rows;
    }

    private static bool TryResolveSegmentRow(
        CadOutputFwDiffAssignmentDecision decision,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        out int rowIndex)
    {
        if (decision.MaskedSeed.HasMatch &&
            decision.MaskedSeed.RegularPadIndex is int maskedRegularPadId &&
            regularPadById.TryGetValue(maskedRegularPadId, out var maskedRegular))
        {
            rowIndex = maskedRegular.Row;
            return true;
        }

        if (decision.RawSeed.HasMatch &&
            decision.RawSeed.RegularPadIndex is int rawRegularPadId &&
            regularPadById.TryGetValue(rawRegularPadId, out var rawRegular))
        {
            rowIndex = rawRegular.Row;
            return true;
        }

        rowIndex = default;
        return false;
    }

    private static void AnalyzeSegmentOffset(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyList<SegmentRowSample> orderedSamples,
        int segmentStartInclusive,
        int segmentEndInclusive)
    {
        var offsetSamples = new List<SegmentOffsetSample>(segmentEndInclusive - segmentStartInclusive + 1);
        for (var i = segmentStartInclusive; i <= segmentEndInclusive; i++)
        {
            var sample = orderedSamples[i];
            if (!decisions.TryGetValue(sample.CadPadId, out var decision))
            {
                continue;
            }

            if (!decision.CurrentPrimaryDiffIndex.HasValue)
            {
                continue;
            }

            var seedDiff = ResolveSeedDiffForOffset(decision);
            if (!seedDiff.HasValue)
            {
                continue;
            }

            offsetSamples.Add(new SegmentOffsetSample(
                CadPadId: sample.CadPadId,
                Offset: decision.CurrentPrimaryDiffIndex.Value - seedDiff.Value));
        }

        if (offsetSamples.Count < 2)
        {
            return;
        }

        var dominantGroup = offsetSamples
            .GroupBy(static sample => sample.Offset)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => Math.Abs(group.Key))
            .ThenBy(static group => group.Key)
            .First();
        var dominantOffset = dominantGroup.Key;
        var supportCount = dominantGroup.Count();
        var supportRatio = supportCount / (double)offsetSamples.Count;
        var isSegmentOffsetSuspected = dominantOffset != 0 &&
                                       supportCount >= 2 &&
                                       supportRatio >= SegmentOffsetDominanceThreshold;
        if (!isSegmentOffsetSuspected)
        {
            return;
        }

        var dominantCadPadIds = dominantGroup
            .Select(static sample => sample.CadPadId)
            .ToHashSet();
        foreach (var sample in offsetSamples)
        {
            if (!decisions.TryGetValue(sample.CadPadId, out var decision))
            {
                continue;
            }

            var reasonCode = decision.ReasonCode;
            if (dominantCadPadIds.Contains(sample.CadPadId) &&
                ShouldPromoteToSegmentOffsetReason(decision))
            {
                reasonCode = CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected;
            }

            decisions[sample.CadPadId] = decision with
            {
                ReasonCode = reasonCode,
                DetectedOffset = dominantOffset,
                OffsetSupportCount = supportCount,
                OffsetSupportRatio = supportRatio,
                SegmentConfidence = supportRatio,
            };
        }
    }

    private static int? ResolveSeedDiffForOffset(CadOutputFwDiffAssignmentDecision decision)
    {
        if (decision.Mode == CadOutputFwDiffAssignmentMode.CsvConstrained &&
            decision.MaskedSeed.HasMatch)
        {
            return decision.MaskedSeed.BestMatchFwDiffIndex;
        }

        if (decision.RawSeed.HasMatch)
        {
            return decision.RawSeed.BestMatchFwDiffIndex;
        }

        return decision.MaskedSeed.HasMatch
            ? decision.MaskedSeed.BestMatchFwDiffIndex
            : null;
    }

    private static bool ShouldPromoteToSegmentOffsetReason(CadOutputFwDiffAssignmentDecision decision)
    {
        if (decision.DecisionSource == CadOutputFwDiffAssignmentDecisionSource.ManualOverride)
        {
            return false;
        }

        return decision.ReasonCode is CadOutputFwDiffAssignmentReasonCode.NeedsReview or
               CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate or
               CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion;
    }

    private readonly record struct SegmentRowSample(
        int CadPadId,
        int OrderedCadIndex,
        int IcIndex,
        int RowIndex);

    private readonly record struct SegmentOffsetSample(
        int CadPadId,
        int Offset);
}
