// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

internal static class DxfOverrideService
{
    public static DxfOverrideSetResult SetOverride(
        int cadPadId,
        int targetIndex,
        Dictionary<int, int> overrides,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        var clampedIndex = Math.Clamp(targetIndex, 0, 1_000_000);
        var targetIc = cadIcIndexByCadId.TryGetValue(cadPadId, out var inferredIc)
            ? inferredIc
            : (int?)null;
        var hadPrevious = overrides.TryGetValue(cadPadId, out var previousIndex);
        if (hadPrevious && previousIndex == clampedIndex)
        {
            return new(false, hadPrevious, previousIndex, new Dictionary<int, int>(), clampedIndex, targetIc, string.Empty);
        }

        var replaced = overrides
            .Where(kv =>
                kv.Key != cadPadId &&
                kv.Value == clampedIndex &&
                MatchesConflictIc(kv.Key, targetIc, cadIcIndexByCadId))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        foreach (var cadId in replaced.Keys)
        {
            overrides.Remove(cadId);
        }

        overrides[cadPadId] = clampedIndex;
        var message = BuildSavedMessage(cadPadId, clampedIndex, targetIc);
        if (replaced.Count != 0)
        {
            message = $"{message} Replaced {replaced.Count} conflicting override(s) in same IC.";
        }

        return new(true, hadPrevious, previousIndex, replaced, clampedIndex, targetIc, message);
    }

    public static DxfOverrideClearResult ClearOverride(int cadPadId, Dictionary<int, int> overrides)
    {
        if (!overrides.TryGetValue(cadPadId, out var previousIndex))
        {
            return new(false, previousIndex);
        }

        overrides.Remove(cadPadId);
        return new(true, previousIndex);
    }

    public static bool IsAnchor(
        int cadPadId,
        IReadOnlyDictionary<int, int> cadIcIndexByCadId,
        IReadOnlyDictionary<int, int> anchorCadPadByIc)
    {
        if (!cadIcIndexByCadId.TryGetValue(cadPadId, out var icIndex))
        {
            return false;
        }

        return anchorCadPadByIc.TryGetValue(icIndex, out var anchorCadId) && anchorCadId == cadPadId;
    }

    private static bool MatchesConflictIc(int cadPadId, int? targetIc, IReadOnlyDictionary<int, int> cadIcIndexByCadId)
    {
        if (!targetIc.HasValue)
        {
            return true;
        }

        if (cadIcIndexByCadId.TryGetValue(cadPadId, out var otherIc))
        {
            return otherIc == targetIc.Value;
        }

        return false;
    }

    private static string BuildSavedMessage(int cadPadId, int index, int? targetIc)
    {
        return targetIc.HasValue
            ? $"CAD Output FW Diff override saved: IC {targetIc.Value + 1}, CAD {cadPadId} -> {index}."
            : $"CAD Output FW Diff override saved: CAD {cadPadId} -> {index}.";
    }
}

internal sealed record DxfOverrideSetResult(
    bool Changed,
    bool HadPrevious,
    int PreviousIndex,
    IReadOnlyDictionary<int, int> ReplacedOverrides,
    int ClampedIndex,
    int? TargetIc,
    string Message);

internal readonly record struct DxfOverrideClearResult(bool HadPrevious, int PreviousIndex);
