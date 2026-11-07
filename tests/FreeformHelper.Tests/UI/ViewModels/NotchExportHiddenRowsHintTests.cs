// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportHiddenRowsHintTests
{
    private static readonly int[] _legacyRowValues = [20, 128, 65535, 0, 0, 65535, 0, 0, 0];
    private static readonly string[] _transferDiffKeys = ["10"];
    private static readonly string[] _allDiffKeys = ["8", "9", "10"];

    [Theory]
    [InlineData(1, "1 row hidden but still exported")]
    [InlineData(2, "2 rows hidden but still exported")]
    public void DefaultTransferOnlyReportsSelectedNoOpRows(int noOpCount, string expectedText)
    {
        var viewModel = new NotchExportSelectionViewModel(BuildTable(noOpCount));

        Assert.Equal(NotchExportRowDisplayMode.TransferOnly, viewModel.SelectedRowDisplayModeOption.Value);
        Assert.True(Assert.Single(viewModel.Rows).IsTransferRow);
        Assert.Equal(noOpCount, viewModel.HiddenSelectedRowCount);
        Assert.True(viewModel.HasHiddenSelectedRows);
        Assert.Equal(expectedText, viewModel.HiddenSelectedRowsHintText);
    }

    [Fact]
    public void AllRowsClearsHiddenRowsHintAndNotifiesBindings()
    {
        var viewModel = new NotchExportSelectionViewModel(BuildTable());
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);

        Assert.Equal(3, viewModel.Rows.Count);
        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
        Assert.False(viewModel.HasHiddenSelectedRows);
        Assert.Equal(string.Empty, viewModel.HiddenSelectedRowsHintText);
        Assert.Contains(nameof(viewModel.HiddenSelectedRowCount), changedProperties);
        Assert.Contains(nameof(viewModel.HasHiddenSelectedRows), changedProperties);
        Assert.Contains(nameof(viewModel.HiddenSelectedRowsHintText), changedProperties);
    }

    [Fact]
    public void DeselectingHiddenRowsRefreshesHintImmediately()
    {
        var viewModel = new NotchExportSelectionViewModel(BuildTable());
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);
        var noOpRows = viewModel.Rows.Where(row => row.IsNoOpV22Node).ToArray();
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.TransferOnly);
        Assert.Equal(2, viewModel.HiddenSelectedRowCount);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        noOpRows[0].IsSelected = false;

        Assert.Equal(1, viewModel.HiddenSelectedRowCount);
        Assert.True(viewModel.HasHiddenSelectedRows);
        Assert.Equal("1 row hidden but still exported", viewModel.HiddenSelectedRowsHintText);

        noOpRows[1].IsSelected = false;

        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
        Assert.False(viewModel.HasHiddenSelectedRows);
        Assert.Equal(string.Empty, viewModel.HiddenSelectedRowsHintText);
        Assert.Single(viewModel.BuildSelectedTable().Rows);
        Assert.Contains(nameof(viewModel.HiddenSelectedRowCount), changedProperties);
        Assert.Contains(nameof(viewModel.HasHiddenSelectedRows), changedProperties);
        Assert.Contains(nameof(viewModel.HiddenSelectedRowsHintText), changedProperties);
    }

    [Fact]
    public void DisplayModeKeepsHiddenSelectedRowsInExport()
    {
        var table = BuildTable();
        var viewModel = new NotchExportSelectionViewModel(table);
        var before = viewModel.BuildSelectedTable();
        Assert.Equal(2, viewModel.HiddenSelectedRowCount);
        Assert.Equal(table.Rows, before.Rows);

        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);
        var after = viewModel.BuildSelectedTable();

        Assert.Equal(before.Rows.Count, after.Rows.Count);
        Assert.Equal(before.Rows, after.Rows);
        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
    }

    [Fact]
    public void VersionChangesCountOnlyRowsEligibleForExport()
    {
        var legacyRow = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 20, 120, 220, _legacyRowValues, "legacy");
        var table = new NotchTable(BuildTable().Rows.Append(legacyRow).ToArray());
        var viewModel = new NotchExportSelectionViewModel(table);
        Assert.Equal(2, viewModel.HiddenSelectedRowCount);

        viewModel.SelectedVersionOption = viewModel.VersionOptions.Single(option => option.Value == NotchAlgorithmVersion.V21);

        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
        Assert.False(viewModel.HasHiddenSelectedRows);
        Assert.Same(legacyRow, Assert.Single(viewModel.BuildSelectedTable().Rows));

        viewModel.SelectedVersionOption = viewModel.VersionOptions.Single(option => option.Value == NotchAlgorithmVersion.V22);

        Assert.Equal(2, viewModel.HiddenSelectedRowCount);
        Assert.True(viewModel.HasHiddenSelectedRows);
        Assert.Equal("2 rows hidden but still exported", viewModel.HiddenSelectedRowsHintText);
        Assert.Equal(3, viewModel.BuildSelectedTable().Rows.Count);
    }

    [Fact]
    public void SearchChangesRefreshHiddenRowsHint()
    {
        var viewModel = new NotchExportSelectionViewModel(BuildTable());
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);

        viewModel.SearchKeyword = "diff=10";

        Assert.Single(viewModel.Rows);
        Assert.Equal(2, viewModel.HiddenSelectedRowCount);
        Assert.Equal("2 rows hidden but still exported", viewModel.HiddenSelectedRowsHintText);

        viewModel.SearchKeyword = string.Empty;

        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
        Assert.False(viewModel.HasHiddenSelectedRows);
    }

    [Fact]
    public void ColumnFilterChangesRefreshHiddenRowsHint()
    {
        var viewModel = new NotchExportSelectionViewModel(BuildTable());
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);

        viewModel.ApplyColumnFilterSelection(NotchExportColumnFilterField.Diff, _transferDiffKeys, _allDiffKeys);

        Assert.Single(viewModel.Rows);
        Assert.Equal(2, viewModel.HiddenSelectedRowCount);
        Assert.Equal("2 rows hidden but still exported", viewModel.HiddenSelectedRowsHintText);

        viewModel.ApplyColumnFilterSelection(NotchExportColumnFilterField.Diff, _allDiffKeys, _allDiffKeys);

        Assert.Equal(0, viewModel.HiddenSelectedRowCount);
        Assert.False(viewModel.HasHiddenSelectedRows);
    }

    private static void SetDisplayMode(NotchExportSelectionViewModel viewModel, NotchExportRowDisplayMode mode)
    {
        viewModel.SelectedRowDisplayModeOption = viewModel.RowDisplayModeOptions.Single(option => option.Value == mode);
    }

    private static NotchTable BuildTable(int noOpCount = 2)
    {
        var rows = new List<NotchTableRow>();
        for (var index = 0; index < noOpCount; index++)
        {
            var diff = 8 + index;
            rows.Add(new NotchTableRow(
                0, diff, 100 + index, 200 + index,
                new NotchV22Node(diff, 100, 65535, 0, 65535, 0, 0), "no-op"));
        }

        var transferDiff = 8 + noOpCount;
        rows.Add(new NotchTableRow(
            0, transferDiff, 100 + noOpCount, 200 + noOpCount,
            new NotchV22Node(transferDiff, 95, transferDiff + 1, 5, 65535, 0, 0), "transfer"));
        return new NotchTable(rows);
    }
}
