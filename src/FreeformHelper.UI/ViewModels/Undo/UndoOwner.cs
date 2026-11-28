// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels.Undo;

/// <summary>Coordinates the shared history and suppression without owning a second stack.</summary>
internal sealed class UndoOwner
{
    private readonly Func<bool> _isLoading;
    private readonly Func<bool> _isSuppressed;
    private readonly Func<IDisposable> _enterSuppression;
    private readonly Action<Action, string> _push;
    private readonly Func<(bool Found, Action? Undo, string? Description)> _pop;
    private readonly Func<bool> _canUndo;
    private readonly Action _refreshCommand;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _propertyChanged;

    public UndoOwner(
        Func<bool> isLoading,
        Func<bool> isSuppressed,
        Func<IDisposable> enterSuppression,
        Action<Action, string> push,
        Func<(bool Found, Action? Undo, string? Description)> pop,
        Func<bool> canUndo,
        Action refreshCommand,
        Action<string> setStatus,
        Action<string> propertyChanged)
    {
        _isLoading = isLoading;
        _isSuppressed = isSuppressed;
        _enterSuppression = enterSuppression;
        _push = push;
        _pop = pop;
        _canUndo = canUndo;
        _refreshCommand = refreshCommand;
        _setStatus = setStatus;
        _propertyChanged = propertyChanged;
    }

    public bool CanUndo => _canUndo();

    public void PushUndo(Action undo, string description)
    {
        if (_isSuppressed() || _isLoading())
        {
            return;
        }

        _push(undo, description);
        _refreshCommand();
        _propertyChanged(nameof(CanUndo));
    }

    public void TrackUndo<T>(T oldValue, T newValue, Action<T> assign, string description)
    {
        if (_isSuppressed() || _isLoading())
        {
            return;
        }

        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        PushUndo(() => assign(oldValue), description);
    }

    public void Undo()
    {
        var entry = _pop();
        if (!entry.Found)
        {
            return;
        }

        using (_enterSuppression())
        {
            entry.Undo!();
        }

        _setStatus($"Undo: {entry.Description}");
        _refreshCommand();
        _propertyChanged(nameof(CanUndo));
    }
}
