// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportRowSelectionCharacterizationTests
{
    [Fact]
    public void UnpinnedExportIncludesCheckedRowsOfEveryVersion()
    {
        var (viewModel, allRows) = CreateSelection();

        var exported = viewModel.BuildSelectedTable();

        Assert.Null(viewModel.SelectedVersionOption.Value);
        AssertRows(exported, allRows[0].Row, allRows[1].Row, allRows[2].Row, allRows[3].Row);
    }

    [Fact]
    public void PinnedExportIncludesOnlyCheckedRowsOfThePinnedVersion()
    {
        var (viewModel, allRows) = CreateSelection();

        viewModel.SelectedExportTypeOption = Assert.Single(viewModel.ExportTypeOptions,
            option => option.PinnedVersion == NotchAlgorithmVersion.V22);
        var exported = viewModel.BuildSelectedTable();

        Assert.True(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.Equal(NotchAlgorithmVersion.V22, viewModel.SelectedVersionOption.Value);
        AssertRows(exported, allRows[0].Row, allRows[2].Row, allRows[3].Row);
    }

    [Fact]
    public void DisplayModeKeepsCheckedHiddenRowsInExport()
    {
        var (viewModel, allRows) = CreateSelection();

        var exported = viewModel.BuildSelectedTable();

        Assert.Equal(NotchExportRowDisplayMode.TransferOnly, viewModel.SelectedRowDisplayModeOption.Value);
        Assert.True(allRows[2].IsSelected);
        Assert.DoesNotContain(allRows[2], viewModel.Rows);
        Assert.Equal(1, viewModel.HiddenSelectedRowCount);
        AssertRows(exported, allRows[0].Row, allRows[1].Row, allRows[2].Row, allRows[3].Row);
    }

    [Fact]
    public void SearchKeepsHiddenCheckedRowsAndExcludesTheVisibleUncheckedRow()
    {
        var (viewModel, allRows) = CreateSelection();
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);

        viewModel.SearchKeyword = "diff=10";
        var exported = viewModel.BuildSelectedTable();

        var visibleRow = Assert.Single(viewModel.Rows);
        Assert.Same(allRows[4], visibleRow);
        Assert.False(visibleRow.IsSelected);
        Assert.Equal(4, viewModel.HiddenSelectedRowCount);
        AssertRows(exported, allRows[0].Row, allRows[1].Row, allRows[2].Row, allRows[3].Row);
    }

    [Fact]
    public void EqualValuedDistinctRowsKeepOriginalInstancesAndOrderEachOnce()
    {
        var (viewModel, allRows) = CreateSelection();
        allRows[1].IsSelected = false;
        allRows[2].IsSelected = false;

        var exported = viewModel.BuildSelectedTable();

        Assert.NotSame(allRows[0].Row, allRows[3].Row);
        Assert.Equal(allRows[0].Row.Values, allRows[3].Row.Values);
        AssertRows(exported, allRows[0].Row, allRows[3].Row);
    }

    [Fact]
    public void VisibleSortDoesNotChangeOriginalExportOrder()
    {
        var (viewModel, allRows) = CreateSelection();

        viewModel.SelectedSortModeOption = Assert.Single(viewModel.SortModeOptions,
            option => option.Value == NotchExportSortMode.DiffAscending);
        var exported = viewModel.BuildSelectedTable();
        var summary = NotchExportSelectionSummaryProjector.Build(allRows, viewModel.Rows, viewModel.SelectedVersionOption.Value);

        Assert.Equal(new[] { allRows[4], allRows[1], allRows[0], allRows[3], allRows[5] }, viewModel.Rows);
        AssertRows(exported, allRows[0].Row, allRows[1].Row, allRows[2].Row, allRows[3].Row);
        Assert.Equal(exported.Rows.Count, summary.SelectedCount);
        Assert.Equal(1, summary.HiddenSelectedRowCount);
    }

    [Theory]
    [InlineData(null, 4, 1)]
    [InlineData(NotchAlgorithmVersion.V21, 1, 0)]
    [InlineData(NotchAlgorithmVersion.V22, 3, 1)]
    public void SummarySelectedAndHiddenCountsAgreeWithExport(
        NotchAlgorithmVersion? scope,
        int expectedSelectedCount,
        int expectedHiddenCount)
    {
        var (viewModel, allRows) = CreateSelection();
        viewModel.SelectedVersionOption = Assert.Single(viewModel.VersionOptions, option => option.Value == scope);

        var exported = viewModel.BuildSelectedTable();
        var summary = NotchExportSelectionSummaryProjector.Build(allRows, viewModel.Rows, scope);

        Assert.Equal(expectedSelectedCount, exported.Rows.Count);
        Assert.Equal(exported.Rows.Count, summary.SelectedCount);
        Assert.Equal(summary.SelectedCount, viewModel.SelectedCount);
        Assert.Equal(expectedHiddenCount, summary.HiddenSelectedRowCount);
        Assert.Equal(summary.HiddenSelectedRowCount, viewModel.HiddenSelectedRowCount);
    }

    [Fact]
    public void ExportedTableUsesEmptyMetadataEvenWhenSourceHasMetadata()
    {
        var row = CreateTransferRow(30);
        var audit = new NotchToFullCoverageAudit(1, 1, 1, Array.Empty<NotchToFullCoverageGap>());
        var timings = new NotchGenerationPhaseTimings(1, 2, 3, 4, 5);
        var source = new NotchTable(new[] { row }, audit, timings);
        var viewModel = new NotchExportSelectionViewModel(source);

        var exported = viewModel.BuildSelectedTable();

        AssertRows(exported, row);
        Assert.Same(NotchToFullCoverageAudit.Empty, exported.ToFullCoverageAudit);
        Assert.Same(NotchGenerationPhaseTimings.Empty, exported.GenerationPhaseTimings);
    }

    private static (NotchExportSelectionViewModel ViewModel, NotchExportRowItemViewModel[] AllRows) CreateSelection()
    {
        var table = new NotchTable(new[]
        {
            CreateTransferRow(30),
            CreateLegacyRow(20),
            new NotchTableRow(0, 8, 108, 208, new NotchV22Node(8, 100, 65535, 0, 65535, 0, 0), "no-op"),
            CreateTransferRow(30),
            CreateTransferRow(10),
            CreateLegacyRow(40),
        });
        var options = new[]
        {
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Csv, "CSV review (.csv)", "csv"),
            new FreeformHelperViewModel.NotchExportFileTypeOption(
                FreeformHelperViewModel.NotchExportFileType.Cv22, "C v2.2 (.c)", "c", NotchAlgorithmVersion.V22),
        };
        var viewModel = new NotchExportSelectionViewModel(table, exportTypeOptions: options, selectedExportTypeOption: options[0]);
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.AllRows);
        var allRows = viewModel.Rows.ToArray();
        allRows[4].IsSelected = false;
        allRows[5].IsSelected = false;
        SetDisplayMode(viewModel, NotchExportRowDisplayMode.TransferOnly);
        return (viewModel, allRows);
    }

    private static NotchTableRow CreateTransferRow(int diff)
    {
        return new NotchTableRow(0, diff, 100 + diff, 200 + diff,
            new NotchV22Node(diff, 95, diff + 1, 5, 65535, 0, 0), "transfer");
    }

    private static NotchTableRow CreateLegacyRow(int diff)
    {
        return new NotchTableRow(NotchAlgorithmVersion.V21, 0, diff, 100 + diff, 200 + diff,
            new[] { diff, 128, 65535, 0, 0, 65535, 0, 0, 0 }, "legacy");
    }

    private static void SetDisplayMode(NotchExportSelectionViewModel viewModel, NotchExportRowDisplayMode mode)
    {
        viewModel.SelectedRowDisplayModeOption = Assert.Single(viewModel.RowDisplayModeOptions, option => option.Value == mode);
    }

    private static void AssertRows(NotchTable actual, params NotchTableRow[] expected)
    {
        Assert.Equal(expected.Length, actual.Rows.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Same(expected[index], actual.Rows[index]);
        }
    }
}
