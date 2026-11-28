// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExplanationComparerTests
{
    [Theory]
    [InlineData(NotchV22ExplanationStep.CandidateSelection)]
    [InlineData(NotchV22ExplanationStep.Overlap)]
    [InlineData(NotchV22ExplanationStep.ToFullRuleOrArea)]
    [InlineData(NotchV22ExplanationStep.RatioBeforeRounding)]
    [InlineData(NotchV22ExplanationStep.Rounding)]
    [InlineData(NotchV22ExplanationStep.GuardOrContinuationSplit)]
    public void ChangedRowUsesTheEarliestDifferentStep(NotchV22ExplanationStep step)
    {
        var before = NotchRowExplanationTests.Generate();
        var after = ChangeStep(ChangeMainCombine(before), step);

        var difference = Assert.Single(NotchV22ExplanationComparer.Compare([before], [after]));

        Assert.Equal(NotchV22DifferenceKind.Changed, difference.Kind);
        Assert.Equal(step, difference.FirstStep);
        Assert.Equal(before.Rows[0].Values, difference.Before);
        Assert.Equal(after.Rows[0].Values, difference.After);
        Assert.NotEmpty(difference.RuleCodesBefore);
        Assert.NotEmpty(difference.RuleCodesAfter);
    }

    [Fact]
    public void AddedSourceHasASelectionStepAndOnlyAnAfterPayload()
    {
        var generated = NotchRowExplanationTests.Generate();
        var source = generated with { Rows = [generated.Rows[0]] };

        var difference = Assert.Single(NotchV22ExplanationComparer.Compare([], [source]));

        Assert.Equal(NotchV22DifferenceKind.Added, difference.Kind);
        Assert.Null(difference.Before);
        Assert.Equal(source.Rows[0].Values, difference.After);
        Assert.Equal(NotchV22ExplanationStep.CandidateSelection, difference.FirstStep);
    }

    [Fact]
    public void RemovedSourceHasASelectionStepAndOnlyABeforePayload()
    {
        var generated = NotchRowExplanationTests.Generate();
        var source = generated with { Rows = [generated.Rows[0]] };

        var difference = Assert.Single(NotchV22ExplanationComparer.Compare([source], []));

        Assert.Equal(NotchV22DifferenceKind.Removed, difference.Kind);
        Assert.Equal(source.Rows[0].Values, difference.Before);
        Assert.Null(difference.After);
        Assert.Equal(NotchV22ExplanationStep.CandidateSelection, difference.FirstStep);
    }

    [Fact]
    public void RemovedContinuationIsAttributedToFinalProjection()
    {
        var before = NotchRowExplanationTests.Generate();
        var after = before with { Rows = [before.Rows[0]] };

        var difference = Assert.Single(NotchV22ExplanationComparer.Compare([before], [after]));

        Assert.Equal(NotchV22DifferenceKind.Removed, difference.Kind);
        Assert.Equal(1, difference.Key.Ordinal);
        Assert.Equal(NotchV22ExplanationStep.GuardOrContinuationSplit, difference.FirstStep);
    }

    [Fact]
    public void UnchangedPayloadsDoNotReportDiagnosticChanges()
    {
        var before = NotchRowExplanationTests.Generate();
        var after = ChangeStep(before, NotchV22ExplanationStep.ToFullRuleOrArea);

        Assert.Empty(NotchV22ExplanationComparer.Compare([before], [after]));
    }

    [Fact]
    public void EffectiveAreaChangeIsAttributedToToFull()
    {
        var before = NotchRowExplanationTests.Generate();
        var after = ChangeMainCombine(before) with
        {
            Regulars = before.Regulars.SetItem(0, before.Regulars[0] with { EffectiveArea = before.Regulars[0].EffectiveArea + 1 }),
        };

        var difference = Assert.Single(NotchV22ExplanationComparer.Compare([before], [after]));

        Assert.Equal(NotchV22ExplanationStep.ToFullRuleOrArea, difference.FirstStep);
    }

    [Fact]
    public void UnexplainedPayloadDifferenceFailsInsteadOfGuessingAStep()
    {
        var before = NotchRowExplanationTests.Generate();
        var after = ChangeMainCombine(before);

        Assert.Throws<InvalidOperationException>(() => NotchV22ExplanationComparer.Compare([before], [after]));
    }

    private static NotchV22RowExplanation ChangeMainCombine(NotchV22RowExplanation source) => source with
    {
        Rows = source.Rows.SetItem(0, source.Rows[0] with { Values = source.Rows[0].Values.SetItem(1, source.Rows[0].Values[1] + 1) }),
    };

    private static NotchV22RowExplanation ChangeStep(NotchV22RowExplanation source, NotchV22ExplanationStep step) => step switch
    {
        NotchV22ExplanationStep.CandidateSelection => source with { SelectedCadPadId = source.SelectedCadPadId + 1 },
        NotchV22ExplanationStep.Overlap => source with
        {
            Regulars = source.Regulars.SetItem(0, source.Regulars[0] with { OverlapArea = source.Regulars[0].OverlapArea + 1 }),
        },
        NotchV22ExplanationStep.ToFullRuleOrArea => source with
        {
            Regulars = source.Regulars.SetItem(0, source.Regulars[0] with { RuleCode = "SYNTHETIC_CHANGED_RULE" }),
        },
        NotchV22ExplanationStep.RatioBeforeRounding => source with
        {
            Targets = source.Targets.SetItem(0, source.Targets[0] with { RatioBeforeRounding = source.Targets[0].RatioBeforeRounding + 0.01 }),
        },
        NotchV22ExplanationStep.Rounding => source with
        {
            Targets = source.Targets.SetItem(0, source.Targets[0] with { RoundedPercent = source.Targets[0].RoundedPercent + 1 }),
        },
        NotchV22ExplanationStep.GuardOrContinuationSplit => source with
        {
            Combine = source.Combine with { AfterGuard = source.Combine.AfterGuard + 1 },
        },
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };
}
