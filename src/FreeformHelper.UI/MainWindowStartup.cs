// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Threading;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI;

/// <summary>
/// Runs the startup sequence of the main window: it creates the shell view model once and starts the runtime IPC host once.
/// The shell is created at once. The window <c>Opened</c> event and the idle fallback create it only if it is still missing.
/// The IPC host starts after the window opened, at background priority, or at the idle fallback when the window is visible.
/// </summary>
internal sealed class MainWindowStartup
{
    private readonly Window _window;
    private readonly Dispatcher _dispatcher;
    private readonly Func<ShellViewModel> _createShell;
    private readonly Action<ShellViewModel> _assignShell;
    private readonly Action<ShellViewModel> _startRuntimeIpc;
    private bool _shellBootstrapped;
    private bool _runtimeIpcStarted;

    public MainWindowStartup(
        Window window,
        Dispatcher dispatcher,
        Func<ShellViewModel> createShell,
        Action<ShellViewModel> assignShell,
        Action<ShellViewModel> startRuntimeIpc)
    {
        _window = window;
        _dispatcher = dispatcher;
        _createShell = createShell;
        _assignShell = assignShell;
        _startRuntimeIpc = startRuntimeIpc;
    }

    /// <summary>Creates the shell view model now and hooks the later startup steps to the window and the dispatcher.</summary>
    public void Attach()
    {
        StartupPerfTracker.Mark("app.shell-vm-immediate-bootstrap");
        BootstrapShellIfNeeded();
        StartupPerfTracker.Mark("app.runtime-ipc-deferred", "opened/background");

        _window.Opened += (_, _) =>
        {
            StartupPerfTracker.Mark("app.main-window-opened");
            StartupPerfTracker.Mark("app.shell-vm-opened-bootstrap");
            BootstrapShellIfNeeded();
            ScheduleRuntimeIpcStart();
        };

        _dispatcher.Post(() =>
        {
            // Fallback path for edge-cases where Opened may be delayed.
            StartupPerfTracker.Mark("app.shell-vm-fallback-dispatch");
            if (!_window.IsVisible)
            {
                return;
            }

            BootstrapShellIfNeeded();
            ScheduleRuntimeIpcStart();
        }, DispatcherPriority.ApplicationIdle);
    }

    private void BootstrapShellIfNeeded()
    {
        if (_shellBootstrapped)
        {
            return;
        }

        _shellBootstrapped = true;
        if (_window.DataContext is ShellViewModel)
        {
            return;
        }

        StartupPerfTracker.Mark("app.shell-vm-create-start");
        var shellViewModel = _createShell();
        StartupPerfTracker.Mark("app.shell-vm-assign-start");
        _assignShell(shellViewModel);
        StartupPerfTracker.Mark("app.shell-vm-assigned");
    }

    private void StartRuntimeIpcIfNeeded()
    {
        if (_runtimeIpcStarted)
        {
            return;
        }

        if (_window.DataContext is not ShellViewModel shellViewModel)
        {
            return;
        }

        _runtimeIpcStarted = true;
        _startRuntimeIpc(shellViewModel);
        StartupPerfTracker.Mark("app.runtime-ipc-started");
    }

    private void ScheduleRuntimeIpcStart()
    {
        _dispatcher.Post(() =>
        {
            StartupPerfTracker.Mark("app.runtime-ipc-background-dispatch");
            StartRuntimeIpcIfNeeded();
        }, DispatcherPriority.Background);
    }
}
