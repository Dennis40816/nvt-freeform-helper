using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

public sealed partial class CadOutputFwDiffIndexAssignmentService
{
    private static CadOutputFwDiffIndexAssignmentResult AssignWithoutGrid(
        IReadOnlyList<CadPad> orderedPads,
        CadOutputFwDiffAutoMode autoMode,
        IReadOnlyDictionary<int, int?> strictDiffByCadId,
        IReadOnlyDictionary<int, int>? manualOverrides,
        Dictionary<int, int> assignedByCadId,
        Dictionary<int, int> cadIcIndexByCadId,
        List<string> conflictSamples,
        List<string> unmatchedSamples)
    {
        var used = new HashSet<int>();
        var overrideAssignedCount = 0;
        var autoAssignedCount = 0;
        var autoConflictCount = 0;
        var autoUnmatchedCount = 0;
        var duplicateOverrides = 0;
        var invalidOverrides = 0;
        var overrideMismatchCount = 0;

        foreach (var pad in orderedPads)
        {
            cadIcIndexByCadId[pad.Id] = 0;
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

            if (!used.Add(overrideIndex))
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

        foreach (var pad in orderedPads)
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
                    unmatchedSamples.Add($"IC1/R?: CAD {pad.Id}");
                }

                continue;
            }

            var targetIndex = strictDiff.Value;
            if (autoMode == CadOutputFwDiffAutoMode.StrictUnique && !used.Add(targetIndex))
            {
                autoConflictCount++;
                if (conflictSamples.Count < 5)
                {
                    conflictSamples.Add($"IC1/R?: CAD {pad.Id} -> diff {targetIndex}");
                }

                continue;
            }

            assignedByCadId[pad.Id] = targetIndex;
            autoAssignedCount++;
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
