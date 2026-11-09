// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfDiffShiftService
{
    public static bool CanOffset(
        IReadOnlyCollection<int> selectedCadIds,
        IReadOnlyDictionary<int, int> diffIndexByCadId,
        decimal shiftDelta) =>
        selectedCadIds.Any(id => diffIndexByCadId.ContainsKey(id)) &&
        decimal.Truncate(shiftDelta) == shiftDelta &&
        shiftDelta != 0m;

    public static string BuildSelectionSummary(
        IReadOnlyCollection<int> selectedCadIds,
        IReadOnlyDictionary<int, int> diffIndexByCadId,
        decimal shiftDelta)
    {
        var visibleSelected = selectedCadIds
            .Where(id => diffIndexByCadId.ContainsKey(id))
            .OrderBy(id => id)
            .ToList();
        if (visibleSelected.Count == 0)
        {
            return "Diff shift: select CAD output pads in AA first.";
        }

        var diffs = visibleSelected
            .Select(id => diffIndexByCadId[id])
            .OrderBy(diff => diff)
            .ToList();
        return FormattableString.Invariant(
            $"Diff shift target: {visibleSelected.Count} CAD pad(s), current range {diffs.First()}-{diffs.Last()}, offset {shiftDelta:0}.");
    }

    public static DxfDiffShiftApplicationResult ApplyShift(
        IReadOnlyCollection<int> selectedCadIds,
        IReadOnlyDictionary<int, int> diffIndexByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        int delta,
        Dictionary<int, int> overrides)
    {
        var plan = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selectedCadIds, diffIndexByCadId, cadIcIndexByCadId, delta);
        if (!plan.IsSuccessful)
        {
            return new(plan, new Dictionary<int, int>(), 0);
        }

        var previousOverrides = overrides.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var changedCount = 0;
        foreach (var pair in plan.ShiftedDiffByCadId.OrderBy(static pair => pair.Key))
        {
            if (overrides.TryGetValue(pair.Key, out var previous) && previous == pair.Value)
            {
                continue;
            }

            overrides[pair.Key] = pair.Value;
            changedCount++;
        }

        return new(plan, previousOverrides, changedCount);
    }
}

internal sealed record DxfDiffShiftApplicationResult(
    CadOutputFwDiffIndexShiftResult Plan,
    Dictionary<int, int> PreviousOverrides,
    int ChangedCount);
