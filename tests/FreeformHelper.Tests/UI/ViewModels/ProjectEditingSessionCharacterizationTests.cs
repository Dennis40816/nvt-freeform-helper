// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectEditingSessionCharacterizationTests
{
    // Public command names in the pre-refactor registration order, independent of reflection enumeration order.
    private static readonly string[] _commandOrder =
    [
        nameof(FreeformHelperViewModel.ExportNotchCommand),
        nameof(FreeformHelperViewModel.ShowNotchDetailCommand),
        nameof(FreeformHelperViewModel.UseSelectedRegularForNotchValidationCommand),
        nameof(FreeformHelperViewModel.AnalyzeNotchValidationCommand),
        nameof(FreeformHelperViewModel.ClearNotchValidationCommand),
        nameof(FreeformHelperViewModel.FocusNotchValidationItemCommand),
        nameof(FreeformHelperViewModel.PreviousNotchPreviewStageCommand),
        nameof(FreeformHelperViewModel.NextNotchPreviewStageCommand),
        nameof(FreeformHelperViewModel.SaveProjectCommand),
        nameof(FreeformHelperViewModel.LoadProjectCommand),
        nameof(FreeformHelperViewModel.ClearSelectionCommand),
        nameof(FreeformHelperViewModel.QuickFocusCommand),
        nameof(FreeformHelperViewModel.ExportDxfVisibleCommand),
        nameof(FreeformHelperViewModel.ExportDxfAllCommand),
        nameof(FreeformHelperViewModel.ExportDxfLayerImageCommand),
        nameof(FreeformHelperViewModel.CheckDxfQualityCommand),
        nameof(FreeformHelperViewModel.SelectDxfOverlapPadsCommand),
        nameof(FreeformHelperViewModel.ClearDxfOverlapHighlightsCommand),
        nameof(FreeformHelperViewModel.ShowDxfOverlapDetailsCommand),
        nameof(FreeformHelperViewModel.AnalyzeIndexMappingCommand),
        nameof(FreeformHelperViewModel.OffsetSelectedCadOutputFwDiffIndicesCommand),
        nameof(FreeformHelperViewModel.SelectStep4DuplicateDiffGroupCommand),
        nameof(FreeformHelperViewModel.DeleteSelectedCadPadsCommand),
        nameof(FreeformHelperViewModel.RestoreLastDeletedCadPadsCommand),
        nameof(FreeformHelperViewModel.RestoreAllHiddenCadPadsCommand),
        nameof(FreeformHelperViewModel.CombineSelectedCadPadsCommand),
        nameof(FreeformHelperViewModel.ClearCombinedCadPadsCommand),
        nameof(FreeformHelperViewModel.ResetAllDxfEditsCommand),
        nameof(FreeformHelperViewModel.OpenHiddenDxfEditChangeListCommand),
        nameof(FreeformHelperViewModel.OpenDuplicateDxfEditChangeListCommand),
        nameof(FreeformHelperViewModel.OpenCombinedDxfEditChangeListCommand),
        nameof(FreeformHelperViewModel.OpenMovedDxfEditChangeListCommand),
        nameof(FreeformHelperViewModel.OpenRotatedDxfEditChangeListCommand),
        nameof(FreeformHelperViewModel.MoveSelectedCadPadsToLayerCommand),
        nameof(FreeformHelperViewModel.CreateLayerAndMoveSelectedCadPadsCommand),
        nameof(FreeformHelperViewModel.RotateSelectedCadPadsCommand),
        nameof(FreeformHelperViewModel.OpenDxfCommand),
        nameof(FreeformHelperViewModel.MatchCommand),
        nameof(FreeformHelperViewModel.ImportRegularVisibilityMaskCommand),
        nameof(FreeformHelperViewModel.ClearRegularVisibilityMaskCommand),
        nameof(FreeformHelperViewModel.AutoDetectFreeformsCommand),
        nameof(FreeformHelperViewModel.ClearStep1ResultCommand),
        nameof(FreeformHelperViewModel.ClearStep2ResultCommand),
        nameof(FreeformHelperViewModel.ClearStep3ResultCommand),
        nameof(FreeformHelperViewModel.ClearStep4ResultCommand),
        nameof(FreeformHelperViewModel.ClearStep5ResultCommand),
        nameof(FreeformHelperViewModel.FitCommand),
        nameof(FreeformHelperViewModel.UndoCommand),
        nameof(FreeformHelperViewModel.SetFreeformNoneCommand),
        nameof(FreeformHelperViewModel.SetFreeformXCommand),
        nameof(FreeformHelperViewModel.SetFreeformYCommand),
        nameof(FreeformHelperViewModel.SetFreeformXYCommand),
    ];

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SavedProjectLoadPublishesAvailabilityInOrderAsync()
    {
        var path = TestFiles.NewTempFilePath("editing-session-", ".json");
        try
        {
            await new JsonProjectStore().SaveAsync(path, new ProjectFile(), TestContext.Current.CancellationToken);
            var vm = new FreeformHelperViewModel();
            var events = ObserveAvailability(vm);

            var result = await vm.LoadProjectFromPathAsync(path);

            Assert.True(result.IsLoaded);
            AssertCompleteAvailabilityCycle(events);
            AssertReady(vm);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CancelledPickerPublishesAvailabilityInOrderAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var events = ObserveAvailability(vm);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            AssertLoadingAvailability(events);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.False(vm.IsProjectEditingEnabled);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        AssertCompleteAvailabilityCycle(events);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ThrowingPickerRestoresReadyAndPublishesAvailabilityInOrderAsync()
    {
        var failure = new InvalidOperationException("Picker failed.");
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => Task.FromException<string?>(failure),
        };
        var events = ObserveAvailability(vm);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadProjectCommand.ExecuteAsync(null));

        Assert.Same(failure, thrown);
        AssertCompleteAvailabilityCycle(events);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SecondDirectLoadReturnsBusyWithoutPublishingAvailabilityAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var events = ObserveAvailability(vm);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            var result = await vm.LoadProjectFromPathAsync("unused-while-loading.json");

            Assert.Equal(ProjectLoadCommandResult.Failed("BUSY", "A project is already loading."), result);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.False(vm.IsProjectEditingEnabled);
            AssertLoadingAvailability(events);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        AssertCompleteAvailabilityCycle(events);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task DirectCommandExecutionWhileLoadingDoesNothingAndPreservesFalseSaveTaskAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveStarts = 0;
        var openStarts = 0;
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
            PickSaveProjectPathAsync = () =>
            {
                saveStarts++;
                return Task.FromResult<string?>(null);
            },
            PickOpenDxfPathAsync = () =>
            {
                openStarts++;
                return Task.FromResult<string?>(null);
            },
        };
        vm.InteractionState.SetSelection([17], []);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.ClearSelectionCommand.CanExecute(null));
            vm.ClearSelectionCommand.Execute(null);
            var save = vm.SaveProjectCommand.ExecuteAsync(null);
            await vm.OpenDxfCommand.ExecuteAsync(null);

            Assert.False(await Assert.IsAssignableFrom<Task<bool>>(save));
            Assert.Equal(0, saveStarts);
            Assert.Equal(0, openStarts);
            Assert.Equal([17], vm.InteractionState.SelectedCadIds);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        vm.ClearSelectionCommand.Execute(null);
        Assert.Empty(vm.InteractionState.SelectedCadIds);
        AssertReady(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task OwnedWindowCommandsReadTheRootAvailabilityPredicateAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var editing = new FreeformHelper.UI.Services.ProjectEditingCommands();
        editing.AttachProject(vm);
        var calls = 0;
        var command = editing.Create(() => calls++);
        var availability = new List<bool>();
        command.CanExecuteChanged += (_, _) => availability.Add(command.CanExecute(null));
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.Equal(vm.IsProjectEditingEnabled, editing.IsProjectEditingEnabled);
            Assert.False(command.CanExecute(null));
            command.Execute(null);
            Assert.Equal(0, calls);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(vm.IsProjectEditingEnabled);
        Assert.Equal(vm.IsProjectEditingEnabled, editing.IsProjectEditingEnabled);
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, calls);
        Assert.Equal([false, true], availability);
    }

    private static List<string> ObserveAvailability(FreeformHelperViewModel vm)
    {
        var events = new List<string>();
        // Observe the availability batch, excluding AsyncRelayCommand's own running-state notifications.
        var publishingAvailability = false;
        vm.PropertyChanging += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.SessionMode))
            {
                events.Add($"PropertyChanging:{change.PropertyName}:{vm.SessionMode}:{vm.IsProjectEditingEnabled}");
            }
        };
        vm.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(vm.SessionMode)) publishingAvailability = true;
            if (change.PropertyName is nameof(vm.SessionMode) or nameof(vm.IsProjectEditingEnabled))
            {
                events.Add($"PropertyChanged:{change.PropertyName}:{vm.SessionMode}:{vm.IsProjectEditingEnabled}");
            }
        };
        foreach (var property in typeof(FreeformHelperViewModel).GetProperties()
                     .Where(property => typeof(IRelayCommand).IsAssignableFrom(property.PropertyType)))
        {
            Observe((IRelayCommand)property.GetValue(vm)!, property.Name);
        }
        return events;

        void Observe(IRelayCommand command, string name)
        {
            command.CanExecuteChanged += (_, _) =>
            {
                if (!publishingAvailability) return;
                events.Add($"CanExecuteChanged:{name}:{vm.SessionMode}:{vm.IsProjectEditingEnabled}");
                if (name == nameof(vm.SetFreeformXYCommand)) publishingAvailability = false;
            };
        }
    }

    private static void AssertLoadingAvailability(List<string> events)
    {
        Assert.Equal(ExpectedAvailabilityChange("Ready:True", "LoadingProject:False"), events);
    }

    private static void AssertCompleteAvailabilityCycle(List<string> events)
    {
        var expected = ExpectedAvailabilityChange("Ready:True", "LoadingProject:False")
            .Concat(ExpectedAvailabilityChange("LoadingProject:False", "Ready:True"));
        Assert.Equal(expected, events);
    }

    private static IEnumerable<string> ExpectedAvailabilityChange(string before, string after)
    {
        return new[]
        {
            $"PropertyChanging:SessionMode:{before}",
            $"PropertyChanged:SessionMode:{after}",
            $"PropertyChanged:IsProjectEditingEnabled:{after}",
        }.Concat(_commandOrder.Select(name => $"CanExecuteChanged:{name}:{after}"));
    }

    private static void AssertReady(FreeformHelperViewModel vm)
    {
        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.True(vm.IsProjectEditingEnabled);
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
        Assert.True(vm.LoadProjectCommand.CanExecute(null));
        Assert.True(vm.ClearSelectionCommand.CanExecute(null));
    }
}
