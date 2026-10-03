using System.Diagnostics;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class NotchV22CompensationService
{
    private static List<CadPad> BuildAllCadPads(CadPad cad, IReadOnlyList<CadPad>? allCadPads)
    {
        var allCad = (allCadPads ?? new[] { cad }).ToList();
        if (!allCad.Any(pad => pad.Id == cad.Id))
        {
            allCad.Add(cad);
        }

        return allCad;
    }

    private static List<NotchAllocation> BuildCompensationAllocations(CadPad cad, RegularGrid grid)
    {
        var allocations = new List<NotchAllocation>();
        var cadArea = Math.Max(cad.Area, AreaEpsilon);
        foreach (var regularPad in RegularGridCandidateQuery.QueryByBounds(grid, cad.Bounds))
        {
            var overlapArea = Polygon2.IntersectionAreaWithRect(cad.Polygon, regularPad.Bounds);
            if (overlapArea <= AreaEpsilon)
            {
                continue;
            }

            var ratio = overlapArea / cadArea;
            allocations.Add(new NotchAllocation(
                regularPad,
                ratio,
                NotchThresholdQ7Contract.EncodeFraction(ratio)));
        }

        return allocations;
    }

    private static StageAOverlapResult RunStageACollectOverlaps(
        CadPad cad,
        IReadOnlyList<NotchAllocation> allocations)
    {
        var overlappedRegulars = new List<OverlappedRegularInfo>();
        var rawToRegularRatio = 0.0;
        var overlappedRegularArea = 0.0;
        var overlapAreaTotal = 0.0;
        var overlapRegularCount = 0;

        foreach (var allocation in allocations)
        {
            var regularPad = allocation.Pad;
            var overlapArea = Math.Max(0.0, allocation.Ratio * cad.Area);
            if (overlapArea <= AreaEpsilon)
            {
                continue;
            }

            overlappedRegulars.Add(new OverlappedRegularInfo(regularPad, overlapArea));
            var regularArea = Math.Max(regularPad.Area, AreaEpsilon);
            rawToRegularRatio += overlapArea / regularArea;
            overlappedRegularArea += regularArea;
            overlapAreaTotal += overlapArea;
            overlapRegularCount++;
        }

        return new StageAOverlapResult(
            RawToRegularRatio: rawToRegularRatio,
            OverlapAreaTotal: overlapAreaTotal,
            OverlappedRegularAreaTotal: overlappedRegularArea,
            OverlapRegularCount: overlapRegularCount,
            OverlappedRegulars: overlappedRegulars);
    }

    private static StageBBoundaryContext RunStageBBuildBoundaryAndOwnership(
        IReadOnlyList<OverlappedRegularInfo> overlappedRegulars,
        NotchV22BoundaryQueryContext boundaryQueryContext,
        IReadOnlySet<int> boundaryRegularIndices)
    {
        return new StageBBoundaryContext(
            boundaryQueryContext,
            FilterBoundaryRegularIndices(
                overlappedRegulars.Select(static item => item.Pad),
                boundaryRegularIndices));
    }

    private static StageCEvaluationResult RunStageCEvaluateToFullAndDiagnostics(
        CadPad cad,
        IReadOnlyList<OverlappedRegularInfo> overlappedRegulars,
        StageBBoundaryContext boundaryContext,
        bool enableToFull,
        bool enableToFullRuleEngine,
        bool enableToFullRuleTrace,
        bool enableBoundaryVirtualAreaCap,
        double boundaryVirtualAreaCapRatio)
    {
        var normalizedBoundaryVirtualAreaCapRatio = NormalizeBoundaryVirtualAreaCapRatio(boundaryVirtualAreaCapRatio);
        var toFullSeedRegionPolygons = new List<Polygon2>();
        var toFullCandidateRegionPolygons = new List<Polygon2>();
        var toFullRegionPolygons = new List<Polygon2>();
        var regularDebugInfos = new List<NotchV22RegularDebugInfo>();
        long ownerAndBlockerLookupElapsedMs = 0;
        long exactOccupiedAreaElapsedMs = 0;
        long sourceCoverageElapsedMs = 0;
        long reachabilityElapsedMs = 0;
        long ruleDecisionElapsedMs = 0;
        long previewAndDebugElapsedMs = 0;

        foreach (var overlap in overlappedRegulars)
        {
            var regularPad = overlap.Pad;
            var overlapArea = overlap.OverlapArea;
            var regularArea = Math.Max(regularPad.Area, AreaEpsilon);
            var strictOverlapThreshold = Math.Max(AreaEpsilon, regularArea * boundaryContext.StrictOverlapRatio);
            var hasSourceArea = overlapArea > strictOverlapThreshold;
            var stepStartedAt = Stopwatch.GetTimestamp();
            var ownerCadPads = hasSourceArea
                ? boundaryContext.GetStrictOwnerCadPads(regularPad)
                : Array.Empty<CadPad>();
            var ownerCadIds = ownerCadPads.Select(static pad => pad.Id).ToArray();
            var blockerCandidates = hasSourceArea
                ? boundaryContext.GetBlockingCadPads(regularPad, cad.Id)
                : Array.Empty<CadPad>();
            ownerAndBlockerLookupElapsedMs += GetElapsedMilliseconds(stepStartedAt);
            var isBoundaryRegular = hasSourceArea && boundaryContext.EffectiveBoundaryRegularIndices.Contains(regularPad.Index);
            var effectiveExpansionEpsilon = Math.Max(AreaEpsilon, regularArea * 1e-9);
            stepStartedAt = Stopwatch.GetTimestamp();
            var exactOccupiedArea = hasSourceArea
                ? ComputeExactOccupiedAreaInRegular(cad, regularPad, blockerCandidates)
                : overlapArea;
            exactOccupiedAreaElapsedMs += GetElapsedMilliseconds(stepStartedAt);
            var exactFreeArea = hasSourceArea
                ? Math.Max(0.0, regularArea - exactOccupiedArea)
                : 0.0;
            var effectiveExpansionThreshold = Math.Max(strictOverlapThreshold, effectiveExpansionEpsilon);
            var hasEffectiveExpansion = hasSourceArea && exactFreeArea > effectiveExpansionThreshold;
            var requiresReachability = enableToFull && hasSourceArea && hasEffectiveExpansion;
            var isToFullBoundaryCandidate = requiresReachability;
            ToFullReachabilityResult reachability;
            stepStartedAt = Stopwatch.GetTimestamp();
            if (requiresReachability)
            {
                reachability = ComputeReachabilityInRegular(cad, regularPad, blockerCandidates, overlapArea);
                reachabilityElapsedMs += GetElapsedMilliseconds(stepStartedAt);
            }
            else
            {
                reachability = ComputeSourceCoverageInRegular(cad, regularPad, overlapArea);
                sourceCoverageElapsedMs += GetElapsedMilliseconds(stepStartedAt);
            }
            var hasDirectionalBlocker = hasSourceArea && reachability.BlockedCellCount > 0;
            var reachabilityAreaTolerance = Math.Max(
                effectiveExpansionEpsilon,
                regularArea / (ReachabilityResolution * ReachabilityResolution));
            var maxAllowedReachableArea = overlapArea + exactFreeArea + reachabilityAreaTolerance;
            var hasReachableExpansion = hasEffectiveExpansion &&
                                        (
                                            (reachability.TotalCellCount > 0 &&
                                             reachability.ReachableCellCount > reachability.SourceCellCount) ||
                                            reachability.ReachableArea > (overlapArea + effectiveExpansionThreshold)
                                        ) &&
                                        reachability.ReachableArea <= maxAllowedReachableArea;
            stepStartedAt = Stopwatch.GetTimestamp();
            var toFullRuleDecision = EvaluateToFullRuleDecision(
                enableToFull,
                hasSourceArea,
                hasEffectiveExpansion,
                hasDirectionalBlocker,
                hasReachableExpansion,
                enableToFullRuleEngine,
                enableToFullRuleTrace);
            ruleDecisionElapsedMs += GetElapsedMilliseconds(stepStartedAt);
            var toFullRuleCode = toFullRuleDecision.RuleCode;
            var applyToFullOnThisRegular = toFullRuleDecision.ShouldApplyToFull;
            var toFullRuleTrace = enableToFullRuleTrace
                ? toFullRuleDecision.Trace
                : Array.Empty<NotchToFullRuleTraceEntry>();

            stepStartedAt = Stopwatch.GetTimestamp();
            if (isToFullBoundaryCandidate)
            {
                toFullCandidateRegionPolygons.Add(ToRectPolygon(regularPad.Bounds));

                var overlapPolygons = BuildCadOverlapPolygons(cad.Polygon, regularPad.Bounds);
                if (overlapPolygons.Length > 0)
                {
                    toFullSeedRegionPolygons.AddRange(overlapPolygons);
                }
                else
                {
                    if (reachability.SourcePolygons.Count > 0)
                    {
                        toFullSeedRegionPolygons.AddRange(reachability.SourcePolygons);
                    }
                }
            }

            if (applyToFullOnThisRegular)
            {
                if (reachability.ReachablePolygons.Count > 0)
                {
                    toFullRegionPolygons.AddRange(reachability.ReachablePolygons);
                }
            }

            var sourceArea = hasSourceArea ? reachability.SourceArea : 0.0;
            var reachableArea = applyToFullOnThisRegular
                ? Math.Max(overlapArea, reachability.ReachableArea)
                : overlapArea;
            var stage3EffectiveArea = ResolveStage3EffectiveArea(
                overlapArea,
                reachableArea,
                applyToFullOnThisRegular,
                enableBoundaryVirtualAreaCap,
                normalizedBoundaryVirtualAreaCapRatio);
            regularDebugInfos.Add(new NotchV22RegularDebugInfo(
                RegularPadId: regularPad.RegularPadId,
                RegularIndex: regularPad.Index,
                IcIndex: regularPad.IcIndex,
                DiffIndex: regularPad.DiffIndex,
                Row: regularPad.Row,
                Col: regularPad.Col,
                RegularArea: regularArea,
                OverlapArea: overlapArea,
                SourceArea: sourceArea,
                BlockedArea: hasDirectionalBlocker ? Math.Max(0.0, reachability.BlockedArea) : 0.0,
                ReachableArea: reachableArea,
                Stage3EffectiveArea: stage3EffectiveArea,
                SourceCellCount: reachability.SourceCellCount,
                BlockedCellCount: hasDirectionalBlocker ? reachability.BlockedCellCount : 0,
                ReachableCellCount: reachability.ReachableCellCount,
                TotalCellCount: reachability.TotalCellCount,
                BlockerCandidateCount: blockerCandidates.Length,
                OwnerCadPadCount: ownerCadIds.Length,
                OwnerCadPadIds: ownerCadIds,
                IsBoundaryRegular: isBoundaryRegular,
                IsToFullBoundaryCandidate: isToFullBoundaryCandidate,
                HasEffectiveExpansion: hasReachableExpansion,
                HasDirectionalBlocker: hasDirectionalBlocker,
                ToFullRuleTrace: toFullRuleTrace,
                ToFullRuleCode: toFullRuleCode,
                IsToFullApplied: applyToFullOnThisRegular));
            previewAndDebugElapsedMs += GetElapsedMilliseconds(stepStartedAt);
        }

        return new StageCEvaluationResult(
            toFullSeedRegionPolygons,
            toFullCandidateRegionPolygons,
            toFullRegionPolygons,
            regularDebugInfos,
            new NotchV22StageCSubphaseTimings(
                OwnerAndBlockerLookupElapsedMs: ownerAndBlockerLookupElapsedMs,
                ExactOccupiedAreaElapsedMs: exactOccupiedAreaElapsedMs,
                SourceCoverageElapsedMs: sourceCoverageElapsedMs,
                ReachabilityElapsedMs: reachabilityElapsedMs,
                RuleDecisionElapsedMs: ruleDecisionElapsedMs,
                PreviewAndDebugElapsedMs: previewAndDebugElapsedMs));
    }

    private static double ResolveStage3EffectiveArea(
        double overlapArea,
        double reachableArea,
        bool applyToFullOnThisRegular,
        bool enableBoundaryVirtualAreaCap,
        double boundaryVirtualAreaCapRatio)
    {
        var insideArea = Math.Max(0.0, overlapArea);
        var rawReachableArea = Math.Max(insideArea, reachableArea);
        if (!applyToFullOnThisRegular || !enableBoundaryVirtualAreaCap)
        {
            return rawReachableArea;
        }

        var virtualArea = Math.Max(0.0, rawReachableArea - insideArea);
        var maxVirtualArea = insideArea * NormalizeBoundaryVirtualAreaCapRatio(boundaryVirtualAreaCapRatio);
        return insideArea + Math.Min(virtualArea, maxVirtualArea);
    }

    private static NotchToFullRuleDecision EvaluateToFullRuleDecision(
        bool enableToFull,
        bool hasSourceArea,
        bool hasEffectiveExpansion,
        bool hasDirectionalBlocker,
        bool hasReachableExpansion,
        bool enableToFullRuleEngine,
        bool enableToFullRuleTrace)
    {
        return NotchToFullRuleDecisionAdapter.Evaluate(
            new NotchToFullRuleAdapterContext(
                EnableRuleEngine: enableToFullRuleEngine,
                EnableRuleTrace: enableToFullRuleTrace,
                EnableToFull: enableToFull,
                HasSourceArea: hasSourceArea,
                HasEffectiveExpansion: hasEffectiveExpansion,
                HasDirectionalBlocker: hasDirectionalBlocker,
                HasReachableExpansion: hasReachableExpansion));
    }

    private static NotchV22CompensationResult RunStageDBuildResult(
        CadPad cad,
        StageAOverlapResult stageA,
        StageCEvaluationResult stageC,
        bool enableToRegular,
        bool enableToFull)
    {
        var safeCadArea = Math.Max(cad.Area, AreaEpsilon);
        var toFullSeedPolygons = enableToFull
            ? BuildToFullPolygons(stageC.ToFullSeedRegionPolygons)
            : Array.Empty<Polygon2>();
        var toFullCandidatePolygons = enableToFull
            ? BuildToFullPolygons(stageC.ToFullCandidateRegionPolygons)
            : Array.Empty<Polygon2>();
        var toFullPolygons = enableToFull
            ? BuildToFullPolygons(stageC.ToFullRegionPolygons)
            : Array.Empty<Polygon2>();
        var isToFullEnabled = enableToFull && stageC.RegularDebugInfos.Any(static info => info.IsToFullApplied);
        var stage3Area = safeCadArea;
        if (isToFullEnabled)
        {
            var expansionArea = stageC.RegularDebugInfos
                .Where(static info => info.IsToFullApplied)
                .Sum(static info => Math.Max(0.0, info.Stage3EffectiveArea - info.OverlapArea));
            stage3Area = Math.Max(safeCadArea, safeCadArea + expansionArea);
        }

        var toFullRatio = Math.Max(1.0, stage3Area / safeCadArea);
        var toRegularRatio = enableToRegular ? stageA.RawToRegularRatio : 1.0;
        var combinedRatio = toRegularRatio * toFullRatio;
        return new NotchV22CompensationResult(
            ToRegularRatio: toRegularRatio,
            ToFullRatio: toFullRatio,
            CombinedRatio: combinedRatio,
            IsToFullEnabled: isToFullEnabled,
            Stage3Area: stage3Area,
            OverlapAreaTotal: stageA.OverlapAreaTotal,
            OverlappedRegularAreaTotal: stageA.OverlappedRegularAreaTotal,
            OverlapRegularCount: stageA.OverlapRegularCount,
            ToFullSeedPolygons: toFullSeedPolygons,
            ToFullCandidatePolygons: toFullCandidatePolygons,
            ToFullPolygons: toFullPolygons,
            RegularDebugInfos: stageC.RegularDebugInfos);
    }

    private static long GetElapsedMilliseconds(long startedAtTimestamp)
    {
        return (long)Stopwatch.GetElapsedTime(startedAtTimestamp).TotalMilliseconds;
    }
}
