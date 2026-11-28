using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Pure rules for the four corner handles of the custom array.</summary>
internal static class CoordinatePlannerArrayHandleMath
{
    public static bool IsCornerKey(string key) =>
        key is "array-tl" or "array-tr" or "array-br" or "array-bl";

    public static decimal RoundMachineCoordinate(decimal value) =>
        decimal.Round(value, 3, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Maps a world point inside the active area bounds to a machine point. The result is clamped to the machine area
    /// and rounded to three decimals. World Y grows upward and machine Y grows downward.
    /// </summary>
    public static (decimal X, decimal Y) MachinePointFromWorld(
        Rect2 activeAreaBounds,
        decimal machineOriginX,
        decimal machineOriginY,
        decimal machineWidth,
        decimal machineHeight,
        Point2 worldPoint)
    {
        var normX = activeAreaBounds.Width <= 1e-9
            ? 0d
            : (worldPoint.X - activeAreaBounds.MinX) / activeAreaBounds.Width;
        var normY = activeAreaBounds.Height <= 1e-9
            ? 0d
            : (activeAreaBounds.MaxY - worldPoint.Y) / activeAreaBounds.Height;
        normX = Math.Clamp(normX, 0d, 1d);
        normY = Math.Clamp(normY, 0d, 1d);

        var width = Math.Max(0.001m, machineWidth);
        var height = Math.Max(0.001m, machineHeight);
        return (
            RoundMachineCoordinate(machineOriginX + (width * (decimal)normX)),
            RoundMachineCoordinate(machineOriginY + (height * (decimal)normY)));
    }
}
