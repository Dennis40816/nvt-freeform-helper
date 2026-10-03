using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal sealed partial class RuntimeQueryUseCase
{
    private RuntimeQueryResponseEnvelope QueryCadPad(int cadPadId)
    {
        var helper = _shellViewModel.FreeformHelper;
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
                cad = new
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
                    notch = cad.Notch is null
                        ? null
                        : new
                        {
                            cad.Notch.ToRegularRatio,
                            cad.Notch.ToFullRatio,
                            cad.Notch.CombinedRatio,
                            cad.Notch.IsToFullEnabled,
                            cad.Notch.Stage3Area,
                            cad.Notch.StrictAreaThreshold,
                            targets = cad.Notch.Targets.Select(target => new
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
                            cad.Notch.Diagnostics,
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
                        }
                },
                ruleTrace = cad.RuleTrace.Select(step => new
                {
                    step.Rule,
                    step.Outcome,
                    step.Detail
                })
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
            notchV22 = cad.Notch is null
                ? null
                : new
                {
                    toRegularRatio = cad.Notch.ToRegularRatio,
                    toFullRatio = cad.Notch.ToFullRatio,
                    combinedRatio = cad.Notch.CombinedRatio,
                    isToFullEnabled = cad.Notch.IsToFullEnabled,
                    stage3Area = cad.Notch.Stage3Area,
                    strictAreaThreshold = cad.Notch.StrictAreaThreshold,
                    targetCount = cad.Notch.Targets.Count,
                    display = notchDisplay is null
                        ? null
                        : new
                        {
                            notchDisplay.ToRegularValueText,
                            notchDisplay.ToFullValueText,
                            notchDisplay.CombinedValueText,
                            notchDisplay.ToFullReasonShortText
                        }
                },
            matchedRegularIds = cad.MatchedRegularPadIds,
            matchedRegularDetails = cad.MatchedRegularDetails.Select(detail => new
            {
                regularPadId = detail.RegularPadId,
                icIndex = detail.IcIndex,
                diffIndex = detail.DiffIndex
            }),
            ruleTrace = cad.RuleTrace.Select(step => new
            {
                step.Rule,
                step.Outcome,
                step.Detail
            })
        });
    }

    private RuntimeQueryResponseEnvelope QueryRegularPad(int regularPadId)
    {
        var helper = _shellViewModel.FreeformHelper;
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
                ruleTrace = regular.RuleTrace.Select(step => new
                {
                    step.Rule,
                    step.Outcome,
                    step.Detail
                })
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
            ruleTrace = regular.RuleTrace.Select(step => new
            {
                step.Rule,
                step.Outcome,
                step.Detail
            })
        });
    }
}
