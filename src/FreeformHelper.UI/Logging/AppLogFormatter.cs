using System.Globalization;
using Nvt.Core.LogConsole;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Provides a single formatting contract for the one-line text of a log event.
/// The runtime query commands use it.
/// </summary>
public static class AppLogFormatter
{
    private const int LevelColumnWidth = 5;
    private const int LoggerColumnWidth = 56;

    /// <summary>
    /// Formats one line using unified fields:
    /// local time, level, logger source, and message.
    /// </summary>
    public static string FormatLine(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return FormatLine(
            entry.Timestamp,
            entry.Level.ToString(),
            entry.SourceId,
            ReadMessage(entry.TextContent));
    }

    /// <summary>
    /// Formats one line from its fields. A blank level becomes INFO and a blank logger becomes App.
    /// </summary>
    public static string FormatLine(DateTimeOffset timestamp, string? level, string? logger, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var levelText = string.IsNullOrWhiteSpace(level)
            ? "INFO"
            : level.ToUpperInvariant();
        var loggerText = string.IsNullOrWhiteSpace(logger)
            ? "App"
            : logger;
        var levelColumn = FitColumn(levelText, LevelColumnWidth);
        var loggerColumn = FitColumn(loggerText, LoggerColumnWidth);

        return string.Concat(
            timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            " | ",
            levelColumn,
            " | ",
            loggerColumn,
            " | ",
            message);
    }

    /// <summary>
    /// Returns the formatted lines of the newest events and the number of events the store keeps.
    /// The method waits until the store has published every write that was accepted before the call.
    /// </summary>
    /// <param name="store">The store to read. The application passes the store of its shell.</param>
    /// <param name="tailCount">The number of newest events to return.</param>
    public static async Task<(IReadOnlyList<string> Lines, int TotalCount)> GetTailLinesAsync(LogStore store, int tailCount)
    {
        ArgumentNullException.ThrowIfNull(store);
        var effectiveTail = Math.Max(0, tailCount);
        using var snapshot = await store.CaptureLatestAsync();
        var totalCount = snapshot.EventCount;
        if (effectiveTail == 0 || totalCount == 0)
        {
            return (Array.Empty<string>(), totalCount);
        }

        var start = Math.Max(0, snapshot.Entries.Length - effectiveTail);
        var lines = new List<string>(snapshot.Entries.Length - start);
        for (var index = start; index < snapshot.Entries.Length; index++)
        {
            lines.Add(FormatLine(snapshot.Entries[index]));
        }

        return (lines, totalCount);
    }

    private static string ReadMessage(ILogTextContent content)
    {
        return string.Create(content.Length, content, static (span, value) => value.Read(0, span));
    }

    private static string FitColumn(string value, int width)
    {
        if (value.Length == width)
        {
            return value;
        }

        if (value.Length < width)
        {
            return value.PadRight(width);
        }

        if (width <= 3)
        {
            return value[..width];
        }

        return string.Concat("...", value.AsSpan(value.Length - (width - 3)));
    }
}
