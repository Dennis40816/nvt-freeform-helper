using System.Diagnostics;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Computes Notch 2.2 compensation ratios for one CAD pad.
/// </summary>
public sealed partial class NotchV22CompensationService
{
    private const double AreaEpsilon = 1e-12;
    private const int ReachabilityResolution = 40;
    private const double DefaultStrictOverlapRatio = 0.001; // 0.1%
    private const double BlockerCellCoverageRatio = 0.2; // legacy reachability helper

    /// <summary>
    /// Builds the complete context used by the canonical compensation path.
    /// </summary>
    public static NotchV22CompensationContext CreateContext(
        CadPad cad,
        RegularGrid grid,
        bool enableToRegular,
        bool enableToFull,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        double? strictOverlapRatioOverride = null,
        bool enableToFullRuleEngine = true,
        bool enableToFullRuleTrace = false,
        bool enableBoundaryVirtualAreaCap = false,
        double boundaryVirtualAreaCapRatio = 1.0)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);
        var allocations = NotchAllocationService.BuildAllocations(cad, grid);
        var strictOverlapRatio = NormalizeStrictOverlapRatio(strictOverlapRatioOverride);
        return new NotchV22CompensationContext(
            cad,
            allocations,
            BuildBoundaryRegularIndicesForOverlaps(
                allocations.Select(static allocation => allocation.Pad),
                grid,
                activeRegularPadIds),
            activeRegularPadIds,
            CreateBoundaryQueryContext(BuildAllCadPads(cad, allCadPads), strictOverlapRatio),
            enableToRegular,
            enableToFull,
            enableToFullRuleEngine,
            enableToFullRuleTrace,
            enableBoundaryVirtualAreaCap,
            boundaryVirtualAreaCapRatio);
    }

    /// <summary>
    /// Compatibility adapter for callers that still pass compensation inputs separately.
    /// </summary>
    /// <param name="cad">Target CAD pad.</param>
    /// <param name="grid">Regular grid.</param>
    /// <param name="enableToRegular">Whether To Regular compensation is enabled.</param>
    /// <param name="enableToFull">Whether To Full compensation is enabled.</param>
    /// <param name="allCadPads">Optional full CAD set used as collision blockers for directional To Full expansion.</param>
    /// <returns>Compensation ratios and overlap diagnostics.</returns>
    public static NotchV22CompensationResult Compute(
        CadPad cad,
        RegularGrid grid,
        bool enableToRegular,
        bool enableToFull,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        double? strictOverlapRatioOverride = null,
        bool enableToFullRuleEngine = true,
        bool enableToFullRuleTrace = false,
        bool enableBoundaryVirtualAreaCap = false,
        double boundaryVirtualAreaCapRatio = 1.0,
        IReadOnlyList<NotchAllocation>? precomputedAllocations = null,
        IReadOnlySet<int>? precomputedBoundaryRegularIndices = null,
        NotchV22BoundaryQueryContext? sharedBoundaryQueryContext = null,
        Action<NotchV22CompensationStageTimings>? timingReporter = null)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);
        var strictOverlapRatio = NormalizeStrictOverlapRatio(strictOverlapRatioOverride);
        var allocations = precomputedAllocations ?? NotchAllocationService.BuildAllocations(cad, grid);
        IReadOnlyList<CadPad> boundaryCadPool = sharedBoundaryQueryContext is null
            ? BuildAllCadPads(cad, allCadPads)
            : Array.Empty<CadPad>();
        var boundaryQueryContext = sharedBoundaryQueryContext ??
                                   CreateBoundaryQueryContext(boundaryCadPool, strictOverlapRatio);
        var boundaryRegularIndices = precomputedBoundaryRegularIndices ??
                                     BuildBoundaryRegularIndicesForOverlaps(
                                         allocations.Select(static allocation => allocation.Pad),
                                         grid,
                                         activeRegularPadIds);
        return Compute(
            new NotchV22CompensationContext(
                cad,
                allocations,
                boundaryRegularIndices,
                activeRegularPadIds,
                boundaryQueryContext,
                enableToRegular,
                enableToFull,
                enableToFullRuleEngine,
                enableToFullRuleTrace,
                enableBoundaryVirtualAreaCap,
                boundaryVirtualAreaCapRatio),
            timingReporter);
    }

    /// <summary>
    /// Calculates ToRegular/ToFull/Combined ratios from one complete context.
    /// </summary>
    public static NotchV22CompensationResult Compute(
        NotchV22CompensationContext context,
        Action<NotchV22CompensationStageTimings>? timingReporter = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        var stageAStopwatch = Stopwatch.StartNew();
        var stageA = RunStageACollectOverlaps(context.Cad, context.Allocations);
        stageAStopwatch.Stop();
        var stageBStopwatch = Stopwatch.StartNew();
        var stageB = RunStageBBuildBoundaryAndOwnership(
            stageA.OverlappedRegulars,
            context.BoundaryQueryContext,
            context.BoundaryRegularIndices);
        stageBStopwatch.Stop();
        var stageCStopwatch = Stopwatch.StartNew();
        var stageC = RunStageCEvaluateToFullAndDiagnostics(
            context.Cad,
            stageA.OverlappedRegulars,
            stageB,
            context.EnableToFull,
            context.EnableToFullRuleEngine,
            context.EnableToFullRuleTrace,
            context.EnableBoundaryVirtualAreaCap,
            context.BoundaryVirtualAreaCapRatio);
        stageCStopwatch.Stop();
        var stageDStopwatch = Stopwatch.StartNew();
        var result = RunStageDBuildResult(
            context.Cad,
            stageA,
            stageC,
            context.EnableToRegular,
            context.EnableToFull);
        stageDStopwatch.Stop();
        timingReporter?.Invoke(new NotchV22CompensationStageTimings(
            stageAStopwatch.ElapsedMilliseconds,
            stageBStopwatch.ElapsedMilliseconds,
            stageCStopwatch.ElapsedMilliseconds,
            stageDStopwatch.ElapsedMilliseconds,
            stageC.StageCBreakdown));
        return result;
    }

    public static int ToPercentRounded(double ratio)
    {
        return (int)Math.Round(Math.Max(0.0, ratio) * 100.0);
    }

    public static double NormalizeStrictOverlapRatio(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return DefaultStrictOverlapRatio;
        }

        return Math.Clamp(value.Value, 0.0, 1.0);
    }

    public static double NormalizeBoundaryVirtualAreaCapRatio(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return 1.0;
        }

        return Math.Clamp(value.Value, 0.0, 10.0);
    }

    public static NotchV22BoundaryQueryContext CreateBoundaryQueryContext(
        IReadOnlyList<CadPad> allCadPads,
        double strictOverlapRatio)
    {
        ArgumentNullException.ThrowIfNull(allCadPads);

        return new NotchV22BoundaryQueryContext(
            allCadPads,
            NormalizeStrictOverlapRatio(strictOverlapRatio));
    }
}

