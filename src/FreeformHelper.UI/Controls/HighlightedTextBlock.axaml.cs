// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Controls;

/// <summary>Displays search matches without changing the text or rebuilding on theme changes.</summary>
public sealed class HighlightedTextBlock : Decorator
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<IReadOnlyList<LayerTextRange>> RangesProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, IReadOnlyList<LayerTextRange>>(
            nameof(Ranges), Array.Empty<LayerTextRange>());

    private readonly TextBlock _content = new() { TextTrimming = TextTrimming.CharacterEllipsis };

    public HighlightedTextBlock()
    {
        Child = _content;
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public IReadOnlyList<LayerTextRange> Ranges
    {
        get => GetValue(RangesProperty);
        set => SetValue(RangesProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == RangesProperty)
        {
            RebuildInlines();
        }
    }

    private void RebuildInlines()
    {
        var inlines = _content.Inlines ??= new InlineCollection();
        inlines.Clear();
        string text = Text ?? string.Empty;
        int offset = 0;
        foreach (var range in Ranges.OrderBy(static range => range.Start))
        {
            int start = Math.Clamp(range.Start, offset, text.Length);
            int end = (int)Math.Clamp((long)range.Start + range.Length, start, text.Length);
            if (start == end)
            {
                continue;
            }

            AddPlainText(inlines, text, offset, start);
            var run = new Run(text[start..end]) { FontWeight = FontWeight.SemiBold };
            run.Bind(TextElement.ForegroundProperty, new DynamicResourceExtension("NfcAccentStrongBrush"));
            inlines.Add(run);
            offset = end;
        }

        AddPlainText(inlines, text, offset, text.Length);
    }

    private static void AddPlainText(InlineCollection inlines, string text, int start, int end)
    {
        if (end > start)
        {
            inlines.Add(new Run(text[start..end]));
        }
    }
}
