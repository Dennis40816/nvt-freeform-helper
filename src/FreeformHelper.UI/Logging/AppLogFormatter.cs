using System.Globalization;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Provides a single formatting contract for in-app console log lines.
/// </summary>
public static class AppLogFormatter
{
    private const int LevelColumnWidth = 5;
    private const int LoggerColumnWidth = 56;

    /// <summary>
    /// Formats one console line using unified fields:
    /// time, level, logger source, and message.
    /// </summary>
    public static string FormatLine(AppLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var level = string.IsNullOrWhiteSpace(entry.Level)
            ? "INFO"
            : entry.Level.ToUpperInvariant();
        var logger = string.IsNullOrWhiteSpace(entry.Logger)
            ? "App"
            : entry.Logger;
        var levelColumn = FitColumn(level, LevelColumnWidth);
        var loggerColumn = FitColumn(logger, LoggerColumnWidth);

        return string.Concat(
            entry.Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            " | ",
            levelColumn,
            " | ",
            loggerColumn,
            " | ",
            entry.Message);
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
