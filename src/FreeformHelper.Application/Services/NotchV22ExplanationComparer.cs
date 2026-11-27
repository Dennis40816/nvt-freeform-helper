// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;

namespace FreeformHelper.Application.Services;

public enum NotchV22DifferenceKind
{
    Changed,
    Added,
    Removed,
}

public enum NotchV22ExplanationStep
{
    CandidateSelection,
    Overlap,
    ToFullRuleOrArea,
    RatioBeforeRounding,
    Rounding,
    GuardOrContinuationSplit,
}

public sealed record NotchV22RowKey(int IcIndex, int SourceDiffIndex, int Ordinal);

public sealed record NotchV22RowDifference(
    NotchV22RowKey Key,
    NotchV22DifferenceKind Kind,
    ImmutableArray<int>? Before,
    ImmutableArray<int>? After,
    NotchV22ExplanationStep FirstStep,
    ImmutableArray<string> RuleCodesBefore,
    ImmutableArray<string> RuleCodesAfter);

/// <summary>Compares final payloads; unchanged integers produce no report entry even if diagnostics change.</summary>
public static class NotchV22ExplanationComparer
{
    public static ImmutableArray<NotchV22RowDifference> Compare(
        IEnumerable<NotchV22RowExplanation> before, IEnumerable<NotchV22RowExplanation> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        var left = Index(before);
        var right = Index(after);
        var differences = ImmutableArray.CreateBuilder<NotchV22RowDifference>();
        foreach (var sourceKey in left.Keys.Union(right.Keys).OrderBy(static key => key.Ic).ThenBy(static key => key.Diff))
        {
            left.TryGetValue(sourceKey, out var oldSource);
            right.TryGetValue(sourceKey, out var newSource);
            var oldRows = oldSource?.Rows.ToDictionary(static row => row.Ordinal) ?? [];
            var newRows = newSource?.Rows.ToDictionary(static row => row.Ordinal) ?? [];
            foreach (var ordinal in oldRows.Keys.Union(newRows.Keys).Order())
            {
                oldRows.TryGetValue(ordinal, out var oldRow);
                newRows.TryGetValue(ordinal, out var newRow);
                if (oldRow is not null && newRow is not null && oldRow.Values.SequenceEqual(newRow.Values))
                {
                    continue;
                }

                var kind = oldRow is null ? NotchV22DifferenceKind.Added
                    : newRow is null ? NotchV22DifferenceKind.Removed : NotchV22DifferenceKind.Changed;
                differences.Add(new NotchV22RowDifference(new(sourceKey.Ic, sourceKey.Diff, ordinal), kind,
                    oldRow?.Values, newRow?.Values, FindFirstStep(oldSource, newSource),
                    RuleCodes(oldSource), RuleCodes(newSource)));
            }
        }

        return differences.ToImmutable();
    }

    private static Dictionary<(int Ic, int Diff), NotchV22RowExplanation> Index(
        IEnumerable<NotchV22RowExplanation> explanations) =>
        explanations.ToDictionary(static item => (item.IcIndex, item.SourceDiffIndex));

    private static NotchV22ExplanationStep FindFirstStep(NotchV22RowExplanation? before, NotchV22RowExplanation? after)
    {
        if (before is null || after is null || before.SelectedCadPadId != after.SelectedCadPadId ||
            before.SelectedRegularPadId != after.SelectedRegularPadId)
        {
            return NotchV22ExplanationStep.CandidateSelection;
        }

        if (!Overlap(before).SequenceEqual(Overlap(after)))
        {
            return NotchV22ExplanationStep.Overlap;
        }

        if (!ToFull(before).SequenceEqual(ToFull(after)))
        {
            return NotchV22ExplanationStep.ToFullRuleOrArea;
        }

        if (!Ratios(before).SequenceEqual(Ratios(after)) ||
            !string.Equals(before.Combine.RuleCode, after.Combine.RuleCode, StringComparison.Ordinal))
        {
            return NotchV22ExplanationStep.RatioBeforeRounding;
        }

        if (!Rounded(before).SequenceEqual(Rounded(after)) || before.Combine.BeforeGuard != after.Combine.BeforeGuard ||
            before.Combine.ToRegularPercent != after.Combine.ToRegularPercent || before.Combine.ToFullPercent != after.Combine.ToFullPercent)
        {
            return NotchV22ExplanationStep.Rounding;
        }

        if (before.NullValue != after.NullValue || before.Combine.AfterGuard != after.Combine.AfterGuard ||
            before.Combine.ContinuationSubtraction != after.Combine.ContinuationSubtraction ||
            !before.Guard.SequenceEqual(after.Guard) || !SameSplit(before.Rows, after.Rows))
        {
            return NotchV22ExplanationStep.GuardOrContinuationSplit;
        }

        throw new InvalidOperationException("Differing V22 payloads have identical recorded steps; the explanation is incomplete.");
    }

    private static bool SameSplit(ImmutableArray<NotchV22ExplainedRow> before, ImmutableArray<NotchV22ExplainedRow> after) =>
        before.Length == after.Length && before.OrderBy(static row => row.Ordinal).Zip(after.OrderBy(static row => row.Ordinal),
            static (left, right) => left.Ordinal == right.Ordinal && left.Values[6] == right.Values[6] &&
                left.Legs.SequenceEqual(right.Legs)).All(static same => same);

    private static IEnumerable<(int Ic, int Diff, int Id, double Area, double Overlap, double Source)> Overlap(
        NotchV22RowExplanation source) => OrderedRegulars(source).Select(static item =>
        (item.IcIndex, item.DiffIndex, item.RegularPadId, item.RegularArea, item.OverlapArea, item.SourceArea));

    private static IEnumerable<(string Rule, bool Applied, double Effective, double Reachable, NotchV22ToFullLimit Limit)> ToFull(
        NotchV22RowExplanation source) => OrderedRegulars(source).Select(static item =>
        (item.RuleCode, item.ToFullApplied, item.EffectiveArea, item.ReachableArea, item.AdditionLimit));

    private static IEnumerable<(int Ic, int Diff, double Ratio, bool Strict)> Ratios(NotchV22RowExplanation source) =>
        OrderedTargets(source).Select(static item => (item.IcIndex, item.DiffIndex, item.RatioBeforeRounding, item.PassesStrictThreshold));

    private static IEnumerable<(int Ic, int Diff, int Percent)> Rounded(NotchV22RowExplanation source) =>
        OrderedTargets(source).Select(static item => (item.IcIndex, item.DiffIndex, item.RoundedPercent));

    private static IOrderedEnumerable<NotchV22RegularExplanation> OrderedRegulars(NotchV22RowExplanation source) =>
        source.Regulars.OrderBy(static item => item.IcIndex).ThenBy(static item => item.DiffIndex).ThenBy(static item => item.RegularPadId);

    private static IOrderedEnumerable<NotchV22TargetExplanation> OrderedTargets(NotchV22RowExplanation source) =>
        source.Targets.OrderBy(static item => item.IcIndex).ThenBy(static item => item.DiffIndex);

    private static ImmutableArray<string> RuleCodes(NotchV22RowExplanation? source) => source is null ? [] :
        OrderedRegulars(source).Select(static item => $"IC{item.IcIndex}/diff{item.DiffIndex}/reg{item.RegularPadId}:{item.RuleCode}")
            .Prepend(source.Combine.RuleCode).ToImmutableArray();
}
