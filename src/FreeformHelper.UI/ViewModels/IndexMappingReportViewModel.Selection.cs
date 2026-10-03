using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    partial void OnSelectedIssueChanged(IndexMappingReportIssueViewModel? value)
    {
        if (value is not null)
        {
            var matchingDecision = EnumerateVisibleDecisions().FirstOrDefault(row => ReferenceEquals(row.Issue, value));
            if (matchingDecision is not null && !ReferenceEquals(SelectedDecision, matchingDecision))
            {
                SelectedDecision = matchingDecision;
                return;
            }
        }

        OnPropertyChanged(nameof(HasSelectedIssue));
        OnPropertyChanged(nameof(CanApplySelectedIssueOverride));
        OnPropertyChanged(nameof(CanApplySelectedDiffOverride));
        OnPropertyChanged(nameof(CanApplySelectedSegmentDiffOverrides));
        OnPropertyChanged(nameof(SelectedSegmentPreviewText));
        OnPropertyChanged(nameof(CanClearSelectedIssueOverride));
        OnPropertyChanged(nameof(HeaderSubtitle));
        MarkAlgorithmTraceNotRequested();
    }

    partial void OnSelectedDecisionChanged(IndexMappingDecisionRowViewModel? value)
    {
        foreach (var row in _allDecisionSeedRows)
        {
            row.IsInspectorSelected = ReferenceEquals(row, value);
        }

        if (!ReferenceEquals(SelectedIssue, value?.Issue))
        {
            SelectedIssue = value?.Issue;
        }

        OnPropertyChanged(nameof(HasSelectedDecision));
        OnPropertyChanged(nameof(CanApplySelectedIssueOverride));
        OnPropertyChanged(nameof(CanApplySelectedDiffOverride));
        OnPropertyChanged(nameof(CanApplySelectedSegmentDiffOverrides));
        OnPropertyChanged(nameof(SelectedSegmentPreviewText));
        OnPropertyChanged(nameof(CanClearSelectedIssueOverride));
        OnPropertyChanged(nameof(CanLocateSelectedDecision));
        NotifySelectedDecisionDetailChanged();
        RefreshVerificationSteps();
        MarkAlgorithmTraceNotRequested();
        if (IsAlgorithmTraceExpanded)
        {
            _ = EnsureAlgorithmTraceForSelectedDecisionAsync(priorityOnly: true);
        }
    }

    partial void OnIsAlgorithmTraceExpandedChanged(bool value)
    {
        if (!value)
        {
            return;
        }

        _ = EnsureAlgorithmTraceForSelectedDecisionAsync(priorityOnly: true);
    }

    partial void OnActiveVerificationStepChanged(IndexMappingVerificationStep value)
    {
        OnPropertyChanged(nameof(ActiveVerificationStepText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        NotifyStepSurfaceStateChanged();
        RefreshVerificationSteps();
    }

    partial void OnAlgorithmTraceStateChanged(IndexMappingAlgorithmTraceState value)
    {
        OnPropertyChanged(nameof(IsAlgorithmTraceNotRequested));
        OnPropertyChanged(nameof(IsAlgorithmTraceLoading));
        OnPropertyChanged(nameof(IsAlgorithmTraceReady));
        OnPropertyChanged(nameof(IsAlgorithmTraceStale));
        OnPropertyChanged(nameof(IsAlgorithmTraceFailed));
    }

    partial void OnTableRevisionChanged(int value)
    {
        OnPropertyChanged(nameof(TableRevisionText));
        OnPropertyChanged(nameof(HeaderSubtitle));
    }

    partial void OnSyncFilterWithStepRailChanged(bool value)
    {
        OnPropertyChanged(nameof(StepFilterSyncText));
    }

    private string BuildHeaderAuditSummaryText()
    {
        var activeNode = GetActiveStepNode();
        if (activeNode is null)
        {
            return string.Empty;
        }

        return $"{activeNode.VerifyHint}. {activeNode.KeyChecks}.";
    }

    private string BuildSelectedEntityText()
    {
        if (SelectedDecision is null)
        {
            return "entity: none";
        }

        if (SelectedDecision.IsAggregateRow)
        {
            return $"aggregate: {SelectedDecision.CategoryText}";
        }

        if (SelectedDecision.CadPadId is int cadId)
        {
            return $"CAD {cadId}";
        }

        if (SelectedDecision.LocateRegularPadIndex is int regularPadId)
        {
            return $"REG {regularPadId}";
        }

        return "entity: aggregate diagnostics row";
    }

    private string BuildSelectedJourneyText()
    {
        if (SelectedDecision is null)
        {
            return "journey: select a CAD/REG row to inspect the full step path.";
        }

        if (SelectedDecision.IsAggregateRow)
        {
            return "journey: aggregate diagnostics row. Select a CAD/REG entity row to inspect step-by-step attribution.";
        }

        return $"journey: raw {SelectedDecision.RawDisplay} -> assigned {SelectedDecision.CurrentDisplay} -> suggested {SelectedDecision.SuggestedDisplay} ({SelectedDecision.DecisionReasonCodeText}/{SelectedDecision.DecisionSourceText})";
    }

    private string BuildStep6TraceDirectionText()
    {
        if (SelectedDecision is null)
        {
            return "trace direction: n/a";
        }

        var hasDuplicateSignal = SelectedDecision.FilterMode == IndexMappingDecisionFilterMode.DuplicateDiff;
        return hasDuplicateSignal
            ? "trace direction: duplicate-conflict path (needs review)"
            : "trace direction: direct/in/out path coherent";
    }

    private IndexMappingVerificationStepNodeViewModel? GetActiveStepNode()
    {
        return VerificationSteps.FirstOrDefault(node => node.Step == ActiveVerificationStep);
    }

    private void NotifyStepSurfaceStateChanged()
    {
        OnPropertyChanged(nameof(IsStep3Active));
        OnPropertyChanged(nameof(IsStep4Active));
        OnPropertyChanged(nameof(IsStep5Active));
        OnPropertyChanged(nameof(IsStep6Active));
        OnPropertyChanged(nameof(IsSimulationActive));
        OnPropertyChanged(nameof(ShowStep3ActionButtons));
        OnPropertyChanged(nameof(ShowStep4ActionButtons));
        OnPropertyChanged(nameof(ShowStep5ActionButtons));
        OnPropertyChanged(nameof(ShowSimulationActionButtons));
        OnPropertyChanged(nameof(ActiveStepCardTitle));
        OnPropertyChanged(nameof(ActiveStepCardStatusText));
        OnPropertyChanged(nameof(ActiveStepWhatToVerifyText));
        OnPropertyChanged(nameof(ActiveStepKeyChecksText));
        OnPropertyChanged(nameof(ActiveStepOpenActionText));
        OnPropertyChanged(nameof(ActiveStepInputText));
        OnPropertyChanged(nameof(ActiveStepDecisionText));
        OnPropertyChanged(nameof(ActiveStepOutputText));
        OnPropertyChanged(nameof(ActiveStepImpactText));
        OnPropertyChanged(nameof(ActiveStepActionText));
        OnPropertyChanged(nameof(HeaderAuditSummaryText));
        OnPropertyChanged(nameof(HasHeaderAuditSummary));
        OnPropertyChanged(nameof(SimulationViewStateText));
        OnPropertyChanged(nameof(SimulationColorStateText));
    }

    private void NotifySelectedDecisionDetailChanged()
    {
        OnPropertyChanged(nameof(SelectedDecisionKeyText));
        OnPropertyChanged(nameof(SelectedDecisionSubtitleText));
        OnPropertyChanged(nameof(SelectedEntityText));
        OnPropertyChanged(nameof(SelectedJourneyText));
        OnPropertyChanged(nameof(Step4ModeText));
        OnPropertyChanged(nameof(Step4ReasonCodeText));
        OnPropertyChanged(nameof(Step4DecisionSourceText));
        OnPropertyChanged(nameof(Step4ReassignedByText));
        OnPropertyChanged(nameof(Step4WeightBreakdownText));
        OnPropertyChanged(nameof(Step4RawBestText));
        OnPropertyChanged(nameof(Step4MaskedBestText));
        OnPropertyChanged(nameof(Step4AssignedText));
        OnPropertyChanged(nameof(Step4PassiveCompensationText));
        OnPropertyChanged(nameof(Step4RepairSuggestionText));
        OnPropertyChanged(nameof(Step4ConfidenceText));
        OnPropertyChanged(nameof(Step4OffsetSupportText));
        OnPropertyChanged(nameof(Step4CandidateAttributionTitle));
        OnPropertyChanged(nameof(Step4CandidateAttributionLines));
        OnPropertyChanged(nameof(Step5RowNumberText));
        OnPropertyChanged(nameof(Step5VersionText));
        OnPropertyChanged(nameof(Step5IcDiffText));
        OnPropertyChanged(nameof(Step5RegularCadText));
        OnPropertyChanged(nameof(Step5PayloadSummaryText));
        OnPropertyChanged(nameof(Step5StatusText));
        OnPropertyChanged(nameof(Step6ValidationRegularText));
        OnPropertyChanged(nameof(Step6TraceCountText));
        OnPropertyChanged(nameof(Step6TraceDirectionText));
        OnPropertyChanged(nameof(SimulationImpactText));
        OnPropertyChanged(nameof(ActiveStepInputText));
        OnPropertyChanged(nameof(ActiveStepDecisionText));
        OnPropertyChanged(nameof(ActiveStepOutputText));
        OnPropertyChanged(nameof(ActiveStepImpactText));
        OnPropertyChanged(nameof(ActiveStepActionText));
    }

    private static string BuildTraceCacheKey(IndexMappingDecisionRowViewModel row)
    {
        return string.Join(
            "|",
            row.CadPadId?.ToString(CultureInfo.InvariantCulture) ?? "-",
            row.LocateRegularPadIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            row.IcIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            row.RowIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            row.SegmentIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            row.FilterMode.ToString(),
            row.DecisionReasonCodeText,
            row.CurrentDisplay,
            row.SuggestedDisplay);
    }

    private IEnumerable<IndexMappingDecisionRowViewModel> EnumerateVisibleDecisions()
    {
        foreach (var row in DecisionRows)
        {
            yield return row;
        }

        foreach (var row in AggregateDecisionRows)
        {
            yield return row;
        }
    }
}
