// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowNavigationInputTests
{
    [AvaloniaTheory]
    [InlineData(ShellPage.Workspace)]
    [InlineData(ShellPage.HowToUse)]
    [InlineData(ShellPage.Dev)]
    [InlineData(ShellPage.Simulation)]
    [InlineData(ShellPage.Coordinate)]
    public async Task ClickShellTab_CurrentPage_PreservesSelectionWithoutNavigationNotifications(ShellPage page)
    {
        using var scenario = await NavigationWindow.CreateAsync(page);
        var tab = scenario.GetTab(page);
        var currentViewModel = scenario.Shell.CurrentViewModel;
        var notifications = new ConcurrentQueue<string?>();
        scenario.Shell.PropertyChanged += (_, args) => notifications.Enqueue(args.PropertyName);
        Assert.True(tab.IsChecked);

        await scenario.ClickAsync(tab);

        Assert.True(tab.IsChecked);
        Assert.Same(currentViewModel, scenario.Shell.CurrentViewModel);
        Assert.DoesNotContain(notifications, IsNavigationProjection);
    }

    [AvaloniaTheory]
    [InlineData(ShellPage.Workspace, ShellPage.HowToUse)]
    [InlineData(ShellPage.Workspace, ShellPage.Dev)]
    [InlineData(ShellPage.Workspace, ShellPage.Simulation)]
    [InlineData(ShellPage.Workspace, ShellPage.Coordinate)]
    [InlineData(ShellPage.HowToUse, ShellPage.Workspace)]
    [InlineData(ShellPage.HowToUse, ShellPage.Dev)]
    [InlineData(ShellPage.HowToUse, ShellPage.Simulation)]
    [InlineData(ShellPage.HowToUse, ShellPage.Coordinate)]
    [InlineData(ShellPage.Dev, ShellPage.Workspace)]
    [InlineData(ShellPage.Dev, ShellPage.HowToUse)]
    [InlineData(ShellPage.Dev, ShellPage.Simulation)]
    [InlineData(ShellPage.Dev, ShellPage.Coordinate)]
    [InlineData(ShellPage.Simulation, ShellPage.Workspace)]
    [InlineData(ShellPage.Simulation, ShellPage.HowToUse)]
    [InlineData(ShellPage.Simulation, ShellPage.Dev)]
    [InlineData(ShellPage.Simulation, ShellPage.Coordinate)]
    [InlineData(ShellPage.Coordinate, ShellPage.Workspace)]
    [InlineData(ShellPage.Coordinate, ShellPage.HowToUse)]
    [InlineData(ShellPage.Coordinate, ShellPage.Dev)]
    [InlineData(ShellPage.Coordinate, ShellPage.Simulation)]
    public async Task ClickShellTab_OtherPage_ChangesSelectionAndContentOnce(ShellPage source, ShellPage target)
    {
        using var scenario = await NavigationWindow.CreateAsync(source);
        var oldTab = scenario.GetTab(source);
        var newTab = scenario.GetTab(target);
        var currentViewModel = scenario.Shell.CurrentViewModel;
        var notifications = new ConcurrentQueue<string?>();
        scenario.Shell.PropertyChanged += (_, args) => notifications.Enqueue(args.PropertyName);
        Assert.True(oldTab.IsChecked);
        Assert.False(newTab.IsChecked);

        await scenario.ClickAsync(newTab);

        Assert.True(newTab.IsChecked);
        Assert.False(oldTab.IsChecked);
        Assert.NotSame(currentViewModel, scenario.Shell.CurrentViewModel);
        Assert.Single(notifications, static name => name == nameof(ShellViewModel.CurrentViewModel));
    }

    private static bool IsNavigationProjection(string? name) => name is
        nameof(ShellViewModel.CurrentViewModel) or nameof(ShellViewModel.IsWorkspaceActive) or
        nameof(ShellViewModel.IsHowToUseActive) or nameof(ShellViewModel.IsDevActive) or
        nameof(ShellViewModel.IsSimulationActive) or nameof(ShellViewModel.IsCoordinateActive);

    private sealed class NavigationWindow : IDisposable
    {
        private readonly MainWindow _window;

        private NavigationWindow(ShellViewModel shell)
        {
            Shell = shell;
            _window = new MainWindow { Width = 1280, Height = 900, WindowState = WindowState.Normal };
            _window.SetShellViewModel(shell);
            _window.Show();
        }

        public ShellViewModel Shell { get; }

        public static async Task<NavigationWindow> CreateAsync(ShellPage page)
        {
            HeadlessAppBootstrap.EnsureInitialized();
            var shell = new ShellViewModel { IsConsoleExpanded = false };
            shell.Simulation.RequestBuildWorkspaceAsync = null;
            shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
            await ShellViewModelNavigationTests.ShowPageAsync(shell, page);
            var scenario = new NavigationWindow(shell);
            await Dispatcher.UIThread.InvokeAsync(scenario._window.UpdateLayout, DispatcherPriority.Background);
            return scenario;
        }

        public ToggleButton GetTab(ShellPage page) => _window.GetVisualDescendants().OfType<ToggleButton>()
            .Single(button => button.Classes.Contains("shellTab") && Equals(button.Content, TabContent(page)));

        public async Task ClickAsync(ToggleButton tab)
        {
            var point = tab.TranslatePoint(new Rect(tab.Bounds.Size).Center, _window);
            Assert.NotNull(point);
            _window.MouseMove(point.Value);
            _window.MouseDown(point.Value, MouseButton.Left);
            _window.MouseUp(point.Value, MouseButton.Left);
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        }

        public void Dispose()
        {
            Shell.FreeformHelper.HasUnsavedChanges = false;
            _window.Close();
            Dispatcher.UIThread.RunJobs();
        }

        private static string TabContent(ShellPage page) => page switch
        {
            ShellPage.Workspace => "Freeform Helper",
            ShellPage.HowToUse => "How To Use",
            ShellPage.Dev => "Dev",
            ShellPage.Simulation => "Simulation",
            ShellPage.Coordinate => "Coordinate",
            _ => throw new ArgumentOutOfRangeException(nameof(page)),
        };
    }
}
