using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public static partial class DxfRegularMaskAuditService
{
    public static IReadOnlyList<DxfRegularMaskAuditRow> BuildAuditRows(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int>? currentAssignedDiffByCadId = null,
        IReadOnlyDictionary<int, int>? manualOverrideDiffByCadId = null,
        IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision>? precomputedDecisionByCadId = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCadPads);
        ArgumentNullException.ThrowIfNull(cadToRegular);
        ArgumentNullException.ThrowIfNull(regularPadById);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        var decisionByCadId = precomputedDecisionByCadId ??
            BuildAssignmentDecisions(
                orderedCadPads,
                cadToRegular,
                regularPadById,
                cadIcIndexByCadId,
                activeRegularPadIds,
                currentAssignedDiffByCadId,
                manualOverrideDiffByCadId);

        var rows = new List<DxfRegularMaskAuditRow>(orderedCadPads.Count);
        for (var index = 0; index < orderedCadPads.Count; index++)
        {
            var cadPad = orderedCadPads[index];
            if (!decisionByCadId.TryGetValue(cadPad.Id, out var decision))
            {
                decision = CadOutputFwDiffAssignmentDecision.Empty(
                    cadPad.Id,
                    index,
                    activeRegularPadIds is null
                        ? CadOutputFwDiffAssignmentMode.GeometryOnly
                        : CadOutputFwDiffAssignmentMode.CsvConstrained);
            }

            var status = ResolveStatus(decision.RawSeed, decision.MaskedSeed);
            rows.Add(new DxfRegularMaskAuditRow(
                CadPadId: cadPad.Id,
                OrderedCadIndex: index,
                RawSeed: decision.RawSeed,
                MaskedSeed: decision.MaskedSeed,
                CsvConfirmedCandidates: decision.CsvConfirmedCandidates,
                CsvConfirmedConfidence: decision.Confidence,
                SuggestedDiffIndex: decision.CsvConfirmedCandidates.Count > 0 ? decision.CsvConfirmedCandidates[0].DiffIndex : null,
                CurrentAssignedDiffIndex: decision.CurrentPrimaryDiffIndex,
                Status: status,
                Message: BuildMessage(decision.RawSeed, decision.MaskedSeed, status, decision.CsvConfirmedCandidates.Count),
                Mode: decision.Mode,
                ReasonCode: decision.ReasonCode,
                DecisionSource: decision.DecisionSource,
                PrimaryAssignedDiffIndex: decision.CurrentPrimaryDiffIndex,
                PassiveCompensationDiffIndex: decision.PassiveCompensationDiffIndex,
                RepairSuggestionDiffIndex: decision.RepairSuggestionDiffIndex,
                RepairCandidateCount: decision.RepairCandidateCount,
                GeometryCandidates: decision.GeometryCandidates,
                DetectedOffset: decision.DetectedOffset,
                OffsetSupportCount: decision.OffsetSupportCount,
                OffsetSupportRatio: decision.OffsetSupportRatio,
                SegmentConfidence: decision.SegmentConfidence,
                IcIndex: decision.IcIndex,
                RowIndex: decision.RowIndex,
                SegmentIndex: decision.SegmentIndex,
                SegmentMemberCount: decision.SegmentMemberCount));
        }

        return rows;
    }

    public static IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> BuildAssignmentDecisions(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds,
        IReadOnlyDictionary<int, int>? currentAssignedDiffByCadId = null,
        IReadOnlyDictionary<int, int>? manualOverrideDiffByCadId = null)
    {
        ArgumentNullException.ThrowIfNull(orderedCadPads);
        ArgumentNullException.ThrowIfNull(cadToRegular);
        ArgumentNullException.ThrowIfNull(regularPadById);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        var mode = activeRegularPadIds is null
            ? CadOutputFwDiffAssignmentMode.GeometryOnly
            : CadOutputFwDiffAssignmentMode.CsvConstrained;
        var duplicateDiffKeys = BuildDuplicateDiffKeySet(
            orderedCadPads,
            currentAssignedDiffByCadId,
            cadIcIndexByCadId);
        var decisions = new Dictionary<int, CadOutputFwDiffAssignmentDecision>(orderedCadPads.Count);

        for (var index = 0; index < orderedCadPads.Count; index++)
        {
            var cadPad = orderedCadPads[index];
            var candidateSet = BuildCandidateSet(
                cadPad.Id,
                cadToRegular,
                regularPadById,
                cadIcIndexByCadId,
                activeRegularPadIds);
            var currentPrimaryDiff = ResolveCurrentPrimaryDiff(currentAssignedDiffByCadId, cadPad.Id);
            var decisionSource = manualOverrideDiffByCadId is not null &&
                                 manualOverrideDiffByCadId.ContainsKey(cadPad.Id)
                ? CadOutputFwDiffAssignmentDecisionSource.ManualOverride
                : CadOutputFwDiffAssignmentDecisionSource.Seed;
            var hasDuplicateConflict = currentPrimaryDiff.HasValue &&
                                       cadIcIndexByCadId.TryGetValue(cadPad.Id, out var cadIcIndex) &&
                                       duplicateDiffKeys.Contains((cadIcIndex, currentPrimaryDiff.Value));
            var reasonCode = ResolveReasonCode(
                mode,
                candidateSet.RawSeed,
                candidateSet.MaskedSeed,
                currentPrimaryDiff,
                hasDuplicateConflict,
                candidateSet.GeometryCandidates,
                candidateSet.CsvConfirmedCandidates);
            var repairSuggestionDiff = ResolveRepairSuggestionDiff(
                mode,
                currentPrimaryDiff,
                candidateSet.GeometryCandidates,
                candidateSet.CsvConfirmedCandidates);
            var confidence = mode == CadOutputFwDiffAssignmentMode.CsvConstrained
                ? ComputeConfidence(candidateSet.CsvConfirmedCandidates)
                : ComputeConfidence(candidateSet.GeometryCandidates);

            decisions[cadPad.Id] = new CadOutputFwDiffAssignmentDecision(
                CadPadId: cadPad.Id,
                OrderedCadIndex: index,
                Mode: mode,
                RawSeed: candidateSet.RawSeed,
                MaskedSeed: candidateSet.MaskedSeed,
                GeometryCandidates: candidateSet.GeometryCandidates,
                CsvConfirmedCandidates: candidateSet.CsvConfirmedCandidates,
                CurrentPrimaryDiffIndex: currentPrimaryDiff,
                PassiveCompensationDiffIndex: null,
                RepairSuggestionDiffIndex: repairSuggestionDiff,
                ReasonCode: reasonCode,
                DecisionSource: decisionSource,
                Confidence: confidence);
        }

        ApplySegmentOffsetSignals(
            decisions,
            orderedCadPads,
            regularPadById,
            cadIcIndexByCadId);
        ApplyLocalRepairAndPassiveCompensationSignals(
            decisions,
            orderedCadPads,
            regularPadById,
            cadIcIndexByCadId);

        return decisions;
    }

    private static CandidateSet BuildCandidateSet(
        int cadPadId,
        IReadOnlyDictionary<int, IReadOnlyList<PadMatchLink>> cadToRegular,
        IReadOnlyDictionary<int, RegularPad> regularPadById,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlySet<int>? activeRegularPadIds)
    {
        if (!cadToRegular.TryGetValue(cadPadId, out var links) || links.Count == 0)
        {
            return CandidateSet.Empty;
        }

        var hasCadIcIndex = cadIcIndexByCadId.TryGetValue(cadPadId, out var cadIcIndex);
        var geometryCandidates = new List<DxfRegularMaskAuditCandidate>(links.Count);
        var csvConfirmedCandidates = activeRegularPadIds is null
            ? null
            : new List<DxfRegularMaskAuditCandidate>(links.Count);
        foreach (var link in links)
        {
            if (!regularPadById.TryGetValue(link.RegularPadId, out var regularPad))
            {
                continue;
            }

            if (hasCadIcIndex && regularPad.IcIndex != cadIcIndex)
            {
                continue;
            }

            var candidate = new DxfRegularMaskAuditCandidate(
                regularPad.RegularPadId,
                regularPad.IcIndex,
                regularPad.DiffIndex,
                link.RegularCoverage,
                link.CadCoverage);
            geometryCandidates.Add(candidate);
            if (activeRegularPadIds is null || activeRegularPadIds.Contains(link.RegularPadId))
            {
                csvConfirmedCandidates?.Add(candidate);
            }
        }

        var effectiveCsvCandidates = csvConfirmedCandidates ?? geometryCandidates;
        return new CandidateSet(
            NormalizeCandidates(geometryCandidates),
            NormalizeCandidates(effectiveCsvCandidates),
            BuildSeed(geometryCandidates),
            BuildSeed(effectiveCsvCandidates));
    }

    private static IReadOnlyList<DxfRegularMaskAuditCandidate> NormalizeCandidates(List<DxfRegularMaskAuditCandidate> candidates) =>
        candidates.Count == 0
            ? Array.Empty<DxfRegularMaskAuditCandidate>()
            : candidates;

    private static CadBestMatchSeed BuildSeed(List<DxfRegularMaskAuditCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return CadBestMatchSeed.Empty;
        }

        var candidate = candidates[0];
        return new CadBestMatchSeed(
            candidate.RegularPadIndex,
            candidate.IcIndex,
            candidate.DiffIndex,
            candidate.RegularCoverage,
            candidate.CadCoverage);
    }

    private static HashSet<(int IcIndex, int DiffIndex)> BuildDuplicateDiffKeySet(
        IReadOnlyList<CadPad> orderedCadPads,
        IReadOnlyDictionary<int, int>? currentAssignedDiffByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        var duplicateKeys = new HashSet<(int IcIndex, int DiffIndex)>();
        if (currentAssignedDiffByCadId is null || currentAssignedDiffByCadId.Count == 0)
        {
            return duplicateKeys;
        }

        var grouped = orderedCadPads
            .Where(pad => currentAssignedDiffByCadId.ContainsKey(pad.Id) && cadIcIndexByCadId.ContainsKey(pad.Id))
            .GroupBy(pad => (IcIndex: cadIcIndexByCadId[pad.Id], DiffIndex: currentAssignedDiffByCadId[pad.Id]))
            .Where(group => group.Count() > 1);
        foreach (var group in grouped)
        {
            duplicateKeys.Add(group.Key);
        }

        return duplicateKeys;
    }

    private static int? ResolveCurrentPrimaryDiff(
        IReadOnlyDictionary<int, int>? currentAssignedDiffByCadId,
        int cadPadId)
    {
        if (currentAssignedDiffByCadId is not null &&
            currentAssignedDiffByCadId.TryGetValue(cadPadId, out var assignedDiff))
        {
            return assignedDiff;
        }

        return null;
    }

    private static CadOutputFwDiffAssignmentReasonCode ResolveReasonCode(
        CadOutputFwDiffAssignmentMode mode,
        CadBestMatchSeed rawSeed,
        CadBestMatchSeed maskedSeed,
        int? currentPrimaryDiff,
        bool hasDuplicateConflict,
        IReadOnlyList<DxfRegularMaskAuditCandidate> geometryCandidates,
        IReadOnlyList<DxfRegularMaskAuditCandidate> csvConfirmedCandidates)
    {
        if (hasDuplicateConflict)
        {
            return CadOutputFwDiffAssignmentReasonCode.DuplicateConflict;
        }

        if (mode == CadOutputFwDiffAssignmentMode.CsvConstrained)
        {
            if (rawSeed.HasMatch && !maskedSeed.HasMatch)
            {
                return CadOutputFwDiffAssignmentReasonCode.InactiveBlocked;
            }

            if (rawSeed.HasMatch &&
                maskedSeed.HasMatch &&
                rawSeed.BestMatchFwDiffIndex != maskedSeed.BestMatchFwDiffIndex)
            {
                return CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate;
            }

            if (currentPrimaryDiff.HasValue &&
                csvConfirmedCandidates.Count > 0 &&
                !csvConfirmedCandidates.Any(candidate => candidate.DiffIndex == currentPrimaryDiff.Value))
            {
                return CadOutputFwDiffAssignmentReasonCode.InactiveBlocked;
            }

            return CadOutputFwDiffAssignmentReasonCode.NeedsReview;
        }

        return currentPrimaryDiff.HasValue || geometryCandidates.Count == 0
            ? CadOutputFwDiffAssignmentReasonCode.NeedsReview
            : CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion;
    }

    private static int? ResolveRepairSuggestionDiff(
        CadOutputFwDiffAssignmentMode mode,
        int? currentPrimaryDiff,
        IReadOnlyList<DxfRegularMaskAuditCandidate> geometryCandidates,
        IReadOnlyList<DxfRegularMaskAuditCandidate> csvConfirmedCandidates)
    {
        var candidates = mode == CadOutputFwDiffAssignmentMode.CsvConstrained
            ? csvConfirmedCandidates
            : geometryCandidates;
        if (candidates.Count == 0)
        {
            return null;
        }

        var suggested = candidates[0].DiffIndex;
        if (currentPrimaryDiff.HasValue && currentPrimaryDiff.Value == suggested)
        {
            return null;
        }

        return suggested;
    }

    private static DxfRegularMaskAuditStatus ResolveStatus(CadBestMatchSeed? rawSeed, CadBestMatchSeed? maskedSeed)
    {
        var rawHasMatch = rawSeed?.HasMatch == true;
        var maskedHasMatch = maskedSeed?.HasMatch == true;

        if (!rawHasMatch)
        {
            return DxfRegularMaskAuditStatus.NoRawBestMatch;
        }

        if (!maskedHasMatch)
        {
            return DxfRegularMaskAuditStatus.MaskRemovedMatch;
        }

        if (rawSeed!.RegularPadIndex != maskedSeed!.RegularPadIndex || rawSeed.BestMatchFwDiffIndex != maskedSeed.BestMatchFwDiffIndex)
        {
            return DxfRegularMaskAuditStatus.ChangedByMask;
        }

        return DxfRegularMaskAuditStatus.Unchanged;
    }

    private static double? ComputeConfidence(IReadOnlyList<DxfRegularMaskAuditCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var leadingCoverage = candidates[0].RegularCoverage;
        var totalCoverage = candidates.Sum(candidate => candidate.RegularCoverage);
        if (totalCoverage <= 0)
        {
            return null;
        }

        return Math.Clamp(leadingCoverage / totalCoverage, 0.0, 1.0);
    }

    private static string BuildMessage(
        CadBestMatchSeed? rawSeed,
        CadBestMatchSeed? maskedSeed,
        DxfRegularMaskAuditStatus status,
        int csvCandidateCount)
    {
        var csvText = csvCandidateCount switch
        {
            0 => "No CSV-active candidate remains.",
            1 => "CSV confirms a single active candidate.",
            _ => $"CSV keeps {csvCandidateCount} active candidates.",
        };

        return status switch
        {
            DxfRegularMaskAuditStatus.NoRawBestMatch => "No raw geometry seed from Step 1 geometry candidates.",
            DxfRegularMaskAuditStatus.MaskRemovedMatch => $"Raw geometry seed REG {rawSeed?.RegularPadIndex} / diff {rawSeed?.BestMatchFwDiffIndex} is inactive in Regular Visibility Mask (SeeRegular.csv). {csvText}",
            DxfRegularMaskAuditStatus.ChangedByMask => $"Mask changed geometry seed from REG {rawSeed?.RegularPadIndex} / diff {rawSeed?.BestMatchFwDiffIndex} to REG {maskedSeed?.RegularPadIndex} / diff {maskedSeed?.BestMatchFwDiffIndex}. {csvText}",
            _ => $"Mask kept geometry seed at REG {maskedSeed?.RegularPadIndex} / diff {maskedSeed?.BestMatchFwDiffIndex}. {csvText}",
        };
    }

    private sealed record CandidateSet(
        IReadOnlyList<DxfRegularMaskAuditCandidate> GeometryCandidates,
        IReadOnlyList<DxfRegularMaskAuditCandidate> CsvConfirmedCandidates,
        CadBestMatchSeed RawSeed,
        CadBestMatchSeed MaskedSeed)
    {
        public static CandidateSet Empty { get; } = new(
            Array.Empty<DxfRegularMaskAuditCandidate>(),
            Array.Empty<DxfRegularMaskAuditCandidate>(),
            CadBestMatchSeed.Empty,
            CadBestMatchSeed.Empty);
    }
}

