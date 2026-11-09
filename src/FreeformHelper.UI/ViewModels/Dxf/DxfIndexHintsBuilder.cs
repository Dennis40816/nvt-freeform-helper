// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;

namespace FreeformHelper.UI.ViewModels;

internal static class DxfIndexHintsBuilder
{
    public static DxfIndexHints Build(
        IReadOnlySet<int> visibleCadIds,
        IReadOnlyDictionary<int, int> overrides,
        IReadOnlyDictionary<int, int> anchorCadPadByIc,
        ScanOrder scanOrder,
        int icGroupCount,
        CadOutputFwDiffAutoMode autoMode,
        CadOutputFwDiffIndexAssignmentResult assignment)
    {
        var visibleOverrideCadIds = overrides.Keys
            .Where(visibleCadIds.Contains)
            .OrderBy(id => id)
            .ToList();

        var anchorCadIds = anchorCadPadByIc
            .OrderBy(kv => kv.Key)
            .Where(kv => visibleCadIds.Contains(kv.Value))
            .Select(kv => kv.Value)
            .Distinct()
            .ToList();

        var anchorPairs = anchorCadPadByIc
            .OrderBy(kv => kv.Key)
            .Take(4)
            .Select(kv => $"IC{kv.Key + 1}->CAD {kv.Value}")
            .ToList();
        var anchorText = anchorPairs.Count == 0
            ? "anchor=none"
            : $"anchor=stored (inactive in current modes: {string.Join(", ", anchorPairs)}{(anchorCadPadByIc.Count > anchorPairs.Count ? " (+)" : string.Empty)})";
        var scanOrderText = $"scan={scanOrder}";
        var icText = $"IC groups={icGroupCount}";
        var unresolvedCount = assignment.AutoConflictCount + assignment.AutoUnmatchedCount;
        var modeText = autoMode == CadOutputFwDiffAutoMode.BestMatchDirect
            ? "geometry seed"
            : "geometry seed (unique per IC)";
        var priorityText = autoMode == CadOutputFwDiffAutoMode.BestMatchDirect
            ? "override > direct geometry-seed"
            : "override > direct geometry-seed (per-IC unique)";
        var qualityText = assignment.DuplicateOverrides == 0 &&
                          assignment.InvalidOverrides == 0 &&
                          assignment.AutoConflictCount == 0 &&
                          assignment.AutoUnmatchedCount == 0 &&
                          assignment.OverrideMismatchCount == 0
            ? string.Empty
            : $" | issues: overrideDuplicate={assignment.DuplicateOverrides}, overrideInvalid={assignment.InvalidOverrides}, autoConflict={assignment.AutoConflictCount}, autoUnmatched={assignment.AutoUnmatchedCount}, overrideMismatch={assignment.OverrideMismatchCount}";

        var summary =
            $"CAD Output FW Diff assignment: override={assignment.OverrideAssignedCount}, auto={assignment.AutoAssignedCount}, unresolved={unresolvedCount}, {icText}, {anchorText}, {scanOrderText}. Mode: {modeText}. Priority: {priorityText}{qualityText}.";
        return new DxfIndexHints(visibleOverrideCadIds, anchorCadIds, summary);
    }
}

internal sealed record DxfIndexHints(
    IReadOnlyList<int> OverrideCadIds,
    IReadOnlyList<int> AnchorCadIdsForCanvas,
    string Summary);
