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
        var orderedRegulars = compensation.RegularDebugInfos
            .OrderByDescending(info => info.OverlapArea)
            .ThenBy(info => info.RegularPadId)
            .ToList();
        var returnedRegulars = orderedRegulars
            .Take(limit)
            .Select(info => new
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
        var toFullAppliedCount = compensation.RegularDebugInfos.Count(info => info.IsToFullApplied);
        var boundaryRegularCount = compensation.RegularDebugInfos.Count(info => info.IsBoundaryRegular);
        var blockerRegularCount = compensation.RegularDebugInfos.Count(info => info.BlockerCandidateCount > 0);
        var strictOverlapPercent = _shellViewModel.FreeformHelper.GetNotchMultiOwnerStrictOverlapPercent();
        var allocation = resolved.TargetAllocation;
        var display = NotchDisplayProjector.Build(
            compensation,
            allocation,
            cacheEntry.DiagnosticsText,
            cad.Notch?.ComputationMode ?? helper.CurrentNotchComputationMode);
        var returnedTargets = allocation.Targets
            .Take(targetLimit)
            .Select(target => new
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

        var seedBounds = resolved.Stage1SeedPolygons
            .Take(polygonLimit)
            .Select(static polygon => new
            {
                minX = polygon.Bounds.MinX,
                minY = polygon.Bounds.MinY,
                maxX = polygon.Bounds.MaxX,
                maxY = polygon.Bounds.MaxY
            })
            .ToList();
        var finalBounds = resolved.Stage3FinalOutlinePolygons
            .Take(polygonLimit)
            .Select(static polygon => new
            {
                minX = polygon.Bounds.MinX,
                minY = polygon.Bounds.MinY,
                maxX = polygon.Bounds.MaxX,
                maxY = polygon.Bounds.MaxY
            })
            .ToList();

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
            ratios = new
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
            },
            threshold = new
            {
                strictOverlapPercent,
                strictOverlapRatio = strictOverlapPercent / 100.0,
                ruleEngineEnabled = helper.EnableToFullRuleEngine,
                ruleTraceEnabled = helper.EnableToFullRuleTrace
            },
            stage3Allocation = new
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
            },
            summary = new
            {
                totalRegulars = compensation.RegularDebugInfos.Count,
                returnedRegulars = returnedRegulars.Count,
                truncatedRegulars = Math.Max(0, compensation.RegularDebugInfos.Count - returnedRegulars.Count),
                boundaryRegularCount,
                toFullAppliedCount,
                blockerRegularCount,
                compensation.IsToFullEnabled
            },
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
}
