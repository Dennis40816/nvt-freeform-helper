using System.Globalization;
using NLog;

namespace FreeformHelper.UI.Logging;

/// <summary>
/// Provides static methods to configure and manage the NLog logging framework
/// for the application.
/// </summary>
public static class LoggingBootstrapper
{
    private static readonly LogLevel[] SupportedLevels =
    {
        LogLevel.Trace,
        LogLevel.Debug,
        LogLevel.Info,
        LogLevel.Warn,
        LogLevel.Error,
        LogLevel.Fatal,
    };

    private static LogLevel _currentMinimumLevel = LogLevel.Info;

    /// <summary>
    /// Gets the list of supported minimum log levels that can be selected in UI.
    /// </summary>
    public static IReadOnlyList<string> SupportedMinimumLevels { get; } =
        SupportedLevels.Select(static level => level.Name).ToList();

    /// <summary>
    /// Configures the NLog logging system.
    /// It attempts to load logging settings from an "NLog.config" file.
    /// If the file is not found, NLog will operate with its default configuration or no configuration.
    /// </summary>
    public static void Configure()
    {
        var logDirectory = GetLogDirectoryPath();
        Directory.CreateDirectory(logDirectory);

        // Set up NLog configuration:
        // - Load configuration from NLog.config file.
        // - 'optional: true' means the application will still run if the file is missing.
        LogManager.Setup().LoadConfigurationFromFile("NLog.config", optional: true);
        _currentMinimumLevel = DetectCurrentMinimumLevel();

        // Get a logger for the current class and log an informational message indicating initialization.
        // This helps confirm that logging has started successfully.
        LogManager.GetCurrentClassLogger().Info(CultureInfo.InvariantCulture, "Logging initialized. dir={0}", logDirectory);
    }

    /// <summary>
    /// Shuts down the NLog logging system.
    /// This should be called when the application is exiting to ensure all buffered
    /// log messages are flushed and resources are released.
    /// </summary>
    public static void Shutdown()
    {
        // Gracefully shut down NLog.
        LogManager.Shutdown();
    }

    /// <summary>
    /// Gets the current global minimum log level name.
    /// </summary>
    public static string GetCurrentMinimumLevelName()
    {
        return _currentMinimumLevel.Name;
    }

    /// <summary>
    /// Applies a global minimum log level to all configured logging rules.
    /// Returns the normalized level name actually applied.
    /// </summary>
    public static string ApplyMinimumLevel(string? levelName)
    {
        var resolvedLevel = ResolveMinimumLevel(levelName);
        var configuration = LogManager.Configuration;
        if (configuration is null)
        {
            _currentMinimumLevel = resolvedLevel;
            return _currentMinimumLevel.Name;
        }

        foreach (var rule in configuration.LoggingRules)
        {
            rule.DisableLoggingForLevels(LogLevel.Trace, LogLevel.Fatal);
            rule.EnableLoggingForLevels(resolvedLevel, LogLevel.Fatal);
        }

        LogManager.ReconfigExistingLoggers();
        _currentMinimumLevel = resolvedLevel;
        return _currentMinimumLevel.Name;
    }

    private static string GetLogDirectoryPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "logs"));
    }

    private static LogLevel ResolveMinimumLevel(string? levelName)
    {
        if (!string.IsNullOrWhiteSpace(levelName))
        {
            var match = SupportedLevels.FirstOrDefault(level =>
                string.Equals(level.Name, levelName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return LogLevel.Info;
    }

    private static LogLevel DetectCurrentMinimumLevel()
    {
        var configuration = LogManager.Configuration;
        if (configuration is null)
        {
            return LogLevel.Info;
        }

        LogLevel? minimum = null;
        foreach (var rule in configuration.LoggingRules)
        {
            foreach (var level in SupportedLevels)
            {
                if (!rule.IsLoggingEnabledForLevel(level))
                {
                    continue;
                }

                minimum = minimum is null || level.Ordinal < minimum.Ordinal
                    ? level
                    : minimum;
                break;
            }
        }

        return minimum ?? LogLevel.Info;
    }
}
