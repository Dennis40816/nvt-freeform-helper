using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadLoadOverlayFrameYieldPolicyTests
{
    // A dispatcher without a platform, or one that is shutting down, reports every thread as its own.
    // Waiting for a render frame on it would never end, so thread access alone must not be enough.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void IsUiThreadThatRunsALoop_NeedsBothThreadAccessAndARunLoop(
        bool hasThreadAccess,
        bool dispatcherRunsLoops,
        bool expected)
    {
        Assert.Equal(
            expected,
            UiThread.IsUiThreadThatRunsALoop(hasThreadAccess, dispatcherRunsLoops));
    }
}
