using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class IndexMappingVerificationWorkspaceProjector
{
    public static IndexMappingVerificationWorkspaceSnapshot Build(
        DxfRegularMappingReport report,
        Func<int, int?>? getOverrideRegularIndex)
    {
        ArgumentNullException.ThrowIfNull(report);

        var issues = report.Issues
            .Select(issue => new IndexMappingReportIssueViewModel(issue, getOverrideRegularIndex))
            .ToList();
        var maskRows = (report.MaskAuditRows ?? Array.Empty<DxfRegularMaskAuditRow>())
            .Select(row => new IndexMappingMaskAuditRowViewModel(row, getOverrideRegularIndex))
            .ToList();
        var decisionContracts = IndexMappingDecisionRowProjector.BuildContracts(issues, maskRows);

        var metrics = BuildMetrics(decisionContracts);
        return new IndexMappingVerificationWorkspaceSnapshot(
            issues,
            maskRows,
            decisionContracts,
            metrics);
    }

    private static IndexMappingVerificationWorkspaceMetrics BuildMetrics(
        IReadOnlyList<IndexMappingDecisionRowContract> decisionContracts)
    {
        var countMismatch = 0;
        var unmapped = 0;
        var lowConfidence = 0;
        var ambiguous = 0;
        var duplicateDiff = 0;
        var changedByMask = 0;
        var removedByMask = 0;

        foreach (var contract in decisionContracts)
        {
            switch (contract.FilterMode)
            {
                case IndexMappingDecisionFilterMode.CountMismatch:
                    countMismatch++;
                    break;
                case IndexMappingDecisionFilterMode.Unmapped:
                    unmapped++;
                    break;
                case IndexMappingDecisionFilterMode.LowConfidence:
                    lowConfidence++;
                    break;
                case IndexMappingDecisionFilterMode.Ambiguous:
                    ambiguous++;
                    break;
                case IndexMappingDecisionFilterMode.DuplicateDiff:
                    duplicateDiff++;
                    break;
                case IndexMappingDecisionFilterMode.ChangedByMask:
                    changedByMask++;
                    break;
                case IndexMappingDecisionFilterMode.RemovedByMask:
                    removedByMask++;
                    break;
            }
        }

        return new IndexMappingVerificationWorkspaceMetrics(
            countMismatch,
            unmapped,
            lowConfidence,
            ambiguous,
            duplicateDiff,
            changedByMask,
            removedByMask);
    }
}
