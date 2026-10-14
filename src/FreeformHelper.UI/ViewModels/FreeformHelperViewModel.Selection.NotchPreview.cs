using Avalonia.Threading;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void QueueDeferredSelectionNotchPreviewRefresh()
    {
        CancelDeferredSelectionNotchPreviewRefresh();
        var (requestId, cts) = _selectionNotchPreviewGate.Begin();
        _ = RunDeferredSelectionNotchPreviewRefreshAsync(requestId, cts, cts.Token);
    }

    private void CancelDeferredSelectionNotchPreviewRefresh()
    {
        _selectionNotchPreviewGate.Cancel();
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

                if (requestId != _selectionNotchPreviewGate.LatestRequestId)
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
            _selectionNotchPreviewGate.Release(cts);
        }
    }
}
