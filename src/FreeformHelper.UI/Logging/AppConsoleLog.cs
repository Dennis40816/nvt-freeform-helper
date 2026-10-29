using Nvt.Core.LogConsole;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// The one in-app log store. The NLog target writes to it, the console view reads it through the
/// console controller, and the runtime query commands read its snapshots.
/// </summary>
public static class AppConsoleLog
{
    /// <summary>The number of events the store keeps.</summary>
    public const int MaxEntries = 10_000;

    private const string DefaultSourceId = "App";

    /// <summary>Gets the process-wide store. The store is never disposed.</summary>
    public static LogStore Store { get; } = new(MaxEntries);

    /// <summary>
    /// Adds one event. The logger name is the source ID, so a runtime query can show the full logger name.
    /// </summary>
    /// <param name="timestamp">The time the event happened.</param>
    /// <param name="level">The event level.</param>
    /// <param name="loggerName">The logger name, or null for the default source.</param>
    /// <param name="message">The rendered message.</param>
    public static void Write(DateTimeOffset timestamp, LogLevel level, string? loggerName, string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var sourceId = string.IsNullOrWhiteSpace(loggerName) ? DefaultSourceId : loggerName;
        Store.Add(level, sourceId, message, timestamp.ToUniversalTime());
    }
}
