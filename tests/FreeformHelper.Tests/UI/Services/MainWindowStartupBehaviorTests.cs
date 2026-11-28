// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Locks the startup sequence of the main window: when the shell view model is created, when the runtime IPC host starts,
/// and which startup markers appear in which order. The markers are parsed by baseline scripts, so their names are part of the contract.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class MainWindowStartupBehaviorTests
{
    private static readonly string[] _immediateMarkers =
    [
        "app.shell-vm-immediate-bootstrap",
        "app.shell-vm-create-start",
        "app.shell-vm-assign-start",
        "app.shell-vm-assigned",
        "app.runtime-ipc-deferred",
    ];

    [AvaloniaFact]
    public void Attach_CreatesTheShellAtOnceAndDoesNotStartTheIpcHost()
    {
        using var fixture = new StartupFixture();

        fixture.Startup.Attach();

        Assert.Equal(1, fixture.ShellsCreated);
        Assert.Equal(0, fixture.IpcStarts);
        Assert.Same(fixture.Shell, fixture.Window.DataContext);
        Assert.Equal(_immediateMarkers, fixture.Stages());
    }

    [AvaloniaFact]
    public void Opened_BootstrapsTheShellAndStartsTheIpcHostOnceAfterTheBackgroundDispatch()
    {
        using var fixture = new StartupFixture();
        fixture.Startup.Attach();
        fixture.ClearMarkers();

        fixture.RaiseOpened();

        Assert.Equal(0, fixture.IpcStarts);
        Assert.Equal(["app.main-window-opened", "app.shell-vm-opened-bootstrap"], fixture.Stages());

        Pump();

        // The idle fallback also runs now that the window is visible; the IPC host still starts once.
        Assert.Equal(1, fixture.ShellsCreated);
        Assert.Equal(1, fixture.IpcStarts);
        Assert.Same(fixture.Shell, fixture.StartedWith);
        var stages = fixture.Stages();
        Assert.True(
            Array.IndexOf(stages, "app.runtime-ipc-background-dispatch") < Array.IndexOf(stages, "app.runtime-ipc-started"),
            string.Join(", ", stages));
        Assert.Single(stages, "app.runtime-ipc-started");
    }

    [AvaloniaFact]
    public void OpenedAndIdleFallbackTogether_StartTheIpcHostOnlyOnce()
    {
        using var fixture = new StartupFixture();
        fixture.Startup.Attach();

        fixture.RaiseOpened();
        Pump();
        Pump();

        Assert.Equal(1, fixture.ShellsCreated);
        Assert.Equal(1, fixture.IpcStarts);
    }

    [AvaloniaFact]
    public void IdleFallback_WhenTheWindowIsNotVisible_DoesNothing()
    {
        using var fixture = new StartupFixture();
        fixture.Startup.Attach();
        fixture.ClearMarkers();

        Pump();

        Assert.Equal(0, fixture.IpcStarts);
        Assert.Equal(["app.shell-vm-fallback-dispatch"], fixture.Stages());
    }

    [AvaloniaFact]
    public void IdleFallback_WhenTheWindowIsVisible_StartsTheIpcHostOnce()
    {
        using var fixture = new StartupFixture();
        fixture.Startup.Attach();
        fixture.Window.Show();

        Pump();

        Assert.Equal(1, fixture.ShellsCreated);
        Assert.Equal(1, fixture.IpcStarts);
        Assert.Contains("app.shell-vm-fallback-dispatch", fixture.Stages());
    }

    [AvaloniaFact]
    public void Attach_WhenTheWindowAlreadyHasAShell_KeepsItAndStartsTheIpcHostWithIt()
    {
        using var existing = new ShellViewModel { IsDevPageAvailable = true };
        using var fixture = new StartupFixture { Window = { DataContext = existing } };

        fixture.Startup.Attach();
        fixture.RaiseOpened();
        Pump();

        Assert.Equal(0, fixture.ShellsCreated);
        Assert.Same(existing, fixture.Window.DataContext);
        Assert.Same(existing, fixture.StartedWith);
        Assert.DoesNotContain("app.shell-vm-create-start", fixture.Stages());
    }

    [AvaloniaFact]
    public void Opened_WhenTheAssignedShellIsNotOnTheWindow_NeverStartsTheIpcHost()
    {
        using var fixture = new StartupFixture(assignShell: false);

        fixture.Startup.Attach();
        fixture.RaiseOpened();
        Pump();

        Assert.Equal(1, fixture.ShellsCreated);
        Assert.Equal(0, fixture.IpcStarts);
    }

    private static void Pump()
    {
        HeadlessUiPump.RunningDispatcher.RunJobs();
    }

    private sealed class StartupFixture : IDisposable
    {
        private static readonly Regex _stagePattern = new("stage=([^;]+);", RegexOptions.CultureInvariant);

        private readonly LoggingConfiguration? _previousConfiguration;
        private readonly MemoryTarget _target = new() { Layout = "${message}" };
        private readonly bool _assignShell;

        public StartupFixture(bool assignShell = true)
        {
            _assignShell = assignShell;
            HeadlessAppBootstrap.EnsureInitialized();
            _previousConfiguration = LogManager.Configuration;
            var configuration = new LoggingConfiguration();
            configuration.AddRule(LogLevel.Debug, LogLevel.Info, _target, typeof(StartupPerfTracker).FullName!);
            LogManager.Configuration = configuration;

            Window = new Window();
            Shell = new ShellViewModel { IsDevPageAvailable = true };
            Startup = new MainWindowStartup(
                Window,
                HeadlessUiPump.RunningDispatcher,
                () =>
                {
                    ShellsCreated++;
                    return Shell;
                },
                shell =>
                {
                    if (_assignShell)
                    {
                        Window.DataContext = shell;
                    }
                },
                shell =>
                {
                    IpcStarts++;
                    StartedWith = shell;
                });
        }

        public Window Window { get; }

        public ShellViewModel Shell { get; }

        public MainWindowStartup Startup { get; }

        public int ShellsCreated { get; private set; }

        public int IpcStarts { get; private set; }

        public ShellViewModel? StartedWith { get; private set; }

        public void RaiseOpened()
        {
            Window.Show();
        }

        public void ClearMarkers()
        {
            _target.Logs.Clear();
        }

        public string[] Stages()
        {
            return _target.Logs
                .Select(message => _stagePattern.Match(message))
                .Where(match => match.Success && match.Groups[1].Value.StartsWith("app.", StringComparison.Ordinal))
                .Select(match => match.Groups[1].Value)
                .ToArray();
        }

        public void Dispose()
        {
            Window.Close();
            Shell.Dispose();
            _target.Dispose();
            LogManager.Configuration = _previousConfiguration;
        }
    }
}
