// Copyright (c) 2026 Dennis Liu. All rights reserved.

using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectSessionOwnershipCharacterizationTests
{
    // TestAppSettingsIsolation redirects default stores before any view model is constructed.
    // LoadingModeTestScope additionally gives file-backed workflows their own app settings store.
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void DirtyAssignmentsPublishOldThenNewValuesThroughTheRootOnlyWhenChanged()
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);
        var events = new List<(object? Sender, string Kind, bool Value)>();
        vm.PropertyChanging += (sender, change) =>
        {
            if (change.PropertyName == nameof(vm.HasUnsavedChanges))
                events.Add((sender, "changing", vm.HasUnsavedChanges));
        };
        vm.PropertyChanged += (sender, change) =>
        {
            if (change.PropertyName == nameof(vm.HasUnsavedChanges))
                events.Add((sender, "changed", vm.HasUnsavedChanges));
        };

        vm.HasUnsavedChanges = false;
        vm.HasUnsavedChanges = true;
        vm.HasUnsavedChanges = true;
        vm.HasUnsavedChanges = false;
        vm.HasUnsavedChanges = false;

        Assert.Equal(
            new[] { ("changing", false), ("changed", true), ("changing", true), ("changed", false) },
            events.Select(entry => (entry.Kind, entry.Value)));
        Assert.All(events, entry => Assert.Same(vm, entry.Sender));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ClosePromptTracksDirtyHistoryAndDxfOpeningClearsTheSavedPathAsync()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveWithCadAsync();
        var vm = files.CreateViewModel();
        vm.HasUnsavedChanges = false;
        Assert.False(vm.ShouldPromptSaveOnExit());
        vm.HasUnsavedChanges = true;
        Assert.True(vm.ShouldPromptSaveOnExit());

        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.ShouldPromptSaveOnExit());
        var document = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);

        await vm.OpenDxfCommand.ExecuteAsync(null);

        Assert.Same(document, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.NotEmpty(vm.CadPads);
        vm.HasUnsavedChanges = false;
        Assert.True(vm.ShouldPromptSaveOnExit());
        Assert.True(await vm.SaveProjectAsync());
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.ShouldPromptSaveOnExit());
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RepeatedLoadsReplaceDocumentsButRetainCommandsAndWindowGuardIdentityAsync()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        var save = vm.SaveProjectCommand;
        var load = vm.LoadProjectCommand;
        var dialogs = vm.ProjectDialogs;
        var original = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        var editing = new FreeformHelper.UI.Services.ProjectEditingCommands();
        editing.AttachProject(vm);
        var guard = editing.Create(() => { });
        var availability = new List<bool>();
        guard.CanExecuteChanged += (_, _) => availability.Add(guard.CanExecute(null));

        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        var first = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);

        Assert.NotSame(original, first);
        Assert.NotSame(first, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.Same(save, vm.SaveProjectCommand);
        Assert.Same(load, vm.LoadProjectCommand);
        Assert.Same(dialogs, vm.ProjectDialogs);
        Assert.Equal([false, true, false, true], availability);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SettingsResetRetainsTheLoadedDocumentIdentityAsync()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        var document = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);

        vm.ResetAllSettingsToDefaults();
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Same(document, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.ShouldPromptSaveOnExit());
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ReplacementPromptNeedsSessionContentAsWellAsDirtyHistoryAsync()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        var prompts = 0;
        vm.ProjectDialogs.ConfirmSaveBeforeLoadAsync = _ =>
        {
            prompts++;
            return Task.FromResult<bool?>(null);
        };
        vm.HasUnsavedChanges = true;

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0, prompts);
        Assert.False(vm.HasUnsavedChanges);
        var loaded = FreeformHelperViewModelPrivateAccess.ProjectFile(vm);
        vm.HasUnsavedChanges = true;
        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(1, prompts);
        Assert.Same(loaded, FreeformHelperViewModelPrivateAccess.ProjectFile(vm));
        Assert.True(vm.HasUnsavedChanges);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CancelledPickerRestoresAvailabilityAndCommandRegistrationOrderAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var trace = new P2SessionAvailabilityTrace(vm);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            trace.AssertLoading(vm);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        trace.AssertComplete(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ThrowingPickerRestoresAvailabilityAndCommandRegistrationOrderAsync()
    {
        var failure = new InvalidOperationException("P2 picker failure.");
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => Task.FromException<string?>(failure),
        };
        var trace = new P2SessionAvailabilityTrace(vm);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadProjectCommand.ExecuteAsync(null));

        Assert.Same(failure, thrown);
        trace.AssertComplete(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SecondDirectLoadReturnsBusyWithoutAnotherAvailabilityCycleAsync()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel
        {
            PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken),
        };
        var trace = new P2SessionAvailabilityTrace(vm);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            var busy = await vm.LoadProjectFromPathAsync("p2-unused-while-loading.json");

            Assert.Equal(ProjectLoadCommandResult.Failed("BUSY", "A project is already loading."), busy);
            Assert.Null(vm.TryStartSaveProjectCommandAsync());
            var blockedSave = vm.SaveProjectCommand.ExecuteAsync(null);
            Assert.False(await Assert.IsAssignableFrom<Task<bool>>(blockedSave));
            trace.AssertLoading(vm);
        }
        finally
        {
            release.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        trace.AssertComplete(vm);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task SaveStartRetainsBooleanResultAndRejectsAnAlreadyRunningCommandAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        vm.PickSaveProjectPathAsync = () =>
        {
            starts++;
            return release.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var save = vm.TryStartSaveProjectCommandAsync();
        Assert.NotNull(save);
        try
        {
            Assert.Same(save, vm.SaveProjectCommand.ExecutionTask);
            Assert.Null(vm.TryStartSaveProjectCommandAsync());
            Assert.Equal(1, starts);
        }
        finally
        {
            release.TrySetResult(files.PathFor("project.json"));
            await save.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await save);
        Assert.True(File.Exists(files.PathFor("project.json")));
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(null);
        var cancelled = vm.SaveProjectCommand.ExecuteAsync(null);
        Assert.False(await Assert.IsAssignableFrom<Task<bool>>(cancelled));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task OpenSettingsWindowAndOwnedCommandGuardObserveRootAvailabilityAsync()
    {
        var vm = new FreeformHelperViewModel();
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Avalonia.Controls.Window { Content = view };
        var editing = new FreeformHelper.UI.Services.ProjectEditingCommands();
        editing.AttachProject(vm);
        var calls = 0;
        var command = editing.Create(() => calls++);
        var observed = new List<(object? Sender, bool Enabled)>();
        vm.PropertyChanged += (sender, change) =>
        {
            if (change.PropertyName == nameof(vm.IsProjectEditingEnabled))
                observed.Add((sender, vm.IsProjectEditingEnabled));
        };
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        SettingsWindow? settings = null;
        Task? load = null;
        try
        {
            owner.Show();
            FreeformHelperViewPrivateAccess.OpenSettingsWindowCore(view, null, "p2-characterization");
            settings = Assert.IsType<SettingsWindow>(FreeformHelperViewPrivateAccess.SettingsWindow(view));
            vm.PickLoadProjectPathAsync = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
            load = vm.LoadProjectCommand.ExecuteAsync(null);

            Assert.False(settings.IsEnabled);
            Assert.False(editing.IsProjectEditingEnabled);
            Assert.False(command.CanExecute(null));
            command.Execute(null);
            Assert.Equal(0, calls);
            release.SetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);

            Assert.True(settings.IsEnabled);
            Assert.True(editing.IsProjectEditingEnabled);
            command.Execute(null);
            Assert.Equal(1, calls);
            Assert.Equal([false, true], observed.Select(entry => entry.Enabled));
            Assert.All(observed, entry => Assert.Same(vm, entry.Sender));
        }
        finally
        {
            release.TrySetResult(null);
            if (load is not null) await load.WaitAsync(TestContext.Current.CancellationToken);
            settings?.Close();
            owner.Close();
        }
    }

    private sealed class P2SessionAvailabilityTrace
    {
        private readonly List<string> _events = [];
        private readonly List<object?> _senders = [];
        // UI thread only. Exclude AsyncRelayCommand's separate running-state notifications.
        private bool _publishing;

        public P2SessionAvailabilityTrace(FreeformHelperViewModel vm)
        {
            vm.PropertyChanging += (sender, change) =>
            {
                if (change.PropertyName != nameof(vm.SessionMode)) return;
                _senders.Add(sender);
                _events.Add($"changing:{vm.SessionMode}:{vm.IsProjectEditingEnabled}");
            };
            vm.PropertyChanged += (sender, change) =>
            {
                if (change.PropertyName == nameof(vm.SessionMode)) _publishing = true;
                if (change.PropertyName is not (nameof(vm.SessionMode) or nameof(vm.IsProjectEditingEnabled))) return;
                _senders.Add(sender);
                _events.Add($"changed:{change.PropertyName}:{vm.SessionMode}:{vm.IsProjectEditingEnabled}");
            };
            // The commands on both sides of the project pair fix its position in the existing registry.
            Observe(vm.NextNotchPreviewStageCommand, nameof(vm.NextNotchPreviewStageCommand));
            Observe(vm.SaveProjectCommand, nameof(vm.SaveProjectCommand));
            Observe(vm.LoadProjectCommand, nameof(vm.LoadProjectCommand));
            Observe(vm.ClearSelectionCommand, nameof(vm.ClearSelectionCommand));
            Observe(vm.SetFreeformXYCommand, nameof(vm.SetFreeformXYCommand));
        }

        public void AssertLoading(FreeformHelperViewModel vm)
        {
            Assert.Equal(Expected("Ready:True", "LoadingProject:False"), _events);
            Assert.All(_senders, sender => Assert.Same(vm, sender));
            Assert.False(vm.IsProjectEditingEnabled);
        }

        public void AssertComplete(FreeformHelperViewModel vm)
        {
            Assert.Equal(Expected("Ready:True", "LoadingProject:False")
                .Concat(Expected("LoadingProject:False", "Ready:True")), _events);
            Assert.All(_senders, sender => Assert.Same(vm, sender));
            Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
            Assert.True(vm.IsProjectEditingEnabled);
            Assert.True(vm.SaveProjectCommand.CanExecute(null));
            Assert.True(vm.LoadProjectCommand.CanExecute(null));
        }

        private void Observe(IRelayCommand command, string name)
        {
            command.CanExecuteChanged += (_, _) =>
            {
                if (!_publishing) return;
                _events.Add(name);
                if (name == nameof(FreeformHelperViewModel.SetFreeformXYCommand)) _publishing = false;
            };
        }

        private static string[] Expected(string before, string after) =>
        [
            $"changing:{before}",
            $"changed:SessionMode:{after}",
            $"changed:IsProjectEditingEnabled:{after}",
            nameof(FreeformHelperViewModel.NextNotchPreviewStageCommand),
            nameof(FreeformHelperViewModel.SaveProjectCommand),
            nameof(FreeformHelperViewModel.LoadProjectCommand),
            nameof(FreeformHelperViewModel.ClearSelectionCommand),
            nameof(FreeformHelperViewModel.SetFreeformXYCommand),
        ];
    }
}
