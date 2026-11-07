// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;
using static FreeformHelper.Tests.NotchExportSelectionInteractionFixture;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportSelectionExportButtonTests
{
    private static readonly int[] _sortedDiffs = [10, 20, 30, 40, 50, 60];
    private static readonly int[] _selectedOnlyDiffs = [30, 10, 20, 40];
    private static readonly string[] _columnFilterKeys = ["40", "50"];
    private static readonly int[] _columnFilterDiffs = [40, 50];
    private static readonly int[] _builderFilterDiffs = [60, 40, 50];

    [AvaloniaFact]
    public async Task ExportButtonReturnsOnlyTwoCheckedRowsInTableOrderDespiteDisplaySortAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        var window = fixture.Window;
        var completions = new List<bool>();
        window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
        await fixture.ClearSelectionAsync();
        await ClickAsync(fixture.FindRowCheckBox(40));
        await ClickAsync(fixture.FindRowCheckBox(30));
        var sort = Assert.Single(window.GetVisualDescendants().OfType<ComboBox>(),
            combo => ReferenceEquals(combo.ItemsSource, fixture.Selection.SortModeOptions));
        sort.SelectedItem = Assert.Single(fixture.Selection.SortModeOptions,
            option => option.Value == NotchExportSortMode.DiffAscending);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(_sortedDiffs, fixture.Selection.Rows.Select(row => row.Row.DiffIndex));

        await ClickAsync(FindButton(window, "Export"));

        var selected = Assert.IsType<NotchTable>(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        AssertRows(selected, fixture.Table.Rows[0], fixture.Table.Rows[4]);
        Assert.True(Assert.Single(completions));
        Assert.False(window.IsVisible);
        Assert.Empty(fixture.Owner.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task GroupSelectAllAndSelectedOnlyPreserveEachGroupsCheckedRowsForExportAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        await fixture.ClearSelectionAsync();
        await ClickAsync(fixture.FindGroupButton(0, "All shown"));
        AssertGroup(fixture.Selection, 0, 3, 3);
        AssertGroup(fixture.Selection, 1, 0, 3);
        await ClickAsync(fixture.FindRowCheckBox(40));
        var selectedOnly = FindButton(fixture.Window, "Selected only");

        await ClickAsync(selectedOnly);

        Assert.True(fixture.Selection.ShowSelectedOnly);
        Assert.Equal(_selectedOnlyDiffs, fixture.Selection.Rows.Select(row => row.Row.DiffIndex));
        AssertGroup(fixture.Selection, 0, 3, 3);
        AssertGroup(fixture.Selection, 1, 1, 1);
        await ClickAsync(selectedOnly);
        Assert.False(fixture.Selection.ShowSelectedOnly);
        AssertGroup(fixture.Selection, 0, 3, 3);
        AssertGroup(fixture.Selection, 1, 1, 3);
        await ClickAsync(FindButton(fixture.Window, "Export"));

        var selected = Assert.IsType<NotchTable>(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        AssertRows(selected, fixture.Table.Rows[0], fixture.Table.Rows[1], fixture.Table.Rows[2], fixture.Table.Rows[4]);
    }

    [AvaloniaFact]
    public async Task ColumnFilterNarrowsShownRowsButExportIncludesHiddenCheckedRowsAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        await fixture.ClearSelectionAsync();
        await ClickAsync(fixture.FindRowCheckBox(30));
        await ClickAsync(fixture.FindRowCheckBox(40));
        var header = Assert.Single(fixture.Window.GetVisualDescendants().OfType<Button>(), button => button.Tag as string == "diff");
        await ClickAsync(header);
        var dialog = Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportColumnFilterWindow>());
        var filter = Assert.IsType<NotchExportColumnFilterDialogViewModel>(dialog.DataContext);
        await ClickAsync(FindButton(dialog, "Clear"));
        foreach (var checkBox in dialog.GetVisualDescendants().OfType<CheckBox>()
            .Where(checkBox => checkBox.DataContext is NotchExportColumnFilterOptionViewModel { Key: "40" or "50" }).ToArray())
        {
            await ClickAsync(checkBox);
        }
        Assert.Equal(_columnFilterKeys, filter.GetSelectedKeys());

        await ClickAsync(FindButton(dialog, "Apply"));

        Assert.False(dialog.IsVisible);
        Assert.Equal(_columnFilterDiffs, fixture.Selection.Rows.Select(row => row.Row.DiffIndex));
        Assert.True(fixture.Selection.Rows[0].IsSelected);
        Assert.False(fixture.Selection.Rows[1].IsSelected);
        Assert.Equal(1, fixture.Selection.HiddenSelectedRowCount);
        await ClickAsync(FindButton(fixture.Window, "Export"));
        var selected = Assert.IsType<NotchTable>(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        AssertRows(selected, fixture.Table.Rows[0], fixture.Table.Rows[4]);
    }

    [AvaloniaFact]
    public async Task FilterBuilderNarrowsShownRowsButExportIncludesHiddenCheckedRowsAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        await fixture.ClearSelectionAsync();
        await ClickAsync(fixture.FindRowCheckBox(10));
        await ClickAsync(fixture.FindRowCheckBox(60));
        await ClickAsync(FindTooltipButton(fixture.Window, "Filter helper"));
        var builder = Assert.Single(fixture.Window.OwnedWindows.OfType<NotchExportFilterBuilderWindow>());
        Assert.IsType<TextBox>(builder.FindControl<TextBox>("IcTextBox")).Text = "2";
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal("ic=2", builder.GeneratedQuery);

        await ClickAsync(FindButton(builder, "Apply"));

        Assert.False(builder.IsVisible);
        Assert.Equal("ic=2", fixture.Selection.SearchKeyword);
        Assert.Equal(_builderFilterDiffs, fixture.Selection.Rows.Select(row => row.Row.DiffIndex));
        Assert.Equal(1, fixture.Selection.HiddenSelectedRowCount);
        await ClickAsync(FindButton(fixture.Window, "Export"));
        var selected = Assert.IsType<NotchTable>(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        AssertRows(selected, fixture.Table.Rows[1], fixture.Table.Rows[3]);
    }

    [AvaloniaFact]
    public async Task CopyPayloadCopiesOnlyInspectedRowEvenWhenUncheckedAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        await ClickAsync(fixture.FindRowCheckBox(40));
        var list = Assert.Single(fixture.Window.GetVisualDescendants().OfType<ListBox>(),
            control => ReferenceEquals(control.ItemsSource, fixture.Selection.Rows));
        list.SelectedItem = fixture.Selection.Rows[4];
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(fixture.Selection.Rows[4].IsSelected);
        var clipboard = Assert.IsAssignableFrom<IClipboard>(fixture.Window.Clipboard);
        var previous = await clipboard.TryGetTextAsync();
        try
        {
            await clipboard.SetTextAsync("before copy");

            await ClickAsync(FindTooltipButton(fixture.Window, "Copy payload"));

            Assert.Equal("{ 40, 95, 41, 5, 65535, 0, 0 }", await clipboard.TryGetTextAsync());
            Assert.True(fixture.Window.IsVisible);
            Assert.False(result.IsCompleted);
        }
        finally
        {
            await clipboard.SetTextAsync(previous ?? string.Empty);
        }
        await ClickAsync(FindButton(fixture.Window, "Cancel"));
        Assert.Null(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
    }

    [AvaloniaFact]
    public async Task NoCheckedRowsDisablesExportUntilARowIsCheckedAsync()
    {
        await using var fixture = new NotchExportSelectionInteractionFixture();
        var result = fixture.OpenAsync();
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0, "Selection window did not open.");
        await HeadlessUiPump.FlushQueueAsync();
        var window = fixture.Window;
        var export = FindButton(window, "Export");
        await fixture.ClearSelectionAsync();

        Assert.False(export.IsEffectivelyEnabled);
        Assert.False(fixture.Selection.HasSelection);
        Assert.Empty(fixture.Selection.BuildSelectedTable().Rows);
        Assert.False(result.IsCompleted);
        Assert.True(window.IsVisible);
        await ClickAsync(fixture.FindRowCheckBox(20));
        Assert.True(export.IsEffectivelyEnabled);
        await ClickAsync(export);
        var selected = Assert.IsType<NotchTable>(await result.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        AssertRows(selected, fixture.Table.Rows[2]);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task ExportCommandWritesOnlyCheckedWindowRowAndItsPayloadToCsvAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var helper = await files.CreateExportReadyAsync();
        await LoadTwoTransferPadsAsync(helper);
        helper.SelectedNotchExportFileTypeOption = Assert.Single(helper.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
        await using var fixture = new NotchExportSelectionInteractionFixture(helper);
        await HeadlessUiPump.FlushQueueAsync();
        helper.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        var export = helper.ExportNotchCommand.ExecuteAsync(null);
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0 || export.IsCompleted, "Export did not open selection.");
        await HeadlessUiPump.FlushQueueAsync();
        var window = fixture.Window;
        var rows = fixture.Selection.Rows.ToArray();
        Assert.True(rows.Length > 1);
        await fixture.ClearSelectionAsync();
        var expected = rows[^1].Row;
        await ClickAsync(fixture.FindRowCheckBox(expected.DiffIndex));

        await ClickAsync(FindButton(window, "Export"));
        await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        var data = Assert.Single(TestFiles.ReadContentLines(files.ExportPath).Skip(1)).Split(',');
        Assert.Equal("2.2", data[1]);
        Assert.Equal(expected.IcIndex.ToString(CultureInfo.InvariantCulture), data[3]);
        Assert.Equal(expected.DiffIndex.ToString(CultureInfo.InvariantCulture), data[5]);
        Assert.Equal(expected.RegularPadIndex.ToString(CultureInfo.InvariantCulture), data[6]);
        Assert.Equal(expected.CadPadId?.ToString(CultureInfo.InvariantCulture), data[7]);
        Assert.Equal(expected.Values.Length.ToString(CultureInfo.InvariantCulture), data[8]);
        Assert.Equal(expected.Values.Select(value => value.ToString(CultureInfo.InvariantCulture)), data.Skip(9).Take(expected.Values.Length));
        Assert.Equal(1.0, helper.NotchExportProgress);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task CancelButtonCancelsExportCommandWithoutWritingAFileAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var helper = await files.CreateExportReadyAsync();
        await using var fixture = new NotchExportSelectionInteractionFixture(helper);
        await HeadlessUiPump.FlushQueueAsync();
        helper.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        var export = helper.ExportNotchCommand.ExecuteAsync(null);
        await HeadlessUiPump.WaitUntilAsync(() => fixture.Owner.OwnedWindows.Count > 0 || export.IsCompleted, "Export did not open selection.");
        await HeadlessUiPump.FlushQueueAsync();
        var window = fixture.Window;
        var completions = new List<bool>();
        window.CompletionRequested += (_, confirmed) => completions.Add(confirmed);
        Assert.True(fixture.Selection.HasSelection);

        await ClickAsync(FindButton(window, "Cancel"));
        await export.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(completions));
        Assert.False(window.IsVisible);
        Assert.False(File.Exists(files.ExportPath));
        Assert.Equal("C v2.2 export canceled at stage 3 (row selection).", helper.NotchExportProgressText);
        Assert.False(helper.IsNotchExporting);
    }

    private static void AssertGroup(NotchExportSelectionViewModel selection, int icIndex, int selectedCount, int totalCount)
    {
        var group = Assert.Single(selection.IcGroups, group => group.IcIndex == icIndex);
        Assert.Equal(totalCount, group.TotalCount);
        Assert.Equal(selectedCount, group.SelectedCount);
        Assert.Equal(selectedCount, group.Rows.Count(row => row.IsSelected));
    }

    private static void AssertRows(NotchTable actual, params NotchTableRow[] expected)
    {
        Assert.Equal(expected.Length, actual.Rows.Count);
        foreach (var (expectedRow, actualRow) in expected.Zip(actual.Rows))
        {
            Assert.Equal(expectedRow.Version, actualRow.Version);
            Assert.Equal(expectedRow.IcIndex, actualRow.IcIndex);
            Assert.Equal(expectedRow.DiffIndex, actualRow.DiffIndex);
            Assert.Equal(expectedRow.RegularPadIndex, actualRow.RegularPadIndex);
            Assert.Equal(expectedRow.CadPadId, actualRow.CadPadId);
            Assert.Equal(expectedRow.Values, actualRow.Values);
            Assert.Equal(expectedRow.Comment, actualRow.Comment);
        }
    }

    private static async Task LoadTwoTransferPadsAsync(FreeformHelperViewModel helper)
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 10, 10), ("signal", 10, 0, 20, 10));
        try
        {
            Assert.Single(helper.CascadeIcSettings).XChannels = 4;
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(helper);
            helper.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await helper.OpenDxfCommand.ExecuteAsync(null);
            await helper.MatchCommand.ExecuteAsync(null);
            await helper.AutoDetectFreeformsCommand.ExecuteAsync(null);
            Assert.Equal(2, helper.CadPads.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
