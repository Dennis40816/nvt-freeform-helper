// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>
/// Decides when the initial canvas fit can run and whether it needs to wait for a grid build.
/// </summary>
public static class InitialCanvasFitPolicy
{
    /// <summary>Returns whether the expected window state and usable layout bounds are available.</summary>
    public static bool IsLayoutReady(bool expectedMaximized, bool currentMaximized, double width, double height)
    {
        if (expectedMaximized && !currentMaximized)
        {
            return false;
        }

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        return true;
    }

    /// <summary>Chooses the initial fit action from canvas availability and the displayed pad sources.</summary>
    public static InitialFitAction Decide(bool hasCanvasHost, bool hasCadPads, bool hasRegularPads)
    {
        if (!hasCanvasHost)
        {
            return InitialFitAction.None;
        }

        if (hasCadPads || hasRegularPads)
        {
            return InitialFitAction.FitNow;
        }

        return InitialFitAction.FitAfterNextBuild;
    }
}
