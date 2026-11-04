using Avalonia.Threading;
using FreeformHelper.UI.Services;
using Nvt.Core.Avalonia.Threading;
using Nvt.Core.Progress;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static readonly TimeSpan _cadLoadOverlayMinVisibleDuration = TimeSpan.FromMilliseconds(120);
    private readonly LoadingScopeCoordinator _cadLoadOverlayScope =
        new(YieldCadLoadCanvasOverlayFrameAsync, _cadLoadOverlayMinVisibleDuration);

    internal async Task RunWithCadLoadCanvasOverlayAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _cadLoadOverlayScope.RunAsync(action, SetCadLoadOverlayVisibleAsyncContract);
    }

    internal async Task<TResult> RunWithCadLoadCanvasOverlayAsync<TResult>(Func<Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        TResult? result = default;

        await _cadLoadOverlayScope.RunAsync(async () =>
        {
            result = await action();
        }, SetCadLoadOverlayVisibleAsyncContract);

        return result!;
    }

    private void SetCadLoadOverlayVisibleAsyncContract(bool isVisible)
    {
        CadLoadSpinnerDebugState.RecordVmOverlayVisibility(isVisible, "set-visible-contract");
        IsCadLoadCanvasOverlayVisible = isVisible;
    }

    internal static async Task YieldCadLoadCanvasOverlayFrameAsync()
    {
        // Yielding a frame only makes sense on the UI thread: it lets the overlay paint before this thread
        // continues. Off the UI thread nothing is blocked, and waiting for a dispatcher that is not being
        // pumped would never end.
        if (!UiThread.IsCurrent(out var dispatcher, out _))
        {
            await Task.Yield();
            return;
        }

        await dispatcher!.InvokeAsync(static () => { }, DispatcherPriority.Render);
    }
}
