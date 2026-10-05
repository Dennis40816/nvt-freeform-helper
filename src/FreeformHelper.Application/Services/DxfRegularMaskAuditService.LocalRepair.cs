namespace FreeformHelper.Application.Services;

public static partial class DxfRegularMaskAuditService
{
    private const int LocalRepairDiffWindow = 3;
    private const int LocalRepairNeighborGapTolerance = 3;
    private const double LocalRepairAutoApplyMinConfidence = 0.80;
    private const double LocalRepairMovePenalty = 0.08;
    private const double LocalRepairDuplicatePenalty = 0.35;
    private const double LocalRepairOrderPenalty = 0.15;
    private const double LocalRepairGapPenalty = 0.05;

    private static Dictionary<int, CadOutputFwDiffAssignmentDecision> ApplyLocalRepairAndPassiveCompensationSignals(
        AuditInvocationContext context,
        SegmentAnalysisResult segmentResult)
    {
        var decisions = segmentResult.Decisions;
        if (decisions.Count == 0)
        {
            return decisions;
        }

        var segmentRows = BuildSegmentRows(
            decisions,
            context.OrderedCadPads,
            context.RegularPadById,
            context.CadIcIndexByCadId);
        if (segmentRows.Count == 0)
        {
            return decisions;
        }

        var primaryCountsByIc = BuildPrimaryDiffCountsByIc(decisions, context.CadIcIndexByCadId);
        foreach (var rowGroup in segmentRows
                     .GroupBy(static sample => (sample.IcIndex, sample.RowIndex))
                     .OrderBy(static group => group.Key.IcIndex)
                     .ThenBy(static group => group.Key.RowIndex))
        {
            var orderedSamples = rowGroup
                .OrderBy(static sample => sample.OrderedCadIndex)
                .ToList();
            var hasRowMaskMismatch = HasCsvRowMaskMismatch(orderedSamples, decisions);
            for (var index = 0; index < orderedSamples.Count; index++)
            {
                var sample = orderedSamples[index];
                if (!decisions.TryGetValue(sample.CadPadId, out var decision))
                {
                    continue;
                }

                var preferredCandidates = ResolvePreferredCandidates(decision);
                var seedDiff = ResolveSeedDiffForOffset(decision);
                var referenceDiff = decision.CurrentPrimaryDiffIndex ?? seedDiff;
                var prevDiff = ResolveNeighborCurrentDiff(decisions, orderedSamples, index - 1);
                var nextDiff = ResolveNeighborCurrentDiff(decisions, orderedSamples, index + 1);
                var repairEvidence = new LocalRepairEvidence(
                    decision,
                    preferredCandidates,
                    referenceDiff,
                    prevDiff,
                    nextDiff,
                    primaryCountsByIc,
                    sample.IcIndex);
                var localRepairResult = ResolveLocalRepairSuggestion(repairEvidence);
                // Passive compensation reads the segment-enriched decision before any repair is projected.
                var passiveCompensationResult = ResolvePassiveCompensationDiff(new PassiveCompensationInput(
                    repairEvidence.OriginalDecision,
                    repairEvidence.ReferenceDiff));
                var rowMismatchShiftSuggestion = hasRowMaskMismatch
                    ? ResolveRowMismatchShiftSuggestion(
                        decision,
                        referenceDiff,
                        prevDiff,
                        nextDiff,
                        primaryCountsByIc,
                        sample.IcIndex)
                    : null;
                var effectiveRepairSuggestion = localRepairResult.SuggestedDiff;
                var effectiveConfidence = localRepairResult.Confidence ?? decision.Confidence;
                if (rowMismatchShiftSuggestion.HasValue)
                {
                    effectiveRepairSuggestion = rowMismatchShiftSuggestion;
                    effectiveConfidence = Math.Max(effectiveConfidence ?? 0d, LocalRepairAutoApplyMinConfidence);
                }
                else if (effectiveRepairSuggestion.HasValue &&
                         localRepairResult.Confidence.HasValue &&
                         localRepairResult.Confidence.Value < LocalRepairAutoApplyMinConfidence)
                {
                    effectiveRepairSuggestion = decision.RepairSuggestionDiffIndex;
                }

                decisions[sample.CadPadId] = decision with
                {
                    RepairSuggestionDiffIndex = effectiveRepairSuggestion ?? decision.RepairSuggestionDiffIndex,
                    PassiveCompensationDiffIndex = passiveCompensationResult.DiffIndex,
                    Confidence = effectiveConfidence,
                };
            }
        }

        return decisions;
    }

