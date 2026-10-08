using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private RuntimeQueryResponseEnvelope QueryCadMultiOwner(
        int cadPadId,
        int limit,
        double? strictOverlapRatioOverride,
        double? overlapPercentOverride)
    {
        var helper = _shellViewModel.FreeformHelper;
        if (!helper.CadPads.Any(pad => pad.Id == cadPadId))
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: $"CAD pad {cadPadId} is not visible.");
        }

        var resolved = helper.GetCadV22ResolvedResult(cadPadId, strictOverlapRatioOverride);
        var snapshot = helper.BuildCadPadInspectorSnapshot(
            cadPadId,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false);
        if (snapshot?.Cad is not CadPadInspectorSnapshot cad)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: $"CAD pad {cadPadId} is not visible.");
        }

        if (resolved is null)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: "Notch compensation is unavailable. Build grid and ensure CAD is visible.");
        }

        var compensation = resolved.Compensation;

        var rows = compensation.RegularDebugInfos
            .Where(info =>
                string.Equals(info.ToFullRuleCode, "GATE_MULTI_OWNER", StringComparison.Ordinal) ||
                info.OwnerCadPadCount > 1)
            .OrderByDescending(info => string.Equals(info.ToFullRuleCode, "GATE_MULTI_OWNER", StringComparison.Ordinal))
            .ThenByDescending(info => info.OwnerCadPadCount)
            .ThenByDescending(info => info.OverlapArea)
            .ThenBy(info => info.RegularPadId)
            .ToList();
        var returnedRows = rows
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
                info.OwnerCadPadCount,
                ownerCadPadIds = info.OwnerCadPadIds,
                info.BlockerCandidateCount,
                info.IsBoundaryRegular,
                info.IsToFullBoundaryCandidate,
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

        var currentSettingPercent = helper.GetNotchMultiOwnerStrictOverlapPercent();
        var effectivePercent = overlapPercentOverride ?? currentSettingPercent;
        var effectiveRatio = strictOverlapRatioOverride ?? (currentSettingPercent / 100.0);
        var gateCount = compensation.RegularDebugInfos.Count(info =>
            string.Equals(info.ToFullRuleCode, "GATE_MULTI_OWNER", StringComparison.Ordinal));
        var ownerCountGreaterThanOne = compensation.RegularDebugInfos.Count(info => info.OwnerCadPadCount > 1);

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "notch-v22-multi-owner",
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
            threshold = new
            {
                source = overlapPercentOverride.HasValue ? "override" : "settings",
                overlapPercent = effectivePercent,
                overlapRatio = effectiveRatio,
                currentSettingPercent,
                ruleEngineEnabled = helper.EnableToFullRuleEngine,
                ruleTraceEnabled = helper.EnableToFullRuleTrace
            },
            summary = new
            {
                totalRegulars = compensation.RegularDebugInfos.Count,
                gateMultiOwnerCount = gateCount,
                ownerCountGreaterThanOne,
                returnedRegulars = returnedRows.Count,
                truncatedRegulars = Math.Max(0, rows.Count - returnedRows.Count)
            },
            regulars = returnedRows
        });
    }

    private RuntimeQueryResponseEnvelope QueryCadNotchStage(int cadPadId, int polygonLimit)
    {
        var helper = _shellViewModel.FreeformHelper;
        if (!_notchQueryCacheService.TryGetCadNotchCacheEntry(helper, cadPadId, out var cacheEntry, out var error))
        {
            return error ?? RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: "Notch stage overlays are unavailable.");
        }

        var overlay = helper.GetCadV22StageOverlays(cadPadId);
        if (!overlay.HasValue)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: "Notch stage overlays are unavailable. Build grid and ensure CAD is visible.");
        }

        var stage = overlay.Value;
        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "notch-v22-stage",
            cache = new
            {
                step3Revision = cacheEntry.Revision,
                cacheKey = $"r{cacheEntry.Revision}:cad{cadPadId}"
            },
            cadPad = new
            {
                id = cacheEntry.Cad.CadPadId,
                cacheEntry.Cad.Name,
                cacheEntry.Cad.Layer,
                cacheEntry.Cad.IcIndex,
                diffIndex = cacheEntry.Cad.DxfIndex,
                diffSource = cacheEntry.Cad.CadOutputFwDiffAssignmentModeText
            },
            isToFullEnabled = stage.IsToFullEnabled,
            stages = new
            {
                stage1 = RuntimeQueryResponseBuilder.BuildStagePolygonPayload("seed", stage.Stage1Seed, polygonLimit),
                stage2 = RuntimeQueryResponseBuilder.BuildStagePolygonPayload("candidate", stage.Stage2Candidate, polygonLimit),
                stage3 = RuntimeQueryResponseBuilder.BuildStagePolygonPayload("final", stage.Stage3Final, polygonLimit)
            }
        });
    }
}
