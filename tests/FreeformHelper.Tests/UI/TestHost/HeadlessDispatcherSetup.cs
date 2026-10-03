using Avalonia.Threading;
using FreeformHelper.UI.Services;

namespace FreeformHelper.Tests;

/// <summary>
/// Stops a headless test at once when it would run on a dispatcher that cannot run a loop.
/// </summary>
/// <remarks>
/// <c>Dispatcher.UIThread</c> is created on first use, by whichever thread asks first. The headless session
/// clears it at the start of every test and only then registers the platform. Another thread that asks in
/// between (background work an earlier test left running) gets a dispatcher without a platform, and the
/// media context and compositor created during setup keep it. Such a test cannot work: its first await
/// fails in <c>PushFrame</c> with <see cref="PlatformNotSupportedException"/> and rendering never produces
/// a frame. Nothing here can undo that, so the setup fails with the reason instead; the session discards
/// the half-built application and the following tests start clean.
/// </remarks>
internal static class HeadlessDispatcherSetup
{
    /// <summary>Runs right after the platform is registered, before the application is created.</summary>
    public static void EnsureRunLoopDispatcher()
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.SupportsRunLoops)
        {
            UiThread.RegisterRunningDispatcher(dispatcher);
            return;
        }

        throw new InvalidOperationException(
            "Another thread created Dispatcher.UIThread while the headless application was being set up, " +
            "so this test got a dispatcher without a platform. The usual cause is background work that an " +
            "earlier test left running and that uses Dispatcher.UIThread.");
    }
}
