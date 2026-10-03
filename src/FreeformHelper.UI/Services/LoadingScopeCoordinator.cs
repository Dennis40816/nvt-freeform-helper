namespace FreeformHelper.UI.Services;

/// <summary>
/// Coordinates loading visibility scopes with an explicit lifecycle contract:
/// show on first enter, keep visible for at least a minimum duration, and hide
/// only after an optional UI frame-yield boundary.
/// </summary>
internal sealed class LoadingScopeCoordinator
{
    private readonly Func<Task> _yieldFrameAsync;
    private readonly TimeSpan _minimumVisibleDuration;
    private readonly object _sync = new();
    private int _scopeCount;
    private DateTimeOffset _visibleSinceUtc;

    public LoadingScopeCoordinator(
        Func<Task> yieldFrameAsync,
        TimeSpan minimumVisibleDuration)
    {
        _yieldFrameAsync = yieldFrameAsync ?? throw new ArgumentNullException(nameof(yieldFrameAsync));
        _minimumVisibleDuration = minimumVisibleDuration < TimeSpan.Zero
            ? TimeSpan.Zero
            : minimumVisibleDuration;
    }

    public async Task RunAsync(Func<Task> action, Action<bool> setVisible)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(setVisible);

        Begin(setVisible);
        try
        {
            await _yieldFrameAsync();
            await action();
        }
        finally
        {
            await EndAsync(setVisible);
        }
    }

    public void Begin(Action<bool> setVisible)
    {
        ArgumentNullException.ThrowIfNull(setVisible);

        var shouldShow = false;
        lock (_sync)
        {
            _scopeCount++;
            if (_scopeCount == 1)
            {
                _visibleSinceUtc = DateTimeOffset.UtcNow;
                shouldShow = true;
            }
        }

        if (shouldShow)
        {
            setVisible(true);
        }
    }

    public async Task EndAsync(Action<bool> setVisible)
    {
        ArgumentNullException.ThrowIfNull(setVisible);

        DateTimeOffset visibleSinceUtc = default;
        var shouldHide = false;
        lock (_sync)
        {
            if (_scopeCount <= 0)
            {
                return;
            }

            _scopeCount--;
            if (_scopeCount == 0)
            {
                visibleSinceUtc = _visibleSinceUtc;
                shouldHide = true;
            }
        }

        if (!shouldHide)
        {
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - visibleSinceUtc;
        var remaining = _minimumVisibleDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        await _yieldFrameAsync();

        lock (_sync)
        {
            if (_scopeCount > 0)
            {
                return;
            }
        }

        setVisible(false);
        await _yieldFrameAsync();
    }
}
