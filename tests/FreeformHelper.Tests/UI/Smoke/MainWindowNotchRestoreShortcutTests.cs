// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowNotchRestoreShortcutTests
{
    [AvaloniaFact]
    public async Task CtrlShiftE_WithoutHiddenExportSelection_DoesNotHandleKeyOrChangeChecksAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        Assert.False(SendRestoreKey(fixture).Handled);
        Assert.Empty(fixture.Window.OwnedWindows.OfType<NotchExportSelectionWindow>());
        var selection = CreateSelection(fixture);
        var open = fixture.Helper.OpenNotchExportSelectionAsync;
        Assert.NotNull(open);
        var result = open(selection);
        var window = Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportSelectionWindow>());
        try
        {
            await HeadlessUiPump.FlushQueueAsync();
            selection.Rows[0].IsSelected = false;
            var checks = selection.Rows.Select(static row => row.IsSelected).ToArray();
            Assert.Contains(false, checks);
            Assert.Contains(true, checks);
            var selectedRows = selection.BuildSelectedTable().Rows.ToArray();
            fixture.FocusShortcutTarget();
            Assert.False(SendRestoreKey(fixture).Handled);
            Assert.True(window.IsVisible);
            Assert.Same(selection, window.DataContext);
            Assert.Equal(checks, selection.Rows.Select(static row => row.IsSelected));
            Assert.Equal(selectedRows, selection.BuildSelectedTable().Rows);
            Assert.Same(window, Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportSelectionWindow>()));
            Assert.False(result.IsCompleted);
        }
        finally
        {
            window.Close();
            await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    [AvaloniaFact]
    public async Task CtrlShiftE_AfterInspectHide_RestoresSameWindowAndPreservesCheckedRowsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var selection = CreateSelection(fixture);
        var open = fixture.Helper.OpenNotchExportSelectionAsync;
        Assert.NotNull(open);
        var result = open(selection);
        var window = Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportSelectionWindow>());
        try
        {
            await HeadlessUiPump.FlushQueueAsync();
            selection.Rows[0].IsSelected = false;
            var checks = selection.Rows.Select(static row => row.IsSelected).ToArray();
            Assert.Contains(false, checks);
            Assert.Contains(true, checks);
            var selectedRows = selection.BuildSelectedTable().Rows.ToArray();
            Assert.True(window.IsVisible);
            Assert.True(selection.HidePanelForInspectCommand.CanExecute(null));

            selection.HidePanelForInspectCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(window.IsVisible);
            Assert.False(result.IsCompleted);
            fixture.FocusShortcutTarget();
            var restore = SendRestoreKey(fixture);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.True(restore.Handled);
            Assert.True(window.IsVisible);
            Assert.Same(selection, window.DataContext);
            Assert.Same(window, Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportSelectionWindow>()));
            Assert.Equal(checks, selection.Rows.Select(static row => row.IsSelected));
            Assert.Equal(selectedRows, selection.BuildSelectedTable().Rows);
            Assert.False(result.IsCompleted);
            fixture.FocusShortcutTarget();
            Assert.False(SendRestoreKey(fixture).Handled);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
            await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        }
    }

    private static NotchExportSelectionViewModel CreateSelection(MainWindowGlobalInputFixture fixture) => new(new NotchTable(
    [
        new(0, 0, 0, fixture.Helper.CadPads[0].Id, new NotchV22Node(0, 95, 1, 5, 65535, 0, 0), "first transfer"),
        new(0, 1, 1, fixture.Helper.CadPads[1].Id, new NotchV22Node(1, 90, 2, 10, 65535, 0, 0), "second transfer"),
    ]));

    private static KeyEventArgs SendRestoreKey(MainWindowGlobalInputFixture fixture) =>
        fixture.SendKey(Key.E, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.E, "E");
}
