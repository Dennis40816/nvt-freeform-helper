using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SaveProjectShortcutTests
{
    [AvaloniaFact]
    public Task MenuSave_BlocksShortcutSave() => AssertSavesAreSerializedAsync(shortcutFirst: false);

    [AvaloniaFact]
    public Task ShortcutSave_BlocksMenuSave() => AssertSavesAreSerializedAsync(shortcutFirst: true);

    [AvaloniaFact]
    public async Task SaveCommand_PreservesSaveResult()
    {
        var path = Path.Combine(Path.GetTempPath(), $"save-command-{Guid.NewGuid():N}.json");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickSaveProjectPathAsync = () => Task.FromResult<string?>(path),
                ConfirmEmbedDxfAsync = () => Task.FromResult(false),
            };
            var task = vm.SaveProjectCommand.ExecuteAsync(null);
            await task;
            Assert.True(await Assert.IsAssignableFrom<Task<bool>>(task));
            Assert.True(File.Exists(path));

            vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(null);
            var cancelled = vm.SaveProjectCommand.ExecuteAsync(null);
            await cancelled;
            Assert.False(await Assert.IsAssignableFrom<Task<bool>>(cancelled));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task AssertSavesAreSerializedAsync(bool shortcutFirst)
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saveCount = 0;
        var vm = new FreeformHelperViewModel
        {
            PickSaveProjectPathAsync = () =>
            {
                saveCount++;
                return release.Task;
            },
        };
        var view = new FreeformHelperView { DataContext = vm };
        var first = shortcutFirst ? SaveFromShortcutAsync(view, vm) : vm.SaveProjectCommand.ExecuteAsync(null);
        Task? second = null;
        try
        {
            Assert.Equal(1, saveCount);
            Assert.True(vm.SaveProjectCommand.IsRunning);
            Assert.False(vm.SaveProjectCommand.CanExecute(null));
            if (shortcutFirst)
            {
                // MenuItem checks CanExecute before invoking its bound command.
                if (vm.SaveProjectCommand.CanExecute(null))
                {
                    second = vm.SaveProjectCommand.ExecuteAsync(null);
                }
            }
            else
            {
                second = SaveFromShortcutAsync(view, vm);
                Assert.True(second.IsCompleted);
            }

            Assert.Equal(1, saveCount);
        }
        finally
        {
            release.TrySetResult(null);
            await first;
            if (second is not null) await second;
        }

        Assert.False(vm.SaveProjectCommand.IsRunning);
        Assert.True(vm.SaveProjectCommand.CanExecute(null));
        Assert.Equal(1, saveCount);
    }

    private static Task SaveFromShortcutAsync(FreeformHelperView view, FreeformHelperViewModel vm)
    {
        var method = typeof(FreeformHelperView).GetMethod("SaveProjectFromShortcutAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method.Invoke(view, [vm]));
    }
}
