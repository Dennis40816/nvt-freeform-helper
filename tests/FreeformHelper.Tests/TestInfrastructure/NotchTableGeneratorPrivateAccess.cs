using System.Runtime.CompilerServices;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

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
    private const string DiffLegListType = "System.Collections.Generic.List`1[[FreeformHelper.Application.Services.NotchTableGenerator+V22DiffLeg, FreeformHelper.Application]]";

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
