using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal sealed record IndexMappingVerificationWorkspaceSnapshot(
    IReadOnlyList<IndexMappingReportIssueViewModel> Issues,
    IReadOnlyList<IndexMappingMaskAuditRowViewModel> MaskAuditRows,
    IReadOnlyList<IndexMappingDecisionRowContract> DecisionRowContracts,
    IndexMappingVerificationWorkspaceMetrics Metrics);
