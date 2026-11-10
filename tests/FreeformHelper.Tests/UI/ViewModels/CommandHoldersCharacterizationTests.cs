// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Reflection;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CommandHoldersCharacterizationTests
{
    private static readonly (string Name, Type DeclaredType)[] _expectedCommands =
    [
        ("ExportNotchCommand", typeof(IAsyncRelayCommand)),
        ("ShowNotchDetailCommand", typeof(IAsyncRelayCommand)),
        ("UseSelectedRegularForNotchValidationCommand", typeof(IRelayCommand)),
        ("AnalyzeNotchValidationCommand", typeof(IRelayCommand)),
        ("ClearNotchValidationCommand", typeof(IRelayCommand)),
        ("FocusNotchValidationItemCommand", typeof(IRelayCommand<FreeformHelperViewModel.NotchValidationDisplayItem>)),
        ("PreviousNotchPreviewStageCommand", typeof(IRelayCommand)),
        ("NextNotchPreviewStageCommand", typeof(IRelayCommand)),
        ("SaveProjectCommand", typeof(IAsyncRelayCommand)),
        ("LoadProjectCommand", typeof(IAsyncRelayCommand)),
        ("ClearSelectionCommand", typeof(IRelayCommand)),
        ("QuickFocusCommand", typeof(IRelayCommand)),
        ("OpenDxfCommand", typeof(IAsyncRelayCommand)),
        ("MatchCommand", typeof(IAsyncRelayCommand)),
        ("ImportRegularVisibilityMaskCommand", typeof(IAsyncRelayCommand)),
        ("ClearRegularVisibilityMaskCommand", typeof(IRelayCommand)),
        ("AutoDetectFreeformsCommand", typeof(IAsyncRelayCommand)),
        ("ClearStep1ResultCommand", typeof(IRelayCommand)),
        ("ClearStep2ResultCommand", typeof(IRelayCommand)),
        ("ClearStep3ResultCommand", typeof(IRelayCommand)),
        ("ClearStep4ResultCommand", typeof(IRelayCommand)),
        ("ClearStep5ResultCommand", typeof(IRelayCommand)),
        ("FitCommand", typeof(IRelayCommand)),
        ("UndoCommand", typeof(IRelayCommand)),
        ("SetFreeformNoneCommand", typeof(IRelayCommand)),
        ("SetFreeformXCommand", typeof(IRelayCommand)),
        ("SetFreeformYCommand", typeof(IRelayCommand)),
        ("SetFreeformXYCommand", typeof(IRelayCommand)),
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
        vm.ShowCad = !vm.ShowCad;
        var commands = ReadCommands(vm);
        var notifications = commands.Select(command => ObserveAvailability(command, GetParameter(command))).ToArray();
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.All(commands, command => Assert.True(command.CanExecute(GetParameter(command))));
        Assert.False(vm.FocusNotchValidationItemCommand.CanExecute(null));

        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.All(commands, command => Assert.False(command.CanExecute(GetParameter(command))));
            Assert.False(vm.FocusNotchValidationItemCommand.CanExecute(null));
            Assert.All(notifications, states => Assert.Contains(false, states));
            foreach (var states in notifications)
            {
                states.Clear();
            }
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.All(commands, command => Assert.True(command.CanExecute(GetParameter(command))));
        Assert.False(vm.FocusNotchValidationItemCommand.CanExecute(null));
        Assert.All(notifications, states => Assert.Contains(true, states));
        Assert.All(commands.Zip(ReadCommands(vm)), pair => Assert.Same(pair.First, pair.Second));
    }

    [AvaloniaFact]
    public void UndoCommandTracksPublicDisplayEditsAndNotifiesAvailability()
    {
        var vm = new FreeformHelperViewModel();
        var originalShowCad = vm.ShowCad;
        var notifications = ObserveAvailability(vm.UndoCommand, null);
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));

        vm.ShowCad = !originalShowCad;

        Assert.True(vm.CanUndo);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.True(Assert.Single(notifications));

        vm.UndoCommand.Execute(null);

        Assert.Equal(originalShowCad, vm.ShowCad);
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));
        Assert.Collection(notifications, available => Assert.True(available), available => Assert.False(available));
    }

    [AvaloniaFact]
    public async Task ClearStep1ResultExecutesOnlyWhenTheSessionIsReadyAsync()
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

            vm.ClearStep1ResultCommand.Execute(null);

            Assert.Equal("Ready.", vm.StatusText);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        vm.ClearStep1ResultCommand.Execute(null);
        Assert.Equal("Step 1 result cleared.", vm.StatusText);
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

    private static List<bool> ObserveAvailability(IRelayCommand command, object? parameter)
    {
        List<bool> states = [];
        command.CanExecuteChanged += (_, _) => states.Add(command.CanExecute(parameter));
        return states;
    }

    private static FreeformHelperViewModel.NotchValidationDisplayItem? GetParameter(IRelayCommand command) =>
        command is IRelayCommand<FreeformHelperViewModel.NotchValidationDisplayItem> ? CreateValidationItem() : null;

    private static FreeformHelperViewModel.NotchValidationDisplayItem CreateValidationItem()
    {
        return new FreeformHelperViewModel.NotchValidationDisplayItem(
            KindText: "DIRECT",
            RowText: "#1",
            VersionText: "2.2",
            IcText: "IC 1",
            SourceText: "REG 11 / diff 10",
            TargetText: "REG 11 / diff 10",
            RatioText: "100%",
            AreaText: "100",
            ValuesText: string.Empty,
            CommentText: string.Empty,
            NoteText: string.Empty,
            SourceRegularPadId: 11,
            TargetRegularPadId: 11,
            CadPadId: 101,
            IcIndex: 0,
            SourceDiffIndex: 10,
            TargetDiffIndex: 10,
            IsDirect: true,
            IsIncoming: false,
            IsOutgoing: false);
    }
}
