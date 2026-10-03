using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Applies link color and hover underline style to parsed console link spans.
/// </summary>
public sealed class ConsoleLinkColorizer : DocumentColorizingTransformer
{
    private IReadOnlyList<ConsoleLinkSpan> _links = Array.Empty<ConsoleLinkSpan>();
    private ConsoleLinkSpan? _hoveredLink;

    public IBrush LinkBrush { get; set; } = Brushes.DeepSkyBlue;

    public TextDecorationCollection HoverDecorations { get; set; } = TextDecorations.Underline;

    public void SetLinks(IReadOnlyList<ConsoleLinkSpan> links)
    {
        _links = links ?? Array.Empty<ConsoleLinkSpan>();
    }

    public void SetHoveredLink(ConsoleLinkSpan? link)
    {
        _hoveredLink = link;
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (_links.Count == 0)
        {
            return;
        }

        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;

        foreach (var link in _links)
        {
            if (link.EndOffset <= lineStart || link.StartOffset >= lineEnd)
            {
                continue;
            }

            var start = Math.Max(link.StartOffset, lineStart);
            var end = Math.Min(link.EndOffset, lineEnd);
            if (start >= end)
            {
                continue;
            }

            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetForegroundBrush(LinkBrush);
                if (IsHovered(link))
                {
                    element.TextRunProperties.SetTextDecorations(HoverDecorations);
                }
            });
        }
    }

    private bool IsHovered(ConsoleLinkSpan link)
    {
        if (_hoveredLink is null)
        {
            return false;
        }

        return _hoveredLink.StartOffset == link.StartOffset &&
               _hoveredLink.Length == link.Length;
    }
}

