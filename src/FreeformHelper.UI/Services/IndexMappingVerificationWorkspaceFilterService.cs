using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal sealed record IndexMappingVerificationWorkspaceFilterOptions(
    bool ShowCountMismatchIssues,
    bool ShowUnmappedCadIssues,
    bool ShowUnmappedRegularIssues,
    bool ShowLowConfidenceIssues,
    bool ShowAmbiguousIssues,
    bool ShowDuplicateDiffIssues,
    bool ShowChangedMaskRowsOnly,
    IndexMappingDecisionFilterMode SelectedFilterMode,
    string SearchKeyword);

internal sealed record IndexMappingVerificationWorkspaceFilterResult(
    IReadOnlyList<IndexMappingReportIssueViewModel> Issues,
    IReadOnlyList<IndexMappingMaskAuditRowViewModel> MaskAuditRows,
    IReadOnlyList<IndexMappingDecisionRowViewModel> EntityRows,
    IReadOnlyList<IndexMappingDecisionRowViewModel> AggregateRows);

internal static class IndexMappingVerificationWorkspaceFilterService
{
    public static IndexMappingVerificationWorkspaceFilterResult Build(
        IndexMappingVerificationWorkspaceSnapshot workspaceSnapshot,
        IReadOnlyList<IndexMappingDecisionRowViewModel> decisionSeedRows,
        IndexMappingVerificationWorkspaceFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(workspaceSnapshot);
        ArgumentNullException.ThrowIfNull(decisionSeedRows);
        ArgumentNullException.ThrowIfNull(options);

        var issues = workspaceSnapshot.Issues
            .Where(issue => issue.Kind switch
            {
                DxfRegularMappingIssueKind.CountMismatch => options.ShowCountMismatchIssues,
                DxfRegularMappingIssueKind.UnmappedCad => options.ShowUnmappedCadIssues,
                DxfRegularMappingIssueKind.UnmappedRegular => options.ShowUnmappedRegularIssues,
                DxfRegularMappingIssueKind.LowConfidence => options.ShowLowConfidenceIssues,
                DxfRegularMappingIssueKind.Ambiguous => options.ShowAmbiguousIssues,
                DxfRegularMappingIssueKind.DuplicateDiff => options.ShowDuplicateDiffIssues,
                _ => true,
            })
            .ToList();

        var maskRows = workspaceSnapshot.MaskAuditRows
            .Where(row => !options.ShowChangedMaskRowsOnly || row.IsChangedByMask || row.IsRemovedByMask)
            .ToList();

        var decisionRows = decisionSeedRows
            .Where(row => options.SelectedFilterMode == IndexMappingDecisionFilterMode.All || row.FilterMode == options.SelectedFilterMode)
            .Where(row => row.Matches(options.SearchKeyword))
            .OrderBy(row => row.SortPriority)
            .ThenBy(row => row.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.CadPadId ?? int.MaxValue)
            .ToList();

        var aggregateRows = decisionRows
            .Where(static row => row.IsAggregateRow)
            .ToList();
        var entityRows = decisionRows
            .Where(static row => row.IsEntityRow)
            .ToList();

        return new IndexMappingVerificationWorkspaceFilterResult(
            issues,
            maskRows,
            entityRows,
            aggregateRows);
    }
}
