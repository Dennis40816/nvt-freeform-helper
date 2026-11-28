// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionCallerBehaviorTests
{
    [Fact]
    public void Constructor_WithEmptyTable_LeavesVersionAndExportSelectionUnavailable()
    {
        using var culture = TestCulture.UseEnUs();

        var vm = new NotchExportSelectionViewModel(new NotchTable([]));

        Assert.False(vm.CanSelectVersion);
        Assert.Null(Assert.Single(vm.VersionOptions).Value);
        Assert.Empty(vm.Rows);
        Assert.Empty(vm.IcGroups);
        Assert.False(vm.HasRows);
        Assert.False(vm.HasSelection);
        Assert.False(vm.AreAllVisibleRowsSelected);
        Assert.False(vm.HasSelectedRow);
        Assert.Equal("-", vm.SelectedRowPositionText);
        Assert.Empty(vm.BuildSelectedTable().Rows);
    }

    [Fact]
    public void Constructor_WhenPreferredExportTypeIsNoLongerOffered_UsesFirstAvailableTypeAndItsVersion()
    {
        using var culture = TestCulture.UseEnUs();
        NotchTableRow[] rows =
        [
            new(NotchAlgorithmVersion.V21, 0, 10, 100, 200,
                [10, 128, 11, 64, 0, 65535, 0, 0, 0], "legacy"),
            new(0, 11, 101, 201, new NotchV22Node(11, 90, 12, 10, 65535, 0, 0), "transfer"),
        ];
        NotchExportFileTypeOption[] offered =
        [
            new(FreeformHelperViewModel.NotchExportFileType.Cv22, "C v2.2 (.c)", "c", NotchAlgorithmVersion.V22),
            new(FreeformHelperViewModel.NotchExportFileType.Csv, "CSV review (.csv)", "csv"),
        ];
        var removedPreference = new NotchExportFileTypeOption(
            FreeformHelperViewModel.NotchExportFileType.Cv21, "C v2.1 (.c)", "c", NotchAlgorithmVersion.V21);

        var vm = new NotchExportSelectionViewModel(
            new NotchTable(rows), exportTypeOptions: offered, selectedExportTypeOption: removedPreference);

        Assert.Equal(offered[0], vm.SelectedExportTypeOption);
        Assert.Equal(NotchAlgorithmVersion.V22, vm.SelectedVersionOption.Value);
        Assert.True(vm.IsVersionSelectionPinnedByExportType);
        Assert.False(vm.CanSelectVersion);
        Assert.Same(rows[1], Assert.Single(vm.Rows).Row);
        Assert.Same(rows[1], Assert.Single(vm.BuildSelectedTable().Rows));
        Assert.Equal(1, vm.SelectedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VisibleSelection_WhenReapplied_NotifiesTheHeaderAndPreservesHiddenExportRows(bool selected)
    {
        using var culture = TestCulture.UseEnUs();
        var table = CreateTableWithHiddenNoOp();
        var vm = new NotchExportSelectionViewModel(table);
        vm.AreAllVisibleRowsSelected = selected;
        var changedProperties = new List<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        vm.AreAllVisibleRowsSelected = selected;

        Assert.Contains(nameof(vm.AreAllVisibleRowsSelected), changedProperties);
        Assert.Equal(selected, vm.AreAllVisibleRowsSelected);
        Assert.All(vm.Rows, row => Assert.Equal(selected, row.IsSelected));
        NotchTableRow[] expectedRows = selected ? table.Rows.ToArray() : [table.Rows[0]];
        Assert.Equal(expectedRows, vm.BuildSelectedTable().Rows);
        Assert.Equal(expectedRows.Length, vm.SelectedCount);
        Assert.Equal(1, vm.HiddenSelectedRowCount);
    }

    [Fact]
    public void ToFullCoverageDetail_WhenSearchHidesEveryRow_KeepsMissingTargetDiagnosticsVisible()
    {
        using var culture = TestCulture.UseEnUs();
        var audit = new NotchToFullCoverageAudit(
            BucketCount: 1, ExpectedTargetDiffCount: 2, CoveredTargetDiffCount: 1,
            MissingGaps: [new NotchToFullCoverageGap(0, 10, 12, [200])]);
        NotchTableRow[] rows =
        [
            new(0, 10, 100, 200, new NotchV22Node(10, 100, 11, 20, 12, 10, 0), "two target legs"),
            new(0, 11, 101, 201, new NotchV22Node(11, 100, 65535, 0, 65535, 0, 0), "covered target"),
        ];
        var vm = new NotchExportSelectionViewModel(new NotchTable(rows, audit));

        vm.SearchKeyword = "diff=999";

        Assert.Empty(vm.Rows);
        Assert.Equal(rows, vm.BuildSelectedTable().Rows);
        Assert.True(vm.HasToFullCoverageWarning);
        Assert.False(vm.HasToFullCoveragePass);
        Assert.True(vm.HasToFullCoverageMissingList);
        Assert.Equal("Expected target diffs=2 | covered=1 | missing=1.", vm.ToFullCoverageDetailText);
        Assert.Equal("IC 1 / FW Diff 10 -> FW Diff 12 | CAD 200", vm.ToFullCoverageMissingListText);
    }

    [Fact]
    public void SelectPreviewRow_ForACachedRowHiddenBySearch_ShowsItsPayloadWithoutChangingExportSelection()
    {
        using var culture = TestCulture.UseEnUs();
        var table = CreateTableWithHiddenNoOp();
        NotchTableRow? callbackRow = null;
        var vm = new NotchExportSelectionViewModel(table, row => callbackRow = row);
        var cachedRow = vm.Rows.Single(static row => row.Row.DiffIndex == 11);
        vm.SearchKeyword = "diff=20";
        Assert.DoesNotContain(cachedRow, vm.Rows);

        vm.SelectPreviewRowCommand.Execute(cachedRow);

        Assert.Same(cachedRow, vm.SelectedRow);
        Assert.True(cachedRow.IsPreviewSelected);
        Assert.Equal("-/1", vm.SelectedRowPositionText);
        Assert.Equal("{ 11, 90, 12, 10, 65535, 0, 0 }", vm.SelectedRowCodePreviewText);
        Assert.Same(cachedRow.Row, callbackRow);
        Assert.False(Assert.Single(vm.Rows).IsPreviewSelected);
        Assert.Equal(table.Rows, vm.BuildSelectedTable().Rows);

        vm.ClearSearchKeywordCommand.Execute(null);

        Assert.Same(cachedRow, vm.SelectedRow);
        Assert.Equal("Row 2/3", vm.SelectedRowPositionText);
        Assert.Equal(table.Rows, vm.BuildSelectedTable().Rows);
    }

    private static NotchTable CreateTableWithHiddenNoOp()
    {
        return new NotchTable(
        [
            new NotchTableRow(0, 10, 100, 200, new NotchV22Node(10, 100, 65535, 0, 65535, 0, 0), "no-op"),
            new NotchTableRow(0, 11, 101, 201, new NotchV22Node(11, 90, 12, 10, 65535, 0, 0), "transfer"),
            new NotchTableRow(1, 20, 102, 202, new NotchV22Node(20, 90, 21, 10, 65535, 0, 0), "other IC"),
        ]);
    }
}
