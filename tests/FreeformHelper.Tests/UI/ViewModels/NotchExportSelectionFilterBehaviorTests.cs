// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionFilterBehaviorTests
{
    [Fact]
    public void ExportTypeSwitch_PinsExportVersionAndPreservesUncheckedRows()
    {
        using var culture = TestCulture.UseEnUs();
        var table = BuildMixedTable();
        NotchExportFileTypeOption[] options =
        [
            new(FreeformHelperViewModel.NotchExportFileType.Csv, "CSV review (.csv)", "csv"),
            new(FreeformHelperViewModel.NotchExportFileType.Cv21, "C v2.1 (.c)", "c", NotchAlgorithmVersion.V21),
            new(FreeformHelperViewModel.NotchExportFileType.Cv22, "C v2.2 (.c)", "c", NotchAlgorithmVersion.V22),
        ];
        var viewModel = new NotchExportSelectionViewModel(table, exportTypeOptions: options);
        var uncheckedRow = viewModel.Rows[2];
        uncheckedRow.IsSelected = false;
        Assert.Equal(options[0], viewModel.SelectedExportTypeOption);

        viewModel.SelectedExportTypeOption = options[1];

        Assert.True(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.False(viewModel.CanSelectVersion);
        Assert.Equal(NotchAlgorithmVersion.V21, viewModel.SelectedVersionOption.Value);
        Assert.Same(table.Rows[0], Assert.Single(viewModel.Rows).Row);
        Assert.Same(table.Rows[0], Assert.Single(viewModel.BuildSelectedTable().Rows));

        viewModel.SelectedExportTypeOption = options[2];

        Assert.Equal(NotchAlgorithmVersion.V22, viewModel.SelectedVersionOption.Value);
        Assert.Equal(2, viewModel.Rows.Count);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));
        Assert.False(viewModel.CanSelectVersion);

        viewModel.SelectedExportTypeOption = options[0];

        Assert.False(viewModel.IsVersionSelectionPinnedByExportType);
        Assert.True(viewModel.CanSelectVersion);
        Assert.Equal(NotchAlgorithmVersion.V22, viewModel.SelectedVersionOption.Value);
        Assert.Same(table.Rows[1], Assert.Single(viewModel.BuildSelectedTable().Rows));

        viewModel.SelectedVersionOption = viewModel.VersionOptions.Single(option => option.Value is null);

        Assert.Equal(table.Rows.Take(2), viewModel.BuildSelectedTable().Rows);
        Assert.Contains(uncheckedRow, viewModel.Rows);
        Assert.False(uncheckedRow.IsSelected);
    }

    [Fact]
    public void SearchWithoutMatches_ClearsInspectorAndKeepsExportSelectionUntilReset()
    {
        using var culture = TestCulture.UseEnUs();
        var table = BuildMixedTable();
        var viewModel = new NotchExportSelectionViewModel(table);
        viewModel.SelectPreviewRowCommand.Execute(viewModel.Rows[1]);
        Assert.Equal("#2  |  v2.2  |  IC 1  |  Diff 11", viewModel.SelectedRowHeaderText);
        Assert.Equal("#2 · v2.2 · IC 1 · Diff 11", viewModel.SelectedRowContextText);
        Assert.Equal("Linked mapping", viewModel.SelectedRowMappingStatusText);
        Assert.Equal(
            "Anchor diff 11, combine 95%, target1 diff 12 (5%), target2 diff 65535 (0%).",
            viewModel.SelectedRowPayloadText);
        Assert.Equal(
            "anchor_diff=11, combine=95, target1_diff=12, target1_ratio=5, target2_diff=none, target2_ratio=0, flags=0",
            viewModel.SelectedRowColumnBreakdownText);
        Assert.Equal("5%", viewModel.SelectedRowAnalysisRatioSumText);
        Assert.Equal("90%", viewModel.SelectedRowAnalysisDeltaText);

        viewModel.SearchKeyword = "cad=999999";
        viewModel.ShowSelectedOnly = true;
        viewModel.AreAllVisibleRowsSelected = false;

        Assert.Empty(viewModel.Rows);
        Assert.Null(viewModel.SelectedRow);
        Assert.True(viewModel.HasNoSelectedRow);
        Assert.Equal("No row selected.", viewModel.SelectedRowHeaderText);
        Assert.Equal("-", viewModel.SelectedRowContextText);
        Assert.Equal("-", viewModel.SelectedRowPositionText);
        Assert.Equal("{ - }", viewModel.SelectedRowCodePreviewText);
        Assert.Equal("-", viewModel.SelectedRowMappingStatusText);
        Assert.Equal("-", viewModel.SelectedRowPayloadText);
        Assert.Equal("-", viewModel.SelectedRowColumnBreakdownText);
        Assert.Equal("-", viewModel.SelectedRowAnalysisRatioSumText);
        Assert.Equal("-", viewModel.SelectedRowAnalysisDeltaText);
        Assert.True(viewModel.HasSearchFilterChip);
        Assert.True(viewModel.HasModeFilterChip);
        Assert.True(viewModel.HasSelectionFilterChip);
        Assert.False(viewModel.HasNoActiveFilterChips);
        Assert.Equal("Selected 3/3 rows · shown 0.", viewModel.SummaryText);
        Assert.Equal(2, viewModel.V22RowCount);
        Assert.Equal("v2.2 2 · warning 0 · legacy 1 · CAD linked 2 · no CAD 1", viewModel.DistributionText);
        Assert.Equal(table.Rows, viewModel.BuildSelectedTable().Rows);

        viewModel.ClearSearchKeywordCommand.Execute(null);

        Assert.Equal(3, viewModel.Rows.Count);
        Assert.Same(table.Rows[0], viewModel.SelectedRow!.Row);
        Assert.False(viewModel.HasSearchFilterChip);
        Assert.False(viewModel.HasModeFilterChip);
        Assert.False(viewModel.HasSelectionFilterChip);
        Assert.True(viewModel.HasNoActiveFilterChips);
        Assert.Equal(table.Rows, viewModel.BuildSelectedTable().Rows);
    }

    private static NotchTable BuildMixedTable()
    {
        return new NotchTable(
        [
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 10, 100, 200,
                [10, 128, 65535, 0, 0, 65535, 0, 0, 0], "legacy"),
            new NotchTableRow(0, 11, 101, 201, new NotchV22Node(11, 95, 12, 5, 65535, 0, 0), "transfer"),
            new NotchTableRow(1, 12, 102, null, new NotchV22Node(12, 90, 13, 10, 65535, 0, 0), "no CAD"),
        ]);
    }
}
