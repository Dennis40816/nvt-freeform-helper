// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.Application.Services;

/// <summary>Immutable evidence from one CadAllocation generation, grouped by IC/source diff.</summary>
public sealed record NotchV22RowExplanation(
    int IcIndex,
    int SourceDiffIndex,
    int SelectedCadPadId,
    int SelectedRegularPadId,
    int NullValue,
    ImmutableArray<NotchV22CandidateExplanation> Candidates,
    ImmutableArray<NotchV22RegularExplanation> Regulars,
    ImmutableArray<NotchV22TargetExplanation> Targets,
    NotchV22CombineExplanation Combine,
    ImmutableArray<NotchV22GuardExplanation> Guard,
    ImmutableArray<NotchV22ExplainedRow> Rows)
{
    public ImmutableArray<Polygon2> PreviewPolygons { get; init; } = [];
}

/// <summary>Selection order: first five keys descending, then CAD ID ascending; ties retain input order.</summary>
public sealed record NotchV22CandidateExplanation(
    int CadPadId,
    int RegularPadId,
    bool HasToFullInfluence,
    bool HasLegs,
    bool IsAnchorMatchedCad,
    double SourceAreaScore,
    int CombinePercent,
    double MaxAllocationRatio);

public enum NotchV22ToFullLimit
{
    None,
    VirtualAreaCap,
    ReachableFreeArea,
}

public sealed record NotchV22RegularExplanation(
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    double RegularArea,
    double OverlapArea,
    double SourceArea,
    double EffectiveArea,
    double ReachableArea,
    bool IsBoundaryRegular,
    string RuleCode,
    bool ToFullApplied,
    NotchV22ToFullLimit AdditionLimit);

public enum NotchV22TargetDisposition
{
    Emitted,
    RetainedAnchor,
    RoundedToZero,
    BelowStrictThreshold,
    GuardRoundedToZero,
}

public sealed record NotchV22TargetExplanation(
    int IcIndex,
    int DiffIndex,
    double RatioBeforeRounding,
    int RoundedPercent,
    bool PassesStrictThreshold,
    int ToFullAppliedRegularCount,
    NotchV22TargetDisposition Disposition);

public sealed record NotchV22ExplanationLeg(int TargetDiffIndex, int Percent);

public sealed record NotchV22CombineExplanation(
    string RuleCode,
    int ToRegularPercent,
    int ToFullPercent,
    int BeforeGuard,
    int RetainedBeforeGuard,
    ImmutableArray<NotchV22ExplanationLeg> LegsBeforeGuard,
    int AfterGuard,
    int ContinuationSubtraction);

/// <summary>Guard rounding uses floor of the scaled magnitude, including retained anchor coverage.</summary>
public sealed record NotchV22GuardExplanation(
    int TargetDiffIndex,
    bool IsRetainedAnchor,
    int BeforePercent,
    double Scale,
    int AfterPercent);

/// <summary>Ordinal zero is MAIN (also for no-target rows); subsequent ordinals identify CONT rows.</summary>
public sealed record NotchV22ExplainedRow(int Ordinal, ImmutableArray<int> Values,
    ImmutableArray<NotchV22ExplanationLeg> Legs);
