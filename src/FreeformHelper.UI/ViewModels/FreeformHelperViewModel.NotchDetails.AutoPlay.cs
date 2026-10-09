using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private CancellationTokenSource? _notchPreviewAutoPlayCts;

    private void RestartNotchPreviewAutoPlayLoop()
    {
        StopNotchPreviewAutoPlayLoop();
        StartNotchPreviewAutoPlayLoop();
    }

    private void StartNotchPreviewAutoPlayLoop()
    {
        if (!NotchPreviewAutoPlayEnabled || !IsNotchCanvasPreviewVisible || _notchPreviewAutoPlayCts is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        _notchPreviewAutoPlayCts = cts;
        _ = RunNotchPreviewAutoPlayLoopAsync(cts, cts.Token);
    }

    private void AdvanceNotchPreviewStageOnce()
    {
        if (!NotchPreviewAutoPlayEnabled || !IsNotchCanvasPreviewVisible)
        {
            return;
        }

        var current = Math.Clamp((int)Math.Round((double)NotchPreviewVisualizationStep), 1, 3);
        var next = current >= 3 ? 1 : current + 1;
        using (_undoSuppression.Enter())
        {
            NotchPreviewVisualizationStep = next;
        }
    }

    private void ShiftNotchPreviewStage(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var current = Math.Clamp((int)Math.Round((double)NotchPreviewVisualizationStep), 1, 3);
        var next = current + delta;
        while (next < 1)
        {
            next += 3;
        }

        while (next > 3)
        {
            next -= 3;
        }

        using (_undoSuppression.Enter())
        {
            NotchPreviewVisualizationStep = next;
        }
    }

    private void StopNotchPreviewAutoPlayLoop()
    {
        var cts = Interlocked.Exchange(ref _notchPreviewAutoPlayCts, null);
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

    private async Task RunNotchPreviewAutoPlayLoopAsync(CancellationTokenSource cts, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var delayMs = (int)Math.Round(Math.Clamp(
                    (double)NotchPreviewAutoPlayIntervalMs,
                    (double)NotchPreviewAutoPlayIntervalMinMs,
                    (double)NotchPreviewAutoPlayIntervalMaxMs));
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
                {
                    break;
                }

                await dispatcher!.InvokeAsync(() =>
                {
                    if (!NotchPreviewAutoPlayEnabled || !IsNotchCanvasPreviewVisible)
                    {
                        return;
                    }

                    AdvanceNotchPreviewStageOnce();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Loop canceled.
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Notch preview auto-play loop crashed.");
        }
        finally
        {
            if (ReferenceEquals(Interlocked.CompareExchange(ref _notchPreviewAutoPlayCts, null, cts), cts))
            {
                cts.Dispose();
            }
        }
    }
}
