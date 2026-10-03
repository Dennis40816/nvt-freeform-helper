using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Handles freeform tagging workflows for manual and automatic tagging.
/// </summary>
public sealed class FreeformTaggingUseCase
{
    public static int ApplyManual(RegularGrid grid, IReadOnlyCollection<int> selectedIndices, FreeformType type)
    {
        var touched = 0;
        if (selectedIndices.Count == 0)
        {
            return touched;
        }

        var selected = selectedIndices.ToHashSet();
        foreach (var pad in grid.Pads)
        {
            if (!selected.Contains(pad.Index))
            {
                continue;
            }

            pad.Freeform = type;
            touched++;
        }

        return touched;
    }

    public static IReadOnlyDictionary<int, FreeformType> BuildAutoDetectAssignments(
        CadPadSet cad,
        RegularGrid grid,
        MatchingSettings settings,
        PadMatchResult? matchResult = null)
    {
        return FreeformDetector.DetectAssignments(cad, grid, settings, matchResult);
    }

    public static void ApplyAutoDetectAssignments(RegularGrid grid, IReadOnlyDictionary<int, FreeformType> assignments)
    {
        FreeformDetector.ApplyAssignments(grid, assignments);
    }

    public static void AutoDetect(CadPadSet cad, RegularGrid grid, MatchingSettings settings, PadMatchResult? matchResult = null)
    {
        ApplyAutoDetectAssignments(grid, BuildAutoDetectAssignments(cad, grid, settings, matchResult));
    }
}
