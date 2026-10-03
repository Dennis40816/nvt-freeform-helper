using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Interaction;

/// <summary>
/// Manages the interactive state of the workspace, specifically concerning
/// selected pads and the currently active pad information display.
/// </summary>
public sealed class WorkspaceInteractionState
{
    // Internal backing fields for selected pads and active pad info.
    private IReadOnlyList<int> _selectedCadIds = Array.Empty<int>();
    private IReadOnlyList<int> _selectedRegularIndices = Array.Empty<int>();
    private PadInfoContext? _activePadInfo;

    /// <summary>
    /// Event raised when the selection of CAD or regular pads changes.
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    /// <summary>
    /// Event raised when the active pad information context changes (opened or closed).
    /// </summary>
    public event EventHandler<PadInfoChangedEventArgs>? PadInfoChanged;

    /// <summary>
    /// Gets a read-only list of the IDs of currently selected CAD pads.
    /// </summary>
    public IReadOnlyList<int> SelectedCadIds => _selectedCadIds;
    /// <summary>
    /// Gets a read-only list of the indices of currently selected regular pads.
    /// </summary>
    public IReadOnlyList<int> SelectedRegularIndices => _selectedRegularIndices;
    /// <summary>
    /// Gets the context for the currently active pad information display, or null if none is active.
    /// </summary>
    public PadInfoContext? ActivePadInfo => _activePadInfo;

    /// <summary>
    /// Sets the current selection of CAD and regular pads.
    /// If the new selection is different from the current one, it raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    /// <param name="cadIds">The IDs of CAD pads to select.</param>
    /// <param name="regularIndices">The indices of regular pads to select.</param>
    public void SetSelection(IReadOnlyList<int> cadIds, IReadOnlyList<int> regularIndices)
    {
        // Normalize the incoming selection lists (remove duplicates, sort).
        var normalizedCad = NormalizeSelection(cadIds);
        var normalizedRegular = NormalizeSelection(regularIndices);

        // Check if the selection has actually changed to avoid unnecessary updates.
        if (_selectedCadIds.SequenceEqual(normalizedCad) && _selectedRegularIndices.SequenceEqual(normalizedRegular))
        {
            return;
        }

        // Update internal state and raise the event.
        _selectedCadIds = normalizedCad;
        _selectedRegularIndices = normalizedRegular;
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(_selectedCadIds, _selectedRegularIndices));
    }

    /// <summary>
    /// Opens the pad information display with the given context.
    /// Raises the <see cref="PadInfoChanged"/> event.
    /// </summary>
    /// <param name="context">The <see cref="PadInfoContext"/> containing information about the pad to display.</param>
    public void OpenPadInfo(PadInfoContext context)
    {
        _activePadInfo = context;
        PadInfoChanged?.Invoke(this, new PadInfoChangedEventArgs(context));
    }

    /// <summary>
    /// Closes the pad information display.
    /// Raises the <see cref="PadInfoChanged"/> event with a null context.
    /// </summary>
    public void ClosePadInfo()
    {
        // Only close if there's an active pad info display.
        if (_activePadInfo is null)
        {
            return;
        }

        _activePadInfo = null;
        PadInfoChanged?.Invoke(this, new PadInfoChangedEventArgs(null));
    }

    /// <summary>
    /// Normalizes a list of integer IDs/indices by removing duplicates and sorting them.
    /// </summary>
    /// <param name="values">The list of integer values.</param>
    /// <returns>A new read-only list of unique, sorted integer values.</returns>
    private static IReadOnlyList<int> NormalizeSelection(IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return Array.Empty<int>();
        }

        if (IsStrictlyIncreasing(values))
        {
            return values;
        }

        return values.Distinct().OrderBy(v => v).ToList();
    }

    private static bool IsStrictlyIncreasing(IReadOnlyList<int> values)
    {
        if (values.Count <= 1)
        {
            return true;
        }

        var previous = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            var current = values[i];
            if (current <= previous)
            {
                return false;
            }

            previous = current;
        }

        return true;
    }
}

/// <summary>
/// Provides data for the <see cref="WorkspaceInteractionState.SelectionChanged"/> event.
/// </summary>
/// <param name="CadIds">The IDs of the currently selected CAD pads.</param>
/// <param name="RegularIndices">The indices of the currently selected regular pads.</param>
public sealed class SelectionChangedEventArgs : EventArgs
{
    public SelectionChangedEventArgs(IReadOnlyList<int> cadIds, IReadOnlyList<int> regularIndices)
    {
        CadIds = cadIds;
        RegularIndices = regularIndices;
    }

    public IReadOnlyList<int> CadIds { get; }

    public IReadOnlyList<int> RegularIndices { get; }
}

/// <summary>
/// Provides data for the <see cref="WorkspaceInteractionState.PadInfoChanged"/> event.
/// </summary>
/// <param name="Context">The <see cref="PadInfoContext"/> for the active pad info, or null if closed.</param>
public sealed class PadInfoChangedEventArgs : EventArgs
{
    public PadInfoChangedEventArgs(PadInfoContext? context)
    {
        Context = context;
    }

    public PadInfoContext? Context { get; }
}

/// <summary>
/// Encapsulates context information for displaying pad details.
/// </summary>
/// <param name="ViewModel">The ViewModel associated with the pad being displayed.</param>
/// <param name="BoundsWorld">The world-coordinate bounding box of the pad.</param>
/// <param name="SelectedBounds">A list of world-coordinate bounding boxes for all selected pads.</param>
public sealed record PadInfoContext(object ViewModel, Rect2 BoundsWorld, IReadOnlyList<Rect2> SelectedBounds);
