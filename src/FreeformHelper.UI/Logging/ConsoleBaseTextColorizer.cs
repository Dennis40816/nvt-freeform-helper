using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Applies a consistent base foreground to all console text runs.
/// This avoids theme/default-foreground mismatches in AvaloniaEdit.
/// </summary>
public sealed class ConsoleBaseTextColorizer : DocumentColorizingTransformer
{
    public IBrush TextBrush { get; set; } = Brushes.White;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (line.Length <= 0)
        {
            return;
        }

        var start = line.Offset;
        var end = line.EndOffset;
        if (start >= end)
        {
            return;
        }

        ChangeLinePart(start, end, element =>
        {
            element.TextRunProperties.SetForegroundBrush(TextBrush);
        });
    }
}
