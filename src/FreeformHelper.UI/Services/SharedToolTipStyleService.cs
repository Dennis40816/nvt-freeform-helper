using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Nvt.Core.Avalonia.Theme;

namespace FreeformHelper.UI.Services;

internal static class SharedToolTipStyleService
{
    private static IDisposable? registration;

    public static void Register()
    {
        registration ??= ToolTip.TipProperty.Changed.AddClassHandler<Control, object?>(NormalizeStringToolTip);
    }

    private static void NormalizeStringToolTip(Control target, AvaloniaPropertyChangedEventArgs<object?> change)
    {
        if (change.NewValue.Value is not string text || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        target.SetCurrentValue(ToolTip.TipProperty, CreateStringToolTipText(target, text));
    }

    private static TextBlock CreateStringToolTipText(Control target, string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = UiResourceResolver.GetBrush(
                target,
                "BrushTooltipForeground",
                Brushes.Black,
                static color => new SolidColorBrush(color)),
        };

        textBlock.AttachedToVisualTree += (_, _) =>
            textBlock.Foreground = UiResourceResolver.GetBrush(
                textBlock,
                "BrushTooltipForeground",
                Brushes.Black,
                static color => new SolidColorBrush(color));

        return textBlock;
    }
}
