// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfCsvRepairRule
{
    private const double CsvLocalRepairAutoApplyMinConfidence = 0.80;

    public static bool ShouldAutoApply(
        CadOutputFwDiffAssignmentDecision decision,
        IReadOnlyDictionary<int, int> overrides)
    {
        if (decision.Mode != CadOutputFwDiffAssignmentMode.CsvConstrained ||
            decision.RepairSuggestionDiffIndex is null ||
            !decision.Confidence.HasValue ||
            decision.Confidence.Value < CsvLocalRepairAutoApplyMinConfidence)
        {
            return false;
        }

        if (decision.CurrentPrimaryDiffIndex.HasValue &&
            decision.CurrentPrimaryDiffIndex.Value == decision.RepairSuggestionDiffIndex.Value)
        {
            return false;
        }

        return !overrides.ContainsKey(decision.CadPadId);
    }

    public static bool WouldCreateSameIcDuplicate(
        int cadPadId,
        int targetDiff,
        IReadOnlyDictionary<int, int> diffIndexByCadId,
        IReadOnlyDictionary<int, int> icIndexByCadId)
    {
        if (!icIndexByCadId.TryGetValue(cadPadId, out var cadIcIndex))
        {
            return false;
        }

        foreach (var (otherCadId, otherDiff) in diffIndexByCadId)
        {
            if (otherCadId == cadPadId || otherDiff != targetDiff)
            {
                continue;
            }

            if (icIndexByCadId.TryGetValue(otherCadId, out var otherIcIndex) &&
                otherIcIndex == cadIcIndex)
            {
                return true;
            }
        }

        return false;
    }

    public static List<int> SelectRepairs(
        IReadOnlyDictionary<int, CadOutputFwDiffAssignmentDecision> decisions,
        IReadOnlyDictionary<int, int> overrides,
        Dictionary<int, int> diffIndexByCadId,
        IReadOnlyDictionary<int, int> icIndexByCadId)
    {
        var appliedCadIds = new List<int>();
        foreach (var decision in decisions.Values.OrderBy(static row => row.OrderedCadIndex))
        {
            if (!ShouldAutoApply(decision, overrides))
            {
                continue;
            }

            var targetDiff = decision.RepairSuggestionDiffIndex!.Value;
            if (WouldCreateSameIcDuplicate(decision.CadPadId, targetDiff, diffIndexByCadId, icIndexByCadId))
            {
                continue;
            }

            diffIndexByCadId[decision.CadPadId] = targetDiff;
            appliedCadIds.Add(decision.CadPadId);
        }

        return appliedCadIds;
    }
}
