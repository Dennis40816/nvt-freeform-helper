using Avalonia;
using Avalonia.Controls;

namespace FreeformHelper.UI.Controls;

/// <summary>
/// Minimal FontIcon replacement that maps a glyph to the <see cref="TextBlock.Text"/> property.
/// </summary>
public sealed class FontIcon : TextBlock
{
    /// <summary>
    /// Defines the glyph to draw.
    /// </summary>
    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<FontIcon, string?>(nameof(Glyph));

    /// <summary>
    /// Gets or sets the glyph text rendered by this icon.
    /// </summary>
    public string? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    static FontIcon()
    {
        GlyphProperty.Changed.AddClassHandler<FontIcon>((control, args) =>
        {
            control.Text = args.NewValue as string ?? string.Empty;
        });
    }
}
