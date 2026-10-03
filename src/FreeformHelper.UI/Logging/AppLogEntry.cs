namespace FreeformHelper.UI.Logging;

/// <summary>
/// Represents a single log entry captured by the application.
/// This immutable record stores details about a logged event.
/// </summary>
/// <param name="Timestamp">The UTC timestamp when the log entry was created.</param>
/// <param name="Level">The severity level of the log entry (e.g., "INFO", "WARN", "ERROR").</param>
/// <param name="Logger">The name of the logger that created the entry (e.g., the class name).</param>
/// <param name="Message">The detailed message of the log entry.</param>
public sealed record AppLogEntry(DateTimeOffset Timestamp, string Level, string Logger, string Message);
