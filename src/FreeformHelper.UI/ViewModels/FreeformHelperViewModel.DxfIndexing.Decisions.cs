using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> GetCadOutputFwDiffAssignmentDecisionsSnapshot()
    {
        var snapshot = BuildWorkflowDataSnapshot();
        return snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value);
    }

    public CadOutputFwDiffAssignmentDecisionSummarySnapshot GetCadOutputFwDiffAssignmentDecisionSummarySnapshot()
    {
        var snapshot = BuildWorkflowDataSnapshot();
        if (snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Count == 0)
        {
            return CadOutputFwDiffAssignmentDecisionSummarySnapshot.Empty;
        }

        var decisions = snapshot.CadOutputFwDiffAssignmentDecisionsByCadId.Values.ToArray();
        var mode = decisions
            .GroupBy(static decision => decision.Mode)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key)
            .Select(static group => group.Key)
            .FirstOrDefault();
        var reasonCounts = decisions
            .GroupBy(static decision => decision.ReasonCode.ToContractString())
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var sourceCounts = decisions
            .GroupBy(static decision => decision.DecisionSource.ToContractString())
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);

        return new CadOutputFwDiffAssignmentDecisionSummarySnapshot(
            DecisionCount: decisions.Length,
            Mode: mode.ToContractString(),
            ReasonCounts: reasonCounts,
            DecisionSourceCounts: sourceCounts);
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