/// <summary>
/// Complete, output-version-neutral inputs for one Notch 2.2 compensation computation.
/// </summary>
public sealed class NotchV22CompensationContext
{
    internal NotchV22CompensationContext(
        CadPad cad,
        IReadOnlyList<NotchAllocation> allocations,
        IReadOnlySet<int> boundaryRegularIndices,
        IReadOnlySet<int>? activeRegularPadIds,
        NotchV22CompensationService.NotchV22BoundaryQueryContext boundaryQueryContext,
        bool enableToRegular,
        bool enableToFull,
        bool enableToFullRuleEngine,
        bool enableToFullRuleTrace,
        bool enableBoundaryVirtualAreaCap,
        double boundaryVirtualAreaCapRatio)
    {
        Cad = cad ?? throw new ArgumentNullException(nameof(cad));
        Allocations = allocations ?? throw new ArgumentNullException(nameof(allocations));
        BoundaryRegularIndices = boundaryRegularIndices ?? throw new ArgumentNullException(nameof(boundaryRegularIndices));
        ActiveRegularPadIds = activeRegularPadIds;
        BoundaryQueryContext = boundaryQueryContext ?? throw new ArgumentNullException(nameof(boundaryQueryContext));
        EnableToRegular = enableToRegular;
        EnableToFull = enableToFull;
        EnableToFullRuleEngine = enableToFullRuleEngine;
        EnableToFullRuleTrace = enableToFullRuleTrace;
        EnableBoundaryVirtualAreaCap = enableBoundaryVirtualAreaCap;
        BoundaryVirtualAreaCapRatio = boundaryVirtualAreaCapRatio;
    }

    internal CadPad Cad { get; }
    internal IReadOnlyList<NotchAllocation> Allocations { get; }
    internal IReadOnlySet<int> BoundaryRegularIndices { get; }
    internal IReadOnlySet<int>? ActiveRegularPadIds { get; }
    internal NotchV22CompensationService.NotchV22BoundaryQueryContext BoundaryQueryContext { get; }
    internal bool EnableToRegular { get; }
    internal bool EnableToFull { get; }
    internal bool EnableToFullRuleEngine { get; }
    internal bool EnableToFullRuleTrace { get; }
    internal bool EnableBoundaryVirtualAreaCap { get; }
    internal double BoundaryVirtualAreaCapRatio { get; }
}

/// <summary>
/// Notch 2.2 compensation result for one CAD pad.
/// </summary>
public sealed record NotchV22CompensationResult(
    double ToRegularRatio,
    double ToFullRatio,
    double CombinedRatio,
    bool IsToFullEnabled,
    double Stage3Area,
    double OverlapAreaTotal,
    double OverlappedRegularAreaTotal,
    int OverlapRegularCount,
    IReadOnlyList<Polygon2> ToFullSeedPolygons,
    IReadOnlyList<Polygon2> ToFullCandidatePolygons,
    IReadOnlyList<Polygon2> ToFullPolygons,
    IReadOnlyList<NotchV22RegularDebugInfo> RegularDebugInfos);

/// <summary>
/// Per-regular diagnostics for Notch 2.2 To Full reachability.
/// </summary>
public sealed record NotchV22RegularDebugInfo(
    int RegularPadId,
    int RegularIndex,
    int IcIndex,
    int DiffIndex,
    int Row,
    int Col,
    double RegularArea,
    double OverlapArea,
    double SourceArea,
    double BlockedArea,
    double ReachableArea,
    double Stage3EffectiveArea,
    int SourceCellCount,
    int BlockedCellCount,
    int ReachableCellCount,
    int TotalCellCount,
    int BlockerCandidateCount,
    int OwnerCadPadCount,
    IReadOnlyList<int> OwnerCadPadIds,
    bool IsBoundaryRegular,
    bool IsToFullBoundaryCandidate,
    bool HasEffectiveExpansion,
    bool HasDirectionalBlocker,
    IReadOnlyList<NotchToFullRuleTraceEntry> ToFullRuleTrace,
    string ToFullRuleCode,
    bool IsToFullApplied);
