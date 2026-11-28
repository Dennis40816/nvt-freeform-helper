// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

internal static class NotchValueText
{
    internal static string FormatPercent(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    internal static string FormatAreaText(double area)
    {
        if (double.IsNaN(area) || double.IsInfinity(area))
        {
            return "-";
        }

        return area.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
