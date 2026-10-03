using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Provides services for capturing and applying user-defined overrides for pad properties
/// (such as freeform type and custom CAD pad values) to and from a <see cref="ProjectFile"/>.
/// This ensures that manual adjustments persist across application sessions.
/// </summary>
public sealed class PadOverrideService
{
    /// <summary>
    /// Captures the current state of custom CAD pad values and freeform type overrides
    /// from the active workspace and stores them in the provided <see cref="ProjectFile"/>.
    /// </summary>
    /// <param name="file">The <see cref="ProjectFile"/> to store the overrides in.</param>
    /// <param name="grid">The <see cref="RegularGrid"/> from which freeform overrides are captured. Can be null.</param>
    /// <param name="cadPadCustomValues">A read-only dictionary of custom numeric values associated with CAD pads.</param>
    public static void Capture(ProjectFile file, RegularGrid? grid, IReadOnlyDictionary<int, double> cadPadCustomValues)
    {
        // Store a copy of the custom CAD pad values.
        file.CadPadCustomValues = new Dictionary<int, double>(cadPadCustomValues);

        if (grid is null)
        {
            return; // No grid, no freeform overrides to capture.
        }

        // Capture non-default FreeformType assignments from regular pads.
        var freeform = new Dictionary<int, FreeformType>();
        foreach (var pad in grid.Pads)
        {
            if (pad.Freeform != FreeformType.None)
            {
                freeform[pad.Index] = pad.Freeform;
            }
        }

        file.FreeformOverrides = freeform;
    }

    /// <summary>
    /// Applies previously captured freeform type overrides from a <see cref="ProjectFile"/>
    /// back to the pads in the provided <see cref="RegularGrid"/>.
    /// </summary>
    /// <param name="file">The <see cref="ProjectFile"/> containing the freeform overrides.</param>
    /// <param name="grid">The <see cref="RegularGrid"/> to apply the overrides to.</param>
    public static void ApplyFreeformOverrides(ProjectFile file, RegularGrid grid)
    {
        // If there are no overrides in the file, or the dictionary is empty, do nothing.
        if (file.FreeformOverrides is null || file.FreeformOverrides.Count == 0)
        {
            return;
        }

        // Iterate through all regular pads in the grid.
        foreach (var pad in grid.Pads)
        {
            // If an override exists for this pad's index, apply it.
            if (file.FreeformOverrides.TryGetValue(pad.Index, out var type))
            {
                pad.Freeform = type;
            }
        }
    }
}