    private static bool HasCsvRowMaskMismatch(
        IReadOnlyList<SegmentRowSample> orderedSamples,
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions)
    {
        var hasCsvRows = false;
        var hasAssignedDuplicate = false;
        var assignedDiffs = new HashSet<int>();
        var activeDiffs = new HashSet<int>();
        foreach (var sample in orderedSamples)
        {
            if (!decisions.TryGetValue(sample.CadPadId, out var decision) ||
                decision.Mode != CadOutputFwDiffAssignmentMode.CsvConstrained)
            {
                continue;
            }

            hasCsvRows = true;
            if (decision.CurrentPrimaryDiffIndex.HasValue &&
                !assignedDiffs.Add(decision.CurrentPrimaryDiffIndex.Value))
            {
                hasAssignedDuplicate = true;
            }

            foreach (var candidate in decision.CsvConfirmedCandidates.Where(static candidate => CandidateHasCadOverlap(candidate)))
            {
                activeDiffs.Add(candidate.DiffIndex);
            }
        }

        if (!hasCsvRows)
        {
            return false;
        }

        if (hasAssignedDuplicate)
        {
            return true;
        }

        return activeDiffs.Count > 0 &&
               !activeDiffs.SetEquals(assignedDiffs);
    }

    private static IReadOnlyList<DxfRegularMaskAuditCandidate> ResolvePreferredCandidates(
        CadOutputFwDiffAssignmentDecision decision)
    {
        return decision.Mode == CadOutputFwDiffAssignmentMode.CsvConstrained
            ? decision.CsvConfirmedCandidates
            : decision.GeometryCandidates;
    }

    private static int? ResolveNeighborCurrentDiff(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyList<SegmentRowSample> orderedSamples,
        int index)
    {
        if (index < 0 || index >= orderedSamples.Count)
        {
            return null;
        }

        var cadPadId = orderedSamples[index].CadPadId;
        return decisions.TryGetValue(cadPadId, out var decision)
            ? decision.CurrentPrimaryDiffIndex
            : null;
    }

    private static int? ResolveRowMismatchShiftSuggestion(
        CadOutputFwDiffAssignmentDecision decision,
        int? referenceDiff,
        int? prevDiff,
        int? nextDiff,
        IReadOnlyDictionary<int, Dictionary<int, int>> primaryCountsByIc,
        int icIndex)
    {
        if (decision.Mode != CadOutputFwDiffAssignmentMode.CsvConstrained ||
            decision.DecisionSource == CadOutputFwDiffAssignmentDecisionSource.ManualOverride ||
            !decision.CurrentPrimaryDiffIndex.HasValue)
        {
            return null;
        }

        var currentDiff = decision.CurrentPrimaryDiffIndex.Value;
        var candidateDiffs = decision.CsvConfirmedCandidates
            .Where(static candidate => CandidateHasCadOverlap(candidate))
            .Select(static candidate => candidate.DiffIndex)
            .Where(diff => diff != currentDiff)
            .Distinct()
            .ToList();
        if (candidateDiffs.Count == 0)
        {
            return null;
        }

        var minAllowed = prevDiff.HasValue ? prevDiff.Value + 1 : int.MinValue;
        var maxAllowed = nextDiff.HasValue ? nextDiff.Value - 1 : int.MaxValue;
        var scopedDiffs = candidateDiffs
            .Where(diff => diff >= minAllowed && diff <= maxAllowed)
            .ToList();
        if (scopedDiffs.Count == 0)
        {
            scopedDiffs = candidateDiffs;
        }

        var nextSharesCurrent = nextDiff.HasValue && nextDiff.Value == currentDiff;
        var prevSharesCurrent = prevDiff.HasValue && prevDiff.Value == currentDiff;
        return scopedDiffs
            .OrderBy(diff => ResolveDuplicateOwners(
                decision,
                diff,
                primaryCountsByIc,
                icIndex))
            .ThenBy(diff => nextSharesCurrent
                ? (diff < currentDiff ? 0 : 1)
                : prevSharesCurrent
                    ? (diff > currentDiff ? 0 : 1)
                    : 0)
            .ThenBy(diff => referenceDiff.HasValue
                ? Math.Abs(diff - referenceDiff.Value)
                : Math.Abs(diff - currentDiff))
            .ThenBy(static diff => diff)
            .FirstOrDefault();
    }

