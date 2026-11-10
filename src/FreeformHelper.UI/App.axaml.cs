using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Views;
using NLog;

namespace FreeformHelper.UI;

/// <summary>
/// Represents the main application class for the FreeformHelper UI, inheriting from Avalonia.Application.
/// This class handles application-level initialization and lifecycle events.
/// </summary>
public sealed class App : global::Avalonia.Application
{
    // Logger instance for recording application events and errors.
    private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Initializes the Avalonia XAML framework for the application.
    /// This method is called automatically during application startup.
    /// </summary>
    public override void Initialize()
    {
        StartupPerfTracker.Mark("app.initialize-start");
        // Loads the XAML UI definitions from App.axaml and associated files.
        AvaloniaXamlLoader.Load(this);
        SharedToolTipStyleService.Register();
        StartupPerfTracker.Mark("app.initialize-complete");
    }

    /// <summary>
    /// This method is called once the Avalonia framework has completed its initialization.
    /// It's used to set up the main window and configure application-level event handlers.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        StartupPerfTracker.Mark("app.framework-init-enter");

        // Check if the application is running in a classic desktop environment.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (CadLoadSpinnerStartupContext.IsSpinnerMode)
            {
                desktop.MainWindow = new CadLoadSpinnerWindow(CadLoadSpinnerStartupContext.Current);
                base.OnFrameworkInitializationCompleted();
                StartupPerfTracker.Mark("app.framework-init-complete");
                return;
            }

            StartMainWindow(desktop);
        }

        StartupPerfTracker.Mark("app.ui-ready");

        // Subscribe to unhandled exceptions on the UI thread to log them.
        Dispatcher.UnhandledException += (_, e) =>
        {
            _logger.Error(e.Exception, "UI thread unhandled exception.");
        };

        // Call the base class implementation.
        base.OnFrameworkInitializationCompleted();
        StartupPerfTracker.Mark("app.framework-init-complete");
    }

    private void StartMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Create and assign the main application window.
        StartupPerfTracker.Mark("app.main-window-create-start");
        var mainWindow = new MainWindow();
        StartupPerfTracker.Mark("app.main-window-create-complete");
        desktop.MainWindow = mainWindow;
        StartupPerfTracker.Mark("app.main-window-created");
        StartupPerfTracker.Mark("app.cad-spinner-warmup-start");
        CadLoadSpinnerHostService.WarmupSharedHost();
        StartupPerfTracker.Mark("app.cad-spinner-warmup-dispatched");

        new MainWindowStartup(
            mainWindow,
            Dispatcher,
            () => new ViewModels.ShellViewModel { IsDevPageAvailable = AppBuildChannel.IsDevelopmentBuild },
            mainWindow.SetShellViewModel,
            RuntimeQueryIpcHost.Start).Attach();

        desktop.Exit += (_, _) =>
        {
            CadLoadSpinnerHostService.ShutdownSharedHost();
            _ = RuntimeQueryIpcHost.StopAsync();
        };
    }
}
