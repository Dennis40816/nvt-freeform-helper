using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Tests.TestInfrastructure;

/// <summary>
/// Compile-time bound access to the private static seams and private nested records of <see cref="NotchTableGenerator"/>.
/// The nested records (<c>V22CadCandidate</c>, <c>V22DiffLeg</c>) are private, so they travel as <see cref="object"/>
/// and are named through <see cref="UnsafeAccessorTypeAttribute"/>.
/// </summary>
internal static class NotchTableGeneratorPrivateAccess
{
    private const string CandidateType = "FreeformHelper.Application.Services.NotchTableGenerator+V22CadCandidate, FreeformHelper.Application";
    private const string CandidateReadOnlyListType = "System.Collections.Generic.IReadOnlyList`1[[FreeformHelper.Application.Services.NotchTableGenerator+V22CadCandidate, FreeformHelper.Application]]";
    private const string CandidatesByDiffType = "System.Collections.Generic.IReadOnlyDictionary`2[System.ValueTuple`2[System.Int32,System.Int32],System.Collections.Generic.List`1[[FreeformHelper.Application.Services.NotchTableGenerator+V22CadCandidate, FreeformHelper.Application]]]";
    private const string DiffLegType = "FreeformHelper.Application.Services.NotchTableGenerator+V22DiffLeg, FreeformHelper.Application";
    private const string GenerationContextType = "FreeformHelper.Application.Services.NotchTableGenerator+CadAllocationGenerationContext, FreeformHelper.Application";
    private const string AllocationProfileType = "FreeformHelper.Application.Services.NotchTableGenerator+CadAllocationProfile, FreeformHelper.Application";
    private const string TimingAccumulatorType = "FreeformHelper.Application.Services.NotchTableGenerator+CandidateTimingAccumulator, FreeformHelper.Application";
    private const string CanonicalBuildResultType = "FreeformHelper.Application.Services.NotchTableGenerator+CanonicalCandidateBuildResult, FreeformHelper.Application";
    private const string ProfileCandidateListType = "System.Collections.Generic.List`1[System.Collections.Generic.KeyValuePair`2[System.ValueTuple`2[System.Int32,System.Int32],[FreeformHelper.Application.Services.NotchTableGenerator+V22CadCandidate, FreeformHelper.Application]]]";
    private const string DiffLegListType = "System.Collections.Generic.List`1[[FreeformHelper.Application.Services.NotchTableGenerator+V22DiffLeg, FreeformHelper.Application]]";

    /// <summary>The private nested type that carries the frozen inputs of one CAD allocation generation.</summary>
    public static Type? GenerationContextClrType => Type.GetType(GenerationContextType);

    // The three candidate helpers below are bound by their exact signature. A call with a null context
    // passes binding and then fails on the first read of the context, so a changed parameter list shows up
    // as a MissingMethodException instead of a NullReferenceException.
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildCanonicalCandidatesByDiff")]
    [return: UnsafeAccessorType(CanonicalBuildResultType)]
    public static extern object BuildCanonicalCandidatesByDiff(
        NotchTableGenerator? owner,
        [UnsafeAccessorType(GenerationContextType)] object? context,
        IProgress<NotchGenerationProgress>? progress,
        int candidatePhaseStep,
        int mergePhaseStep,
        int phaseStepCount);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildCanonicalCandidatesForProfile")]
    [return: UnsafeAccessorType(ProfileCandidateListType)]
    public static extern object BuildCanonicalCandidatesForProfile(
        NotchTableGenerator? owner,
        [UnsafeAccessorType(AllocationProfileType)] object? profile,
        [UnsafeAccessorType(GenerationContextType)] object? context,
        [UnsafeAccessorType(TimingAccumulatorType)] object? timingAccumulator,
        out NotchV22ResolvedResult? selectedResolvedResult);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildV22CadCandidate")]
    [return: UnsafeAccessorType(CandidateType)]
    public static extern object BuildV22CadCandidate(
        NotchTableGenerator? owner,
        RegularPad? anchor,
        int sourceDiffIndex,
        [UnsafeAccessorType(AllocationProfileType)] object? profile,
        [UnsafeAccessorType(GenerationContextType)] object? context,
        [UnsafeAccessorType(TimingAccumulatorType)] object? timingAccumulator,
        out NotchV22ResolvedResult? selectedResolvedResult);

    // Static kind: the first parameter only names the owner type; its value is ignored, so callers pass null.
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "SelectPrimaryV22Candidate")]
    [return: UnsafeAccessorType(CandidateType)]
    public static extern object SelectPrimaryV22Candidate(
        NotchTableGenerator? owner,
        [UnsafeAccessorType(CandidateReadOnlyListType)] object candidates);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildV22DiffCentricRows")]
    public static extern IReadOnlyList<NotchTableRow> BuildV22DiffCentricRows(
        NotchTableGenerator? owner,
        [UnsafeAccessorType(CandidatesByDiffType)] object candidatesByDiff,
        int nullValue,
        bool applyTargetCoverageGuard,
        int targetCoverageCapPercent);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildV22DiffLegs")]
    [return: UnsafeAccessorType(DiffLegListType)]
    public static extern object BuildV22DiffLegs(
        NotchTableGenerator? owner,
        IReadOnlyList<NotchV22TargetAllocation> targets,
        NotchCompensationModel model);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "BuildToFullCoverageAudit")]
    public static extern NotchToFullCoverageAudit BuildToFullCoverageAudit(
        NotchTableGenerator? owner,
        [UnsafeAccessorType(CandidatesByDiffType)] object candidatesByDiff,
        IReadOnlyList<NotchTableRow> canonicalRows,
        int nullValue);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(DiffLegType)]
    public static extern object NewV22DiffLeg(int targetDiffIndex, int ratioPercent, bool hasToFullCoverage);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(CandidateType)]
    public static extern object NewV22CadCandidate(
        int icIndex,
        int diffIndex,
        int regularPadId,
        int cadPadId,
        double maxRatio,
        string? projectionError,
        int combinePercent,
        string commentPrefix,
        double sourceAreaScore,
        bool isAnchorMatchedCad,
        bool hasToFullInfluence,
        [UnsafeAccessorType(DiffLegListType)] object legs);
}
