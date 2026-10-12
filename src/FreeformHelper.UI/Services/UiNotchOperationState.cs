// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Owns the notch operation snapshot on the UI thread and its Interlocked busy counter.</summary>
internal sealed class UiNotchOperationState
{
    internal readonly record struct Snapshot(bool IsBusy, double Progress, string Text);

    // The UI thread owns snapshot reads, replacements and change callbacks.
    internal Snapshot Current { get; private set; } = new(false, 0.0, "Idle.");

    // Interlocked protects the counter; snapshot writes still go through the UI-thread forward.
    private int _busyScopeCount;

    internal event Action? Changed;

    internal void SetBusy(bool isBusy) => SetCurrent(Current with { IsBusy = isBusy });

    internal void SetProgress(double progress) => SetCurrent(Current with { Progress = progress });

    internal void SetText(string text) => SetCurrent(Current with { Text = text });

    internal void SetCurrent(Snapshot value)
    {
        if (Current == value)
        {
            return;
        }

        Current = value;
        Changed?.Invoke();
    }

    // The callback preserves PropertyChanging before replacement through the existing VM forward.
    internal void BeginBusyScope(Action<bool> setBusy)
    {
        Interlocked.Increment(ref _busyScopeCount);
        setBusy(true);
    }

    internal void EndBusyScope(Action<bool> setBusy)
    {
        setBusy(Interlocked.Decrement(ref _busyScopeCount) > 0);
    }
}
