namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> implements the undo/redo
/// functionality for various operations within the application. It maintains an undo stack
/// and provides methods to push undoable actions and execute the last undo action.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    // One suppression owner for programmatic changes and undo actions.
    private readonly UndoSuppression _undoSuppression = new();

    /// <summary>
    /// Gets a value indicating whether there are any actions currently available to be undone.
    /// </summary>
    public bool CanUndo => _undoService.CanUndo;

    /// <summary>
    /// Pushes a new undoable action onto the undo stack.
    /// </summary>
    /// <param name="undo">The <see cref="Action"/> to execute to revert the change.</param>
    /// <param name="description">A description of the change, used for UI feedback.</param>
    private void PushUndo(Action undo, string description)
    {
        // Suppress undo recording during programmatic changes or settings loading.
        if (_undoSuppression.IsActive || _isLoadingSettings)
        {
            return;
        }

        _undoService.Push(undo, description);
        UndoCommand?.NotifyCanExecuteChanged(); // Notify UI that undo command's CanExecute state might have changed.
        OnPropertyChanged(nameof(CanUndo)); // Notify UI that CanUndo property has changed.
    }

    /// <summary>
    /// Tracks a change to a property for undo purposes. If the old and new values are different,
    /// an undo entry is pushed onto the stack.
    /// </summary>
    /// <typeparam name="T">The type of the property being tracked.</typeparam>
    /// <param name="oldValue">The value of the property before the change.</param>
    /// <param name="newValue">The value of the property after the change.</param>
    /// <param name="assign">An <see cref="Action{T}"/> delegate that assigns a given value back to the property.</param>
    /// <param name="description">A description of the change.</param>
    private void TrackUndo<T>(T oldValue, T newValue, Action<T> assign, string description)
    {
        // Suppress undo recording during programmatic changes or settings loading.
        if (_undoSuppression.IsActive || _isLoadingSettings)
        {
            return;
        }

        // Only push an undo entry if the value has actually changed.
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        PushUndo(() => assign(oldValue), description); // Push an action to restore the old value.
    }

    /// <summary>
    /// Executes the last action on the undo stack, reverting a previous change.
    /// </summary>
    public void Undo()
    {
        if (!_undoService.TryPop(out var entry))
        {
            return; // Nothing to undo.
        }
        using (_undoSuppression.Enter())
        {
            entry.Undo(); // Execute the undo action.
        }

        SetStatus($"Undo: {entry.Description}"); // Update status text with undo information.
        UndoCommand?.NotifyCanExecuteChanged(); // Notify UI that undo command's CanExecute state might have changed.
        OnPropertyChanged(nameof(CanUndo)); // Notify UI that CanUndo property has changed.
    }
}
