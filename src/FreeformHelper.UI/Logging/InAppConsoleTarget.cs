using NLog;
using NLog.Targets;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// A custom NLog target that redirects log events to the <see cref="AppLogStore"/>.
/// This allows log messages to be displayed directly within the application's UI,
/// effectively creating an in-app console.
/// </summary>
[Target("InAppConsole")] // Registers this class as an NLog target named "InAppConsole".
public sealed class InAppConsoleTarget : TargetWithLayout
{
    /// <summary>
    /// Overrides the base <see cref="Target.Write(LogEventInfo)"/> method to process incoming log events.
    /// </summary>
    /// <param name="logEvent">The <see cref="LogEventInfo"/> object containing details about the log event.</param>
    protected override void Write(LogEventInfo logEvent)
    {
        // Render the log event's message using the configured layout, if available.
        // Fallback to FormattedMessage if layout rendering is null or whitespace.
        var message = Layout?.Render(logEvent);
        if (string.IsNullOrWhiteSpace(message))
        {
            message = logEvent.FormattedMessage;
        }

        // Create a new AppLogEntry instance from the log event data.
        var entry = new AppLogEntry(
            logEvent.TimeStamp, // Timestamp of the log event.
            logEvent.Level.Name, // Severity level (e.g., "INFO", "WARN").
            logEvent.LoggerName ?? "App", // Name of the logger, defaulting to "App" if null.
            message); // The rendered log message.

        // Add the created log entry to the singleton AppLogStore.
        AppLogStore.Instance.Add(entry);
    }
}
