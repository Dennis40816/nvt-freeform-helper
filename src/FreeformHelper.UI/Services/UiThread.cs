using Avalonia.Threading;

namespace FreeformHelper.UI.Services;

internal static class UiThread
{
    private static Dispatcher? s_runningDispatcher;

    internal static void RegisterRunningDispatcher(Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (dispatcher.SupportsRunLoops)
        {
            Volatile.Write(ref s_runningDispatcher, dispatcher);
        }
    }

    internal static bool TryGetRunningDispatcher(out Dispatcher? dispatcher)
    {
        dispatcher = null;
        if (global::Avalonia.Application.Current is null)
        {
            return false;
        }

        var currentDispatcher = Volatile.Read(ref s_runningDispatcher);
        if (currentDispatcher is null || !currentDispatcher.SupportsRunLoops)
        {
            return false;
        }

        dispatcher = currentDispatcher;
        return true;
    }

    internal static bool IsCurrent(out Dispatcher? dispatcher, out global::Avalonia.Application? application)
    {
        application = null;
        if (!TryGetRunningDispatcher(out dispatcher) || !dispatcher!.CheckAccess())
        {
            return false;
        }

        application = global::Avalonia.Application.Current;
        if (application is null)
        {
            dispatcher = null;
            return false;
        }

        return true;
    }

    internal static bool IsUiThreadThatRunsALoop(bool hasThreadAccess, bool dispatcherRunsLoops)
    {
        return hasThreadAccess && dispatcherRunsLoops;
    }
}
