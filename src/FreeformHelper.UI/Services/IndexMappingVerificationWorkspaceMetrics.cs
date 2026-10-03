namespace FreeformHelper.UI.Services;

internal sealed record IndexMappingVerificationWorkspaceMetrics(
    int CountMismatchDecisionCount,
    int UnmappedDecisionCount,
    int LowConfidenceDecisionCount,
    int AmbiguousDecisionCount,
    int DuplicateDiffDecisionCount,
    int ChangedByMaskDecisionCount,
    int RemovedByMaskDecisionCount)
{
    public int TotalDecisionCount => CountMismatchDecisionCount +
                                     UnmappedDecisionCount +
                                     LowConfidenceDecisionCount +
                                     AmbiguousDecisionCount +
                                     DuplicateDiffDecisionCount +
                                     ChangedByMaskDecisionCount +
                                     RemovedByMaskDecisionCount;
}
