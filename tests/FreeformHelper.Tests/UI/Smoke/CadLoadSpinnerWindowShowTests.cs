using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Views;
using Nvt.Core.Avalonia.Threading;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins that the spinner window stays visible after a Show. It once hid itself again 3 ms after every Show,
/// because the hide meant for the warm-up open ran on each open.
/// </summary>
public sealed class CadLoadSpinnerWindowShowTests
{
    private static CadLoadSpinnerRequest ShowRequest => new(
        CadLoadSpinnerIpcProtocol.ShowCommand,
        OwnerLeft: 0,
        OwnerTop: 0,
        OwnerWidth: 800,
        OwnerHeight: 600);

    private static CadLoadSpinnerRequest HideRequest => new(CadLoadSpinnerIpcProtocol.HideCommand);

    [AvaloniaFact]
    public async Task Show_AfterTheWarmupOpen_StaysVisibleUntilHide()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var options = new CadLoadSpinnerWindowOptions(0, "nfh-spinner-test-" + Guid.NewGuid().ToString("N"));
        using var window = new CadLoadSpinnerWindow(options);

        try
        {
            window.Show();
            await FlushUiQueueAsync();
            Assert.False(window.IsVisible, "The warm-up open must end hidden.");

            window.ApplyRequest(ShowRequest);
            await FlushUiQueueAsync();
            Assert.True(window.IsVisible, "The first Show must stay visible.");

            window.ApplyRequest(HideRequest);
            Assert.False(window.IsVisible);

            window.ApplyRequest(ShowRequest);
            await FlushUiQueueAsync();
            Assert.True(window.IsVisible, "A later Show must stay visible too.");
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task FlushUiQueueAsync()
    {
        Assert.True(UiThread.TryGetRunningDispatcher(out var dispatcher), "No UI dispatcher is registered.");
        await dispatcher!.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Background);
    }
}
