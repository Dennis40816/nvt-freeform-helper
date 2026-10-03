using System.Diagnostics.CodeAnalysis;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class CadOutputFwDiffIndexAssignmentService
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Instance API is intentionally preserved for ViewModel service call-site stability.")]
    public CadOutputFwDiffIndexAssignmentResult Assign(
        IReadOnlyList<CadPad> orderedPads,
        RegularGrid? grid,
        GridSettings gridSettings,
        CadOutputFwDiffAutoMode autoMode,
        IReadOnlyDictionary<int, int?> strictDiffByCadId,
        IReadOnlyDictionary<int, int>? manualOverrides = null,
        IReadOnlyDictionary<int, int>? anchorCadIdByIc = null)
    {
        ArgumentNullException.ThrowIfNull(orderedPads);
        ArgumentNullException.ThrowIfNull(gridSettings);
        ArgumentNullException.ThrowIfNull(strictDiffByCadId);
        _ = anchorCadIdByIc;

        var assignedByCadId = new Dictionary<int, int>(orderedPads.Count);
        var cadIcIndexByCadId = new Dictionary<int, int>(orderedPads.Count);
        var conflictSamples = new List<string>(5);
        var unmatchedSamples = new List<string>(5);

        if (orderedPads.Count == 0)
        {
            return new CadOutputFwDiffIndexAssignmentResult(
                assignedByCadId,
                cadIcIndexByCadId,
                Array.Empty<int>(),
                conflictSamples,
                unmatchedSamples,
                OverrideAssignedCount: 0,
                AutoAssignedCount: 0,
                AutoConflictCount: 0,
                AutoUnmatchedCount: 0,
                DuplicateOverrides: 0,
                InvalidOverrides: 0,
                OverrideMismatchCount: 0,
                SequenceAdjustedCount: 0,
                AnchorAppliedCount: 0,
                AnchorIgnoredCount: 0,
                RowSequenceAppliedCount: 0,
                RowSequenceFallbackCount: 0);
        }

        if (grid is null || grid.Rows <= 0 || grid.Cols <= 0)
        {
            return AssignWithoutGrid(
                orderedPads,
                autoMode,
                strictDiffByCadId,
                manualOverrides,
                assignedByCadId,
                cadIcIndexByCadId,
                conflictSamples,
                unmatchedSamples);
        }

        return AssignWithGridDirect(
            orderedPads,
            grid,
            gridSettings,
            autoMode,
            strictDiffByCadId,
            manualOverrides,
            assignedByCadId,
            cadIcIndexByCadId,
            conflictSamples,
            unmatchedSamples);
    }

    private static CadOutputFwDiffIndexAssignmentResult AssignWithGridDirect(
        IReadOnlyList<CadPad> orderedPads,
        RegularGrid grid,
        GridSettings gridSettings,
        CadOutputFwDiffAutoMode autoMode,
        IReadOnlyDictionary<int, int?> strictDiffByCadId,
        IReadOnlyDictionary<int, int>? manualOverrides,
        Dictionary<int, int> assignedByCadId,
        Dictionary<int, int> cadIcIndexByCadId,
        List<string> conflictSamples,
        List<string> unmatchedSamples)
    {
        var perIcCols = GridIcChannelAllocationService.ResolvePerIcColumns(gridSettings, grid.Cols);
        var rowGroupsByIc = BuildRowGroups(orderedPads, grid, perIcCols, cadIcIndexByCadId);
        var overrideAssignedCount = 0;
        var autoAssignedCount = 0;
        var autoConflictCount = 0;
        var autoUnmatchedCount = 0;
        var duplicateOverrides = 0;
        var invalidOverrides = 0;
        var overrideMismatchCount = 0;

        foreach (var icEntry in rowGroupsByIc.OrderBy(pair => pair.Key))
        {
            var icIndex = icEntry.Key;
            var usedDiffs = new HashSet<int>();

            foreach (var rowGroup in icEntry.Value)
            {
                foreach (var pad in rowGroup.Pads)
                {
                    if (manualOverrides is null ||
                        !manualOverrides.TryGetValue(pad.Id, out var overrideIndex))
                    {
                        continue;
                    }

                    if (overrideIndex < 0)
                    {
                        invalidOverrides++;
                        continue;
                    }

                    if (!usedDiffs.Add(overrideIndex))
                    {
                        duplicateOverrides++;
                        continue;
                    }

                    assignedByCadId[pad.Id] = overrideIndex;
                    overrideAssignedCount++;
                    if (strictDiffByCadId.TryGetValue(pad.Id, out var strictDiff) &&
                        strictDiff.HasValue &&
                        strictDiff.Value != overrideIndex)
                    {
                        overrideMismatchCount++;
                    }
                }
            }

            foreach (var rowGroup in icEntry.Value)
            {
                foreach (var pad in rowGroup.Pads)
                {
                    if (assignedByCadId.ContainsKey(pad.Id))
                    {
                        continue;
                    }

                    if (!strictDiffByCadId.TryGetValue(pad.Id, out var strictDiff) || !strictDiff.HasValue)
                    {
                        autoUnmatchedCount++;
                        if (unmatchedSamples.Count < 5)
                        {
                            unmatchedSamples.Add($"IC{icIndex + 1}/R{rowGroup.RowIndex}: CAD {pad.Id}");
                        }

                        continue;
                    }

                    var targetIndex = strictDiff.Value;
                    if (autoMode == CadOutputFwDiffAutoMode.StrictUnique && !usedDiffs.Add(targetIndex))
                    {
                        autoConflictCount++;
                        if (conflictSamples.Count < 5)
                        {
                            conflictSamples.Add($"IC{icIndex + 1}/R{rowGroup.RowIndex}: CAD {pad.Id} -> diff {targetIndex}");
                        }

                        continue;
                    }

                    assignedByCadId[pad.Id] = targetIndex;
                    autoAssignedCount++;
                }
            }
        }

        return new CadOutputFwDiffIndexAssignmentResult(
            assignedByCadId,
            cadIcIndexByCadId,
            Array.Empty<int>(),
            conflictSamples,
            unmatchedSamples,
            overrideAssignedCount,
            autoAssignedCount,
            autoConflictCount,
            autoUnmatchedCount,
            duplicateOverrides,
            invalidOverrides,
            overrideMismatchCount,
            SequenceAdjustedCount: 0,
            AnchorAppliedCount: 0,
            AnchorIgnoredCount: 0,
            RowSequenceAppliedCount: 0,
            RowSequenceFallbackCount: 0);
    }
}

public sealed record CadOutputFwDiffIndexAssignmentResult(
    IReadOnlyDictionary<int, int> AssignedIndexByCadId,
    IReadOnlyDictionary<int, int> CadIcIndexByCadId,
    IReadOnlyList<int> SequenceAdjustedCadIds,
    IReadOnlyList<string> ConflictSamples,
    IReadOnlyList<string> UnmatchedSamples,
    int OverrideAssignedCount,
    int AutoAssignedCount,
    int AutoConflictCount,
    int AutoUnmatchedCount,
    int DuplicateOverrides,
    int InvalidOverrides,
    int OverrideMismatchCount,
    int SequenceAdjustedCount,
    int AnchorAppliedCount,
    int AnchorIgnoredCount,
    int RowSequenceAppliedCount,
    int RowSequenceFallbackCount);
