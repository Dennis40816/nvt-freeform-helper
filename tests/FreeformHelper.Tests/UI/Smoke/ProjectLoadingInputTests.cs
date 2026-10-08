using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadingInputTests
{
    [AvaloniaFact]
    public async Task LoadPickerAwait_BlocksCommandsAndRestoresReadyAfterCancellation()
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel { PickLoadProjectPathAsync = () => release.Task };
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.False(vm.SaveProjectCommand.CanExecute(null));
            Assert.False(vm.LoadProjectCommand.CanExecute(null));
            Assert.False(vm.RebuildGridCommand.CanExecute(null));
        }
        finally
        {
            release.TrySetResult(null);
            await load;
        }

        Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
        Assert.True(vm.IsProjectEditingEnabled);
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
        Assert.True(vm.LoadProjectCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task FailedProjectLoad_RestoresReadyAndCommandAvailability()
    {
        var path = Path.Combine(Path.GetTempPath(), $"loading-invalid-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{");
            var vm = new FreeformHelperViewModel();
            var result = await vm.LoadProjectFromPathAsync(path);
            Assert.False(result.IsLoaded);
            Assert.Equal("LOAD_FAILED", result.Code);
            Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
            Assert.True(vm.IsProjectEditingEnabled);
            Assert.True(vm.SaveProjectCommand.CanExecute(null));
            Assert.True(vm.LoadProjectCommand.CanExecute(null));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [AvaloniaFact]
    public async Task ProjectLoad_BlocksEditingAtFrameAwaitAndRestoresItAfterCompletion()
    {
        var path = Path.Combine(Path.GetTempPath(), $"loading-input-{Guid.NewGuid():N}.json");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel();
        var commandStates = new List<bool>();
        vm.RebuildGridCommand.CanExecuteChanged += (_, _) => commandStates.Add(vm.RebuildGridCommand.CanExecute(null));
        var scopeField = typeof(FreeformHelperViewModel).GetField("_cadLoadOverlayScope", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(scopeField);
        scopeField.SetValue(vm, new LoadingScopeCoordinator(() =>
        {
            entered.TrySetResult();
            return release.Task;
        }, TimeSpan.Zero));
        var view = new FreeformHelperView { DataContext = vm };
        var window = new Window { Width = 1200, Height = 800, Content = view };
        Task? load = null;
        var saveCount = 0;
        try
        {
            JsonProjectStore.Save(path, new ProjectFile());
            window.Show();
            var editor = view.GetVisualDescendants().OfType<CheckBox>()
                .Single(c => Equals(c.Content, "Only closed polylines"));
            Assert.True(editor.Focus());
            var openSettings = typeof(FreeformHelperView).GetMethod("OpenSettingsWindowCore", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(openSettings);
            openSettings.Invoke(view, [null, "test"]);
            var settingsField = typeof(FreeformHelperView).GetField("_settingsWindow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(settingsField);
            var settingsWindow = Assert.IsType<SettingsWindow>(settingsField.GetValue(view));
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            vm.PickSaveProjectPathAsync = () =>
            {
                saveCount++;
                return Task.FromResult<string?>(null);
            };
            load = vm.LoadProjectCommand.ExecuteAsync(null);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var loadingField = typeof(FreeformHelperViewModel).GetField("_isLoadingSettings", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(loadingField);
            Assert.True(Assert.IsType<bool>(loadingField.GetValue(vm)));
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            Assert.False(view.IsEnabled);
            Assert.False(editor.IsEffectivelyEnabled);
            Assert.False(settingsWindow.IsEnabled);
            Assert.Contains(false, commandStates);
            var draft = Assert.IsType<SettingsWindowViewModel>(settingsWindow.DataContext);
            Assert.False(draft.SaveCommand.CanExecute(null));
            var value = vm.ImportOnlyClosedPolylines;
            draft.ImportOnlyClosedPolylines = !value;
            vm.ApplySettingsWindowDraft(draft);
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            var point = editor.TranslatePoint(new Avalonia.Rect(editor.Bounds.Size).Center, window);
            Assert.NotNull(point);
            window.MouseDown(point.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
            window.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
            window.KeyRelease(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
            var canvas = view.GetVisualDescendants().OfType<FreeformHelper.UI.Controls.PadCanvas>().Single();
            var selectionField = typeof(FreeformHelper.UI.Controls.PadCanvas).GetField("_selectedCadIds", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(selectionField);
            var selected = Assert.IsType<HashSet<int>>(selectionField.GetValue(canvas));
            selected.Add(17);
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Assert.Contains(17, selected);
            foreach (var property in typeof(FreeformHelperViewModel).GetProperties()
                         .Where(p => typeof(IRelayCommand).IsAssignableFrom(p.PropertyType)))
            {
                var command = Assert.IsAssignableFrom<IRelayCommand>(property.GetValue(vm));
                Assert.False(command.CanExecute(null), property.Name);
            }

            await vm.SaveProjectCommand.ExecuteAsync(null);
            await vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.Equal(0, saveCount);
            Assert.Equal(value, vm.ImportOnlyClosedPolylines);
            Assert.False(vm.CanUndo);
            Assert.False(vm.HasUnsavedChanges);

            release.SetResult();
            await load;
            Assert.Equal(ProjectSessionMode.Ready, vm.SessionMode);
            Assert.True(view.IsEnabled);
            Assert.True(settingsWindow.IsEnabled);
            Assert.True(draft.SaveCommand.CanExecute(null));
            Assert.Contains(true, commandStates);
            settingsWindow.Close();
            window.Activate();
            Assert.True(editor.IsEffectivelyEnabled);
            Assert.True(vm.SaveProjectCommand.CanExecute(null));
            Assert.True(vm.LoadProjectCommand.CanExecute(null));
            Assert.True(editor.Focus());
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.NotEqual(value, vm.ImportOnlyClosedPolylines);
            Assert.True(vm.CanUndo);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            release.TrySetResult();
            if (load is not null) await load;
            window.Close();
            File.Delete(path);
        }
    }
}
