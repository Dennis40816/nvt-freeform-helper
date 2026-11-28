// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditCommandsCharacterizationTests
{
    private static readonly (string Name, Type DeclaredType)[] _expectedCommands =
    [
        ("ExportDxfVisibleCommand", typeof(IAsyncRelayCommand)),
        ("ExportDxfAllCommand", typeof(IAsyncRelayCommand)),
        ("ExportDxfLayerImageCommand", typeof(IAsyncRelayCommand)),
        ("CheckDxfQualityCommand", typeof(IAsyncRelayCommand)),
        ("SelectDxfOverlapPadsCommand", typeof(IRelayCommand)),
        ("ClearDxfOverlapHighlightsCommand", typeof(IRelayCommand)),
        ("ShowDxfOverlapDetailsCommand", typeof(IAsyncRelayCommand)),
        ("AnalyzeIndexMappingCommand", typeof(IAsyncRelayCommand)),
        ("OffsetSelectedCadOutputFwDiffIndicesCommand", typeof(IRelayCommand)),
        ("SelectStep4DuplicateDiffGroupCommand", typeof(IRelayCommand<Step4DuplicateDiffGroupViewModel?>)),
        ("DeleteSelectedCadPadsCommand", typeof(IRelayCommand)),
        ("RestoreLastDeletedCadPadsCommand", typeof(IRelayCommand)),
        ("RestoreAllHiddenCadPadsCommand", typeof(IRelayCommand)),
        ("CombineSelectedCadPadsCommand", typeof(IRelayCommand)),
        ("ClearCombinedCadPadsCommand", typeof(IRelayCommand)),
        ("ResetAllDxfEditsCommand", typeof(IRelayCommand)),
        ("OpenHiddenDxfEditChangeListCommand", typeof(IAsyncRelayCommand)),
        ("OpenDuplicateDxfEditChangeListCommand", typeof(IAsyncRelayCommand)),
        ("OpenCombinedDxfEditChangeListCommand", typeof(IAsyncRelayCommand)),
        ("OpenMovedDxfEditChangeListCommand", typeof(IAsyncRelayCommand)),
        ("OpenRotatedDxfEditChangeListCommand", typeof(IAsyncRelayCommand)),
        ("MoveSelectedCadPadsToLayerCommand", typeof(IRelayCommand)),
        ("CreateLayerAndMoveSelectedCadPadsCommand", typeof(IRelayCommand)),
        ("RotateSelectedCadPadsCommand", typeof(IRelayCommand)),
    ];

    [Fact]
    public void CommandsExposeTheExpectedPublicNamesAndDeclaredTypesWithoutPublicSetters()
    {
        Assert.All(_expectedCommands, expected =>
        {
            var property = GetPublicCommandProperty(expected.Name);
            Assert.Equal(expected.DeclaredType, property.PropertyType);
            Assert.Null(property.GetSetMethod());
        });
    }

    [AvaloniaFact]
    public void CommandsKeepDistinctInstancesForOneViewModel()
    {
        var vm = new FreeformHelperViewModel();
        var commands = ReadCommands(vm);

        Assert.All(_expectedCommands, expected =>
            Assert.Same(ReadCommand(vm, expected.Name), ReadCommand(vm, expected.Name)));
        AssertPairwiseDistinct(commands);
    }

    [AvaloniaFact]
    public void CommandsAreNotSharedBetweenViewModels()
    {
        var first = ReadCommands(new FreeformHelperViewModel());
        var second = ReadCommands(new FreeformHelperViewModel());

        Assert.All(first, command => Assert.All(second, other => Assert.NotSame(command, other)));
    }

    [AvaloniaFact]
    public async Task CommandsNotifyAndFollowTheProjectSessionGateAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var commands = ReadCommands(vm);
        var notifications = commands.Select(ObserveAvailability).ToArray();
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.All(commands, command => Assert.True(command.CanExecute(null)));

        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.All(commands, command => Assert.False(command.CanExecute(null)));
            Assert.All(notifications, states => Assert.Contains(false, states));
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.All(commands, command => Assert.True(command.CanExecute(null)));
        Assert.All(notifications, states => Assert.Contains(true, states));
        Assert.All(commands.Zip(ReadCommands(vm)), pair => Assert.Same(pair.First, pair.Second));
    }

    [AvaloniaFact]
    public async Task ClearCombinedCadPadsExecutesOnlyWhenTheSessionIsReadyAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.Equal("Ready.", vm.StatusText);

            vm.ClearCombinedCadPadsCommand.Execute(null);

            Assert.Equal("Ready.", vm.StatusText);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        Assert.Equal("DXF combine: no combined pad.", vm.StatusText);
    }

    private static PropertyInfo GetPublicCommandProperty(string name)
    {
        var property = typeof(FreeformHelperViewModel).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        return property;
    }

    private static IRelayCommand ReadCommand(FreeformHelperViewModel vm, string name) =>
        Assert.IsAssignableFrom<IRelayCommand>(GetPublicCommandProperty(name).GetValue(vm));

    private static IRelayCommand[] ReadCommands(FreeformHelperViewModel vm) =>
        _expectedCommands.Select(expected => ReadCommand(vm, expected.Name)).ToArray();

    private static void AssertPairwiseDistinct(IRelayCommand[] commands)
    {
        for (var index = 0; index < commands.Length; index++)
        {
            for (var other = index + 1; other < commands.Length; other++)
            {
                Assert.NotSame(commands[index], commands[other]);
            }
        }
    }

    private static List<bool> ObserveAvailability(IRelayCommand command)
    {
        List<bool> states = [];
        command.CanExecuteChanged += (_, _) => states.Add(command.CanExecute(null));
        return states;
    }
}
