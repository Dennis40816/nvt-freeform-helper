namespace FreeformHelper.UI.Services;

/// <summary>
/// Single entry point for grid rebuild requests.
/// Manages pending rebuilds and fit-to-content requests.
/// </summary>
public sealed class GridRebuildUseCase
{
    private readonly Action _requestFit;
    private readonly Func<Task> _rebuildAsync;
    private readonly Lock _sync = new();
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
                var runTask = RunQueuedRequestsAsync();

                // A run that already ended (it failed before its first await) has reset the flag, so it is not the active run.
                _activeRunTask = _isRebuildInProgress ? runTask : Task.CompletedTask;
                return runTask;
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
        var drained = false;
        try
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
                        drained = true;
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
        finally
        {
            if (!drained)
            {
                // The fit action or the rebuild threw. Free the use case so that the next request starts a new run
                // instead of getting this faulted task back. A request that arrived during the failed run stays pending.
                lock (_sync)
                {
                    _isRebuildInProgress = false;
                    _activeRunTask = Task.CompletedTask;
                }
            }
        }
    }
}