public enum DxfRegularMaskAuditStatus
{
    Unchanged = 0,
    ChangedByMask = 1,
    MaskRemovedMatch = 2,
    NoRawBestMatch = 3,
}

public enum CadOutputFwDiffAssignmentMode
{
    GeometryOnly = 0,
    CsvConstrained = 1,
}

public enum CadOutputFwDiffAssignmentReasonCode
{
    InactiveBlocked = 0,
    DuplicateConflict = 1,
    SegmentOffsetSuspected = 2,
    GapCompensationCandidate = 3,
    GeometryOnlySuggestion = 4,
    NeedsReview = 5,
}

public enum CadOutputFwDiffAssignmentDecisionSource
{
    Seed = 0,
    AutoRepaired = 1,
    ManualOverride = 2,
    PropagatedOverride = 3,
}

public sealed record DxfRegularMaskAuditRow(
    int CadPadId,
    int OrderedCadIndex,
    CadBestMatchSeed RawSeed,
    CadBestMatchSeed MaskedSeed,
    IReadOnlyList<DxfRegularMaskAuditCandidate> CsvConfirmedCandidates,
    double? CsvConfirmedConfidence,
    int? SuggestedDiffIndex,
    int? CurrentAssignedDiffIndex,
    DxfRegularMaskAuditStatus Status,
    string Message,
    CadOutputFwDiffAssignmentMode Mode = CadOutputFwDiffAssignmentMode.GeometryOnly,
    CadOutputFwDiffAssignmentReasonCode ReasonCode = CadOutputFwDiffAssignmentReasonCode.NeedsReview,
    CadOutputFwDiffAssignmentDecisionSource DecisionSource = CadOutputFwDiffAssignmentDecisionSource.Seed,
    int? PrimaryAssignedDiffIndex = null,
    int? PassiveCompensationDiffIndex = null,
    int? RepairSuggestionDiffIndex = null,
    int RepairCandidateCount = 0,
    IReadOnlyList<DxfRegularMaskAuditCandidate>? GeometryCandidates = null,
    int? DetectedOffset = null,
    int OffsetSupportCount = 0,
    double? OffsetSupportRatio = null,
    double? SegmentConfidence = null,
    int? IcIndex = null,
    int? RowIndex = null,
    int? SegmentIndex = null,
    int SegmentMemberCount = 0);

