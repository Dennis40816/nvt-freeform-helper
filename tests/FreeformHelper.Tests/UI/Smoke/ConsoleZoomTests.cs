using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using NLog;
using NLog.Config;
using Nvt.Core.Avalonia.LogConsole;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ConsoleZoomTests
{
    private const double StoredZoom = 1.5;
    private const double ChangedZoom = 2.0;

    [AvaloniaFact]
    public void ConsoleSession_NewSession_StartsAtNormalZoom()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var shell = new ShellViewModel();

        Assert.Equal(1.0, shell.ConsoleSession.Zoom);
    }

    [AvaloniaFact]
    public void ConsoleList_NewPanelOverSessionWithZoom_ShowsTheStoredZoom()
    {
        using var console = new ZoomFixture(StoredZoom);

        Assert.Equal(StoredZoom, console.List.Zoom);
    }

    [AvaloniaFact]
    public void ConsoleList_NewPanelOverNewSession_ShowsNormalZoom()
    {
        using var console = new ZoomFixture(1.0);

        Assert.Equal(1.0, console.List.Zoom);
    }

    [AvaloniaFact]
    public void ConsoleList_ZoomIn_StoresTheNewZoomInTheSession()
    {
        using var console = new ZoomFixture(1.0);

        console.List.Zoom = ChangedZoom;

        Assert.Equal(ChangedZoom, console.Shell.ConsoleSession.Zoom);
    }

    [AvaloniaFact]
    public void ConsoleList_ZoomInByStep_StoresOneStopAboveNormal()
    {
        using var console = new ZoomFixture(1.0);

        console.List.ZoomIn();

        Assert.Equal(console.List.Zoom, console.Shell.ConsoleSession.Zoom);
        Assert.True(console.Shell.ConsoleSession.Zoom > 1.0);
    }

    [AvaloniaFact]
    public void ConsoleList_ZoomBeyondTheMaximum_KeepsTheClampedZoomInTheSession()
    {
        using var console = new ZoomFixture(1.0);

        console.List.Zoom = ConsoleListView.MaximumZoom + 1.0;

        Assert.Equal(ConsoleListView.MaximumZoom, console.Shell.ConsoleSession.Zoom);
    }

    [AvaloniaFact]
    public void ConsoleToolbar_InThePanel_IsWiredToTheListAndTheCopyVisibleCommand()
    {
        using var console = new ZoomFixture(1.0);

        // Core disables the zoom, jump and copy buttons until both are set.
        Assert.Same(console.List, console.Toolbar.List);
        Assert.Same(console.Shell.ConsoleSession.CopyVisibleCommand, console.Toolbar.CopyVisibleCommand);
    }

    private sealed class ZoomFixture : IDisposable
    {
        private readonly LoggingConfiguration? _previousConfiguration;

        public ZoomFixture(double zoom)
        {
            HeadlessAppBootstrap.EnsureInitialized();
            _previousConfiguration = LogManager.Configuration;
            LogManager.Configuration = new LoggingConfiguration();
            Shell = new ShellViewModel();
            Shell.ConsoleSession.Zoom = zoom;
            Window = new MainWindow { Width = 1280, Height = 900 };
            Window.SetShellViewModel(Shell);
            Window.Show();
            HeadlessUiPump.Drain(Window);
            List = Window.GetVisualDescendants().OfType<ConsoleListView>().First();
            Toolbar = Window.GetVisualDescendants().OfType<ConsoleToolbar>().First();
        }

        public ShellViewModel Shell { get; }

        public MainWindow Window { get; }

        public ConsoleListView List { get; }

        public ConsoleToolbar Toolbar { get; }

        public void Dispose()
        {
            Window.Close();
            Shell.Dispose();
            LogManager.Configuration = _previousConfiguration;
        }
    }
}
