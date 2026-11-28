// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Gates current Notch generation updates for one progress reporter.</summary>
internal sealed class NotchProgressGate
{
    private readonly TimeProvider _timeProvider;
    private readonly Func<bool> _isCurrent;
    private readonly int _minUpdateIntervalMs;
    // UI thread only, through the owning Progress<T> reporter's captured context.
    private long _lastTick;

    internal NotchProgressGate(TimeProvider timeProvider, Func<bool> isCurrent, int minUpdateIntervalMs)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(isCurrent);
        _timeProvider = timeProvider;
        _isCurrent = isCurrent;
        _minUpdateIntervalMs = minUpdateIntervalMs;
    }

    internal bool ShouldPublish(NotchGenerationProgress progress)
    {
        if (!_isCurrent())
        {
            return false;
        }

        var total = Math.Max(progress.TotalCount, 1);
        var processed = Math.Clamp(progress.ProcessedCount, 0, total);
        var isFinal = processed >= total;
        var now = _timeProvider.GetTimestamp();
        if (!isFinal && _timeProvider.GetElapsedTime(_lastTick).TotalMilliseconds < _minUpdateIntervalMs)
        {
            return false;
        }

        _lastTick = now;
        return true;
    }
}
