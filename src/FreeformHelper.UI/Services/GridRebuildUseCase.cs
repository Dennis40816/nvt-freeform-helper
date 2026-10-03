namespace FreeformHelper.UI.Services;

/// <summary>
/// Single entry point for grid rebuild requests.
/// Manages pending rebuilds and fit-to-content requests.
/// </summary>
public sealed class GridRebuildUseCase
{
    private readonly Action _requestFit;
    private readonly Func<Task> _rebuildAsync;
    private readonly object _sync = new();
    private bool _isRebuildInProgress;
    private bool _pendingRebuild;
    private bool _pendingRequestFit;
    private Task _activeRunTask = Task.CompletedTask;

    public GridRebuildUseCase(Action requestFit, Func<Task> rebuildAsync)
    {
        _requestFit = requestFit ?? throw new ArgumentNullException(nameof(requestFit));
        _rebuildAsync = rebuildAsync ?? throw new ArgumentNullException(nameof(rebuildAsync));
    }

    public Task RequestAsync(bool requestFit)
    {
        lock (_sync)
        {
            _pendingRebuild = true;
            _pendingRequestFit |= requestFit;

            if (!_isRebuildInProgress)
            {
                _isRebuildInProgress = true;
                _activeRunTask = RunQueuedRequestsAsync();
            }

            return _activeRunTask;
        }
    }

    internal Task WaitForIdleAsync()
    {
        lock (_sync)
        {
            return _activeRunTask;
        }
    }

    private async Task RunQueuedRequestsAsync()
    {
        while (true)
        {
            bool requestFit;
            lock (_sync)
            {
                if (!_pendingRebuild)
                {
                    _isRebuildInProgress = false;
                    _activeRunTask = Task.CompletedTask;
                    return;
                }

                requestFit = _pendingRequestFit;
                _pendingRebuild = false;
                _pendingRequestFit = false;
            }

            if (requestFit)
            {
                _requestFit();
            }

            await _rebuildAsync();
        }
    }
}
