// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

internal sealed class NotchPreviewTiming
{
    public const int SelectionDeferredDelayMs = 120;
    public const decimal AutoPlayIntervalMinMs = 200m;
    public const decimal AutoPlayIntervalMaxMs = 5000m;

    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    public static int NextAutoPlayStage(int current)
    {
        current = Math.Clamp(current, 1, 3);
        return current >= 3 ? 1 : current + 1;
    }

    public static int ShiftStage(int current, int delta)
    {
        var next = Math.Clamp(current, 1, 3) + delta;
        while (next < 1)
        {
            next += 3;
        }

        while (next > 3)
        {
            next -= 3;
        }

        return next;
    }

    public static int ClampAutoPlayIntervalMs(decimal interval)
    {
        return (int)Math.Round(Math.Clamp((double)interval, (double)AutoPlayIntervalMinMs, (double)AutoPlayIntervalMaxMs));
    }

    public async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, TimeProvider, cancellationToken).ConfigureAwait(false);
    }
}
