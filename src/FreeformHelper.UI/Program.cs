using System.Globalization;
using Avalonia;
using Avalonia.Fonts.Inter;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.Services;
using NLog;

namespace FreeformHelper.UI;

/// <summary>
/// The main entry point class for the FreeformHelper Avalonia UI application.
/// This class is responsible for application bootstrapping, global error handling, and launching the UI.
/// </summary>
internal sealed class Program
{
    // Logger instance for recording application events and errors.
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The main entry point of the application.
    /// Configures logging, sets up global exception handlers, and starts the Avalonia UI framework.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    [STAThread] // Indicates that the application uses a single-threaded apartment model.
    public static void Main(string[] args)
    {
        LoggingBootstrapper.Configure(); // Initialize NLog logging configuration.
        TryApplyStartupLogLevelOverride();
        StartupPerfTracker.Mark("program.logging-configured");
        RegisterGlobalExceptionHandlers(); // Set up handlers for unhandled exceptions.
        StartupPerfTracker.Mark("program.exception-handlers-registered");

        try
        {
            StartupPerfTracker.Mark("program.query-check-start");
            CadLoadSpinnerStartupContext.TryInitializeFromArgs(args);
            if (RuntimeQueryCommandLine.TryHandleQueryCommand(args, out var cliExitCode))
            {
                StartupPerfTracker.Mark("program.query-command-exit");
                Environment.ExitCode = cliExitCode;
                return;
            }

            StartupPerfTracker.Mark("program.avalonia-start");
            StartupPerfTracker.Mark("program.appbuilder-configure-start");
            var appBuilder = BuildAvaloniaApp(); // Build the Avalonia application instance.
            StartupPerfTracker.Mark("program.appbuilder-configure-complete");
            StartupPerfTracker.Mark("program.desktop-lifetime-start");
            appBuilder.StartWithClassicDesktopLifetime(args); // Start the application with a classic desktop lifetime.
            StartupPerfTracker.Mark("program.desktop-lifetime-returned");
            StartupPerfTracker.Mark("program.avalonia-exit");
        }
        catch (Exception ex)
        {
            // Log any fatal exceptions that occur during application startup.
            Logger.Error(ex, "Fatal exception during app startup.");
            throw; // Re-throw the exception to terminate the application.
        }
        finally
        {
            StartupPerfTracker.Mark("program.shutdown");
            LoggingBootstrapper.Shutdown(); // Ensure NLog logging is properly shut down on exit.
        }
    }

    /// <summary>
    /// Configures and builds the Avalonia application.
    /// </summary>
    /// <returns>An <see cref="AppBuilder"/> instance configured for the application.</returns>
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>() // Start configuration for the 'App' class.
            .UsePlatformDetect() // Automatically detect and configure for the current platform.
            .WithSystemFontSource(AppFontBootstrapper.InterSystemFontSourceUri)
            .With(AppFontBootstrapper.CreateFontManagerOptions())
            .ConfigureFonts(static fontManager => fontManager.AddFontCollection(new InterFontCollection()))
            .WithInterFont() // Configure Inter as the default font.
            .AfterPlatformServicesSetup(static _ => UiThread.RegisterRunningDispatcher(
                Avalonia.Threading.Dispatcher.UIThread));

#if DEBUG
        builder = builder.LogToTrace();
#else
        // Release default keeps startup leaner; allow forcing trace with env toggle.
        var forceTrace = Environment.GetEnvironmentVariable("FREEFORM_ENABLE_AVALONIA_TRACE");
        if (string.Equals(forceTrace, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(forceTrace, "true", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.LogToTrace();
        }
#endif

        return builder;
    }

    /// <summary>
    /// Registers global exception handlers for unhandled exceptions in the application domain
    /// and for unobserved task exceptions. This helps in logging errors that might otherwise crash the application silently.
    /// </summary>
    private static void RegisterGlobalExceptionHandlers()
    {
        // Handles unhandled exceptions that occur on any thread within the application domain.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Error(ex, "Unhandled exception.");
            }
            else
            {
                Logger.Error(CultureInfo.InvariantCulture, "Unhandled exception: {0}", e.ExceptionObject);
            }
        };

        // Handles exceptions that occur within tasks but are not observed (e.g., awaited).
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error(e.Exception, "Unobserved task exception.");
            e.SetObserved(); // Mark the exception as observed to prevent the process from terminating.
        };
    }

    private static void TryApplyStartupLogLevelOverride()
    {
        try
        {
            var store = new AppGeneralSettingsStore();
            var document = store.TryLoad();
            var requestedLevel = document?.Import?.LogLevel;
            if (string.IsNullOrWhiteSpace(requestedLevel))
            {
                return;
            }

            var appliedLevel = LoggingBootstrapper.ApplyMinimumLevel(requestedLevel);
            Logger.Info(CultureInfo.InvariantCulture, "Startup log level override applied from app settings: {0}", appliedLevel);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to apply startup log level override.");
        }
    }
}