    private static LocalRepairResult ResolveLocalRepairSuggestion(LocalRepairEvidence evidence)
    {
        var decision = evidence.OriginalDecision;
        var preferredCandidates = evidence.PreferredCandidates;
        var referenceDiff = evidence.ReferenceDiff;
        var prevDiff = evidence.PreviousDiff;
        var nextDiff = evidence.NextDiff;
        var primaryCountsByIc = evidence.PrimaryCountsByIc;
        var icIndex = evidence.IcIndex;
        if (preferredCandidates.Count == 0)
        {
            return new LocalRepairResult(null, null);
        }

        IReadOnlyList<DxfRegularMaskAuditCandidate> scopedCandidates = preferredCandidates;
        if (referenceDiff.HasValue)
        {
            var localWindowCandidates = preferredCandidates
                .Where(candidate => Math.Abs(candidate.DiffIndex - referenceDiff.Value) <= LocalRepairDiffWindow)
                .ToList();
            if (localWindowCandidates.Count > 0)
            {
                scopedCandidates = localWindowCandidates;
            }
        }

        var maxCoverage = scopedCandidates.Max(static candidate => candidate.RegularCoverage);
        var bestCandidate = scopedCandidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Cost = ComputeLocalRepairCost(
                    decision,
                    candidate,
                    maxCoverage,
                    referenceDiff,
                    prevDiff,
                    nextDiff,
                    primaryCountsByIc,
                    icIndex),
            })
            .OrderBy(static entry => entry.Cost)
            .ThenByDescending(static entry => entry.Candidate.RegularCoverage)
            .ThenBy(entry => referenceDiff.HasValue
                ? Math.Abs(entry.Candidate.DiffIndex - referenceDiff.Value)
                : entry.Candidate.DiffIndex)
            .First();
        var confidence = Math.Clamp(1.0 - bestCandidate.Cost, 0.0, 1.0);
        if (decision.CurrentPrimaryDiffIndex.HasValue &&
            decision.CurrentPrimaryDiffIndex.Value == bestCandidate.Candidate.DiffIndex)
        {
            return new LocalRepairResult(null, confidence);
        }

        return new LocalRepairResult(bestCandidate.Candidate.DiffIndex, confidence);
    }

    private static double ComputeLocalRepairCost(
        CadOutputFwDiffAssignmentDecision decision,
        DxfRegularMaskAuditCandidate candidate,
        double maxCoverage,
        int? referenceDiff,
        int? prevDiff,
        int? nextDiff,
        IReadOnlyDictionary<int, Dictionary<int, int>> primaryCountsByIc,
        int icIndex)
    {
        var matchLoss = maxCoverage > 0
            ? Math.Clamp((maxCoverage - candidate.RegularCoverage) / maxCoverage, 0.0, 1.0)
            : 1.0;
        var moveCost = referenceDiff.HasValue
            ? Math.Abs(candidate.DiffIndex - referenceDiff.Value) * LocalRepairMovePenalty
            : 0.0;
        var duplicateOwners = ResolveDuplicateOwners(
            decision,
            candidate.DiffIndex,
            primaryCountsByIc,
            icIndex);
        var duplicatePenalty = duplicateOwners * LocalRepairDuplicatePenalty;
        var orderPenalty = 0.0;
        if (prevDiff.HasValue && candidate.DiffIndex < prevDiff.Value)
        {
            orderPenalty += LocalRepairOrderPenalty;
        }

        if (nextDiff.HasValue && candidate.DiffIndex > nextDiff.Value)
        {
            orderPenalty += LocalRepairOrderPenalty;
        }

        var gapPenalty = 0.0;
        if (prevDiff.HasValue)
        {
            var prevGap = Math.Abs(candidate.DiffIndex - prevDiff.Value);
            if (prevGap > LocalRepairNeighborGapTolerance)
            {
                gapPenalty += (prevGap - LocalRepairNeighborGapTolerance) * LocalRepairGapPenalty;
            }
        }

        if (nextDiff.HasValue)
        {
            var nextGap = Math.Abs(candidate.DiffIndex - nextDiff.Value);
            if (nextGap > LocalRepairNeighborGapTolerance)
            {
                gapPenalty += (nextGap - LocalRepairNeighborGapTolerance) * LocalRepairGapPenalty;
            }
        }

        return matchLoss + moveCost + duplicatePenalty + orderPenalty + gapPenalty;
    }

    private static int ResolveDuplicateOwners(
        CadOutputFwDiffAssignmentDecision decision,
        int candidateDiff,
        IReadOnlyDictionary<int, Dictionary<int, int>> primaryCountsByIc,
        int icIndex)
    {
        if (!primaryCountsByIc.TryGetValue(icIndex, out var countsByDiff) ||
            !countsByDiff.TryGetValue(candidateDiff, out var owners))
        {
            return 0;
        }

        var selfOwner = decision.CurrentPrimaryDiffIndex.HasValue &&
                        decision.CurrentPrimaryDiffIndex.Value == candidateDiff
            ? 1
            : 0;

        return Math.Max(0, owners - selfOwner);
    }

    private static PassiveCompensationResult ResolvePassiveCompensationDiff(PassiveCompensationInput input)
    {
        var decision = input.OriginalDecision;
        var referenceDiff = input.ReferenceDiff;
        if (decision.Mode != CadOutputFwDiffAssignmentMode.CsvConstrained ||
            decision.CsvConfirmedCandidates.Count > 0 ||
            !decision.RawSeed.HasMatch)
        {
            return new PassiveCompensationResult(null);
        }

        var overlapGeometryCandidates = decision.GeometryCandidates
            .Where(static candidate => CandidateHasCadOverlap(candidate))
            .ToList();
        if (overlapGeometryCandidates.Count == 0)
        {
            return new PassiveCompensationResult(null);
        }

        if (decision.DetectedOffset.HasValue)
        {
            var expectedDiff = decision.RawSeed.BestMatchFwDiffIndex!.Value + decision.DetectedOffset.Value;
            var expectedMatch = overlapGeometryCandidates
                .Where(candidate => Math.Abs(candidate.DiffIndex - expectedDiff) <= LocalRepairDiffWindow)
                .OrderBy(candidate => Math.Abs(candidate.DiffIndex - expectedDiff))
                .ThenByDescending(static candidate => candidate.RegularCoverage)
                .ThenByDescending(static candidate => candidate.CadCoverage)
                .FirstOrDefault();
            if (expectedMatch is not null)
            {
                return new PassiveCompensationResult(expectedMatch.DiffIndex);
            }
        }

        IReadOnlyList<DxfRegularMaskAuditCandidate> scopedCandidates = overlapGeometryCandidates;
        if (referenceDiff.HasValue)
        {
            var localWindowCandidates = overlapGeometryCandidates
                .Where(candidate => Math.Abs(candidate.DiffIndex - referenceDiff.Value) <= LocalRepairDiffWindow)
                .ToList();
            if (localWindowCandidates.Count > 0)
            {
                scopedCandidates = localWindowCandidates;
            }
        }

        return new PassiveCompensationResult(scopedCandidates
            .OrderByDescending(static candidate => candidate.RegularCoverage)
            .ThenByDescending(static candidate => candidate.CadCoverage)
            .ThenBy(static candidate => candidate.DiffIndex)
            .First()
            .DiffIndex);
    }

    private static Dictionary<int, Dictionary<int, int>> BuildPrimaryDiffCountsByIc(
        Dictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        var countsByIc = new Dictionary<int, Dictionary<int, int>>();
        foreach (var (cadPadId, decision) in decisions)
        {
            if (!decision.CurrentPrimaryDiffIndex.HasValue ||
                !cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
            {
                continue;
            }

            if (!countsByIc.TryGetValue(icIndex, out var countsByDiff))
            {
                countsByDiff = new Dictionary<int, int>();
                countsByIc[icIndex] = countsByDiff;
            }

            var diffIndex = decision.CurrentPrimaryDiffIndex.Value;
            countsByDiff.TryGetValue(diffIndex, out var count);
            countsByDiff[diffIndex] = count + 1;
        }

        return countsByIc;
    }

    private static bool CandidateHasCadOverlap(DxfRegularMaskAuditCandidate candidate)
    {
        return candidate.RegularCoverage > 0d || candidate.CadCoverage > 0d;
    }
}
