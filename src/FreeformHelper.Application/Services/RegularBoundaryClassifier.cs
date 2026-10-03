using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Classifies whether a regular pad sits on an effective boundary.
/// Effective boundary means outer grid edge or any missing active cardinal neighbor.
/// </summary>
public sealed class RegularBoundaryClassifier
{
    public static IReadOnlySet<(int Row, int Col)> BuildOccupiedGridCells(
        RegularGrid grid,
        IReadOnlySet<int>? activeRegularPadIds = null)
    {
        ArgumentNullException.ThrowIfNull(grid);

        var filteredPads = activeRegularPadIds is not null && activeRegularPadIds.Count > 0
            ? grid.Pads.Where(pad => activeRegularPadIds.Contains(pad.RegularPadId))
            : grid.Pads;

        var occupied = filteredPads
            .Select(static pad => (pad.Row, pad.Col))
            .ToHashSet();

        // Fallback for stale Step1 mappings (e.g., grid rebuilt but match result not rerun yet):
        // do not treat every regular as boundary if activeRegularPadIds no longer maps to current grid.
        if (occupied.Count == 0 && grid.Pads.Count > 0)
        {
            return grid.Pads
                .Select(static pad => (pad.Row, pad.Col))
                .ToHashSet();
        }

        return occupied;
    }

    public static bool IsBoundaryPad(
        RegularPad pad,
        int rows,
        int cols,
        IReadOnlySet<(int Row, int Col)> occupiedGridCells)
    {
        ArgumentNullException.ThrowIfNull(pad);
        ArgumentNullException.ThrowIfNull(occupiedGridCells);

        if (pad.Row <= 0 ||
            pad.Col <= 0 ||
            pad.Row >= rows - 1 ||
            pad.Col >= cols - 1)
        {
            return true;
        }

        if (occupiedGridCells.Count == 0)
        {
            return true;
        }

        if (!occupiedGridCells.Contains((pad.Row, pad.Col)))
        {
            return true;
        }

        return !occupiedGridCells.Contains((pad.Row - 1, pad.Col)) ||
               !occupiedGridCells.Contains((pad.Row + 1, pad.Col)) ||
               !occupiedGridCells.Contains((pad.Row, pad.Col - 1)) ||
               !occupiedGridCells.Contains((pad.Row, pad.Col + 1));
    }
}
