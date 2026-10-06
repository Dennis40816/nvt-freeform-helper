using Avalonia.Threading;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void QueueDeferredSelectionNotchPreviewRefresh()
    {
        CancelDeferredSelectionNotchPreviewRefresh();
        var cts = new CancellationTokenSource();
        _selectionNotchPreviewCts = cts;
        var requestId = Interlocked.Increment(ref _selectionNotchPreviewRequestId);
        _ = RunDeferredSelectionNotchPreviewRefreshAsync(requestId, cts, cts.Token);
    }

    private void CancelDeferredSelectionNotchPreviewRefresh()
    {
        var cts = Interlocked.Exchange(ref _selectionNotchPreviewCts, null);
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task RunDeferredSelectionNotchPreviewRefreshAsync(
        long requestId,
        CancellationTokenSource cts,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SelectionNotchPreviewDeferredDelayMs, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
            {
                return;
            }

            await dispatcher!.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (requestId != Interlocked.Read(ref _selectionNotchPreviewRequestId))
                {
                    return;
                }

                if (_grid is null || _selectedCadIds.Count == 0)
                {
                    return;
                }

                RefreshNotchCanvasPreview(showStatus: false);
            }, DispatcherPriority.Background, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Selection changed before deferred preview started.
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _selectionNotchPreviewCts, null, cts), cts))
            {
                cts.Dispose();
            }
        }
    }
}
