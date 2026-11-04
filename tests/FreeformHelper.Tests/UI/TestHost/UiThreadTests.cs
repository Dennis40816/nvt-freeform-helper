using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using Nvt.Core.Avalonia.Threading;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiThreadTests
{
    [AvaloniaFact]
    public void TryGetRunningDispatcher_WhenGlobalSlotIsEmpty_UsesRegisteredDispatcher()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var original = Assert.IsType<Dispatcher>(DispatcherPrivateAccess.UiThreadSlot(null));
        Assert.True(UiThread.TryGetRunningDispatcher(out var registered));
        Assert.Same(original, registered);

        DispatcherPrivateAccess.UiThreadSlot(null) = null;
        try
        {
            Assert.True(UiThread.TryGetRunningDispatcher(out var actual));
            Assert.Same(original, actual);
            Assert.Null(DispatcherPrivateAccess.UiThreadSlot(null));
        }
        finally
        {
            DispatcherPrivateAccess.UiThreadSlot(null) = original;
        }
    }
}