public sealed record DxfRegularMaskAuditCandidate(
    int RegularPadIndex,
    int IcIndex,
    int DiffIndex,
    double RegularCoverage,
    double CadCoverage);

public sealed record CadOutputFwDiffAssignmentDecision(
    int CadPadId,
    int OrderedCadIndex,
    CadOutputFwDiffAssignmentMode Mode,
    CadBestMatchSeed RawSeed,
    CadBestMatchSeed MaskedSeed,
    IReadOnlyList<DxfRegularMaskAuditCandidate> GeometryCandidates,
    IReadOnlyList<DxfRegularMaskAuditCandidate> CsvConfirmedCandidates,
    int? CurrentPrimaryDiffIndex,
    int? PassiveCompensationDiffIndex,
    int? RepairSuggestionDiffIndex,
    CadOutputFwDiffAssignmentReasonCode ReasonCode,
    CadOutputFwDiffAssignmentDecisionSource DecisionSource,
    double? Confidence,
    int? DetectedOffset = null,
    int OffsetSupportCount = 0,
    double? OffsetSupportRatio = null,
    double? SegmentConfidence = null,
    int? IcIndex = null,
    int? RowIndex = null,
    int? SegmentIndex = null,
    int SegmentMemberCount = 0)
{
    public int RepairCandidateCount => Mode == CadOutputFwDiffAssignmentMode.CsvConstrained
        ? CsvConfirmedCandidates.Count
        : GeometryCandidates.Count;

    public static CadOutputFwDiffAssignmentDecision Empty(
        int cadPadId,
        int orderedCadIndex,
        CadOutputFwDiffAssignmentMode mode)
    {
        return new CadOutputFwDiffAssignmentDecision(
            CadPadId: cadPadId,
            OrderedCadIndex: orderedCadIndex,
            Mode: mode,
            RawSeed: CadBestMatchSeed.Empty,
            MaskedSeed: CadBestMatchSeed.Empty,
            GeometryCandidates: Array.Empty<DxfRegularMaskAuditCandidate>(),
            CsvConfirmedCandidates: Array.Empty<DxfRegularMaskAuditCandidate>(),
            CurrentPrimaryDiffIndex: null,
            PassiveCompensationDiffIndex: null,
            RepairSuggestionDiffIndex: null,
            ReasonCode: CadOutputFwDiffAssignmentReasonCode.NeedsReview,
            DecisionSource: CadOutputFwDiffAssignmentDecisionSource.Seed,
            Confidence: null,
            DetectedOffset: null,
            OffsetSupportCount: 0,
            OffsetSupportRatio: null,
            SegmentConfidence: null);
    }
}

