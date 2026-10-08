using FreeformHelper.UI.Services;
using Nvt.Core.Progress;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private static readonly TimeSpan ModalLoadingSpinnerMinVisibleDuration = TimeSpan.FromMilliseconds(120);
    private readonly LoadingScopeCoordinator _modalLoadingSpinnerScope =
        new(YieldCadLoadCanvasOverlayFrameAsync, ModalLoadingSpinnerMinVisibleDuration);
    private int _modalLoadingSpinnerScopeCount;

    internal async Task RunWithModalLoadingSpinnerAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _modalLoadingSpinnerScope.RunAsync(action, SetModalLoadingSpinnerVisibleAsyncContract);
    }

    internal async Task<TResult> RunWithModalLoadingSpinnerAsync<TResult>(Func<Task<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        TResult? result = default;
        await _modalLoadingSpinnerScope.RunAsync(async () =>
        {
            result = await action();
        }, SetModalLoadingSpinnerVisibleAsyncContract);
        return result!;
    }

    internal void BeginModalLoadingSpinnerScope()
    {
        _modalLoadingSpinnerScopeCount++;
        IsModalLoadingSpinnerVisible = true;
    }

    internal void EndModalLoadingSpinnerScope()
    {
        if (_modalLoadingSpinnerScopeCount > 0)
        {
            _modalLoadingSpinnerScopeCount--;
        }

        IsModalLoadingSpinnerVisible = _modalLoadingSpinnerScopeCount > 0;
    }

    private void SetModalLoadingSpinnerVisibleAsyncContract(bool isVisible)
    {
        CadLoadSpinnerDebugState.RecordVmOverlayVisibility(isVisible, "modal-loading");
        IsModalLoadingSpinnerVisible = isVisible;
    }
}
