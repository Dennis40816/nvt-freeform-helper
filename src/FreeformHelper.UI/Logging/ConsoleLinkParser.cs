using System.Text.RegularExpressions;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Parses console text and extracts clickable link spans (URL / file path / directory path).
/// </summary>
public static class ConsoleLinkParser
{
    private static readonly HashSet<string> RelativeFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".sln", ".cs", ".c", ".axaml", ".xaml", ".md", ".json", ".ps1",
        ".txt", ".log", ".csv", ".dxf", ".xml", ".yml", ".yaml", ".toml"
    };

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CompilerPathRegex = new(
        @"(?<path>[A-Za-z]:\\[^()\r\n]+?\.[A-Za-z0-9]+)\((?<line>\d+),(?<col>\d+)\)",
        RegexOptions.Compiled);

    private static readonly Regex StackTracePathRegex = new(
        @"(?<path>[A-Za-z]:\\[^:\r\n]+?\.[A-Za-z0-9]+):line\s+(?<line>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex PathWithLineRegex = new(
        @"(?<path>[A-Za-z]:\\[^:\r\n]+?\.[A-Za-z0-9]+):(?<line>\d+)(?::(?<col>\d+))?",
        RegexOptions.Compiled);

    private static readonly Regex WindowsPathRegex = new(
        @"(?<path>[A-Za-z]:\\[^<>:""|?*\r\n]+)",
        RegexOptions.Compiled);

    private static readonly Regex RelativePathRegex = new(
        @"(?<path>(?:\.{1,2}[\\/])?(?:[A-Za-z0-9 _\.-]+[\\/])*[A-Za-z0-9 _\.-]+\.[A-Za-z][A-Za-z0-9]{0,9})",
        RegexOptions.Compiled);

    private static readonly char[] TrimChars = ['.', ',', ';', ')', ']', '}', '"', '\''];

    /// <summary>
    /// Parses all links from the given text.
    /// </summary>
    public static IReadOnlyList<ConsoleLinkSpan> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<ConsoleLinkSpan>();
        }

        var links = new List<ConsoleLinkSpan>();

        AddUrlLinks(text, links);
        AddFileLinksFromRegex(text, CompilerPathRegex, links);
        AddFileLinksFromRegex(text, StackTracePathRegex, links);
        AddFileLinksFromRegex(text, PathWithLineRegex, links);
        AddPlainWindowsPathLinks(text, links);
        AddRelativePathLinks(text, links);

        return links
            .OrderBy(static link => link.StartOffset)
            .ToArray();
    }

    /// <summary>
    /// Finds a link at caret offset. If caret is exactly after a link, fallback to previous char.
    /// </summary>
    public static ConsoleLinkSpan? FindAtOffset(IReadOnlyList<ConsoleLinkSpan> links, int offset)
    {
        if (links is null || links.Count == 0)
        {
            return null;
        }

        var candidate = links.FirstOrDefault(link => offset >= link.StartOffset && offset < link.EndOffset);
        if (candidate is not null)
        {
            return candidate;
        }

        if (offset > 0)
        {
            return links.FirstOrDefault(link => (offset - 1) >= link.StartOffset && (offset - 1) < link.EndOffset);
        }

        return null;
    }

    private static void AddUrlLinks(string text, List<ConsoleLinkSpan> links)
    {
        foreach (Match match in UrlRegex.Matches(text))
        {
            if (!match.Success)
            {
                continue;
            }

            var span = TrimPathSpan(text, match.Index, match.Length);
            if (span.length <= 0)
            {
                continue;
            }

            var target = text.Substring(span.start, span.length);
            TryAddNonOverlapping(links, new ConsoleLinkSpan(
                StartOffset: span.start,
                Length: span.length,
                Target: target,
                IsUrl: true));
        }
    }

    private static void AddFileLinksFromRegex(string text, Regex regex, List<ConsoleLinkSpan> links)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (!match.Success)
            {
                continue;
            }

            var pathGroup = match.Groups["path"];
            if (!pathGroup.Success)
            {
                continue;
            }

            var path = pathGroup.Value;
            if (!File.Exists(path))
            {
                continue;
            }

            var line = TryParseInt(match.Groups["line"].Value, minValue: 1);
            var col = TryParseInt(match.Groups["col"].Value, minValue: 1);

            TryAddNonOverlapping(links, new ConsoleLinkSpan(
                StartOffset: match.Index,
                Length: match.Length,
                Target: path,
                IsUrl: false,
                Line: line,
                Column: col));
        }
    }

    private static void AddPlainWindowsPathLinks(string text, List<ConsoleLinkSpan> links)
    {
        foreach (Match match in WindowsPathRegex.Matches(text))
        {
            if (!match.Success)
            {
                continue;
            }

            var pathGroup = match.Groups["path"];
            if (!pathGroup.Success)
            {
                continue;
            }

            var span = TrimPathSpan(text, pathGroup.Index, pathGroup.Length);
            if (span.length <= 0)
            {
                continue;
            }

            var path = text.Substring(span.start, span.length).Trim();
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                continue;
            }

            TryAddNonOverlapping(links, new ConsoleLinkSpan(
                StartOffset: span.start,
                Length: span.length,
                Target: path,
                IsUrl: false));
        }
    }

    private static void AddRelativePathLinks(string text, List<ConsoleLinkSpan> links)
    {
        foreach (Match match in RelativePathRegex.Matches(text))
        {
            if (!match.Success)
            {
                continue;
            }

            var pathGroup = match.Groups["path"];
            if (!pathGroup.Success)
            {
                continue;
            }

            var span = TrimPathSpan(text, pathGroup.Index, pathGroup.Length);
            if (span.length <= 0)
            {
                continue;
            }

            var target = text.Substring(span.start, span.length).Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                continue;
            }

            if (!CanTreatAsRelativeLink(target))
            {
                continue;
            }

            TryAddNonOverlapping(links, new ConsoleLinkSpan(
                StartOffset: span.start,
                Length: span.length,
                Target: target,
                IsUrl: false));
        }
    }

    private static bool CanTreatAsRelativeLink(string target)
    {
        if (target.IndexOfAny(['\\', '/']) >= 0)
        {
            return true;
        }

        var extension = Path.GetExtension(target);
        return !string.IsNullOrEmpty(extension) && RelativeFileExtensions.Contains(extension);
    }

    private static (int start, int length) TrimPathSpan(string text, int start, int length)
    {
        var end = start + length;
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && ShouldTrim(text[end - 1]))
        {
            end--;
        }

        return (start, end - start);
    }

    private static bool ShouldTrim(char ch) =>
        char.IsWhiteSpace(ch) || Array.IndexOf(TrimChars, ch) >= 0;

    private static int TryParseInt(string? value, int minValue)
    {
        if (!int.TryParse(value, out var parsed))
        {
            return 0;
        }

        return Math.Max(minValue, parsed);
    }

    private static void TryAddNonOverlapping(List<ConsoleLinkSpan> links, ConsoleLinkSpan candidate)
    {
        if (candidate.Length <= 0)
        {
            return;
        }

        var overlaps = links.Any(existing =>
            candidate.StartOffset < existing.EndOffset &&
            candidate.EndOffset > existing.StartOffset);
        if (overlaps)
        {
            return;
        }

        links.Add(candidate);
    }
}
