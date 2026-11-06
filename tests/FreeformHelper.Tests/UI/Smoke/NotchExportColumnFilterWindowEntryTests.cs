// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using FreeformHelper.UI.Views.WorkspaceSections;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportColumnFilterWindowEntryTests
{
    [AvaloniaFact]
    public async Task SelectAll_ChecksEveryOptionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateDialogViewModel();
        var owner = new Window();
        var window = new NotchExportColumnFilterWindow { DataContext = viewModel };
        try
        {
            owner.Show();
            window.Show(owner);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Contains(viewModel.Options, static option => !option.IsSelected);

            await ClickButtonAsync(window, "Select all");

            Assert.All(viewModel.Options, static option => Assert.True(option.IsSelected));
            Assert.Equal("3/3 selected", viewModel.SummaryText);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Clear_UnchecksEveryOptionAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateDialogViewModel();
        var owner = new Window();
        var window = new NotchExportColumnFilterWindow { DataContext = viewModel };
        try
        {
            owner.Show();
            window.Show(owner);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Contains(viewModel.Options, static option => option.IsSelected);

            await ClickButtonAsync(window, "Clear");

            Assert.All(viewModel.Options, static option => Assert.False(option.IsSelected));
            Assert.Empty(viewModel.GetSelectedKeys());
            Assert.Equal("0/3 selected", viewModel.SummaryText);
            Assert.True(window.IsVisible);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Apply_ReturnsTrueAndPreservesCheckedStateAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var viewModel = CreateDialogViewModel();
        var owner = new Window();
        var window = new NotchExportColumnFilterWindow { DataContext = viewModel };
        try
        {
            owner.Show();
            var result = window.ShowDialog<bool>(owner);
            await HeadlessUiPump.FlushQueueAsync();
            viewModel.Options[1].IsSelected = true;
            var checkedState = viewModel.Options.Select(static option => option.IsSelected).ToArray();
            var selectedKeys = viewModel.GetSelectedKeys().ToArray();

            await ClickButtonAsync(window, "Apply");

            Assert.True(await result.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            Assert.False(window.IsVisible);
            Assert.DoesNotContain(window, owner.OwnedWindows);
            Assert.Equal(checkedState, viewModel.Options.Select(static option => option.IsSelected).ToArray());
            Assert.Equal(selectedKeys, viewModel.GetSelectedKeys());
            Assert.True(owner.IsEnabled);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    [AvaloniaFact]
    public async Task Cancel_ReturnsFalseAndPreservesOwnerFilterAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var probe = new UiEventProbe();
        var selection = CreateFilteredSelection(probe);
        var startingRows = selection.Rows.ToArray();
        var draft = BuildDialogViewModel(selection);
        var startingKeys = draft.GetSelectedKeys().ToArray();
        var pane = new NotchExportSelectionRowsPaneView { DataContext = selection };
        var owner = new Window { Content = pane };
        var window = new NotchExportColumnFilterWindow { DataContext = draft };
        try
        {
            owner.Show();
            var result = window.ShowDialog<bool>(owner);
            await HeadlessUiPump.FlushQueueAsync();
            await ClickButtonAsync(window, "Clear");
            Assert.Empty(draft.GetSelectedKeys());
            await ClickButtonAsync(window, "Cancel");

            Assert.False(await result.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken));
            Assert.False(window.IsVisible);

            // Exercise the real caller's result gate as well as the dialog's bool result.
            await ClickButtonAsync(pane, "Diff");
            var opened = Assert.Single(owner.OwnedWindows.OfType<NotchExportColumnFilterWindow>());
            var ownerDraft = Assert.IsType<NotchExportColumnFilterDialogViewModel>(opened.DataContext);
            Assert.Equal(startingKeys, ownerDraft.GetSelectedKeys());
            await ClickButtonAsync(opened, "Clear");
            Assert.Empty(ownerDraft.GetSelectedKeys());
            await ClickButtonAsync(opened, "Cancel");

            Assert.False(opened.IsVisible);
            Assert.Empty(owner.OwnedWindows);
            Assert.True(selection.IsDiffColumnFilterActive);
            Assert.Equal(startingRows, selection.Rows.ToArray());
            Assert.Equal(startingKeys, BuildDialogViewModel(selection).GetSelectedKeys());
            Assert.True(owner.IsEnabled);
            Assert.Empty(probe.Reports);
            Assert.Empty(probe.FallbackReports);
        }
        finally
        {
            window.Close();
            CloseWindows(owner);
        }
    }

    private static NotchExportColumnFilterDialogViewModel CreateDialogViewModel()
    {
        return new NotchExportColumnFilterDialogViewModel(NotchExportColumnFilterField.Diff, "Diff",
        [
            new NotchExportColumnFilterOptionViewModel("9", "9", isSelected: true),
            new NotchExportColumnFilterOptionViewModel("11", "11", isSelected: false),
            new NotchExportColumnFilterOptionViewModel("13", "13", isSelected: false),
        ]);
    }

    private static NotchExportSelectionViewModel CreateFilteredSelection(UiEventProbe probe)
    {
        var table = new NotchTable(
        [
            new NotchTableRow(0, 9, 100, 200, new NotchV22Node(9, 100, 10, 25, 65535, 0, 0), "transfer-a"),
            new NotchTableRow(0, 11, 101, 201, new NotchV22Node(11, 100, 12, 25, 65535, 0, 0), "transfer-b"),
        ]);
        var selection = new NotchExportSelectionViewModel(table) { UiEvents = probe.Runner };
        var dialog = BuildDialogViewModel(selection);
        selection.ApplyColumnFilterSelection(dialog.Field, dialog.GetAllKeys().Take(1).ToArray(), dialog.GetAllKeys());
        Assert.Single(selection.Rows);
        return selection;
    }

    private static NotchExportColumnFilterDialogViewModel BuildDialogViewModel(NotchExportSelectionViewModel selection)
    {
        Assert.True(selection.TryBuildColumnFilterDialog("diff", out var dialog));
        return Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog);
    }

    private static async Task ClickButtonAsync(Control root, string content)
    {
        var button = Assert.Single(root.GetVisualDescendants().OfType<Button>(),
            button => button.Content is string text && text == content);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await HeadlessUiPump.FlushQueueAsync();
    }

    private static void CloseWindows(Window owner)
    {
        foreach (var window in owner.OwnedWindows.ToArray())
        {
            window.Close();
        }

        owner.Close();
    }
}
