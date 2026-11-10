using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Runtime query command that returns one pad snapshot, by CAD id or by regular pad id.
/// </summary>
internal sealed class RuntimeQueryPadCommands
{
    private readonly RuntimeQueryContext _context;

    public RuntimeQueryPadCommands(RuntimeQueryContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>Adds the commands of this group to the registry.</summary>
    /// <param name="registry">The registry that receives the handlers.</param>
    public void Register(RuntimeQueryCommandRegistry registry)
    {
        registry.AddSync("pad", QueryPad);
    }

    private RuntimeQueryResponseEnvelope QueryPad(IReadOnlyDictionary<string, string>? args)
    {
        var hasCadId = RuntimeQueryArgumentParser.TryGetIntArg(args, "cad-id", 0, int.MaxValue, out var cadPadId, out var cadError);
        if (cadError is not null)
        {
            return cadError;
        }

        var hasRegularId = RuntimeQueryArgumentParser.TryGetIntArg(args, "regular-id", 0, int.MaxValue, out var regularPadId, out var regularError);
        if (regularError is not null)
        {
            return regularError;
        }

        if (hasCadId == hasRegularId)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "INVALID_ARGUMENTS",
                message: "Use exactly one of --cad-id or --regular-id.");
        }

        return hasCadId
            ? QueryCadPad(cadPadId)
            : QueryRegularPad(regularPadId);
    }

    private RuntimeQueryResponseEnvelope QueryCadPad(int cadPadId)
    {
        var helper = _context.Shell.FreeformHelper;
        var snapshot = helper.BuildCadPadInspectorSnapshot(cadPadId);
        if (snapshot?.Cad is not CadPadInspectorSnapshot cad)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: $"CAD pad {cadPadId} is not visible.");
        }

        var notchDisplay = cad.Notch is null
            ? null
            : NotchDisplayProjector.Build(cad.Notch);

        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "cad",
            snapshot = new
            {
                kind = snapshot.Kind,
                cad = BuildCadSnapshotPayload(cad, notchDisplay),
                ruleTrace = BuildRuleTracePayload(cad.RuleTrace)
            },
            cadPad = new
            {
                id = cad.CadPadId,
                cad.Name,
                cad.Layer,
                cad.Area,
                centroid = new { cad.Centroid.X, cad.Centroid.Y },
                bounds = new { cad.Bounds.MinX, cad.Bounds.MinY, cad.Bounds.MaxX, cad.Bounds.MaxY },
                vertices = cad.Vertices,
                icIndex = cad.IcIndex,
                diffIndex = cad.DxfIndex,
                isSelected = helper.SelectedCadPadIds.Contains(cadPadId)
            },
            notchV22 = BuildCadNotchSummaryPayload(cad.Notch, notchDisplay),
            matchedRegularIds = cad.MatchedRegularPadIds,
            matchedRegularDetails = cad.MatchedRegularDetails.Select(detail => new
            {
                regularPadId = detail.RegularPadId,
                icIndex = detail.IcIndex,
                diffIndex = detail.DiffIndex
            }),
            ruleTrace = BuildRuleTracePayload(cad.RuleTrace)
        });
    }

    private static object BuildCadSnapshotPayload(CadPadInspectorSnapshot cad, NotchDisplayProjection? notchDisplay)
    {
        return new
        {
            cadPadId = cad.CadPadId,
            cad.Name,
            cad.Layer,
            cad.Area,
            centroid = new { cad.Centroid.X, cad.Centroid.Y },
            bounds = new { cad.Bounds.MinX, cad.Bounds.MinY, cad.Bounds.MaxX, cad.Bounds.MaxY },
            cad.Vertices,
            cad.DxfIndex,
            cad.CadOutputFwDiffOverride,
            cad.IsDxfIndexAnchor,
            cad.DxfIndexDisplayText,
            cad.CadOutputFwDiffAssignmentModeText,
            cad.IcIndex,
            cad.MatchText,
            cad.MatchDetailsText,
            cad.MatchConfidence,
            matchedRegularPadIds = cad.MatchedRegularPadIds,
            matchedRegularDetails = cad.MatchedRegularDetails.Select(detail => new
            {
                detail.RegularPadId,
                detail.IcIndex,
                detail.DiffIndex,
                detail.CadCoverage,
                detail.RegularCoverage
            }),
            notch = BuildCadNotchDetailPayload(cad.Notch, notchDisplay)
        };
    }

    private static object? BuildCadNotchDetailPayload(PadInspectorNotchSnapshot? notch, NotchDisplayProjection? notchDisplay)
    {
        if (notch is null)
        {
            return null;
        }

        return new
        {
            notch.ToRegularRatio,
            notch.ToFullRatio,
            notch.CombinedRatio,
            notch.IsToFullEnabled,
            notch.Stage3Area,
            notch.StrictAreaThreshold,
            targets = notch.Targets.Select(target => new
            {
                target.IcIndex,
                target.DiffIndex,
                target.EffectiveArea,
                target.Ratio,
                target.RatioPercentRounded,
                target.PassesStrictThreshold,
                target.IsAnchorDiff,
                target.ToFullAppliedRegularCount,
                target.RegularCount
            }),
            notch.Diagnostics,
            display = notchDisplay is null
                ? null
                : new
                {
                    notchDisplay.ToRegularRatioText,
                    notchDisplay.ToFullRatioText,
                    notchDisplay.CombinedRatioText,
                    notchDisplay.ToRegularValueText,
                    notchDisplay.ToFullValueText,
                    notchDisplay.CombinedValueText,
                    notchDisplay.ToFullReasonShortText,
                    notchDisplay.ToFullReasonFullText,
                    notchDisplay.Stage3AreaText,
                    notchDisplay.TargetAllocationSummaryText,
                    notchDisplay.OwnerSummaryText
                }
        };
    }

    private static object? BuildCadNotchSummaryPayload(PadInspectorNotchSnapshot? notch, NotchDisplayProjection? notchDisplay)
    {
        if (notch is null)
        {
            return null;
        }

        return new
        {
            toRegularRatio = notch.ToRegularRatio,
            toFullRatio = notch.ToFullRatio,
            combinedRatio = notch.CombinedRatio,
            isToFullEnabled = notch.IsToFullEnabled,
            stage3Area = notch.Stage3Area,
            strictAreaThreshold = notch.StrictAreaThreshold,
            targetCount = notch.Targets.Count,
            display = notchDisplay is null
                ? null
                : new
                {
                    notchDisplay.ToRegularValueText,
                    notchDisplay.ToFullValueText,
                    notchDisplay.CombinedValueText,
                    notchDisplay.ToFullReasonShortText
                }
        };
    }

    private RuntimeQueryResponseEnvelope QueryRegularPad(int regularPadId)
    {
        var helper = _context.Shell.FreeformHelper;
        var snapshot = helper.BuildRegularPadInspectorSnapshot(regularPadId);
        if (snapshot?.Regular is not RegularPadInspectorSnapshot regular)
        {
            return RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: $"Regular pad {regularPadId} is not visible.");
        }

        var visibleRegularPad = helper.RegularPads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        return RuntimeQueryResponseEnvelope.Success(new
        {
            kind = "regular",
            snapshot = new
            {
                kind = snapshot.Kind,
                regular = new
                {
                    regular.RegularPadId,
                    regular.Index,
                    regular.Row,
                    regular.Col,
                    regular.DisplayRow,
                    regular.IcIndex,
                    regular.DiffIndex,
                    regular.Area,
                    regular.MatchText,
                    regular.MatchDetailsText,
                    freeform = regular.Freeform.ToString(),
                    regular.DiffSource,
                    regular.MatchConfidence,
                    bounds = new { regular.Bounds.MinX, regular.Bounds.MinY, regular.Bounds.MaxX, regular.Bounds.MaxY },
                    centroid = new { regular.Centroid.X, regular.Centroid.Y },
                    matchedCadPadIds = regular.MatchedCadPadIds,
                    matchedCadDetails = regular.MatchedCadDetails.Select(detail => new
                    {
                        detail.CadPadId,
                        detail.DxfIndex,
                        detail.CadCoverage,
                        detail.RegularCoverage
                    })
                },
                ruleTrace = BuildRuleTracePayload(regular.RuleTrace)
            },
            regularPad = new
            {
                regular.RegularPadId,
                regular.Index,
                regular.Row,
                regular.Col,
                regular.IcIndex,
                regular.DiffIndex,
                regular.Area,
                matchScore = visibleRegularPad?.MatchScore ?? 0.0,
                matchedCadPadId = visibleRegularPad?.MatchedCadPadId,
                freeform = regular.Freeform.ToString(),
                bounds = new { regular.Bounds.MinX, regular.Bounds.MinY, regular.Bounds.MaxX, regular.Bounds.MaxY },
                isSelected = helper.SelectedRegularPadIndices.Contains(regular.Index)
            },
            matchedCadIds = regular.MatchedCadPadIds,
            ruleTrace = BuildRuleTracePayload(regular.RuleTrace)
        });
    }

    private static IEnumerable<object> BuildRuleTracePayload(IReadOnlyList<PadInspectorRuleTraceEntry> ruleTrace)
    {
        return ruleTrace.Select(step => (object)new
        {
            step.Rule,
            step.Outcome,
            step.Detail
        });
    }
}
