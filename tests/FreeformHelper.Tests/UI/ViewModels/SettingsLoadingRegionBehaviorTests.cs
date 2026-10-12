// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.LoadingModeTestScope;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsLoadingRegionBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(nameof(FreeformHelperViewModel.ShowCad))]
    [InlineData(nameof(FreeformHelperViewModel.ImportOnlyClosedPolylines))]
    public async Task LoadProjectCommand_ViewOrImportSnapshot_SuppressesNestedHandlerAndRestoresIt(string notification)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm =>
        {
            vm.NotchThresholdQ7 = 32m;
            vm.ShowCad = false;
            vm.ImportOnlyClosedPolylines = false;
        });
        var vm = files.CreateViewModel();
        using var edit = OnNextChange(vm, notification, () =>
        {
            vm.NotchThresholdQ7 = 64m;
            Assert.Equal(25m, vm.NotchThresholdPercent);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        });

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(edit.WasInvoked);
        vm.NotchThresholdQ7 = 96m;
        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task SaveCommand_SettingsDraftApply_SuppressesNestedNotchHandlerAndRestoresIt()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        draft.XChannels = 6m;
        var percent = vm.NotchThresholdPercent;
        using var edit = OnNextChange(vm, nameof(vm.XChannels), () =>
        {
            vm.NotchThresholdQ7 = 64m;
            Assert.Equal(percent, vm.NotchThresholdPercent);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        });

        draft.SaveCommand.Execute(null);

        Assert.True(edit.WasInvoked);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync());
        vm.NotchThresholdQ7 = 96m;
        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task SaveCommand_LoadStartedInsideDraftApply_EndsWithNotchHandlerEnabled()
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(vm => vm.NotchThresholdQ7 = 32m);
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        draft.XChannels = 6m;
        Task load = Task.CompletedTask;
        using var start = OnNextChange(vm, nameof(vm.XChannels), () => load = vm.LoadProjectCommand.ExecuteAsync(null));

        draft.SaveCommand.Execute(null);
        await load;

        Assert.True(start.WasInvoked);
        Assert.True(vm.IsProjectEditingEnabled);
        vm.NotchThresholdQ7 = 96m;
        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task SaveCommand_NestedDefaultsReset_ClearsOuterLoadingModeBeforeApplyReturns()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var draft = vm.CreateSettingsWindowViewModel();
        draft.XChannels = 6m;
        using var reset = OnNextChange(vm, nameof(vm.XChannels), () =>
        {
            vm.CreateSettingsWindowViewModel().ResetAllSettingsToDefaults();
            Assert.False(vm.CanUndo);
            vm.NotchThresholdQ7 = 64m;
            Assert.Equal(50m, vm.NotchThresholdPercent);
            Assert.True(vm.CanUndo); // Looks like a defect: LoadSettingsToUi cleared the outer apply mode.
        });

        draft.SaveCommand.Execute(null);

        Assert.True(reset.WasInvoked);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
    }

    [AvaloniaFact]
    public void UndoCommand_InsideSettingsDraftApply_ConsumesExistingEntryWhileLoading()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.ShowCad = false;
        var draft = vm.CreateSettingsWindowViewModel();
        draft.MatchThreshold = 0.27m;
        using var undo = OnNextChange(vm, nameof(vm.MatchThreshold), () =>
        {
            vm.UndoCommand.Execute(null);
            Assert.True(vm.ShowCad);
            Assert.False(vm.CanUndo); // Current Undo guard is SessionMode, not the settings-loading flag.
            Assert.False(vm.HasUnsavedChanges);
        });
        vm.HasUnsavedChanges = false;

        draft.SaveCommand.Execute(null);

        Assert.True(undo.WasInvoked);
    }

    [AvaloniaFact]
    public void SelectedNotchCompensationModelOption_LegacySwitchRefresh_SuppressesNestedThresholdSync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var percent = vm.NotchThresholdPercent;
        var originalModel = vm.SelectedNotchCompensationModelOption;
        using var edit = OnNextChange(vm, nameof(vm.EnableToRegular), () =>
        {
            Assert.True(vm.CanUndo);
            vm.NotchThresholdQ7 = 64m;
            Assert.Equal(percent, vm.NotchThresholdPercent);
            Assert.False(vm.HasUnsavedChanges);
            Assert.True(vm.CanUndo);
        });

        vm.SelectedNotchCompensationModelOption = vm.NotchCompensationModelOptions.Single(
            option => option.Value == NotchCompensationModel.Disabled);

        Assert.True(edit.WasInvoked);
        vm.UndoCommand.Execute(null);
        Assert.Equal(originalModel, vm.SelectedNotchCompensationModelOption);
        Assert.Equal(64m, vm.NotchThresholdQ7);
        Assert.False(vm.CanUndo);
        vm.HasUnsavedChanges = false;
        vm.NotchThresholdQ7 = 96m;
        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Constructor_AppGeneralLayer_LoadsQuietlyAndLeavesNotchHandlerEnabled()
    {
        using var files = new LoadingModeTestScope();
        files.AppStore.Save(new AppGeneralSettingsDocument
        {
            View = new UiViewSnapshot { ShowCad = false },
            Behavior = new AppGeneralBehaviorSettings { ApplyVisualPreferencesOnProjectLoad = false },
        });

        var vm = new FreeformHelperViewModel(files.AppStore);

        Assert.False(vm.ShowCad);
        Assert.False(vm.ApplyAppVisualPreferencesOnProjectLoad);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        vm.NotchThresholdQ7 = 96m;
        Assert.Equal(75m, vm.NotchThresholdPercent);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task SaveProjectAsync_AppPreferenceEditedDuringLoad_DoesNotFlushSuppressedEdit()
    {
        using var files = new LoadingModeTestScope();
        files.AppStore.Save(new AppGeneralSettingsDocument { View = new UiViewSnapshot { GlobalFontSizePercent = 110 } });
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        using var edit = OnNextChange(vm, nameof(vm.RegularPads), () => vm.GlobalFontSizePercent = 125m);
        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.True(await vm.SaveProjectAsync());

        Assert.True(edit.WasInvoked);
        Assert.Equal(125m, vm.GlobalFontSizePercent);
        Assert.Equal(110, Assert.IsType<AppGeneralSettingsDocument>(files.AppStore.TryLoad()).View.GlobalFontSizePercent);
    }

    [AvaloniaFact]
    public async Task SaveProjectAsync_AppPreferenceEditedAfterLoad_FlushesDeferredEdit()
    {
        using var files = new LoadingModeTestScope();
        files.AppStore.Save(new AppGeneralSettingsDocument { View = new UiViewSnapshot { GlobalFontSizePercent = 110 } });
        await files.SaveAsync();
        var vm = files.CreateViewModel();
        await vm.LoadProjectCommand.ExecuteAsync(null);
        vm.GlobalFontSizePercent = 125m;

        Assert.True(await vm.SaveProjectAsync());

        Assert.Equal(125, Assert.IsType<AppGeneralSettingsDocument>(files.AppStore.TryLoad()).View.GlobalFontSizePercent);
    }
}

