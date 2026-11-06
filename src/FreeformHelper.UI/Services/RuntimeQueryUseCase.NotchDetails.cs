using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private RuntimeQueryResponseEnvelope QueryCadNotch(int cadPadId, int limit, int targetLimit, int polygonLimit)
    {
        var helper = _shellViewModel.FreeformHelper;
        if (!_notchQueryCacheService.TryGetCadNotchCacheEntry(helper, cadPadId, out var cacheEntry, out var error))
        {
            return error ?? RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: "Notch compensation is unavailable.");
        }

        var cad = cacheEntry.Cad;
        var resolved = cacheEntry.Resolved;
        var compensation = resolved.Compensation;
        var returnedRegulars = BuildCadNotchRegularRows(compensation, limit);
        var strictOverlapPercent = _shellViewModel.FreeformHelper.GetNotchMultiOwnerStrictOverlapPercent();
        var allocation = resolved.TargetAllocation;
        var display = NotchDisplayProjector.Build(
            compensation,
            allocation,
            cacheEntry.DiagnosticsText,
            cad.Notch?.ComputationMode ?? helper.CurrentNotchComputationMode);
        var returnedTargets = BuildCadNotchTargetRows(allocation, targetLimit);
        var seedBounds = BuildCadNotchPolygonBoundsRows(resolved.Stage1SeedPolygons, polygonLimit);
        var finalBounds = BuildCadNotchPolygonBoundsRows(resolved.Stage3FinalOutlinePolygons, polygonLimit);

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "notch-v22",
            cache = new
            {
                step3Revision = cacheEntry.Revision,
                cacheKey = $"r{cacheEntry.Revision}:cad{cadPadId}"
            },
            cadPad = new
            {
                id = cad.CadPadId,
                cad.Name,
                cad.Layer,
                cad.IcIndex,
                diffIndex = cad.DxfIndex,
                diffSource = cad.CadOutputFwDiffAssignmentModeText,
                cad.Area
            },
            ratios = BuildCadNotchRatiosPayload(compensation, display),
            threshold = new
            {
                strictOverlapPercent,
                strictOverlapRatio = strictOverlapPercent / 100.0,
                ruleEngineEnabled = helper.EnableToFullRuleEngine,
                ruleTraceEnabled = helper.EnableToFullRuleTrace
            },
            stage3Allocation = BuildCadNotchAllocationPayload(allocation, returnedTargets, display),
            summary = BuildCadNotchSummaryPayload(compensation, returnedRegulars.Count),
            toFullPolygons = new
            {
                seedCount = resolved.Stage1SeedPolygons.Count,
                finalCount = resolved.Stage3FinalOutlinePolygons.Count,
                returnedSeedBounds = seedBounds.Count,
                returnedFinalBounds = finalBounds.Count,
                seedBounds,
                finalBounds
            },
            regulars = returnedRegulars,
            diagnosticsText = cacheEntry.DiagnosticsText
        });
    }

    private static List<object> BuildCadNotchRegularRows(NotchV22CompensationResult compensation, int limit)
    {
        var orderedRegulars = compensation.RegularDebugInfos
            .OrderByDescending(info => info.OverlapArea)
            .ThenBy(info => info.RegularPadId)
            .ToList();
        return orderedRegulars
            .Take(limit)
            .Select(static info => (object)new
            {
                regularPadId = info.RegularPadId,
                regularIndex = info.RegularIndex,
                icIndex = info.IcIndex,
                diffIndex = info.DiffIndex,
                info.Row,
                info.Col,
                info.RegularArea,
                info.OverlapArea,
                info.SourceArea,
                info.BlockedArea,
                info.ReachableArea,
                info.SourceCellCount,
                info.BlockedCellCount,
                info.ReachableCellCount,
                info.TotalCellCount,
                info.BlockerCandidateCount,
                info.OwnerCadPadCount,
                ownerCadPadIds = info.OwnerCadPadIds,
                info.IsBoundaryRegular,
                info.IsToFullBoundaryCandidate,
                info.HasEffectiveExpansion,
                info.HasDirectionalBlocker,
                info.ToFullRuleCode,
                ruleTrace = info.ToFullRuleTrace.Select(step => new
                {
                    step.Rule,
                    step.Passed,
                    step.Detail
                }).ToList(),
                info.IsToFullApplied
            })
            .ToList();
    }

    private static List<object> BuildCadNotchTargetRows(NotchV22TargetAllocationSummary allocation, int targetLimit)
    {
        return allocation.Targets
            .Take(targetLimit)
            .Select(static target => (object)new
            {
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount,
                regularPadIds = target.RegularPadIds
            })
            .ToList();
    }

    private static List<object> BuildCadNotchPolygonBoundsRows(IReadOnlyList<Polygon2> polygons, int polygonLimit)
    {
        return polygons
            .Take(polygonLimit)
            .Select(static polygon => (object)new
            {
                minX = polygon.Bounds.MinX,
                minY = polygon.Bounds.MinY,
                maxX = polygon.Bounds.MaxX,
                maxY = polygon.Bounds.MaxY
            })
            .ToList();
    }

    private static object BuildCadNotchRatiosPayload(NotchV22CompensationResult compensation, NotchDisplayProjection display)
    {
        return new
        {
            compensation.ToRegularRatio,
            compensation.ToFullRatio,
            compensation.CombinedRatio,
            compensation.Stage3Area,
            compensation.IsToFullEnabled,
            compensation.OverlapAreaTotal,
            compensation.OverlappedRegularAreaTotal,
            compensation.OverlapRegularCount,
            display = new
            {
                display.ToRegularRatioText,
                display.ToFullRatioText,
                display.CombinedRatioText,
                display.ToRegularValueText,
                display.ToFullValueText,
                display.CombinedValueText,
                display.ToFullReasonShortText,
                display.ToFullReasonFullText,
                display.Stage3AreaText
            }
        };
    }

    private static object BuildCadNotchAllocationPayload(
        NotchV22TargetAllocationSummary allocation,
        List<object> returnedTargets,
        NotchDisplayProjection display)
    {
        return new
        {
            allocation.CadArea,
            allocation.Stage3Area,
            allocation.StrictAreaThreshold,
            targetCount = allocation.Targets.Count,
            returnedTargets = returnedTargets.Count,
            truncatedTargets = Math.Max(0, allocation.Targets.Count - returnedTargets.Count),
            targets = returnedTargets,
            display = new
            {
                display.TargetAllocationSummaryText,
                display.TargetAllocationLines,
                display.OwnerSummaryText
            }
        };
    }

    private static object BuildCadNotchSummaryPayload(NotchV22CompensationResult compensation, int returnedRegularCount)
    {
        return new
        {
            totalRegulars = compensation.RegularDebugInfos.Count,
            returnedRegulars = returnedRegularCount,
            truncatedRegulars = Math.Max(0, compensation.RegularDebugInfos.Count - returnedRegularCount),
            boundaryRegularCount = compensation.RegularDebugInfos.Count(info => info.IsBoundaryRegular),
            toFullAppliedCount = compensation.RegularDebugInfos.Count(info => info.IsToFullApplied),
            blockerRegularCount = compensation.RegularDebugInfos.Count(info => info.BlockerCandidateCount > 0),
            compensation.IsToFullEnabled
        };
    }
}
