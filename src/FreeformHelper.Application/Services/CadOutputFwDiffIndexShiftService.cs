namespace FreeformHelper.Application.Services;

public static class CadOutputFwDiffIndexShiftService
{
    private const int MaxDiffIndex = 1_000_000;

    public static CadOutputFwDiffIndexShiftResult BuildShiftPlan(
        IReadOnlyCollection<int> selectedCadIds,
        IReadOnlyDictionary<int, int> currentCadOutputFwDiffByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        int delta)
    {
        ArgumentNullException.ThrowIfNull(selectedCadIds);
        ArgumentNullException.ThrowIfNull(currentCadOutputFwDiffByCadId);
        ArgumentNullException.ThrowIfNull(cadIcIndexByCadId);

        if (selectedCadIds.Count == 0)
        {
            return CadOutputFwDiffIndexShiftResult.Blocked("CAD Output FW Diff offset: select CAD pads first.");
        }

        if (delta == 0)
        {
            return CadOutputFwDiffIndexShiftResult.Blocked("CAD Output FW Diff offset: input a non-zero offset first.");
        }

        var selectedTargets = new Dictionary<int, int>();
        var missingCadIds = new List<int>();
        foreach (var cadId in selectedCadIds.Distinct().OrderBy(static id => id))
        {
            if (!currentCadOutputFwDiffByCadId.TryGetValue(cadId, out var currentDiff))
            {
                missingCadIds.Add(cadId);
                continue;
            }

            var target = currentDiff + delta;
            if (target < 0 || target > MaxDiffIndex)
            {
                return CadOutputFwDiffIndexShiftResult.Blocked(
                    FormattableString.Invariant(
                        $"CAD Output FW Diff offset blocked: CAD {cadId} would move from {currentDiff} to {target}, outside 0..{MaxDiffIndex}."));
            }

            selectedTargets[cadId] = target;
        }

        if (selectedTargets.Count == 0)
        {
            var missingText = missingCadIds.Count == 0
                ? "selected CAD pads are not visible."
                : $"CAD Output FW Diff missing for CAD {string.Join(", ", missingCadIds.Take(4))}{(missingCadIds.Count > 4 ? " (+)" : string.Empty)}.";
            return CadOutputFwDiffIndexShiftResult.Blocked($"CAD Output FW Diff offset blocked: {missingText}");
        }

        var conflicts = BuildConflicts(
            currentCadOutputFwDiffByCadId,
            cadIcIndexByCadId,
            selectedTargets);
        if (conflicts.Count > 0)
        {
            var sample = conflicts[0];
            return CadOutputFwDiffIndexShiftResult.Blocked(
                FormattableString.Invariant(
                    $"CAD Output FW Diff offset blocked: duplicate diff after shift at IC {sample.IcIndex + 1}, diff {sample.DiffIndex} -> CAD {string.Join(", ", sample.CadPadIds)}."));
        }

        var beforeDiffs = selectedTargets.Keys
            .Select(cadId => currentCadOutputFwDiffByCadId[cadId])
            .OrderBy(static diff => diff)
            .ToList();
        var afterDiffs = selectedTargets.Values
            .OrderBy(static diff => diff)
            .ToList();
        var summary = FormattableString.Invariant(
            $"Shifted {selectedTargets.Count} CAD pad(s) by {delta}. Range {beforeDiffs.First()}-{beforeDiffs.Last()} -> {afterDiffs.First()}-{afterDiffs.Last()}.");

        return CadOutputFwDiffIndexShiftResult.Success(
            selectedTargets,
            missingCadIds,
            summary);
    }

    private static List<CadOutputFwDiffIndexShiftConflict> BuildConflicts(
        IReadOnlyDictionary<int, int> currentCadOutputFwDiffByCadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        Dictionary<int, int> selectedTargets)
    {
        var groups = new Dictionary<(int IcIndex, int DiffIndex), List<int>>();
        foreach (var pair in currentCadOutputFwDiffByCadId.OrderBy(static pair => pair.Key))
        {
            var cadId = pair.Key;
            var diff = selectedTargets.TryGetValue(cadId, out var shiftedDiff)
                ? shiftedDiff
                : pair.Value;
            var icIndex = cadIcIndexByCadId.TryGetValue(cadId, out var ic)
                ? ic
                : 0;
            var key = (icIndex, diff);
            if (!groups.TryGetValue(key, out var cadIds))
            {
                cadIds = new List<int>();
                groups[key] = cadIds;
            }

            cadIds.Add(cadId);
        }

        return groups
            .Where(static group => group.Value.Count > 1)
            .Where(group => group.Value.Any(selectedTargets.ContainsKey))
            .OrderBy(static group => group.Key.IcIndex)
            .ThenBy(static group => group.Key.DiffIndex)
            .Select(group => new CadOutputFwDiffIndexShiftConflict(
                group.Key.IcIndex,
                group.Key.DiffIndex,
                group.Value.OrderBy(static id => id).ToArray()))
            .ToList();
    }
}

public sealed record CadOutputFwDiffIndexShiftResult(
    bool IsSuccessful,
    IReadOnlyDictionary<int, int> ShiftedDiffByCadId,
    IReadOnlyList<int> MissingCadPadIds,
    string Message)
{
    public static CadOutputFwDiffIndexShiftResult Success(
        IReadOnlyDictionary<int, int> shiftedDiffByCadId,
        IReadOnlyList<int> missingCadPadIds,
        string message)
        => new(true, shiftedDiffByCadId, missingCadPadIds, message);

    public static CadOutputFwDiffIndexShiftResult Blocked(string message)
        => new(false, new Dictionary<int, int>(), Array.Empty<int>(), message);
}

public sealed record CadOutputFwDiffIndexShiftConflict(
    int IcIndex,
    int DiffIndex,
    IReadOnlyList<int> CadPadIds);
