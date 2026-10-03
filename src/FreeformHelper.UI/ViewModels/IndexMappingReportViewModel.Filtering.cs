using FreeformHelper.UI.Services;
namespace FreeformHelper.UI.ViewModels;

public sealed partial class IndexMappingReportViewModel
{
    partial void OnShowCountMismatchIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowUnmappedCadIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowUnmappedRegularIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowLowConfidenceIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowAmbiguousIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowDuplicateDiffIssuesChanged(bool value) => RebuildWorkspaceView();
    partial void OnShowChangedMaskRowsOnlyChanged(bool value) => RebuildWorkspaceView();
    partial void OnSearchKeywordChanged(string value) => RebuildWorkspaceView();

    partial void OnSelectedFilterModeChanged(IndexMappingDecisionFilterMode value)
    {
        OnPropertyChanged(nameof(IsFilterAllActive));
        OnPropertyChanged(nameof(IsFilterChangedByMaskActive));
        OnPropertyChanged(nameof(IsFilterRemovedByMaskActive));
        OnPropertyChanged(nameof(IsFilterAmbiguousActive));
        OnPropertyChanged(nameof(IsFilterDuplicateDiffActive));
        OnPropertyChanged(nameof(IsFilterLowConfidenceActive));
        OnPropertyChanged(nameof(IsFilterUnmappedActive));
        OnPropertyChanged(nameof(IsFilterCountMismatchActive));
        OnPropertyChanged(nameof(SelectedFilterLabel));
        OnPropertyChanged(nameof(SimulationScopeText));
        OnPropertyChanged(nameof(SimulationColorStateText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        RebuildWorkspaceView();
    }

    private void ApplyIssueFilters()
    {
        RebuildWorkspaceView();
    }

    private void ApplyMaskAuditFilter()
    {
        RebuildWorkspaceView();
    }

    private void RebuildWorkspaceView()
    {
        var filterResult = IndexMappingVerificationWorkspaceFilterService.Build(
            _workspaceSnapshot,
            _allDecisionSeedRows,
            BuildFilterOptions());

        Issues = filterResult.Issues;
        MaskAuditRows = filterResult.MaskAuditRows;
        DecisionRows = filterResult.EntityRows;
        AggregateDecisionRows = filterResult.AggregateRows;

        OnPropertyChanged(nameof(HasIssues));
        OnPropertyChanged(nameof(HasNoIssues));
        OnPropertyChanged(nameof(IssueTitle));
        OnPropertyChanged(nameof(TruncationNote));
        OnPropertyChanged(nameof(HasTruncationNote));
        OnPropertyChanged(nameof(HasMaskAudit));
        OnPropertyChanged(nameof(HasNoMaskAudit));
        OnPropertyChanged(nameof(MaskAuditTitle));
        OnPropertyChanged(nameof(MaskAuditSummary));
        OnPropertyChanged(nameof(HasDecisionRows));
        OnPropertyChanged(nameof(HasNoDecisionRows));
        OnPropertyChanged(nameof(HasAggregateDecisionRows));
        OnPropertyChanged(nameof(HasNoAggregateDecisionRows));
        OnPropertyChanged(nameof(VisibleDecisionCount));
        OnPropertyChanged(nameof(VisibleAggregateDecisionCount));
        OnPropertyChanged(nameof(TotalDecisionCount));
        OnPropertyChanged(nameof(DecisionTitle));
        OnPropertyChanged(nameof(DecisionSummaryText));
        OnPropertyChanged(nameof(AggregateDecisionTitle));
        OnPropertyChanged(nameof(AggregateDecisionSummaryText));
        OnPropertyChanged(nameof(HasSearchKeyword));
        OnPropertyChanged(nameof(CanApplyCadOutputFwDiffOverrides));
        OnPropertyChanged(nameof(CanApplySelectedSegmentDiffOverrides));
        OnPropertyChanged(nameof(SelectedSegmentPreviewText));
        OnPropertyChanged(nameof(SimulationScopeText));
        OnPropertyChanged(nameof(SimulationScaleText));
        OnPropertyChanged(nameof(HeaderSubtitle));
        RefreshVerificationSteps();
        MarkAlgorithmTraceNotRequested();

        var selectedStillVisible = SelectedDecision is not null &&
                                   (DecisionRows.Contains(SelectedDecision) || AggregateDecisionRows.Contains(SelectedDecision));
        if (!selectedStillVisible)
        {
            SelectedDecision = DecisionRows.Count > 0
                ? DecisionRows[0]
                : AggregateDecisionRows.Count > 0
                    ? AggregateDecisionRows[0]
                    : null;
        }

        if (SelectedIssue is null || !Issues.Contains(SelectedIssue))
        {
            SelectedIssue = Issues.Count > 0 ? Issues[0] : null;
        }

        if (IsAlgorithmTraceExpanded && SelectedDecision is not null)
        {
            _ = EnsureAlgorithmTraceForSelectedDecisionAsync(priorityOnly: true);
        }
    }

    private IndexMappingVerificationWorkspaceFilterOptions BuildFilterOptions()
    {
        return new IndexMappingVerificationWorkspaceFilterOptions(
            ShowCountMismatchIssues,
            ShowUnmappedCadIssues,
            ShowUnmappedRegularIssues,
            ShowLowConfidenceIssues,
            ShowAmbiguousIssues,
            ShowDuplicateDiffIssues,
            ShowChangedMaskRowsOnly,
            SelectedFilterMode,
            SearchKeyword);
    }

    private void SetDecisionFilterMode(IndexMappingDecisionFilterMode mode)
    {
        SelectedFilterMode = SelectedFilterMode == mode && mode != IndexMappingDecisionFilterMode.All
            ? IndexMappingDecisionFilterMode.All
            : mode;
    }
}
