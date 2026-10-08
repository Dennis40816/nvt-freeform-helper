using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Nvt.Core.Lifecycle;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed partial class ProjectLoadingSecondaryWindowTests
{
    private static readonly int[] HiddenIds = [10];
    private static readonly int[] AutoHiddenIds = [11];

    [AvaloniaTheory]
    [InlineData(DxfEditChangeKind.Hidden)]
    [InlineData(DxfEditChangeKind.Duplicate)]
    public async Task LoadPickerAwait_BlocksOpenDxfChangeListWithoutChangingHiddenUndoOrDirtyState(DxfEditChangeKind kind)
    {
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = new FreeformHelperViewModel();
        SetField(vm, "_cad", new CadPadSet(new[] { CreateCad(10), CreateCad(11), CreateCad(12) }));
        var hidden = GetField<HashSet<int>>(vm, "_hiddenCadPadIds");
        hidden.Add(10);
        var hiddenUndo = GetField<Stack<List<int>>>(vm, "_hiddenCadUndoStack");
        hiddenUndo.Push([10]);
        var duplicateIds = GetField<Dictionary<int, int>>(vm, "_duplicateCadPadIdToCanonicalId");
        duplicateIds[11] = 10;
        duplicateIds[12] = 10;
        var autoHidden = GetField<HashSet<int>>(vm, "_autoHiddenDuplicateCadPadIds");
        autoHidden.Add(11);
        var view = new FreeformHelperView { DataContext = vm };
        var owner = new Window { Content = view };
        DxfEditChangeListWindow? window = null;
        Task? load = null;
        try
        {
            owner.Show();
            await (kind == DxfEditChangeKind.Hidden
                ? vm.OpenHiddenDxfEditChangeListCommand
                : vm.OpenDuplicateDxfEditChangeListCommand).ExecuteAsync(null);
            window = GetField<DxfEditChangeListWindow>(view, "_dxfEditChangeListWindow");
            var changes = Assert.IsType<DxfEditChangeListViewModel>(window.DataContext);
            changes.AreAllVisibleRowsSelected = true;
            var entry = Assert.IsType<DxfEditChangeListEntry>(changes.SelectedEntry);
            IRelayCommand[] commands = [changes.ApplySelectedEntriesCommand, changes.ApplySelectedEntryCommand,
                changes.ApplyEntryCommand, changes.RestoreSelectedDuplicateEntriesCommand, changes.RehideSelectedDuplicateEntriesCommand];
            var before = commands.Select(command => command.CanExecute(entry)).ToArray();
            Assert.True(before[1]);
            Assert.True(before[2]);
            Assert.True(kind == DxfEditChangeKind.Hidden ? before[0] : before[3] && before[4]);
            var notifications = new int[commands.Length];
            for (var i = 0; i < commands.Length; i++)
            {
                var index = i;
                commands[i].CanExecuteChanged += (_, _) => notifications[index]++;
            }

            vm.PickLoadProjectPathAsync = () => release.Task;
            load = vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.Equal(ProjectSessionMode.LoadingProject, vm.SessionMode);
            foreach (var command in commands) Assert.False(command.CanExecute(entry));
            Assert.All(notifications, count => Assert.True(count > 0));
            Assert.False(window.IsEnabled);
            Assert.All(window.GetVisualDescendants().OfType<Button>(), button => Assert.False(button.IsEffectivelyEnabled));
            changes.AreAllVisibleRowsSelected = false;
            window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
            Assert.False(changes.AreAllVisibleRowsSelected);
            changes.AreAllVisibleRowsSelected = true;
            foreach (var command in commands) command.Execute(entry);
            Assert.Equal(HiddenIds, hidden.Order().ToArray());
            Assert.Equal(AutoHiddenIds, autoHidden.Order().ToArray());
            Assert.Equal(HiddenIds, Assert.Single(hiddenUndo));
            Assert.False(GetField<UndoService>(vm, "_undoService").CanUndo);
            Assert.False(vm.HasUnsavedChanges);

            release.SetResult(null);
            await load;
            Assert.True(window.IsEnabled);
            Assert.Equal(before, commands.Select(command => command.CanExecute(entry)).ToArray());
            Assert.All(notifications, count => Assert.True(count >= 2));
            if (kind == DxfEditChangeKind.Hidden) changes.ApplySelectedEntriesCommand.Execute(null);
            else changes.RestoreSelectedDuplicateEntriesCommand.Execute(null);
            Assert.Empty(kind == DxfEditChangeKind.Hidden ? hidden : autoHidden);
            Assert.True(vm.CanUndo);
            Assert.True(vm.HasUnsavedChanges);
        }
        finally
        {
            release.TrySetResult(null);
            if (load is not null) await load;
            window?.Close();
            owner.Close();
        }
    }

    private static CadPad CreateCad(int id) => new(id, $"CAD{id}", "L1", new Polygon2(new[]
    {
        new Point2(0, 0), new Point2(10, 0), new Point2(10, 10), new Point2(0, 10)
    }));

    private static T GetField<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(target));
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }
}
