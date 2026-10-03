using System.Text;
using Avalonia.Media;
using AvaloniaEdit;
using FreeformHelper.UI.Logging;

namespace FreeformHelper.UI.Views;

public sealed partial class FreeformHelperView
{
    internal int ConsoleLinkParseCount { get; private set; }

    private void RefreshConsoleLinks()
    {
        var text = _consoleEditor?.Text;
        if (text is null)
        {
            return;
        }

        ConsoleLinkParseCount++;
        _consoleLinks = ConsoleLinkParser.Parse(text);
        if (_consoleEditor is null)
        {
            return;
        }

        UpdateConsoleColorizers();
        _consoleSearchHighlightColorizer?.SetRanges(_consoleSearchRanges);
        if (_consoleLinkColorizer is null)
        {
            return;
        }

        _consoleLinkColorizer.LinkBrush = GetResourceBrush("BrushConsoleLink")
            ?? GetResourceBrush("BrushAccent")
            ?? Brushes.DeepSkyBlue;
        _consoleLinkColorizer.SetLinks(_consoleLinks);
        if (_consoleHoveredLink is not null)
        {
            var hoveredExists = false;
            foreach (var item in _consoleLinks)
            {
                if (item.StartOffset == _consoleHoveredLink.StartOffset &&
                    item.Length == _consoleHoveredLink.Length)
                {
                    hoveredExists = true;
                    break;
                }
            }

            if (!hoveredExists)
            {
                _consoleHoveredLink = null;
            }
        }

        _consoleLinkColorizer.SetHoveredLink(_consoleHoveredLink);
        _consoleEditor.TextArea.TextView.InvalidateVisual();
    }

    private void EnsureConsoleColorizers(TextEditor editor)
    {
        if (_consoleBaseTextColorizer is null)
        {
            _consoleBaseTextColorizer = new ConsoleBaseTextColorizer
            {
                TextBrush = GetResourceBrush("BrushTextPrimary")
                    ?? GetResourceBrush("BrushWhite")
                    ?? Brushes.White
            };
            editor.TextArea.TextView.LineTransformers.Insert(0, _consoleBaseTextColorizer);
        }

        if (_consoleSearchHighlightColorizer is null)
        {
            _consoleSearchHighlightColorizer = new ConsoleSearchHighlightColorizer
            {
                HighlightBackgroundBrush = GetResourceBrush("BrushConsoleSearchHighlightBg")
                    ?? Brushes.DarkSlateBlue,
                HighlightForegroundBrush = GetResourceBrush("BrushConsoleSearchHighlightFg")
                    ?? Brushes.White
            };
            editor.TextArea.TextView.LineTransformers.Insert(1, _consoleSearchHighlightColorizer);
        }

        if (_consoleLinkColorizer is not null)
        {
            UpdateConsoleColorizers();
            return;
        }

        _consoleLinkColorizer = new ConsoleLinkColorizer
        {
            LinkBrush = GetResourceBrush("BrushConsoleLink")
                ?? GetResourceBrush("BrushAccent")
                ?? Brushes.DeepSkyBlue
        };
        editor.TextArea.TextView.LineTransformers.Add(_consoleLinkColorizer);
        UpdateConsoleColorizers();
    }

    private void UpdateConsoleColorizers()
    {
        if (_consoleBaseTextColorizer is not null)
        {
            _consoleBaseTextColorizer.TextBrush = GetResourceBrush("BrushTextPrimary")
                ?? GetResourceBrush("BrushWhite")
                ?? Brushes.White;
        }

        if (_consoleLinkColorizer is not null)
        {
            _consoleLinkColorizer.LinkBrush = GetResourceBrush("BrushConsoleLink")
                ?? GetResourceBrush("BrushAccent")
                ?? Brushes.DeepSkyBlue;
        }

        if (_consoleSearchHighlightColorizer is not null)
        {
            _consoleSearchHighlightColorizer.HighlightBackgroundBrush = GetResourceBrush("BrushConsoleSearchHighlightBg")
                ?? Brushes.DarkSlateBlue;
            _consoleSearchHighlightColorizer.HighlightForegroundBrush = GetResourceBrush("BrushConsoleSearchHighlightFg")
                ?? Brushes.White;
        }
    }

