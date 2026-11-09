namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public CadOutputFwDiffAssignmentDecisionSummarySnapshot GetCadOutputFwDiffAssignmentDecisionSummarySnapshot()
    {
        var snapshot = BuildWorkflowDataSnapshot();
        if (snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Count == 0)
        {
            return CadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty;
        }

        return DxfAssignmentSummaryBuilder.Build(snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Values.ToArray());
    }
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
