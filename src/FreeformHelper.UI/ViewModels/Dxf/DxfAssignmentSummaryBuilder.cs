// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfAssignmentSummaryBuilder
{
    public static CadOutputFwDiffAssignmentDecisionSummarySnapshot Build(IReadOnlyList<CadOutputFwDiffAssignmentDecision> decisions)
    {
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
            DecisionCount: decisions.Count,
            Mode: mode.ToContractString(),
            ReasonCounts: reasonCounts,
            DecisionSourceCounts: sourceCounts);
    }
}
