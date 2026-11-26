// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Media;
using Nvt.Core.Avalonia.Theme;
using Nvt.Core.Avalonia.Threading;

namespace FreeformHelper.UI.Services;

internal static class ThemeBrushResolver
{
    public static IBrush GetBrush(
        Control owner,
        string key,
        IBrush fallback,
        Func<Color, ISolidColorBrush> brushFactory)
    {
        // Core uses Default for detached controls. Legacy application brushes followed the app theme.
        // Keep that behavior as a fallback, with owner resources still taking precedence in Core.
        if (owner.ActualThemeVariant is null &&
            UiThread.IsCurrent(out _, out var app) &&
            app is not null &&
            app.TryFindResource(key, app.ActualThemeVariant, out var resource))
        {
            fallback = resource switch
            {
                IBrush brush => brush,
                Color color => brushFactory(color),
                _ => fallback,
            };
        }

        return UiResourceResolver.GetBrush(owner, key, fallback, brushFactory);
    }
}
