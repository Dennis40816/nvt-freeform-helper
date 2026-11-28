using FreeformHelper.UI.ViewModels.Undo;

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
    public bool CanUndo => UndoCoordinator.CanUndo;

    // UI thread only. The shared suppression and history instances remain in the host.
    private UndoOwner? _undoOwner;

    private UndoOwner UndoCoordinator => _undoOwner ??= new UndoOwner(
        () => _isLoadingSettings,
        () => _undoSuppression.IsActive,
        () => _undoSuppression.Enter(),
        (undo, description) => _undoService.Push(undo, description),
        () => _undoService.TryPop(out var entry)
            ? (true, entry.Undo, entry.Description)
            : (false, null, null),
        () => _undoService.CanUndo,
        () => UndoCommand?.NotifyCanExecuteChanged(),
        status => SetStatus(status),
        name => OnPropertyChanged(name));

    /// <summary>
    /// Pushes a new undoable action onto the undo stack.
    /// </summary>
    /// <param name="undo">The <see cref="Action"/> to execute to revert the change.</param>
    /// <param name="description">A description of the change, used for UI feedback.</param>
    private void PushUndo(Action undo, string description)
    {
        UndoCoordinator.PushUndo(undo, description);
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
        UndoCoordinator.TrackUndo(oldValue, newValue, assign, description);
    }

    /// <summary>
    /// Executes the last action on the undo stack, reverting a previous change.
    /// </summary>
    public void Undo()
    {
        UndoCoordinator.Undo();
    }
}
