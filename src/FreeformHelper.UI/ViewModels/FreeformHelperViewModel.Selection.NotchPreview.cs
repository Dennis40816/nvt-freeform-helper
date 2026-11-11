using Avalonia.Threading;
using FreeformHelper.UI.ViewModels.Selection;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private SelectionPreviewRefreshOwner CreateSelectionPreviewRefreshOwner() => new(
        new LatestRequestGate(),
        cancellationToken => _notchPreviewTiming.DelayAsync(
            TimeSpan.FromMilliseconds(NotchPreviewTiming.SelectionDeferredDelayMs), cancellationToken),
        DispatchSelectionPreviewAsync,
        () => _grid is not null && _selectedCadIds.Count > 0,
        () => RefreshNotchCanvasPreview(showStatus: false));

    private static async Task DispatchSelectionPreviewAsync(Action refresh, CancellationToken cancellationToken)
    {
        if (!UiThread.TryGetRunningDispatcher(out var dispatcher))
        {
            return;
        }

        await dispatcher!.InvokeAsync(refresh, DispatcherPriority.Background, cancellationToken);
    }
}