public static class CadOutputFwDiffAssignmentContractText
{
    public static string ToContractString(this CadOutputFwDiffAssignmentMode mode)
    {
        return mode switch
        {
            CadOutputFwDiffAssignmentMode.CsvConstrained => "csv-constrained",
            _ => "geometry-only",
        };
    }

    public static string ToContractString(this CadOutputFwDiffAssignmentReasonCode reasonCode)
    {
        return reasonCode switch
        {
            CadOutputFwDiffAssignmentReasonCode.InactiveBlocked => "inactive-blocked",
            CadOutputFwDiffAssignmentReasonCode.DuplicateConflict => "duplicate-conflict",
            CadOutputFwDiffAssignmentReasonCode.SegmentOffsetSuspected => "segment-offset-suspected",
            CadOutputFwDiffAssignmentReasonCode.GapCompensationCandidate => "gap-compensation-candidate",
            CadOutputFwDiffAssignmentReasonCode.GeometryOnlySuggestion => "geometry-only-suggestion",
            _ => "needs-review",
        };
    }

    public static string ToContractString(this CadOutputFwDiffAssignmentDecisionSource decisionSource)
    {
        return decisionSource switch
        {
            CadOutputFwDiffAssignmentDecisionSource.AutoRepaired => "auto-repaired",
            CadOutputFwDiffAssignmentDecisionSource.ManualOverride => "manual-override",
            CadOutputFwDiffAssignmentDecisionSource.PropagatedOverride => "propagated-override",
            _ => "seed",
        };
    }
}
