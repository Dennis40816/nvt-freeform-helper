// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditDetailsIntegratedLayoutTests
{
    private static readonly string[] _categoryLabels = ["Hidden 2", "Duplicate 3", "Combined 1", "Moved 1", "Rotated 1"];
    private static readonly string[] _columnLabels = ["Pad", "Change"];
    private static readonly int[] _restoredIds = [20, 22];
    private static readonly int[] _rehiddenIds = [21];
    private static readonly ThemeVariant[] _themes = [ThemeVariant.Dark, ThemeVariant.Light];
    private static readonly DxfEditChangeKind[] _singleActionKinds =
        [DxfEditChangeKind.Hidden, DxfEditChangeKind.Combined, DxfEditChangeKind.Moved, DxfEditChangeKind.Rotated];

    [AvaloniaFact]
    public async Task CategoryCountsAppearOnlyOnSwitchesAndFooterOnlyClosesAsync()
    {
        var entries = CreateEntries();
        var vm = CreateViewModel(DxfEditChangeKind.Duplicate, entries);
        await WithWindowAsync(vm, async window =>
        {
            var switches = Assert.IsType<WrapPanel>(window.FindControl<WrapPanel>("CategorySwitches"));
            var toggles = switches.Children.OfType<ToggleButton>().ToArray();
            Assert.Equal(_categoryLabels, toggles.Select(static toggle => Assert.IsType<string>(toggle.Content)));
            Assert.All(toggles, static toggle =>
            {
                Assert.Contains("actionGhost", toggle.Classes);
                Assert.Contains("flat", toggle.Classes);
            });
            Assert.Same(vm.SetDuplicateKindCommand, Assert.Single(toggles, static toggle => toggle.IsChecked == true).Command);
            Assert.DoesNotContain(VisibleTextOutsideSwitches(window), text => _categoryLabels.Contains(text, StringComparer.Ordinal));
            Assert.DoesNotContain(VisibleTextOutsideSwitches(window), static text => text.StartsWith("Showing ", StringComparison.Ordinal));

            var footer = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("FooterActions"));
            Assert.Equal(HorizontalAlignment.Right, footer.HorizontalAlignment);
            var close = Assert.IsType<Button>(Assert.Single(footer.Children));
            Assert.Equal("Close", close.Content);
            Assert.Same(vm.CloseCommand, close.Command);
            Assert.Contains("actionNeutral", close.Classes);
            var refresh = CommandButton(window, vm.RefreshCommand);
            Assert.Equal("Refresh", refresh.Content);
            Assert.Contains("actionNeutral", refresh.Classes);

            entries.Add(CreateEntry(DxfEditChangeKind.Duplicate, 23, "Restore"));
            refresh.Command!.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("Duplicate 4", toggles[1].Content);
        });
    }

    [AvaloniaFact]
    public async Task InspectorOwnsOnlyPrimaryActionAndScrollableDetailInBothThemesAsync()
    {
        var vm = CreateViewModel(DxfEditChangeKind.Duplicate, CreateEntries());
        await WithWindowAsync(vm, async window =>
        {
            foreach (var theme in _themes)
            {
                window.RequestedThemeVariant = theme;
                await HeadlessUiPump.FlushQueueAsync();
                var primary = Assert.Single(window.GetVisualDescendants().OfType<Button>(),
                    static button => button.Classes.Contains("actionPrimary"));
                Assert.Same(vm.ApplySelectedEntryCommand, primary.Command);
                var actions = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("InspectorActions"));
                Assert.Contains(primary, actions.Children);
                Assert.Same(vm.FocusSelectedEntryCommand, CommandButton(window, vm.FocusSelectedEntryCommand).Command);
                foreach (var command in new[]
                {
                    vm.ApplySelectedEntriesCommand, vm.RestoreSelectedDuplicateEntriesCommand, vm.RehideSelectedDuplicateEntriesCommand,
                })
                {
                    var bulk = CommandButton(window, command);
                    Assert.Contains("actionNeutral", bulk.Classes);
                    Assert.DoesNotContain("actionPrimary", bulk.Classes);
                }

                var info = Assert.Single(actions.Children.OfType<Button>(),
                    static button => button.Classes.Contains("actionIconButton"));
                Assert.Equal(vm.SelectedRowActionText, TipText(info));
                var scroll = Assert.IsType<ScrollViewer>(window.FindControl<ScrollViewer>("InspectorDetailScroll"));
                Assert.Equal(ScrollBarVisibility.Auto, scroll.VerticalScrollBarVisibility);
                Assert.Equal(ScrollBarVisibility.Disabled, scroll.HorizontalScrollBarVisibility);
                var detail = Assert.IsType<SelectableTextBlock>(scroll.Content);
                Assert.Equal(TextWrapping.Wrap, detail.TextWrapping);
                Assert.Equal(vm.SelectedRowDetailText, detail.Text);
            }
        });
    }

    [AvaloniaFact]
    public async Task DuplicateBulkLabelsMatchProcessedRowsAndExplainSkippedChecksAsync()
    {
        var appliedIds = new List<int>();
        var vm = CreateViewModel(DxfEditChangeKind.Duplicate, CreateEntries(), entry => appliedIds.Add(entry.CadPadId));
        await WithWindowAsync(vm, async window =>
        {
            var restore = CommandButton(window, vm.RestoreSelectedDuplicateEntriesCommand);
            var hide = CommandButton(window, vm.RehideSelectedDuplicateEntriesCommand);
            Assert.Equal("Restore selected (0)", restore.Content);
            Assert.Equal("Hide selected (0)", hide.Content);
            Assert.False(restore.IsEffectivelyEnabled);
            Assert.False(hide.IsEffectivelyEnabled);

            vm.AreAllVisibleRowsSelected = true;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("3 checked", vm.SelectedVisibleRowsSummaryText);
            Assert.Equal("Restore selected (2)", restore.Content);
            Assert.Equal("Hide selected (1)", hide.Content);
            Assert.Contains("1 checked row(s) are already restored", TipText(restore), StringComparison.Ordinal);
            Assert.Contains("2 checked row(s) are already hidden", TipText(hide), StringComparison.Ordinal);
            Assert.True(restore.IsEffectivelyEnabled);
            Assert.True(hide.IsEffectivelyEnabled);
            restore.Command!.Execute(null);
            Assert.Equal(_restoredIds, appliedIds);
            appliedIds.Clear();
            hide.Command!.Execute(null);
            Assert.Equal(_rehiddenIds, appliedIds);

            vm.AreAllVisibleRowsSelected = false;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal("Restore selected (0)", restore.Content);
            Assert.Equal("Hide selected (0)", hide.Content);
            Assert.False(restore.IsEffectivelyEnabled);
            Assert.False(hide.IsEffectivelyEnabled);
        });
    }

    [AvaloniaFact]
    public async Task SingleActionBulkLabelsFollowCurrentCategoryAndCheckedRowsAsync()
    {
        var vm = CreateViewModel(DxfEditChangeKind.Hidden, CreateEntries());
        await WithWindowAsync(vm, async window =>
        {
            var bulk = CommandButton(window, vm.ApplySelectedEntriesCommand);
            foreach (var kind in _singleActionKinds)
            {
                vm.SelectedKind = kind;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.Equal($"{vm.BulkActionText} (0)", bulk.Content);
                Assert.False(bulk.IsEffectivelyEnabled);
                vm.AreAllVisibleRowsSelected = true;
                await HeadlessUiPump.FlushQueueAsync();
                Assert.Equal($"{vm.BulkActionText} ({vm.Rows.Count})", bulk.Content);
                Assert.True(bulk.IsEffectivelyEnabled);
            }
        });
    }

    [AvaloniaFact]
    public async Task RowsOnlySelectOrCheckAndControlAOnlyChecksCurrentCategoryAsync()
    {
        var appliedIds = new List<int>();
        var vm = CreateViewModel(DxfEditChangeKind.Hidden, CreateEntries(), entry => appliedIds.Add(entry.CadPadId));
        await WithWindowAsync(vm, async window =>
        {
            var header = Assert.IsType<Border>(window.FindControl<Border>("RowsHeader"));
            var columns = Assert.IsType<Grid>(header.Child);
            Assert.Equal(3, columns.ColumnDefinitions.Count);
            Assert.Equal(_columnLabels, columns.Children.OfType<TextBlock>().Select(static text => text.Text));
            var rowButton = window.GetVisualDescendants().OfType<Button>()
                .First(static button => button.Classes.Contains("workspaceDataRow"));
            Assert.Same(vm.SelectEntryCommand, rowButton.Command);
            rowButton.Command!.Execute(rowButton.CommandParameter);
            Assert.Same(rowButton.CommandParameter, vm.SelectedEntry);
            Assert.Empty(appliedIds);

            PressControlA(window);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(2, vm.SelectedVisibleCount);
            Assert.All(vm.Rows, static row => Assert.True(row.IsSelected));
            vm.SetDuplicateKindCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(0, vm.SelectedVisibleCount);
            Assert.All(vm.Rows, static row => Assert.False(row.IsSelected));
            PressControlA(window);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.Equal(3, vm.SelectedVisibleCount);
            vm.SetMovedKindCommand.Execute(null);
            Assert.False(Assert.Single(vm.Rows).IsSelected);
            vm.SetHiddenKindCommand.Execute(null);
            Assert.All(vm.Rows, static row => Assert.True(row.IsSelected));
            Assert.Empty(appliedIds);
        });
    }

    [AvaloniaFact]
    public async Task EmptyCategoryAndNoSelectionShowHintsAndDisableInspectorActionsAsync()
    {
        var entries = new List<DxfEditChangeListEntry>();
        var vm = CreateViewModel(DxfEditChangeKind.Duplicate, entries);
        await WithWindowAsync(vm, async window =>
        {
            var emptyHint = Assert.IsType<TextBlock>(window.FindControl<TextBlock>("EmptyRowsHint"));
            var selectionHint = Assert.IsType<TextBlock>(window.FindControl<TextBlock>("InspectorSelectionHint"));
            Assert.True(emptyHint.IsEffectivelyVisible);
            Assert.Equal("No DXF edit rows in this scope.", emptyHint.Text);
            Assert.True(selectionHint.IsEffectivelyVisible);
            Assert.Equal("No DXF edit selected.", selectionHint.Text);
            Assert.False(CommandButton(window, vm.FocusSelectedEntryCommand).IsEffectivelyEnabled);
            Assert.False(CommandButton(window, vm.ApplySelectedEntryCommand).IsEffectivelyEnabled);

            entries.Add(CreateEntry(DxfEditChangeKind.Duplicate, 20, "Restore"));
            vm.RefreshCommand.Execute(null);
            await HeadlessUiPump.FlushQueueAsync();
            Assert.False(emptyHint.IsEffectivelyVisible);
            Assert.True(vm.HasSelectedEntry);
            vm.SelectedEntry = null;
            await HeadlessUiPump.FlushQueueAsync();
            Assert.True(vm.HasRows);
            Assert.Equal("No DXF edit selected.", selectionHint.Text);
            Assert.True(selectionHint.IsEffectivelyVisible);
            Assert.False(CommandButton(window, vm.FocusSelectedEntryCommand).IsEffectivelyEnabled);
            Assert.False(CommandButton(window, vm.ApplySelectedEntryCommand).IsEffectivelyEnabled);
        });
    }

    private static async Task WithWindowAsync(DxfEditChangeListViewModel vm, Func<DxfEditChangeListWindow, Task> assertions)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var window = new DxfEditChangeListWindow { DataContext = vm };
        try
        {
            window.Show();
            await HeadlessUiPump.FlushQueueAsync();
            await assertions(window);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button CommandButton(Window window, System.Windows.Input.ICommand command)
    {
        return Assert.Single(window.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, command));
    }

    private static string TipText(Control control)
    {
        return ToolTip.GetTip(control) switch
        {
            string text => text,
            TextBlock text => text.Text ?? string.Empty,
            _ => string.Empty,
        };
    }

    private static IEnumerable<string> VisibleTextOutsideSwitches(Window window)
    {
        foreach (var control in window.GetVisualDescendants().OfType<Control>())
        {
            if (!control.IsEffectivelyVisible || control.GetVisualAncestors().OfType<ToggleButton>().Any())
            {
                continue;
            }

            var text = control switch
            {
                SelectableTextBlock block => block.Text,
                TextBlock block => block.Text,
                _ => null,
            };
            if (text is not null)
            {
                yield return text;
            }
        }
    }

    private static void PressControlA(Window window)
    {
        window.Focus();
        window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
    }

    private static DxfEditChangeListViewModel CreateViewModel(
        DxfEditChangeKind kind, List<DxfEditChangeListEntry> entries, Action<DxfEditChangeListEntry>? apply = null)
    {
        return new DxfEditChangeListViewModel(kind, () => entries, static _ => { }, apply ?? (static _ => { }));
    }

    private static List<DxfEditChangeListEntry> CreateEntries()
    {
        return
        [
            CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore"),
            CreateEntry(DxfEditChangeKind.Hidden, 11, "Restore"),
            CreateEntry(DxfEditChangeKind.Duplicate, 20, "Restore"),
            CreateEntry(DxfEditChangeKind.Duplicate, 21, "Hide again"),
            CreateEntry(DxfEditChangeKind.Duplicate, 22, "Restore"),
            CreateEntry(DxfEditChangeKind.Combined, 30, "Clear group"),
            CreateEntry(DxfEditChangeKind.Moved, 40, "Restore layer"),
            CreateEntry(DxfEditChangeKind.Rotated, 50, "Restore geometry"),
        ];
    }

    private static DxfEditChangeListEntry CreateEntry(DxfEditChangeKind kind, int cadPadId, string actionLabel)
    {
        return new DxfEditChangeListEntry(
            kind, cadPadId, cadPadId, $"PAD_{cadPadId}", kind.ToString(), "Layer 0", "detail", "Complete change detail.",
            IconGlyphs.Undo, actionLabel, "Apply this row's scoped action.", canFocus: true, canApply: true);
    }
}
