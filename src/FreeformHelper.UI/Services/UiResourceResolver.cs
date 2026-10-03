using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace FreeformHelper.UI.Services;

internal static class UiResourceResolver
{
    public static IBrush GetBrush(
        Control owner,
        string key,
        IBrush fallback,
        Func<Color, ISolidColorBrush> brushFactory)
    {
        if (TryResolve(owner, key, out var value))
        {
            if (value is IBrush brush)
            {
                return brush;
            }

            if (value is Color color)
            {
                return brushFactory(color);
            }
        }

        return fallback;
    }

    public static Color GetColor(Control owner, string key, Color fallback = default)
    {
        if (TryGetColor(owner, key, out var color))
        {
            return color;
        }

        return fallback;
    }

    public static bool TryGetColor(Control owner, string key, out Color color)
    {
        if (TryResolve(owner, key, out var value))
        {
            if (value is Color resolvedColor)
            {
                color = resolvedColor;
                return true;
            }

            if (value is SolidColorBrush brush)
            {
                color = brush.Color;
                return true;
            }
        }

        color = default;
        return false;
    }

    public static double GetDouble(Control owner, string key, double fallback = 0.0)
    {
        if (TryResolve(owner, key, out var value))
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

    public static CornerRadius GetCornerRadius(Control owner, string key, CornerRadius fallback)
    {
        return TryResolve(owner, key, out var value) && value is CornerRadius cornerRadius
            ? cornerRadius
            : fallback;
    }

    public static Thickness GetThickness(Control owner, string key, Thickness fallback)
    {
        return TryResolve(owner, key, out var value) && value is Thickness thickness
            ? thickness
            : fallback;
    }

    private static bool TryResolve(Control owner, string key, out object? value)
    {
        var theme = owner.ActualThemeVariant ?? ThemeVariant.Default;
        if (owner.TryFindResource(key, theme, out value))
        {
            return true;
        }

        if (UiThread.IsCurrent(out _, out var app) && app!.TryFindResource(key, theme, out value))
        {
            return true;
        }

        value = null;
        return false;
    }
}
