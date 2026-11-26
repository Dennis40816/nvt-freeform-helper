// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

internal sealed class MainWindowGlobalInputFixture : IAsyncDisposable
{
    private readonly DxfEditingBehaviorTestScope _files = new();
    private readonly ShellViewModel _shell;

    private MainWindowGlobalInputFixture()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        Helper = _files.CreateViewModel();
        Helper.ActiveAreaWidth = 30;
        Helper.ActiveAreaHeight = 10;
        Helper.PanelBiasX = 0;
        Helper.PanelBiasY = 0;
        Helper.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        Helper.GridPaddingPercent = 0;
        var cascade = Assert.Single(Helper.CascadeIcSettings);
        cascade.XChannels = 3;
        cascade.YChannels = 1;
        _shell = new ShellViewModel(Helper) { IsConsoleExpanded = false, IsDevPageAvailable = false };
        _shell.Simulation.RequestPrewarmWorkspaceAsync = null;
        _shell.CoordinatePlanner.RequestPrewarmWorkspaceAsync = null;
        Window = new MainWindow { Width = 1280, Height = 900, WindowState = WindowState.Normal };
        Window.SetShellViewModel(_shell);
        HeadlessSessionGuardAttribute.CloseAtTestEnd(Window);
        Window.Show();
    }

    public FreeformHelperViewModel Helper { get; }
    public MainWindow Window { get; }

    public static async Task<MainWindowGlobalInputFixture> CreateAsync()
    {
        var fixture = new MainWindowGlobalInputFixture();
        try
        {
            await HeadlessUiPump.FlushQueueAsync();
            await fixture._files.OpenAsync(fixture.Helper,
                ("alpha", 0, 0, 10, 10), ("beta", 10, 0, 20, 10), ("gamma", 20, 0, 30, 10));
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(3, fixture.Helper.CadPads.Count);
            Assert.Equal(3, fixture.Helper.RegularPads.Count);
            Assert.All(fixture.Helper.LayerToggles, static layer => Assert.True(layer.IsSelected));
            fixture.FocusShortcutTarget();
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public void FocusShortcutTarget()
    {
        Window.Activate();
        var tab = Assert.Single(Window.GetVisualDescendants().OfType<ToggleButton>(),
            button => button.Classes.Contains("shellTab") && button.Tag is ShellPage.Workspace);
        Assert.True(tab.Focus());
    }

    public KeyEventArgs SendKey(Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, string? symbol)
    {
        KeyEventArgs? received = null;
        void Capture(object? _, KeyEventArgs args) => received = args;
        Window.AddHandler(InputElement.KeyDownEvent, Capture, RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            Window.KeyPress(key, modifiers, physicalKey, symbol);
            Window.KeyRelease(key, modifiers, physicalKey, symbol);
        }
        finally
        {
            Window.RemoveHandler(InputElement.KeyDownEvent, Capture);
        }

        Assert.NotNull(received);
        return received;
    }

    public async Task ClickLayerAsync(string name, RawInputModifiers modifiers)
    {
        var toggle = Assert.Single(Window.GetVisualDescendants().OfType<ToggleButton>(),
            button => button.Classes.Contains("layerToggle") &&
                button.DataContext is LayerToggle layer && layer.Name == name);
        Assert.True(toggle.IsEffectivelyVisible);
        Assert.True(toggle.IsEffectivelyEnabled);
        var point = toggle.TranslatePoint(new Rect(toggle.Bounds.Size).Center, Window);
        Assert.NotNull(point);
        Window.MouseMove(point.Value);
        Window.MouseDown(point.Value, MouseButton.Left, modifiers);
        Window.MouseUp(point.Value, MouseButton.Left, modifiers);
        await HeadlessUiPump.FlushQueueAsync();
        await Helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var child in Window.OwnedWindows.ToArray())
        {
            child.Close();
        }

        Helper.HasUnsavedChanges = false;
        Window.Close();
        _shell.Dispose();
        await Helper.WaitForGridRebuildIdleAsync().WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        _files.Dispose();
    }
}
