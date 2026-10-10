// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class ShellViewModelNavigationTests
{
    public static TheoryData<ShellPage, ShellPage> PageTransitions => new()
    {
        { ShellPage.Workspace, ShellPage.Workspace },
        { ShellPage.Workspace, ShellPage.HowToUse },
        { ShellPage.Workspace, ShellPage.Dev },
        { ShellPage.Workspace, ShellPage.Simulation },
        { ShellPage.Workspace, ShellPage.Coordinate },
        { ShellPage.HowToUse, ShellPage.Workspace },
        { ShellPage.HowToUse, ShellPage.HowToUse },
        { ShellPage.HowToUse, ShellPage.Dev },
        { ShellPage.HowToUse, ShellPage.Simulation },
        { ShellPage.HowToUse, ShellPage.Coordinate },
        { ShellPage.Dev, ShellPage.Workspace },
        { ShellPage.Dev, ShellPage.HowToUse },
        { ShellPage.Dev, ShellPage.Dev },
        { ShellPage.Dev, ShellPage.Simulation },
        { ShellPage.Dev, ShellPage.Coordinate },
        { ShellPage.Simulation, ShellPage.Workspace },
        { ShellPage.Simulation, ShellPage.HowToUse },
        { ShellPage.Simulation, ShellPage.Dev },
        { ShellPage.Simulation, ShellPage.Simulation },
        { ShellPage.Simulation, ShellPage.Coordinate },
        { ShellPage.Coordinate, ShellPage.Workspace },
        { ShellPage.Coordinate, ShellPage.HowToUse },
        { ShellPage.Coordinate, ShellPage.Dev },
        { ShellPage.Coordinate, ShellPage.Simulation },
        { ShellPage.Coordinate, ShellPage.Coordinate },
    };

    [Fact]
    public void Constructor_DefaultSelection_ProjectsWorkspaceOnly()
    {
        using var shell = new ShellViewModel();

        Assert.Equal((true, false, false, false, false), ReadActiveFlags(shell));
        Assert.Same(shell.FreeformHelper, shell.CurrentViewModel);
        Assert.Null(shell.Simulation.CurrentWorkspace);
        Assert.Null(shell.CoordinatePlanner.CurrentWorkspace);
    }

    [Theory]
    [MemberData(nameof(PageTransitions))]
    public async Task ShowPage_FromEachPage_ProjectsExactlyOneSelectedPage(ShellPage source, ShellPage target)
    {
        using var shell = CreateShellWithoutWorkspaceBuilders();
        await ShowPageAsync(shell, source);

        await ShowPageAsync(shell, target);

        Assert.Equal(ExpectedActiveFlags(target), ReadActiveFlags(shell));
        Assert.Same(ExpectedViewModel(shell, target), shell.CurrentViewModel);
    }

    [Theory]
    [MemberData(nameof(PageTransitions))]
    public async Task ShowPage_FromEachPage_NotifiesOnlyChangedProjections(ShellPage source, ShellPage target)
    {
        using var shell = CreateShellWithoutWorkspaceBuilders();
        await ShowPageAsync(shell, source);
        var notifications = new ConcurrentQueue<string?>();
        shell.PropertyChanged += (_, args) => notifications.Enqueue(args.PropertyName);

        await ShowPageAsync(shell, target);

        Assert.Equal(ExpectedNotifications(source, target), notifications.Where(IsNavigationProjection));
    }

    [Theory]
    [MemberData(nameof(PageTransitions))]
    public async Task ShowPage_WhileNotifying_ExposesOneCoherentSelection(ShellPage source, ShellPage target)
    {
        using var shell = CreateShellWithoutWorkspaceBuilders();
        await ShowPageAsync(shell, source);
        var observations = new ConcurrentQueue<(string? Name, (bool, bool, bool, bool, bool) Flags, object ViewModel)>();
        shell.PropertyChanged += (_, args) => observations.Enqueue((args.PropertyName, ReadActiveFlags(shell), shell.CurrentViewModel));

        await ShowPageAsync(shell, target);

        var navigationObservations = observations.Where(static observation => IsNavigationProjection(observation.Name)).ToArray();
        Assert.Equal(source == target ? 0 : 3, navigationObservations.Length);
        Assert.All(navigationObservations, observation => Assert.Equal(ExpectedActiveFlags(target), observation.Flags));
        Assert.All(navigationObservations, observation => Assert.Same(ExpectedViewModel(shell, target), observation.ViewModel));
    }

    internal static Task ShowPageAsync(ShellViewModel shell, ShellPage page) => page switch
    {
        ShellPage.Workspace => Execute(shell.ShowWorkspaceCommand),
        ShellPage.HowToUse => Execute(shell.ShowHowToUseCommand),
        ShellPage.Dev => Execute(shell.ShowDevCommand),
        ShellPage.Simulation => shell.ShowSimulationCommand.ExecuteAsync(null),
        ShellPage.Coordinate => shell.ShowCoordinateCommand.ExecuteAsync(null),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    private static Task Execute(System.Windows.Input.ICommand command)
    {
        command.Execute(null);
        return Task.CompletedTask;
    }

    private static ShellViewModel CreateShellWithoutWorkspaceBuilders()
    {
        var shell = new ShellViewModel();
        shell.Simulation.RequestBuildWorkspaceAsync = null;
        shell.CoordinatePlanner.RequestBuildWorkspaceAsync = null;
        return shell;
    }

    private static (bool, bool, bool, bool, bool) ReadActiveFlags(ShellViewModel shell) =>
        (shell.IsWorkspaceActive, shell.IsHowToUseActive, shell.IsDevActive, shell.IsSimulationActive, shell.IsCoordinateActive);

    private static (bool, bool, bool, bool, bool) ExpectedActiveFlags(ShellPage page) =>
        (page == ShellPage.Workspace, page == ShellPage.HowToUse, page == ShellPage.Dev,
            page == ShellPage.Simulation, page == ShellPage.Coordinate);

    private static object ExpectedViewModel(ShellViewModel shell, ShellPage page) => page switch
    {
        ShellPage.Workspace => shell.FreeformHelper,
        ShellPage.HowToUse => shell.HowToUse,
        ShellPage.Dev => shell.Dev,
        ShellPage.Simulation => shell.Simulation,
        ShellPage.Coordinate => shell.CoordinatePlanner,
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };

    private static string[] ExpectedNotifications(ShellPage source, ShellPage target) => source == target
        ? []
        : [nameof(ShellViewModel.CurrentViewModel), ActivePropertyName(target), ActivePropertyName(source)];

    private static bool IsNavigationProjection(string? name) => name is
        nameof(ShellViewModel.CurrentViewModel) or nameof(ShellViewModel.IsWorkspaceActive) or
        nameof(ShellViewModel.IsHowToUseActive) or nameof(ShellViewModel.IsDevActive) or
        nameof(ShellViewModel.IsSimulationActive) or nameof(ShellViewModel.IsCoordinateActive);

    private static string ActivePropertyName(ShellPage page) => page switch
    {
        ShellPage.Workspace => nameof(ShellViewModel.IsWorkspaceActive),
        ShellPage.HowToUse => nameof(ShellViewModel.IsHowToUseActive),
        ShellPage.Dev => nameof(ShellViewModel.IsDevActive),
        ShellPage.Simulation => nameof(ShellViewModel.IsSimulationActive),
        ShellPage.Coordinate => nameof(ShellViewModel.IsCoordinateActive),
        _ => throw new ArgumentOutOfRangeException(nameof(page)),
    };
}
