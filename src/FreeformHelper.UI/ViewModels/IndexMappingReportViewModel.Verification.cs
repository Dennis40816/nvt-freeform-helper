using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    private void SelectVerificationStep(IndexMappingVerificationStep step)
    {
        ActiveVerificationStep = step;
        if (!SyncFilterWithStepRail)
        {
            return;
        }

        var targetFilterMode = ResolveStepFilterMode(step);
        if (targetFilterMode.HasValue && targetFilterMode.Value != SelectedFilterMode)
        {
            SelectedFilterMode = targetFilterMode.Value;
        }
    }

    private IndexMappingDecisionFilterMode? ResolveStepFilterMode(IndexMappingVerificationStep step)
    {
        return step switch
        {
            IndexMappingVerificationStep.Step3 => IndexMappingDecisionFilterMode.All,
            IndexMappingVerificationStep.Step4 => ResolvePreferredFilterMode(
                IndexMappingDecisionFilterMode.ChangedByMask,
                IndexMappingDecisionFilterMode.LowConfidence,
                IndexMappingDecisionFilterMode.Ambiguous,
                IndexMappingDecisionFilterMode.RemovedByMask,
                IndexMappingDecisionFilterMode.Unmapped),
            IndexMappingVerificationStep.Step5 => ResolvePreferredFilterMode(
                IndexMappingDecisionFilterMode.CountMismatch,
                IndexMappingDecisionFilterMode.Unmapped),
            IndexMappingVerificationStep.Step6 => ResolvePreferredFilterMode(IndexMappingDecisionFilterMode.DuplicateDiff),
            IndexMappingVerificationStep.Simulation => ResolvePreferredFilterMode(
                IndexMappingDecisionFilterMode.ChangedByMask,
                IndexMappingDecisionFilterMode.RemovedByMask),
            _ => null,
        };
    }

    private IndexMappingDecisionFilterMode ResolvePreferredFilterMode(params IndexMappingDecisionFilterMode[] candidates)
    {
        foreach (var mode in candidates)
        {
            if (GetFilterModeCount(mode) > 0)
            {
                return mode;
            }
        }

        return IndexMappingDecisionFilterMode.All;
    }

    private int GetFilterModeCount(IndexMappingDecisionFilterMode mode)
    {
        return mode switch
        {
            IndexMappingDecisionFilterMode.CountMismatch => CountMismatchDecisionCount,
            IndexMappingDecisionFilterMode.Unmapped => UnmappedDecisionCount,
            IndexMappingDecisionFilterMode.LowConfidence => LowConfidenceDecisionCount,
            IndexMappingDecisionFilterMode.Ambiguous => AmbiguousDecisionCount,
            IndexMappingDecisionFilterMode.DuplicateDiff => DuplicateDiffDecisionCount,
            IndexMappingDecisionFilterMode.ChangedByMask => ChangedByMaskDecisionCount,
            IndexMappingDecisionFilterMode.RemovedByMask => RemovedByMaskDecisionCount,
            _ => TotalDecisionCount,
        };
    }

    private void RefreshVerificationSteps()
    {
        var nodes = new List<IndexMappingVerificationStepNodeViewModel>(5)
        {
            CreateStepNode(IndexMappingVerificationStep.Step3, "Step 3", "Notch input readiness", BuildStep3KeyChecks(), BuildStep3OpenAction()),
            CreateStepNode(IndexMappingVerificationStep.Step4, "Step 4", "Diff assignment decision", BuildStep4KeyChecks(), BuildStep4OpenAction()),
            CreateStepNode(IndexMappingVerificationStep.Step5, "Step 5", "Notch table row integrity", BuildStep5KeyChecks(), BuildStep5OpenAction()),
            CreateStepNode(IndexMappingVerificationStep.Step6, "Step 6", "Validation trace consistency", BuildStep6KeyChecks(), BuildStep6OpenAction()),
            CreateStepNode(IndexMappingVerificationStep.Simulation, "Simulation", "Before/After/Delta sanity", BuildSimulationKeyChecks(), BuildSimulationOpenAction()),
        };

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            node.IsActive = node.Step == ActiveVerificationStep;
            node.IsLast = i == nodes.Count - 1;
        }

        VerificationSteps = nodes;
        OnPropertyChanged(nameof(HasVerificationSteps));
        OnPropertyChanged(nameof(OpenIssueCount));
        OnPropertyChanged(nameof(OverallStatusText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        OnPropertyChanged(nameof(HeaderAuditSummaryText));
        OnPropertyChanged(nameof(HasHeaderAuditSummary));
        NotifyStepSurfaceStateChanged();
    }

    private IndexMappingVerificationStepNodeViewModel CreateStepNode(
        IndexMappingVerificationStep step,
        string title,
        string verifyHint,
        string keyChecks,
        string openActionText)
    {
        var status = BuildStepStatus(step);
        return new IndexMappingVerificationStepNodeViewModel(step, title, verifyHint, keyChecks, status, openActionText);
    }

    private string BuildStepStatus(IndexMappingVerificationStep step)
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return step switch
            {
                IndexMappingVerificationStep.Step5 => CountMismatchDecisionCount > 0 || UnmappedDecisionCount > 0 ? "warning" : "pass",
                IndexMappingVerificationStep.Step6 => DuplicateDiffDecisionCount > 0 ? "warning" : "pass",
                _ => OpenIssueCount > 0 ? "warning" : "pass",
            };
        }

        return step switch
        {
            IndexMappingVerificationStep.Step3 => SelectedDecision.FilterMode == IndexMappingDecisionFilterMode.CountMismatch ? "warning" : "pass",
            IndexMappingVerificationStep.Step4 => SelectedDecision.FilterMode is IndexMappingDecisionFilterMode.LowConfidence or
                IndexMappingDecisionFilterMode.Ambiguous or
                IndexMappingDecisionFilterMode.ChangedByMask or
                IndexMappingDecisionFilterMode.RemovedByMask or
                IndexMappingDecisionFilterMode.Unmapped or
                IndexMappingDecisionFilterMode.DuplicateDiff ? "warning" : "pass",
            IndexMappingVerificationStep.Step5 => SelectedDecision.FilterMode is IndexMappingDecisionFilterMode.CountMismatch or
                IndexMappingDecisionFilterMode.Unmapped ? "warning" : "pass",
            IndexMappingVerificationStep.Step6 => SelectedDecision.FilterMode == IndexMappingDecisionFilterMode.DuplicateDiff ? "warning" : "pass",
            IndexMappingVerificationStep.Simulation => SelectedDecision.FilterMode is IndexMappingDecisionFilterMode.ChangedByMask or IndexMappingDecisionFilterMode.RemovedByMask ? "warning" : "pass",
            _ => "pass",
        };
    }

    private string BuildStep3KeyChecks()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return $"rows={TotalDecisionCount}";
        }

        return SelectedDecision.HasSegmentScope
            ? $"entity={SelectedEntityText}, segment={SelectedDecision.SegmentKey}, members={SelectedDecision.SegmentMemberCount}"
            : $"entity={SelectedEntityText}, segment=n/a";
    }

    private string BuildStep4KeyChecks()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return $"changed={ChangedByMaskDecisionCount}, lowConf={LowConfidenceDecisionCount}";
        }

        return $"assigned={SelectedDecision.CurrentDisplay}, suggested={SelectedDecision.SuggestedDisplay}, reason={SelectedDecision.DecisionReasonCodeText}";
    }

    private string BuildStep5KeyChecks()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return $"mismatch={CountMismatchDecisionCount}, unmapped={UnmappedDecisionCount}";
        }

        return $"row={SelectedDecision.RowIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, ic={SelectedDecision.IcIndex?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}, contract={SelectedDecision.DecisionSourceText}";
    }

    private string BuildStep6KeyChecks()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return $"duplicate={DuplicateDiffDecisionCount}";
        }

        return $"trace={SelectedDecision.CandidateLines.Count.ToString(CultureInfo.InvariantCulture)}, direction={BuildStep6TraceDirectionText()}";
    }

    private string BuildSimulationKeyChecks()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return $"maskChanged={ChangedByMaskDecisionCount}, maskRemoved={RemovedByMaskDecisionCount}";
        }

        return $"entity={SelectedEntityText}, impact={SelectedDecision.SuggestedDisplay}";
    }

    private string BuildStep3OpenAction()
    {
        return SelectedDecision is null || SelectedDecision.IsAggregateRow
            ? "Confirm input rows are coherent before table export."
            : $"Confirm {SelectedEntityText} can enter notch table generation.";
    }

    private string BuildStep4OpenAction()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return "Review reason code and seed/mask decision source.";
        }

        if (SelectedDecision.CanApplyDiffOverride)
        {
            return $"Apply diff repair for {SelectedEntityText}.";
        }

        if (SelectedDecision.CanApplyOverride)
        {
            return $"Apply regular override for {SelectedEntityText}.";
        }

        return $"Review decision source for {SelectedEntityText}.";
    }

    private string BuildStep5OpenAction()
    {
        if (SelectedDecision is null || SelectedDecision.IsAggregateRow)
        {
            return "Check final notch row payload before export.";
        }

        return SelectedDecision.HasSegmentScope
            ? $"Apply segment repairs around {SelectedDecision.SegmentKey}."
            : $"Check row payload fields for {SelectedEntityText}.";
    }

    private string BuildStep6OpenAction()
    {
        return SelectedDecision is null || SelectedDecision.IsAggregateRow
            ? "Validate trace direction and resolve duplicate conflicts."
            : $"Locate {SelectedEntityText} on AA and verify trace direction.";
    }

    private string BuildSimulationOpenAction()
    {
        return SelectedDecision is null || SelectedDecision.IsAggregateRow
            ? "Replay notch impact using the same exported table contract."
            : $"Replay simulation for {SelectedEntityText} and compare before/after.";
    }

    private string BuildActiveStepInputText()
    {
        return ActiveVerificationStep switch
        {
            IndexMappingVerificationStep.Step3 => SelectedDecision is null || SelectedDecision.IsAggregateRow
                ? $"rows={TotalDecisionCount}, filter={SelectedFilterLabel}"
                : $"{SelectedEntityText}, {SelectedDecision.SegmentScopeText}",
            IndexMappingVerificationStep.Step4 => $"{Step4RawBestText}; {Step4MaskedBestText}",
            IndexMappingVerificationStep.Step5 => $"{Step5RowNumberText}; {Step5IcDiffText}",
            IndexMappingVerificationStep.Step6 => $"{Step6ValidationRegularText}; {Step6TraceCountText}",
            _ => SimulationViewStateText,
        };
    }

    private string BuildActiveStepDecisionText()
    {
        return ActiveVerificationStep switch
        {
            IndexMappingVerificationStep.Step3 => BuildStep3KeyChecks(),
            IndexMappingVerificationStep.Step4 => $"{Step4ReasonCodeText}; {Step4DecisionSourceText}; {Step4ConfidenceText}",
            IndexMappingVerificationStep.Step5 => Step5PayloadSummaryText,
            IndexMappingVerificationStep.Step6 => Step6TraceDirectionText,
            _ => SimulationColorStateText,
        };
    }

    private string BuildActiveStepOutputText()
    {
        return ActiveVerificationStep switch
        {
            IndexMappingVerificationStep.Step3 => BuildStep3OpenAction(),
            IndexMappingVerificationStep.Step4 => $"{Step4AssignedText}; {Step4RepairSuggestionText}",
            IndexMappingVerificationStep.Step5 => $"{Step5StatusText}; {Step5RegularCadText}",
            IndexMappingVerificationStep.Step6 => BuildStep6KeyChecks(),
            _ => SimulationImpactText,
        };
    }

    private string BuildActiveStepImpactText()
    {
        return ActiveVerificationStep switch
        {
            IndexMappingVerificationStep.Step3 => $"openIssues={OpenIssueCount}, countMismatch={CountMismatchDecisionCount}",
            IndexMappingVerificationStep.Step4 => SelectedDecision?.DiffRepairPreviewText ?? "Diff preview: n/a.",
            IndexMappingVerificationStep.Step5 => SelectedDecision?.HasSegmentScope == true
                ? SelectedSegmentPreviewText
                : $"visible rows {VisibleDecisionCount}/{TotalDecisionCount}",
            IndexMappingVerificationStep.Step6 => $"duplicate rows={DuplicateDiffDecisionCount}, selected={SelectedEntityText}",
            _ => SimulationScopeText,
        };
    }

    private string BuildActiveStepActionText()
    {
        return ActiveVerificationStep switch
        {
            IndexMappingVerificationStep.Step3 => BuildStep3OpenAction(),
            IndexMappingVerificationStep.Step4 => BuildStep4OpenAction(),
            IndexMappingVerificationStep.Step5 => BuildStep5OpenAction(),
            IndexMappingVerificationStep.Step6 => BuildStep6OpenAction(),
            _ => BuildSimulationOpenAction(),
        };
    }

}
