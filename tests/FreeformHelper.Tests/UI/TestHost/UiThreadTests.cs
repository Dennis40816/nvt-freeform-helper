using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class UiThreadTests
{
    [AvaloniaFact]
    public void TryGetRunningDispatcher_WhenGlobalSlotIsEmpty_UsesRegisteredDispatcher()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dispatcherField = typeof(Dispatcher).GetField("s_uiThread", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(dispatcherField);
        var original = Assert.IsType<Dispatcher>(dispatcherField.GetValue(null));
        Assert.True(UiThread.TryGetRunningDispatcher(out var registered));
        Assert.Same(original, registered);

        dispatcherField.SetValue(null, null);
        try
        {
            Assert.True(UiThread.TryGetRunningDispatcher(out var actual));
            Assert.Same(original, actual);
            Assert.Null(dispatcherField.GetValue(null));
        }
        finally
        {
            dispatcherField.SetValue(null, original);
        }
    }
}
