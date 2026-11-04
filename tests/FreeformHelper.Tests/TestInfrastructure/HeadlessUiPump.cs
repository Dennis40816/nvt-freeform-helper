// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Threading;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class HeadlessUiPump
{
    // A failure guard for a layout that keeps invalidating itself, not a number of passes to wait.
    private const int MaxLayoutCycles = 100;

    /// <summary>Gets the dispatcher of the running headless application.</summary>
    public static Dispatcher RunningDispatcher =>
        global::Avalonia.Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("No Avalonia application is current, so there is no UI dispatcher to use.");

    /// <summary>Returns after the dispatcher has run the jobs queued at the default priority.</summary>
    public static Task YieldAsync()
    {
        return YieldAsync(DispatcherPriority.Default);
    }

    /// <summary>Returns after the dispatcher has run the jobs queued at or above the given priority.</summary>
    public static Task YieldAsync(DispatcherPriority priority)
    {
        return RunningDispatcher.InvokeAsync(static () => { }, priority).GetTask();
    }

    /// <summary>Lets the background, loaded and render work of the running application finish.</summary>
    public static async Task FlushQueueAsync()
    {
        await YieldAsync(DispatcherPriority.Background);
        await YieldAsync(DispatcherPriority.Loaded);
        await YieldAsync(DispatcherPriority.Render);
        await YieldAsync(DispatcherPriority.Background);
    }

    public static void Drain(TopLevel root)
    {
        var dispatcher = root.Dispatcher;
        dispatcher.VerifyAccess();

        for (var cycle = 0; cycle < MaxLayoutCycles; cycle++)
        {
            dispatcher.RunJobs();
            root.UpdateLayout();

            // The headless dispatcher finishes a layout chain inside RunJobs, so one cycle is the normal case
            // (a pump reduced to one RunJobs and one UpdateLayout gives the same result today). The loop is a
            // safety net for work that a layout callback enqueues during UpdateLayout.
            if (!dispatcher.HasJobsWithPriority(DispatcherPriority.SystemIdle) &&
                root.IsMeasureValid && root.IsArrangeValid)
            {
                return;
            }
        }

        throw new InvalidOperationException("Headless dispatcher and layout did not reach an idle state.");
    }
}
