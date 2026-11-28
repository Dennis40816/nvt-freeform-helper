// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowCadShortcutTests
{
    [AvaloniaFact]
    public async Task DeleteThenCtrlZ_HidesOnlySelectedCadPadsAndRestoresTheirIdentitiesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var selectedIds = originalIds.Take(2).ToArray();
        vm.ApplyCanvasSelection(selectedIds, []);
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));

        var delete = fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.True(delete.Handled);
        Assert.Equal(originalIds.Length - 2, vm.CadPads.Count);
        Assert.Equal(originalIds.Except(selectedIds), vm.CadPads.Select(static pad => pad.Id));
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.True(vm.UndoCommand.CanExecute(null));
        var undo = fixture.SendKey(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
        await vm.WaitForGridRebuildIdleAsync();

        Assert.True(undo.Handled);
        Assert.Equal(originalIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Equal(0, vm.DeletedCadPadCount);
    }

    [AvaloniaFact]
    public async Task Delete_WithTextBoxFocus_EditsTextAndPreservesSelectedCadPadsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        vm.ApplyCanvasSelection(originalIds.Take(2).ToArray(), []);
        var editor = new TextBox
        {
            Text = "notes",
            Width = 200,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        Assert.IsType<Grid>(fixture.Window.Content).Children.Add(editor);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.True(editor.Focus());
        editor.CaretIndex = 0;
        editor.SelectionStart = 0;
        editor.SelectionEnd = 0;
        Assert.Equal(2, vm.InteractionState.SelectedCadIds.Count);
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));

        fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Equal("otes", editor.Text);
        Assert.Equal(originalIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(originalIds.Take(2), vm.InteractionState.SelectedCadIds.Order());
    }

    [AvaloniaFact]
    public async Task DeleteAndCtrlZ_DuringLoadPickerAwait_PreservePadsAndPendingUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var originalIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        vm.ApplyCanvasSelection(originalIds.Take(2).ToArray(), []);
        fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        await vm.WaitForGridRebuildIdleAsync();
        var remainingId = Assert.Single(vm.CadPads).Id;
        vm.ApplyCanvasSelection([remainingId], []);
        Assert.Equal(2, vm.DeletedCadPadCount);
        Assert.True(vm.CanUndo);
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickLoadProjectPathAsync = () => picker.Task.WaitAsync(TestContext.Current.CancellationToken);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.IsProjectEditingEnabled);
            fixture.FocusShortcutTarget();

            var delete = fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

            Assert.False(delete.Handled);
            Assert.Equal(remainingId, Assert.Single(vm.CadPads).Id);
            Assert.Equal(2, vm.DeletedCadPadCount);
            var undo = fixture.SendKey(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
            Assert.False(undo.Handled);
            Assert.Equal(remainingId, Assert.Single(vm.CadPads).Id);
            Assert.Equal(2, vm.DeletedCadPadCount);
            Assert.True(vm.CanUndo);
        }
        finally
        {
            picker.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(vm.IsProjectEditingEnabled);
        fixture.FocusShortcutTarget();
        Assert.True(fixture.SendKey(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z").Handled);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Equal(originalIds, vm.CadPads.Select(static pad => pad.Id).Order());
        Assert.Equal(0, vm.DeletedCadPadCount);
    }
}
