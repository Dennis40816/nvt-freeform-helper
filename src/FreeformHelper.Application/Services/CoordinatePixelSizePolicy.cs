// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.Application.Services;

/// <summary>
/// Normalizes the coordinate pixel size that the live settings and the Settings window draft both accept.
/// </summary>
public static class CoordinatePixelSizePolicy
{
    /// <summary>
    /// Rounds the value away from zero and keeps it at 1 or more.
    /// </summary>
    /// <param name="value">The size that the user entered.</param>
    /// <returns>The size to store.</returns>
    public static decimal Normalize(decimal value)
    {
        return Math.Max(1m, Math.Round(value, MidpointRounding.AwayFromZero));
    }
}