    private string GetLatestConsoleTextSnapshot()
    {
        EnsureShellViewModel();
        if (_shellViewModel is not null)
        {
            var shellText = _shellViewModel.ConsoleText ?? string.Empty;
            if (!string.IsNullOrEmpty(shellText))
            {
                return shellText;
            }
        }

        var fallbackEntries = AppLogStore.Instance.GetTail(ConsoleFallbackTailLines);
        if (fallbackEntries.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var entry in fallbackEntries)
        {
            builder.AppendLine(AppLogFormatter.FormatLine(entry));
        }

        return builder.ToString();
    }

    private string GetPreferredConsoleTextSnapshot()
    {
        var latestText = GetLatestConsoleTextSnapshot();
        if (!string.IsNullOrEmpty(latestText))
        {
            return latestText;
        }

        return AppLogStore.Instance.GetTotalCount() > 0
            ? _consoleSourceText
            : string.Empty;
    }

    private void UpdateConsoleAutoFollowState()
    {
        if (_consoleEditor is null)
        {
            _consoleAutoFollow = true;
            return;
        }

        _consoleAutoFollow = IsConsoleNearBottom(_consoleEditor);
    }

    private static bool IsConsoleNearBottom(TextEditor editor)
    {
        if (editor.ExtentHeight <= 0 || editor.ViewportHeight <= 0)
        {
            return true;
        }

        var remaining = editor.ExtentHeight - (editor.VerticalOffset + editor.ViewportHeight);
        return remaining <= 1.0;
    }

    private void EnableConsoleAutoFollowAndScrollToEnd()
    {
        _consoleAutoFollow = true;
        ScrollConsoleToEnd(preserveHorizontalOffset: true);
    }

    private void RefreshConsolePresentation()
    {
        ApplyConsoleRender(_consoleSourceText);
    }

    private void ApplyConsoleRender(string sourceText)
    {
        _consoleSourceText = sourceText ?? string.Empty;
        if (_consoleEditor is null)
        {
            return;
        }

        EnsureShellViewModel();
        var searchText = _shellViewModel?.ConsoleSearchText ?? string.Empty;
        var filterEnabled = _shellViewModel?.IsConsoleFilterEnabled == true;
        var snapshot = BuildConsoleRenderSnapshot(_consoleSourceText, searchText, filterEnabled);
        _consoleSearchRanges = snapshot.SearchRanges;

        var textChanged = !string.Equals(_consoleEditor.Text, snapshot.RenderedText, StringComparison.Ordinal);
        if (textChanged)
        {
            _consoleEditor.Text = snapshot.RenderedText;
            _consoleEditor.TextArea.TextView.InvalidateVisual();
            _consoleEditor.TextArea.TextView.InvalidateArrange();
            return;
        }

        RefreshConsoleLinks();
    }

    private static ConsoleRenderSnapshot BuildConsoleRenderSnapshot(string sourceText, string searchText, bool filterEnabled)
    {
        sourceText ??= string.Empty;
        var keyword = searchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
        {
            return new ConsoleRenderSnapshot(sourceText, Array.Empty<ConsoleTextRange>());
        }

        if (!filterEnabled)
        {
            return new ConsoleRenderSnapshot(sourceText, FindSearchRanges(sourceText, keyword));
        }

        var lines = sourceText.Split('\n');
        var ranges = new List<ConsoleTextRange>();
        var builder = new StringBuilder(sourceText.Length);
        foreach (var rawLine in lines)
        {
            var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
            var lineMatches = FindSearchRanges(line, keyword);
            if (lineMatches.Count == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            var lineStart = builder.Length;
            builder.Append(line);
            foreach (var match in lineMatches)
            {
                ranges.Add(new ConsoleTextRange(lineStart + match.StartOffset, match.Length));
            }
        }

        return new ConsoleRenderSnapshot(builder.ToString(), ranges);
    }

    private static IReadOnlyList<ConsoleTextRange> FindSearchRanges(string text, string keyword)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
        {
            return Array.Empty<ConsoleTextRange>();
        }

        var ranges = new List<ConsoleTextRange>();
        var offset = 0;
        while (offset < text.Length)
        {
            var index = text.IndexOf(keyword, offset, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                break;
            }

            ranges.Add(new ConsoleTextRange(index, keyword.Length));
            offset = index + keyword.Length;
        }

        return ranges;
    }

    private readonly record struct ConsoleRenderSnapshot(
        string RenderedText,
        IReadOnlyList<ConsoleTextRange> SearchRanges);
}
