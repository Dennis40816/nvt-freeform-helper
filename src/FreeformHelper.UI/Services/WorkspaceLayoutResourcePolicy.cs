// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Resolves workspace layout resources and applies console row sizing.
/// </summary>
internal static class WorkspaceLayoutResourcePolicy
{
    public static void ApplyConsoleRowHeight(Control resourceOwner, RowDefinition? row, bool isExpanded)
    {
        if (row is null)
        {
            return;
        }

        if (isExpanded)
        {
            var heightResource = resourceOwner.FindResource("ConsolePanelRowHeight");
            if (heightResource is GridLength gridLength)
            {
                row.Height = gridLength;
            }
            else if (heightResource is double heightDouble)
            {
                row.Height = new GridLength(heightDouble);
            }

            var minResource = resourceOwner.FindResource("ConsolePanelMinHeight");
            if (minResource is double minDouble)
            {
                row.MinHeight = minDouble;
            }
        }
        else
        {
            row.Height = GridLength.Auto;
            var collapsedMin = GetResourceDouble(resourceOwner, "ConsoleCollapsedMinHeight");
            row.MinHeight = collapsedMin > 0 ? collapsedMin : 0;
        }
    }

    public static double GetResourceDouble(Control resourceOwner, string key, double fallback = 0.0)
    {
        if (resourceOwner.TryFindResource(key, out var value))
        {
            switch (value)
            {
                case double doubleValue:
                    return doubleValue;
                case float floatValue:
                    return floatValue;
                case int intValue:
                    return intValue;
            }
        }

        return fallback;
    }
}
