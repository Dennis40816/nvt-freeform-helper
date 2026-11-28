// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

/// <summary>Use a fresh collector for each Generate call. Only the requested preview retains geometry; no input paths are retained.</summary>
public sealed class NotchV22ExplanationCollector(int? previewCadPadId = null)
{
    private const double AreaNoiseRatio = 1e-9;

    // Candidate workers own distinct keys; final projection is the sole writer of explanations/guard.
    private readonly ConcurrentDictionary<CandidateKey, CandidateEvidence> _candidates = new();
    private readonly Dictionary<(int Ic, int Diff), ImmutableArray<NotchV22GuardExplanation>> _guard = new();
    private readonly List<NotchV22RowExplanation> _explanations = [];

    /// <summary>Read after Generate returns; each access publishes an owned immutable snapshot.</summary>
    public ImmutableArray<NotchV22RowExplanation> Explanations => _explanations.ToImmutableArray();

    internal void CaptureCandidate(
        int ic, int diff, NotchV22CandidateExplanation selection,
        NotchV22CompensationResult compensation, NotchV22TargetAllocationSummary allocation,
        string combineRuleCode, int toRegularPercent, int toFullPercent,
        IEnumerable<NotchV22ExplanationLeg> legs)
    {
        var emitted = allocation.TargetCoverageProjection.EmittedTargets;
        var evidence = new CandidateEvidence(selection,
            compensation.RegularDebugInfos.Select(ExplainRegular).ToImmutableArray(),
            allocation.Targets.Select(target => new NotchV22TargetExplanation(
                target.IcIndex, target.DiffIndex, target.Ratio, target.RatioPercentRounded,
                target.PassesStrictThreshold, target.ToFullAppliedRegularCount,
                DescribeDisposition(target, emitted))).ToImmutableArray(),
            combineRuleCode, toRegularPercent, toFullPercent, legs.ToImmutableArray(),
            selection.CadPadId == previewCadPadId ? compensation.ToFullPolygons.ToImmutableArray() : []);
        if (!_candidates.TryAdd(new CandidateKey(ic, diff, selection.CadPadId), evidence))
        {
            throw new InvalidOperationException("A collector must be used for only one generation run.");
        }
    }

    internal void CaptureGuard(int ic, int diff, IEnumerable<NotchV22GuardExplanation> scaling) =>
        _guard.Add((ic, diff), scaling.ToImmutableArray());

    public ImmutableArray<Polygon2> GetPreviewPolygons(int cadPadId) =>
        _candidates.Where(pair => pair.Key.CadId == cadPadId)
            .OrderBy(static pair => pair.Key.Ic).ThenBy(static pair => pair.Key.Diff)
            .SelectMany(static pair => pair.Value.PreviewPolygons).ToImmutableArray();

    internal void CaptureRows(int ic, int diff, int cadId, IEnumerable<int> candidateIds,
        int finalCombine, int continuationSubtraction, int nullValue, IEnumerable<NotchTableRow> rows)
    {
        var evidence = _candidates[new CandidateKey(ic, diff, cadId)];
        var explainedRows = rows.Select((row, ordinal) => new NotchV22ExplainedRow(
            ordinal, row.Values.ToImmutableArray(), ExplainLegs(row.V22Node!).ToImmutableArray())).ToImmutableArray();
        var finalTargets = explainedRows.SelectMany(static row => row.Legs).Select(static leg => leg.TargetDiffIndex).ToHashSet();
        var targets = evidence.Targets.Select(target => target.Disposition == NotchV22TargetDisposition.Emitted &&
                !finalTargets.Contains(target.DiffIndex)
            ? target with { Disposition = NotchV22TargetDisposition.GuardRoundedToZero } : target).ToImmutableArray();
        var combine = new NotchV22CombineExplanation(evidence.CombineRuleCode,
            evidence.ToRegularPercent, evidence.ToFullPercent, evidence.Selection.CombinePercent,
            evidence.Selection.CombinePercent - evidence.Legs.Sum(static leg => Math.Max(0, leg.Percent)),
            evidence.Legs, finalCombine, continuationSubtraction);
        _explanations.Add(new NotchV22RowExplanation(ic, diff, cadId, evidence.Selection.RegularPadId, nullValue,
            candidateIds.Select(id => _candidates[new CandidateKey(ic, diff, id)].Selection).ToImmutableArray(),
            evidence.Regulars, targets, combine,
            _guard.GetValueOrDefault((ic, diff), ImmutableArray<NotchV22GuardExplanation>.Empty), explainedRows)
        {
            PreviewPolygons = evidence.PreviewPolygons,
        });
    }

    private static NotchV22RegularExplanation ExplainRegular(NotchV22RegularDebugInfo info) =>
        new(info.RegularPadId, info.IcIndex, info.DiffIndex, info.RegularArea, info.OverlapArea,
            info.SourceArea, info.Stage3EffectiveArea, info.ReachableArea, info.IsBoundaryRegular,
            info.ToFullRuleCode, info.IsToFullApplied, DescribeLimit(info.IsToFullApplied,
                info.RegularArea, info.OverlapArea, info.Stage3EffectiveArea, info.ReachableArea));

    internal static NotchV22ToFullLimit DescribeLimit(
        bool toFullApplied, double regularArea, double overlapArea, double effectiveArea, double reachableArea)
    {
        if (!toFullApplied || reachableArea <= overlapArea)
        {
            return NotchV22ToFullLimit.None;
        }

        // The compensation step adds the capped virtual area back to the overlap, so an uncapped regular
        // can differ from its reachable area by floating point noise.
        var noise = regularArea * AreaNoiseRatio;
        if (effectiveArea < reachableArea - noise)
        {
            return NotchV22ToFullLimit.VirtualAreaCap;
        }

        return reachableArea < regularArea - noise
            ? NotchV22ToFullLimit.ReachableFreeArea
            : NotchV22ToFullLimit.None;
    }

    private static NotchV22TargetDisposition DescribeDisposition(NotchV22TargetAllocation target,
        IReadOnlyList<NotchV22TargetAllocation> emitted)
    {
        if (target.IsAnchorDiff)
        {
            return NotchV22TargetDisposition.RetainedAnchor;
        }

        if (emitted.Contains(target))
        {
            return NotchV22TargetDisposition.Emitted;
        }

        return target.RatioPercentRounded == 0
            ? NotchV22TargetDisposition.RoundedToZero
            : NotchV22TargetDisposition.BelowStrictThreshold;
    }

    private static IEnumerable<NotchV22ExplanationLeg> ExplainLegs(NotchV22Node node)
    {
        if (node.TargetRatioPercent1 != 0)
        {
            yield return new NotchV22ExplanationLeg(node.TargetDiffIndex1, node.TargetRatioPercent1);
        }

        if (node.TargetRatioPercent2 != 0)
        {
            yield return new NotchV22ExplanationLeg(node.TargetDiffIndex2, node.TargetRatioPercent2);
        }
    }

    private readonly record struct CandidateKey(int Ic, int Diff, int CadId);

    private sealed record CandidateEvidence(NotchV22CandidateExplanation Selection,
        ImmutableArray<NotchV22RegularExplanation> Regulars, ImmutableArray<NotchV22TargetExplanation> Targets,
        string CombineRuleCode, int ToRegularPercent, int ToFullPercent, ImmutableArray<NotchV22ExplanationLeg> Legs,
        ImmutableArray<Polygon2> PreviewPolygons);
}
