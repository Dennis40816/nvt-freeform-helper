namespace FreeformHelper.UI.Services;

/// <summary>
/// Stores undo actions and exposes a single entry for undo operations.
/// </summary>
public sealed class UndoService
{
    private readonly Stack<UndoAction> _stack = new();

    public bool CanUndo => _stack.Count > 0;

    public void Push(Action undo, string description)
    {
        _stack.Push(new UndoAction(description, undo));
    }

    public bool TryPop(out UndoAction action)
    {
        if (_stack.Count == 0)
        {
            action = default!;
            return false;
        }

        action = _stack.Pop();
        return true;
    }
}

public sealed record UndoAction(string Description, Action Undo);
