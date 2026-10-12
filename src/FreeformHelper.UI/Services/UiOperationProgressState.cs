// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.Services;

/// <summary>Owns one operation's immutable progress snapshot; accessed only on the UI thread.</summary>
internal sealed class UiOperationProgressState
{
    internal readonly record struct Snapshot(bool IsBusy, double Progress);

    // The UI thread owns all reads, replacements and change callbacks.
    internal Snapshot Current { get; private set; }

    internal event Action? Changed;

    internal void SetBusy(bool isBusy) => SetCurrent(Current with { IsBusy = isBusy });

    internal void SetProgress(double progress) => SetCurrent(Current with { Progress = progress });

    internal void SetCurrent(Snapshot value)
    {
        if (Current == value)
        {
            return;
        }

        Current = value;
        Changed?.Invoke();
    }
}
