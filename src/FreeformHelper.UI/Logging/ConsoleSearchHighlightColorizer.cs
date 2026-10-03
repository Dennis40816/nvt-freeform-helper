using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Applies search highlight style to console text ranges.
/// </summary>
public sealed class ConsoleSearchHighlightColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<ConsoleTextRange> _ranges = Array.Empty<ConsoleTextRange>();

    public IBrush HighlightBackgroundBrush { get; set; } = Brushes.DarkSlateBlue;

    public IBrush HighlightForegroundBrush { get; set; } = Brushes.White;

    public void SetRanges(IReadOnlyList<ConsoleTextRange> ranges)
    {
        _ranges = ranges ?? Array.Empty<ConsoleTextRange>();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_ranges.Count == 0)
        {
            return;
        }

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;

        foreach (var range in _ranges)
        {
            if (range.EndOffset <= lineStart || range.StartOffset >= lineEnd)
            {
                continue;
            }

            var start = Math.Max(range.StartOffset, lineStart);
            var end = Math.Min(range.EndOffset, lineEnd);
            if (start >= end)
            {
                continue;
            }

            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(HighlightBackgroundBrush);
                element.TextRunProperties.SetForegroundBrush(HighlightForegroundBrush);
            });
        }
    }
}

