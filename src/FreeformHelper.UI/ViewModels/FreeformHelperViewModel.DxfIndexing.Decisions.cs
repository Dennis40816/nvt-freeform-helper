namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public CadOutputFwDiffAssignmentDecisionSummarySnapshot GetCadOutputFwDiffAssignmentDecisionSummarySnapshot()
        => IndexMapping.Assignment.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
}

public readonly record struct CadOutputFwDiffAssignmentDecisionSummarySnapshot(
    int DecisionCount,
    string Mode,
    IReadOnlyDictionary<string, int> ReasonCounts,
    IReadOnlyDictionary<string, int> DecisionSourceCounts)
{
    public static readonly CadOutputFwDiffAssignmentDecisionSummarySnapshot Empty = new(
        DecisionCount: 0,
        Mode: "unknown",
        ReasonCounts: new Dictionary<string, int>(StringComparer.Ordinal),
        DecisionSourceCounts: new Dictionary<string, int>(StringComparer.Ordinal));
}
