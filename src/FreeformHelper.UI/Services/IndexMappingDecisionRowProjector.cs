using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class IndexMappingDecisionRowProjector
{
    public static List<IndexMappingDecisionRowContract> BuildContracts(
        IReadOnlyList<IndexMappingReportIssueViewModel> issues,
        IReadOnlyList<IndexMappingMaskAuditRowViewModel> maskRows)
    {
        var contracts = new List<IndexMappingDecisionRowContract>(issues.Count + maskRows.Count);
        contracts.AddRange(issues
            .Select(IndexMappingDecisionRowViewModel.FromIssue)
            .Select(row => row.ToContract()));
        contracts.AddRange(maskRows
            .Where(row => row.IsDecisionRelevant)
            .Select(IndexMappingDecisionRowViewModel.FromMaskAudit)
            .Select(row => row.ToContract()));
        return contracts;
    }

    public static List<IndexMappingDecisionRowViewModel> Build(
        IReadOnlyList<IndexMappingReportIssueViewModel> issues,
        IReadOnlyList<IndexMappingMaskAuditRowViewModel> maskRows)
    {
        return BuildContracts(issues, maskRows)
            .Select(IndexMappingDecisionRowViewModel.FromContract)
            .ToList();
    }
}
