using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Runtime query command that returns the regular pads gated by the multi-owner rule.
/// </summary>
internal sealed class RuntimeQueryMultiOwnerCommands
{
    private readonly RuntimeQueryContext _context;

    public RuntimeQueryMultiOwnerCommands(RuntimeQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Adds the commands of this group to the registry.</summary>
    /// <param name="registry">The registry that receives the handlers.</param>
    public void Register(RuntimeQueryCommandRegistry registry)
    {
        registry.AddSync("multi-owner", QueryMultiOwner);
    }

    private RuntimeQueryResponseEnvelope QueryMultiOwner(IReadOnlyDictionary<string, string>? args)
    {
        if (!RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadIdError) || cadIdError is not null)
        {
            return cadIdError ?? RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Argument '--cad-id' is required for query multi-owner.");
        }

        var limit = RuntimeQueryLimits.DefaultNotchRegularLimit;
        if (RuntimeQueryArgumentParser.TryGetIntArg(args, "limit", 1, RuntimeQueryLimits.MaxNotchRegularLimit, out var parsedLimit, out var limitError))
        {
            limit = parsedLimit;
        }
        else if (limitError is not null)
        {
            return limitError;
        }

        var hasOverlapPercentOverride = RuntimeQueryArgumentParser.TryGetDoubleArg(
            args,
            "overlap-percent",
            0.0,
            100.0,
            out var overlapPercentOverride,
            out var overlapPercentError);
        if (overlapPercentError is not null)
        {
            return overlapPercentError;
        }

        var strictOverlapRatioOverride = hasOverlapPercentOverride
            ? NotchV22CompensationService.NormalizeStrictOverlapRatio(overlapPercentOverride / 100.0)
            : (double?)null;
        return QueryCadMultiOwner(cadPadId, limit, strictOverlapRatioOverride, hasOverlapPercentOverride ? overlapPercentOverride : null);
    }

    private RuntimeQueryResponseEnvelope QueryCadMultiOwner(
        int cadPadId,
        int limit,
        double? strictOverlapRatioOverride,
        double? overlapPercentOverride)
    {
        var helper = _context.Shell.FreeformHelper;
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

        var rows = SelectMultiOwnerRows(compensation.RegularDebugInfos);
        var returnedRows = rows
            .Take(limit)
            .Select(BuildMultiOwnerRegularPayload)
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
            cadPad = BuildMultiOwnerCadPayload(cad),
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

    private static object BuildMultiOwnerCadPayload(CadPadInspectorSnapshot cad)
    {
        return new
        {
            id = cad.CadPadId,
            cad.Name,
            cad.Layer,
            cad.IcIndex,
            diffIndex = cad.DxfIndex,
            diffSource = cad.CadOutputFwDiffAssignmentModeText,
            cad.Area
        };
    }

    private static List<NotchV22RegularDebugInfo> SelectMultiOwnerRows(IReadOnlyList<NotchV22RegularDebugInfo> debugInfos)
    {
        return debugInfos
            .Where(info =>
                string.Equals(info.ToFullRuleCode, "GATE_MULTI_OWNER", StringComparison.Ordinal) ||
                info.OwnerCadPadCount > 1)
            .OrderByDescending(info => string.Equals(info.ToFullRuleCode, "GATE_MULTI_OWNER", StringComparison.Ordinal))
            .ThenByDescending(info => info.OwnerCadPadCount)
            .ThenByDescending(info => info.OverlapArea)
            .ThenBy(info => info.RegularPadId)
            .ToList();
    }

    private static object BuildMultiOwnerRegularPayload(NotchV22RegularDebugInfo info)
    {
        return new
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
        };
    }
}
