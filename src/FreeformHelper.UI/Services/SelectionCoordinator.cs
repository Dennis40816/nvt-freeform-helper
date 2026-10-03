using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Interaction;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Coordinates selection changes between the canvas and interaction state.
/// </summary>
public sealed class SelectionCoordinator
{
    private readonly WorkspaceInteractionState _interactionState;

    public SelectionCoordinator(WorkspaceInteractionState interactionState)
    {
        _interactionState = interactionState ?? throw new ArgumentNullException(nameof(interactionState));
    }

    public void ApplyCanvasSelection(IReadOnlyList<int> cadIds, IReadOnlyList<int> regularIndices)
    {
        _interactionState.SetSelection(cadIds, regularIndices);
    }

    public void ClearSelection(ICanvasHost? canvasHost)
    {
        canvasHost?.ClearSelection();
        _interactionState.SetSelection(Array.Empty<int>(), Array.Empty<int>());
    }

    public void ApplyProgrammaticSelection(
        IReadOnlyCollection<int> cadIds,
        IReadOnlyCollection<int> regularIndices,
        ICanvasHost? canvasHost)
    {
        canvasHost?.SetSelection(cadIds, regularIndices);
        _interactionState.SetSelection(cadIds.ToArray(), regularIndices.ToArray());
    }

    public void SelectRegularRange(
        RegularGrid grid,
        IReadOnlyList<int> rows,
        IReadOnlyList<int> cols,
        ICanvasHost? canvasHost)
    {
        if (rows.Count == 0 || cols.Count == 0)
        {
            ClearSelection(canvasHost);
            return;
        }

        var rowSet = rows.ToHashSet();
        var colSet = cols.ToHashSet();
        var indices = grid.Pads
            .Where(p => rowSet.Contains(p.Row) && colSet.Contains(p.Col))
            .Select(p => p.Index)
            .ToList();

        ApplyProgrammaticSelection(Array.Empty<int>(), indices, canvasHost);
    }
}