internal sealed class LoadingModeTestScope : IDisposable
{
    private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();
    private readonly string _dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 10, 20));
    private readonly string _invalidProjectPath = TestFiles.WriteTempCsv("{");

    public AppGeneralSettingsStore AppStore => new(PathFor("app-settings.json"));
    public string PathFor(string name) => Path.Combine(_directory.FullName, name);
    public string UnreadableProjectPath(bool invalidContent) => invalidContent ? _invalidProjectPath : PathFor("missing.json");

    public FreeformHelperViewModel CreateViewModel(string? loadPath = null) => new(AppStore)
    {
        ApplyAppVisualPreferencesOnProjectLoad = false,
        PickSaveProjectPathAsync = () => Task.FromResult<string?>(PathFor("project.json")),
        PickLoadProjectPathAsync = () => Task.FromResult<string?>(loadPath ?? PathFor("project.json")),
        PickOpenDxfPathAsync = () => Task.FromResult<string?>(_dxfPath),
        ConfirmEmbedDxfAsync = () => Task.FromResult(false),
    };

    public async Task SaveAsync(Action<FreeformHelperViewModel>? configure = null)
    {
        var vm = CreateViewModel();
        configure?.Invoke(vm);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.True(await vm.SaveProjectAsync());
    }

    public async Task SaveWithCadAsync()
    {
        var vm = CreateViewModel();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        Assert.True(await vm.SaveProjectAsync());
    }

    public void Dispose()
    {
        File.Delete(_dxfPath);
        File.Delete(_invalidProjectPath);
        _directory.Delete(recursive: true);
    }

    public static ConcurrentQueue<string?> ObserveChanges(FreeformHelperViewModel vm)
    {
        var changes = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, change) => changes.Enqueue(change.PropertyName);
        return changes;
    }

    public static NextChange OnNextChange(FreeformHelperViewModel vm, string propertyName, Action action) => new(vm, propertyName, action);

    public static NextChange OnNextChangeThrowingExpectedException(FreeformHelperViewModel vm, string propertyName, Exception exception) =>
        new(vm, propertyName, exception);

    internal sealed class NextChange : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly string _propertyName;
        private readonly Action _action;
        private readonly Exception? _expectedException;
        // Mutable observations are confined to the headless UI thread.
        private ExceptionDispatchInfo? _callbackFailure;

        public NextChange(FreeformHelperViewModel vm, string propertyName, Action action)
        {
            _vm = vm;
            _propertyName = propertyName;
            _action = action;
            _vm.PropertyChanged += OnChange;
        }

        public NextChange(FreeformHelperViewModel vm, string propertyName, Exception expectedException)
            : this(vm, propertyName, () => { })
        {
            _expectedException = expectedException;
        }

        public bool WasInvoked { get; private set; }

        private void OnChange(object? sender, PropertyChangedEventArgs change)
        {
            if (change.PropertyName != _propertyName) return;
            _vm.PropertyChanged -= OnChange;
            WasInvoked = true;
            // Only the dedicated injection factory bypasses callback failure recording.
            if (_expectedException is not null) throw _expectedException;
            try
            {
                _action();
            }
            catch (Exception exception)
            {
                _callbackFailure = ExceptionDispatchInfo.Capture(exception);
                throw;
            }
        }

        public void Dispose()
        {
            _vm.PropertyChanged -= OnChange;
            _callbackFailure?.Throw();
        }
    }
}
