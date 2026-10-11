// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Threading;

namespace FreeformHelper.Tests.TestInfrastructure;

internal static class HeadlessUiPump
{
    // A failure guard for a layout that keeps invalidating itself, not a number of passes to wait.
    private const int MaxLayoutCycles = 100;

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
