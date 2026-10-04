using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

public sealed record PadInspectorRuleTraceEntry(
    string Rule,
    string Outcome,
    string Detail);

public sealed record PadInspectorNotchSnapshot(
    double ToRegularRatio,
    double ToFullRatio,
    double CombinedRatio,
    bool IsToFullEnabled,
    double Stage3Area,
    double StrictAreaThreshold,
    IReadOnlyList<PadInspectorNotchTargetSnapshot> Targets,
    string? Diagnostics)
{
    internal NotchV22TargetCoverageProjection? TargetCoverageProjection { get; init; }
    internal NotchComputationMode ComputationMode { get; init; } = NotchComputationMode.CadAllocation;
}

public sealed record PadInspectorNotchTargetSnapshot(
    int IcIndex,
    int DiffIndex,
    double EffectiveArea,
    double Ratio,
    int RatioPercentRounded,
    bool PassesStrictThreshold,
    bool IsAnchorDiff,
    int ToFullAppliedRegularCount,
    int RegularCount,
    IReadOnlyList<PadInspectorNotchTargetRegularAreaSnapshot> RegularAreas,
    IReadOnlyList<int> RegularPadIds);

public sealed record PadInspectorNotchTargetRegularAreaSnapshot(
    int RegularPadId,
    double EffectiveArea);

public sealed record PadInspectorMatchedRegularSnapshot(
    int RegularPadId,
    int? IcIndex,
    int? DiffIndex,
    double CadCoverage,
    double RegularCoverage);

public sealed record PadInspectorMatchedCadSnapshot(
    int CadPadId,
    int? DxfIndex,
    double CadCoverage,
    double RegularCoverage);

public sealed record CadPadInspectorSnapshot(
    int CadPadId,
    string Name,
    string Layer,
    double Area,
    Rect2 Bounds,
    Point2 Centroid,
    int Vertices,
    int? DxfIndex,
    int? CadOutputFwDiffOverride,
    bool IsDxfIndexAnchor,
    string DxfIndexDisplayText,
    string CadOutputFwDiffAssignmentModeText,
    int? IcIndex,
    string MatchText,
    string MatchDetailsText,
    IReadOnlyList<int> MatchedRegularPadIds,
    IReadOnlyList<PadInspectorMatchedRegularSnapshot> MatchedRegularDetails,
    bool IsNotchRowEligible,
    string NotchRowSummary,
    PadInspectorNotchSnapshot? Notch,
    IReadOnlyList<PadInspectorRuleTraceEntry> RuleTrace,
    double MatchConfidence);

public sealed record RegularPadInspectorSnapshot(
    int RegularPadId,
    int Index,
    int Row,
    int Col,
    int DisplayRow,
    int IcIndex,
    int DiffIndex,
    double Area,
    Rect2 Bounds,
    Point2 Centroid,
    FreeformType Freeform,
    string MatchText,
    string MatchDetailsText,
    IReadOnlyList<int> MatchedCadPadIds,
    IReadOnlyList<PadInspectorMatchedCadSnapshot> MatchedCadDetails,
    IReadOnlyList<PadInspectorRuleTraceEntry> RuleTrace,
    double MatchConfidence,
    string DiffSource)
{
    public string FreeformSource { get; init; } = "None";
    public string FreeformSourceDetail { get; init; } = "No Step 2 freeform tag.";
}

public sealed record PadInspectorSnapshot(
    string Kind,
    CadPadInspectorSnapshot? Cad,
    RegularPadInspectorSnapshot? Regular)
{
    public bool HasCad => Cad is not null;
    public bool HasRegular => Regular is not null;

    public IReadOnlyList<PadInspectorRuleTraceEntry> RuleTrace =>
        Cad?.RuleTrace ?? Regular?.RuleTrace ?? Array.Empty<PadInspectorRuleTraceEntry>();
}
