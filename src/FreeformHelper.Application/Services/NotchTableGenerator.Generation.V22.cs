using System.Diagnostics;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchTableGenerator
{

    private static IReadOnlyList<NotchTableRow> BuildV22DiffCentricRows(
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), List<V22CadCandidate>> candidatesByDiff,
        int nullValue,
        bool applyTargetCoverageGuard,
        int targetCoverageCapPercent)
    {
        if (candidatesByDiff.Count == 0)
        {
            return Array.Empty<NotchTableRow>();
        }

        var selectedCandidates = candidatesByDiff
            .OrderBy(static item => item.Key.IcIndex)
            .ThenBy(static item => item.Key.DiffIndex)
            .Where(static entry => entry.Value.Count > 0)
            .Select(static entry => SelectPrimaryV22Candidate(entry.Value))
            .ToList();
        if (applyTargetCoverageGuard)
        {
            selectedCandidates = ApplyTargetCoverageGuard(
                selectedCandidates,
                targetCoverageCapPercent);
        }

        var rows = new List<NotchTableRow>();
        foreach (var selected in selectedCandidates)
        {
            var orderedLegs = selected.Legs
                .OrderByDescending(leg => Math.Abs(leg.RatioPercent))
                .ThenBy(leg => leg.TargetDiffIndex)
                .ToList();

            if (orderedLegs.Count == 0)
            {
                var node = new NotchV22Node(
                    AnchorDiffIndex: selected.DiffIndex,
                    CombinePercent: selected.CombinePercent,
                    TargetDiffIndex1: nullValue,
                    TargetRatioPercent1: 0,
                    TargetDiffIndex2: nullValue,
                    TargetRatioPercent2: 0,
                    Flags: 0);
                rows.Add(new NotchTableRow(
                    selected.IcIndex,
                    selected.DiffIndex,
                    selected.RegularPadId,
                    selected.CadPadId,
                    node,
                    $"{selected.CommentPrefix} NT"));
                continue;
            }

            for (var i = 0; i < orderedLegs.Count; i += 2)
            {
                var isContinuation = i > 0;
                var leg1 = orderedLegs[i];
                var hasLeg2 = i + 1 < orderedLegs.Count;
                var leg2 = hasLeg2 ? orderedLegs[i + 1] : null;
                var combinePercent = isContinuation ? 100 : selected.CombinePercent;
                var flags = isContinuation ? V22ContinuationFlag : 0;
                var commentTag = isContinuation ? "CONT" : "MAIN";
                var node = new NotchV22Node(
                    AnchorDiffIndex: selected.DiffIndex,
                    CombinePercent: combinePercent,
                    TargetDiffIndex1: leg1.TargetDiffIndex,
                    TargetRatioPercent1: Math.Clamp(leg1.RatioPercent, -100, 100),
                    TargetDiffIndex2: hasLeg2 ? leg2!.TargetDiffIndex : nullValue,
                    TargetRatioPercent2: hasLeg2 ? Math.Clamp(leg2!.RatioPercent, -100, 100) : 0,
                    Flags: Math.Max(0, flags));
                rows.Add(new NotchTableRow(
                    selected.IcIndex,
                    selected.DiffIndex,
                    selected.RegularPadId,
                    selected.CadPadId,
                    node,
                    $"{selected.CommentPrefix} {commentTag} L={orderedLegs.Count}"));
            }
        }

        return rows;
    }

    private static List<V22CadCandidate> ApplyTargetCoverageGuard(
        IReadOnlyList<V22CadCandidate> candidates,
        int targetCoverageCapPercent)
    {
        var capPercent = Math.Clamp(targetCoverageCapPercent, 0, 255);
        if (capPercent <= 0 || candidates.Count == 0)
        {
            return candidates.ToList();
        }

        var coverageByTarget = new Dictionary<(int IcIndex, int DiffIndex), int>();
        foreach (var candidate in candidates)
        {
            var legSum = candidate.Legs.Sum(static leg => Math.Max(0, leg.RatioPercent));
            var retainedPercent = Math.Max(0, candidate.CombinePercent - legSum);
            AddCoverage((candidate.IcIndex, candidate.DiffIndex), retainedPercent);
            foreach (var leg in candidate.Legs)
            {
                AddCoverage((candidate.IcIndex, leg.TargetDiffIndex), Math.Max(0, leg.RatioPercent));
            }
        }

        var scaleByTarget = coverageByTarget
            .Where(item => item.Value > capPercent)
            .ToDictionary(
                static item => item.Key,
                item => capPercent / Math.Max(1.0, item.Value));
        if (scaleByTarget.Count == 0)
        {
            return candidates.ToList();
        }

        return candidates
            .Select(candidate => ApplyTargetCoverageScale(candidate, scaleByTarget))
            .ToList();

        void AddCoverage((int IcIndex, int DiffIndex) key, int percent)
        {
            if (percent <= 0)
            {
                return;
            }

            coverageByTarget[key] = coverageByTarget.GetValueOrDefault(key) + percent;
        }
    }

    private static V22CadCandidate ApplyTargetCoverageScale(
        V22CadCandidate candidate,
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), double> scaleByTarget)
    {
        var legSum = candidate.Legs.Sum(static leg => Math.Max(0, leg.RatioPercent));
        var retainedPercent = Math.Max(0, candidate.CombinePercent - legSum);
        var anchorScale = scaleByTarget.GetValueOrDefault((candidate.IcIndex, candidate.DiffIndex), 1.0);
        var scaledRetainedPercent = ScaleCoveragePercent(retainedPercent, anchorScale);
        var scaledLegs = candidate.Legs
            .GroupBy(static leg => leg.TargetDiffIndex)
            .SelectMany(group =>
            {
                var rawPercent = group.Sum(static leg => leg.RatioPercent);
                var targetScale = scaleByTarget.GetValueOrDefault((candidate.IcIndex, group.Key), 1.0);
                var scaledPercent = ScaleCoveragePercent(rawPercent, targetScale);
                var hasToFullCoverage = group.Any(static leg => leg.HasToFullCoverage);
                return SplitRatioPercent(scaledPercent)
                    .Select(chunk => new V22DiffLeg(group.Key, chunk, hasToFullCoverage));
            })
            .Where(static leg => leg.RatioPercent != 0)
            .ToList();
        var scaledLegSum = scaledLegs.Sum(static leg => Math.Max(0, leg.RatioPercent));
        var scaledCombinePercent = Math.Clamp(scaledRetainedPercent + scaledLegSum, 0, 255);
        return candidate with
        {
            CombinePercent = scaledCombinePercent,
            CommentPrefix = $"{candidate.CommentPrefix} TG={scaledCombinePercent}%",
            Legs = scaledLegs
        };
    }

    private static int ScaleCoveragePercent(int percent, double scale)
    {
        if (percent == 0)
        {
            return 0;
        }

        if (scale >= 1.0)
        {
            return percent;
        }

        var sign = Math.Sign(percent);
        var scaledMagnitude = (int)Math.Floor(Math.Abs(percent) * Math.Clamp(scale, 0.0, 1.0));
        return sign * scaledMagnitude;
    }

    private static IReadOnlyList<NotchTableRow> ProjectV22RowsToV21Rows(
        IReadOnlyList<NotchTableRow> canonicalV22Rows,
        int nullValue)
    {
        if (canonicalV22Rows.Count == 0)
        {
            return Array.Empty<NotchTableRow>();
        }

        var projected = new List<NotchTableRow>(canonicalV22Rows.Count);
        foreach (var row in canonicalV22Rows)
        {
            var node = row.V22Node ?? NotchV22Node.FromValues(row.Values);
            var values = new int[9];
            values[0] = node.AnchorDiffIndex;
            values[1] = Math.Clamp(node.CombinePercent, 0, 255);
            values[2] = Math.Clamp(node.CombinePercent, 0, 255);
            FillLegacyLeg(
                targetDiffIndex: node.TargetDiffIndex1,
                ratioPercent: node.TargetRatioPercent1,
                targetSlot: 3,
                typeSlot: 4,
                ratioSlot: 5);
            FillLegacyLeg(
                targetDiffIndex: node.TargetDiffIndex2,
                ratioPercent: node.TargetRatioPercent2,
                targetSlot: 6,
                typeSlot: 7,
                ratioSlot: 8);

            projected.Add(new NotchTableRow(
                NotchAlgorithmVersion.V21,
                row.IcIndex,
                row.DiffIndex,
                row.RegularPadIndex,
                row.CadPadId,
                values,
                row.Comment));

            void FillLegacyLeg(int targetDiffIndex, int ratioPercent, int targetSlot, int typeSlot, int ratioSlot)
            {
                var clampedPercent = Math.Clamp(ratioPercent, -100, 100);
                if (targetDiffIndex == nullValue || clampedPercent == 0)
                {
                    values[targetSlot] = nullValue;
                    values[typeSlot] = NotchV21Q7Codec.TypeNone;
                    values[ratioSlot] = 0;
                    return;
                }

                values[targetSlot] = targetDiffIndex;
                values[typeSlot] = clampedPercent > 0
                    ? NotchV21Q7Codec.TypeAdd
                    : NotchV21Q7Codec.TypeSub;
                values[ratioSlot] = NotchV21Q7Codec.EncodePercentMagnitude(clampedPercent);
            }
        }

        return projected;
    }

    private static V22CadCandidate SelectPrimaryV22Candidate(IReadOnlyList<V22CadCandidate> candidates)
    {
        return candidates
            .OrderByDescending(candidate => candidate.HasToFullInfluence)
            .ThenByDescending(candidate => candidate.Legs.Count > 0)
            .ThenByDescending(candidate => candidate.IsAnchorMatchedCad)
            .ThenByDescending(candidate => candidate.SourceAreaScore)
            .ThenByDescending(candidate => candidate.CombinePercent)
            .ThenBy(candidate => candidate.CadPadId)
            .First();
    }

    private static V22CadCandidate BuildV22CadCandidate(
        RegularPad anchor,
        int sourceDiffIndex,
        CadAllocationProfile profile,
        CadAllocationGenerationContext context,
        CandidateTimingAccumulator? timingAccumulator,
        out NotchV22ResolvedResult? selectedResolvedResult)
    {
        selectedResolvedResult = null;
        var cadPad = profile.CadPad;
        var buildCadCandidateStopwatch = Stopwatch.StartNew();
        var candidateCadPool = context.CadPoolByIc.TryGetValue(anchor.IcIndex, out var sameIcCadPads) &&
                               sameIcCadPads.Count > 0
            ? sameIcCadPads
            : context.CadPads;
        context.BoundaryQueryContextsByIc.TryGetValue(anchor.IcIndex, out var sharedBoundaryQueryContext);
        NotchV22CompensationStageTimings compensationStageTimings = NotchV22CompensationStageTimings.Empty;
        var compensationComputeStopwatch = Stopwatch.StartNew();
        var selectedRequest = context.SelectedSparseResultRequest;
        var isSelectedResult = selectedRequest is not null &&
                               selectedRequest.CadPadId == cadPad.Id &&
                               selectedRequest.AnchorIcIndex == anchor.IcIndex &&
                               selectedRequest.AnchorDiffIndex == sourceDiffIndex;
        var reusableResolved = isSelectedResult ? selectedRequest!.ReusableResult : null;
        var selectedIdentity = isSelectedResult
            ? NotchV22ResolvedResultService.CreateIdentity(
                cadPad,
                context.Grid,
                candidateCadPool,
                context.ActiveRegularPadIds,
                context.ResolutionSettings.CompensationModel,
                context.ResolutionSettings.EnableToRegular,
                context.ResolutionSettings.EnableToFull,
                context.ResolutionSettings.EnableToFullRuleEngine,
                context.ResolutionSettings.EnableToFullRuleTrace,
                context.ResolutionSettings.EnableBoundaryVirtualAreaCap,
                context.ResolutionSettings.BoundaryVirtualAreaCapRatio,
                context.ResolutionSettings.StrictOverlapRatio,
                anchor.IcIndex,
                sourceDiffIndex,
                context.TargetAllocationAreaMode)
            : null;
        if (reusableResolved is not null && reusableResolved.Identity != selectedIdentity)
        {
            throw new ArgumentException(
                "Reusable selected CadAllocation result does not match the current computation identity.",
                nameof(context));
        }

        var compensation = reusableResolved?.Compensation ?? NotchV22CompensationService.Compute(
            new NotchV22CompensationContext(
                cadPad,
                profile.Allocations,
                context.BoundaryRegularIndices,
                context.ActiveRegularPadIds,
                sharedBoundaryQueryContext ?? NotchV22CompensationService.CreateBoundaryQueryContext(
                    candidateCadPool,
                    context.ResolutionSettings.StrictOverlapRatio),
                context.ResolutionSettings.EnableToRegular,
                context.ResolutionSettings.EnableToFull,
                context.ResolutionSettings.EnableToFullRuleEngine,
                context.ResolutionSettings.EnableToFullRuleTrace,
                context.ResolutionSettings.EnableBoundaryVirtualAreaCap,
                context.ResolutionSettings.BoundaryVirtualAreaCapRatio),
            timingReporter: timings => compensationStageTimings = timings);
        compensationComputeStopwatch.Stop();
        var toRegularPercent = context.ResolutionSettings.EnableToRegular
            ? NotchV22CompensationService.ToPercentRounded(compensation.ToRegularRatio)
            : 100;
        // Keep v2.2 export aligned with inspector: both consume the same compensation output.
        var toFullPercent = context.ResolutionSettings.EnableToFull
            ? NotchV22CompensationService.ToPercentRounded(compensation.ToFullRatio)
            : 100;
        NotchV22TargetAllocationSummary allocation;
        if (reusableResolved is not null)
        {
            selectedResolvedResult = reusableResolved;
            allocation = reusableResolved.TargetAllocation;
        }
        else if (isSelectedResult)
        {
            selectedResolvedResult = new NotchV22ResolvedResultService().Build(
                cadPad,
                compensation,
                context.ResolutionSettings.StrictOverlapRatio,
                anchorIcIndex: anchor.IcIndex,
                anchorDiffIndex: sourceDiffIndex,
                allocationAreaMode: context.TargetAllocationAreaMode,
                identity: selectedIdentity);
            allocation = selectedResolvedResult.TargetAllocation;
        }
        else
        {
            allocation = NotchV22TargetAllocationService.Build(
                cadPad,
                compensation,
                context.ResolutionSettings.StrictOverlapRatio,
                anchorIcIndex: anchor.IcIndex,
                anchorDiffIndex: sourceDiffIndex,
                areaMode: context.TargetAllocationAreaMode);
        }
        var hasToFullInfluenceOnAnchorIc = allocation.Targets
            .Where(target => target.IcIndex == anchor.IcIndex)
            .Any(target => target.ToFullAppliedRegularCount > 0);
        var targetCoverageProjection = allocation.TargetCoverageProjection;
        var legs = BuildV22DiffLegs(
            targetCoverageProjection.EmittedTargets,
            context.ResolutionSettings.CompensationModel);
        var rawCombinedPercent = targetCoverageProjection.RawCombinedPercent ??
                                 NotchSettings.ResolveCombinedPercent(
                                     context.ResolutionSettings.CompensationModel,
                                     toRegularPercent,
                                     toFullPercent);
        var hasCombinedOverflowRisk = targetCoverageProjection.RawCombinedPercent.HasValue
            ? targetCoverageProjection.HasCombinedOverflowRisk
            : NotchV22TargetAllocationPolicy.HasCombinedOverflowRisk(rawCombinedPercent);
        var projectionError = hasCombinedOverflowRisk
            ? $"Combine ratio exceeds 255% at IC{anchor.IcIndex + 1}/diff{sourceDiffIndex} " +
              $"(CAD {cadPad.Id}): toRegular={toRegularPercent}%, toFull={toFullPercent}%, raw={rawCombinedPercent}%."
            : null;
        var combinedPercent = Math.Clamp(rawCombinedPercent, 0, 255);

        var anchorSourceArea = allocation.Targets
            .Where(target => target.IsAnchorDiff)
            .Where(target => target.IcIndex == anchor.IcIndex)
            .Where(target => target.DiffIndex == sourceDiffIndex)
            .Select(target => target.EffectiveArea)
            .DefaultIfEmpty(compensation.OverlapAreaTotal)
            .Max();
        var isAnchorMatchedCad = anchor.MatchedCadPadId.HasValue && anchor.MatchedCadPadId.Value == cadPad.Id;
        var crossIcText = profile.IcIndices.Count > 1
            ? $" XIC={string.Join("/", profile.IcIndices.Select(ic => ic + 1))}"
            : string.Empty;
        var commentPrefix =
            $"CAD={cadPad.Id} R={toRegularPercent}% F={toFullPercent}% C={combinedPercent}%{crossIcText}";
        var candidate = new V22CadCandidate(
            anchor.IcIndex,
            sourceDiffIndex,
            anchor.RegularPadId,
            cadPad.Id,
            profile.MaxRatio,
            projectionError,
            combinedPercent,
            commentPrefix,
            Math.Max(0.0, anchorSourceArea),
            isAnchorMatchedCad,
            hasToFullInfluenceOnAnchorIc,
            legs);
        buildCadCandidateStopwatch.Stop();
        timingAccumulator?.Add(
            buildCadCandidateStopwatch.ElapsedMilliseconds,
            compensationComputeStopwatch.ElapsedMilliseconds,
            compensationStageTimings);
        return candidate;
    }

    private static List<V22DiffLeg> BuildV22DiffLegs(
        IReadOnlyList<NotchV22TargetAllocation> targets,
        NotchCompensationModel model) =>
        NotchV22TargetAllocationPolicy.UsesTargetRegularCoverage(model)
            ? BuildTargetCoverageLegs(targets)
            : BuildSourceAreaDominantLegs(targets);

    private static List<V22DiffLeg> BuildSourceAreaDominantLegs(
        IReadOnlyList<NotchV22TargetAllocation> targets)
    {
        var legs = new List<V22DiffLeg>();
        foreach (var target in targets)
        {
            if (target.RatioPercentRounded == 0)
            {
                continue;
            }

            legs.Add(new V22DiffLeg(
                TargetDiffIndex: target.DiffIndex,
                RatioPercent: Math.Clamp(target.RatioPercentRounded, -100, 100),
                HasToFullCoverage: target.ToFullAppliedRegularCount > 0));
        }

        return legs;
    }

    private static List<V22DiffLeg> BuildTargetCoverageLegs(
        IReadOnlyList<NotchV22TargetAllocation> targets)
    {
        var legs = new List<V22DiffLeg>();
        foreach (var target in targets)
        {
            foreach (var chunk in SplitRatioPercent(target.RatioPercentRounded))
            {
                if (chunk == 0)
                {
                    continue;
                }

                legs.Add(new V22DiffLeg(
                    TargetDiffIndex: target.DiffIndex,
                    RatioPercent: chunk,
                    HasToFullCoverage: target.ToFullAppliedRegularCount > 0));
            }
        }

        return legs;
    }

    private static IEnumerable<int> SplitRatioPercent(int ratioPercent)
    {
        var sign = Math.Sign(ratioPercent);
        var remaining = Math.Abs(ratioPercent);
        while (remaining > 0)
        {
            var chunk = Math.Min(100, remaining);
            yield return chunk * sign;
            remaining -= chunk;
        }
    }

    private sealed record V22CadCandidate(
        int IcIndex,
        int DiffIndex,
        int RegularPadId,
        int CadPadId,
        double MaxRatio,
        string? ProjectionError,
        int CombinePercent,
        string CommentPrefix,
        double SourceAreaScore,
        bool IsAnchorMatchedCad,
        bool HasToFullInfluence,
        List<V22DiffLeg> Legs);

    private sealed record V22DiffLeg(
        int TargetDiffIndex,
        int RatioPercent,
        bool HasToFullCoverage);
}
