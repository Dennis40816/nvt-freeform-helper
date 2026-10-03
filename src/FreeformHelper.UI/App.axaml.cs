using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FreeformHelper.UI.Logging;
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
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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

            // If so, create and assign the main application window.
            StartupPerfTracker.Mark("app.main-window-create-start");
            var mainWindow = new MainWindow();
            StartupPerfTracker.Mark("app.main-window-create-complete");
            desktop.MainWindow = mainWindow;
            StartupPerfTracker.Mark("app.main-window-created");
            StartupPerfTracker.Mark("app.cad-spinner-warmup-start");
            CadLoadSpinnerHostService.WarmupSharedHost();
            StartupPerfTracker.Mark("app.cad-spinner-warmup-dispatched");
            var shellBootstrapped = false;
            var runtimeIpcStarted = false;

            void BootstrapShellIfNeeded()
            {
                if (shellBootstrapped)
                {
                    return;
                }

                shellBootstrapped = true;
                if (mainWindow.DataContext is not ViewModels.ShellViewModel shellViewModel)
                {
                    StartupPerfTracker.Mark("app.shell-vm-create-start");
                    shellViewModel = new ViewModels.ShellViewModel();
                    StartupPerfTracker.Mark("app.shell-vm-assign-start");
                    mainWindow.SetShellViewModel(shellViewModel);
                    StartupPerfTracker.Mark("app.shell-vm-assigned");
                }
            }

            void StartRuntimeIpcIfNeeded()
            {
                if (runtimeIpcStarted)
                {
                    return;
                }

                if (mainWindow.DataContext is not ViewModels.ShellViewModel shellViewModel)
                {
                    return;
                }

                runtimeIpcStarted = true;
                RuntimeQueryIpcHost.Start(shellViewModel);
                StartupPerfTracker.Mark("app.runtime-ipc-started");
            }

            void ScheduleRuntimeIpcStart()
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StartupPerfTracker.Mark("app.runtime-ipc-background-dispatch");
                    StartRuntimeIpcIfNeeded();
                }, DispatcherPriority.Background);
            }

            StartupPerfTracker.Mark("app.shell-vm-immediate-bootstrap");
            BootstrapShellIfNeeded();
            StartupPerfTracker.Mark("app.runtime-ipc-deferred", "opened/background");

            mainWindow.Opened += (_, _) =>
            {
                StartupPerfTracker.Mark("app.main-window-opened");
                StartupPerfTracker.Mark("app.shell-vm-opened-bootstrap");
                BootstrapShellIfNeeded();
                ScheduleRuntimeIpcStart();
            };

            Dispatcher.UIThread.Post(() =>
            {
                // Fallback path for edge-cases where Opened may be delayed.
                StartupPerfTracker.Mark("app.shell-vm-fallback-dispatch");
                if (!mainWindow.IsVisible)
                {
                    return;
                }

                BootstrapShellIfNeeded();
                ScheduleRuntimeIpcStart();
            }, DispatcherPriority.ApplicationIdle);

            desktop.Exit += (_, _) =>
            {
                CadLoadSpinnerHostService.ShutdownSharedHost();
                _ = RuntimeQueryIpcHost.StopAsync();
            };
        }

        // Mark the UI as ready in the application log store.
        AppLogStore.Instance.MarkUiReady();
        StartupPerfTracker.Mark("app.ui-ready");

        // Subscribe to unhandled exceptions on the UI thread to log them.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Logger.Error(e.Exception, "UI thread unhandled exception.");
        };

        // Call the base class implementation.
        base.OnFrameworkInitializationCompleted();
        StartupPerfTracker.Mark("app.framework-init-complete");
    }
}
