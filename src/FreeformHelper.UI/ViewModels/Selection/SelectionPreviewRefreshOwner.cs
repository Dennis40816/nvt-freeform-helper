// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels.Selection;

/// <summary>Owns the deferred selection preview gate while the host adapts timing and dispatch.</summary>
internal sealed class SelectionPreviewRefreshOwner
{
    private readonly LatestRequestGate _gate;
    private readonly Func<CancellationToken, Task> _delay;
    private readonly Func<Action, CancellationToken, Task> _dispatch;
    private readonly Func<bool> _canRefresh;
    private readonly Action _refresh;

    public SelectionPreviewRefreshOwner(
        LatestRequestGate gate,
        Func<CancellationToken, Task> delay,
        Func<Action, CancellationToken, Task> dispatch,
        Func<bool> canRefresh,
        Action refresh)
    {
        _gate = gate;
        _delay = delay;
        _dispatch = dispatch;
        _canRefresh = canRefresh;
        _refresh = refresh;
    }

    public void Queue()
    {
        Cancel();
        var (requestId, cts) = _gate.Begin();
        _ = RunAsync(requestId, cts, cts.Token);
    }

    public void Cancel() => _gate.Cancel();

    private async Task RunAsync(long requestId, CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        try
        {
            await _delay(cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await _dispatch(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (requestId != _gate.LatestRequestId)
                {
                    return;
                }

                if (!_canRefresh())
                {
                    return;
                }

                _refresh();
            }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Selection changed before deferred preview started.
        }
        finally
        {
            _gate.Release(cts);
        }
    }
}
