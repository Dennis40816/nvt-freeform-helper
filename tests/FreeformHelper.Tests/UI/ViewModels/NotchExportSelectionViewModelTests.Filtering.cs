using System.Collections.ObjectModel;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchExportSelectionViewModelTests
{
    private static readonly string[] _linkedStatusFilterKeys = ["Linked"];
    private static readonly string[] _statusFilterAllKeys = ["Linked", "Warning", "No CAD", "Legacy"];
    private static readonly string[] _linkedAndNoOpStatusKeys = ["Linked", "No-op"];
    private static readonly string[] _diff9FilterKeys = ["9"];
    private static readonly string[] _diffFilterAllKeys = ["8", "9", "10"];
    private static readonly string[] _expectedDiffDialogKeys = ["10", "9"];
    private static readonly string[] _expectedRangeSelectedKeys = ["21", "24"];

    [Fact]
    public void ToggleSelectedOnlyCommand_FiltersAndRestoresRowsWithoutChangingSelection()
    {
        var vm = new NotchExportSelectionViewModel(BuildSampleTable());
        var originalRows = vm.Rows.ToArray();
        var uncheckedRow = vm.Rows[1];
        uncheckedRow.IsSelected = false;

        vm.ToggleSelectedOnlyCommand.Execute(null);

        Assert.True(vm.IsSelectedOnlyActive);
        Assert.Equal(2, vm.Rows.Count);
        Assert.DoesNotContain(uncheckedRow, vm.Rows);
        Assert.All(vm.Rows, row => Assert.True(row.IsSelected));
        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(vm.Rows.Select(row => row.Row), vm.BuildSelectedTable().Rows);

        vm.ToggleSelectedOnlyCommand.Execute(null);

        Assert.False(vm.IsSelectedOnlyActive);
        Assert.Equal(originalRows, vm.Rows);
        Assert.False(uncheckedRow.IsSelected);
        Assert.Equal(2, vm.SelectedCount);
    }


    [Fact]
    public void RowDisplayMode_WarningOnly_ShowsNoOpRows()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        var warningOnly = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.WarningOnly);
        vm.SelectedRowDisplayModeOption = warningOnly;

        Assert.Single(vm.Rows);
        Assert.True(vm.Rows[0].IsStatusWarning);
        Assert.Equal(1, vm.WarningRowCount);
    }


    [Fact]
    public void SearchKeyword_FiltersVisibleRows_ByMappingTokens()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        vm.SearchKeyword = "CAD 201";

        Assert.Single(vm.Rows);
        Assert.Equal("201", vm.Rows[0].CadText);
    }


    [Fact]
    public void SearchKeyword_FieldQuery_FiltersByAnchorAlias()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        vm.SearchKeyword = "a=9";

        Assert.Single(vm.Rows);
        Assert.Equal(9, vm.Rows[0].Row.DiffIndex);
    }


    [Fact]
    public void SearchKeyword_FieldQuery_CanFilterNoCadRows()
    {
        var table = BuildCadStatusFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        vm.SearchKeyword = "cad=none";

        Assert.Single(vm.Rows);
        Assert.False(vm.Rows[0].Row.CadPadId.HasValue);
    }


    [Fact]
    public void SearchKeyword_FieldQuery_StatusSupportsNoCadAlias()
    {
        var table = BuildCadStatusFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        vm.SearchKeyword = "status=nocad";

        Assert.Single(vm.Rows);
        Assert.Equal("No CAD", vm.Rows[0].StatusText);
    }


    [Fact]
    public void ClearSearchKeywordCommand_ResetsAllViewFilters_ToViewAll()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);
        vm.SearchKeyword = "cad=201";
        vm.ShowSelectedOnly = true;
        vm.ApplyColumnFilterSelection(
            NotchExportColumnFilterField.Status,
            _linkedStatusFilterKeys,
            _statusFilterAllKeys);

        Assert.NotEqual(NotchExportRowDisplayMode.AllRows, vm.SelectedRowDisplayModeOption.Value);
        Assert.True(vm.HasSearchKeyword);
        Assert.True(vm.ShowSelectedOnly);
        Assert.True(vm.HasResettableViewFilters);

        vm.ClearSearchKeywordCommand.Execute(null);

        Assert.False(vm.HasSearchKeyword);
        Assert.False(vm.ShowSelectedOnly);
        Assert.Equal(NotchExportRowDisplayMode.AllRows, vm.SelectedRowDisplayModeOption.Value);
        Assert.False(vm.HasResettableViewFilters);
        Assert.Equal(string.Empty, vm.SearchKeyword);
    }


    [Fact]
    public void SelectedVersionOption_FiltersVisibleRows_AndExportRows()
    {
        var table = BuildSampleTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;
        var v21OnlyOption = vm.VersionOptions.First(option => option.Value == NotchAlgorithmVersion.V21);

        vm.SelectedVersionOption = v21OnlyOption;
        var selected = vm.BuildSelectedTable();

        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(2, selected.Rows.Count);
        Assert.All(selected.Rows, row => Assert.Equal(NotchAlgorithmVersion.V21, row.Version));
    }


    [Fact]
    public void SetViewFilterModeCommand_ClickSameMode_TogglesBackToAllRows()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Equal(NotchExportRowDisplayMode.TransferOnly, vm.SelectedRowDisplayModeOption.Value);
        vm.SetViewFilterModeCommand.Execute(NotchExportRowDisplayMode.TransferOnly);

        Assert.Equal(NotchExportRowDisplayMode.AllRows, vm.SelectedRowDisplayModeOption.Value);
    }




    [Fact]
    public void AreAllVisibleRowsSelected_TogglesSelectionForVisibleRowsOnly()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);

        Assert.Single(vm.Rows);
        Assert.True(vm.AreAllVisibleRowsSelected);

        vm.AreAllVisibleRowsSelected = false;
        Assert.False(vm.AreAllVisibleRowsSelected);
        Assert.Equal(1, vm.SelectedCount); // hidden no-op row remains selected

        vm.AreAllVisibleRowsSelected = true;
        Assert.True(vm.AreAllVisibleRowsSelected);
        Assert.Equal(2, vm.SelectedCount);
    }


    [Fact]
    public void TryBuildColumnFilterDialog_ReturnsDistinctValues_ForTargetColumn()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        var built = vm.TryBuildColumnFilterDialog("status", out var dialog);

        Assert.True(built);
        Assert.NotNull(dialog);
        Assert.Equal(NotchExportColumnFilterField.Status, dialog!.Field);
        Assert.Equal(2, dialog.Options.Count);
    }


    [Fact]
    public void TryBuildColumnFilterDialog_DiffOptions_ExcludeCurrentDiffFilter_ButKeepSharedScope()
    {
        var table = BuildColumnFilterProjectionTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        vm.ApplyColumnFilterSelection(
            NotchExportColumnFilterField.Status,
            _linkedStatusFilterKeys,
            _linkedAndNoOpStatusKeys);
        vm.ApplyColumnFilterSelection(
            NotchExportColumnFilterField.Diff,
            _diff9FilterKeys,
            _diffFilterAllKeys);

        Assert.Single(vm.Rows);

        var built = vm.TryBuildColumnFilterDialog("diff", out var dialog);

        Assert.True(built);
        Assert.NotNull(dialog);
        Assert.Equal(_expectedDiffDialogKeys, dialog!.Options.Select(option => option.Key).ToArray());
    }


    [Fact]
    public void ApplyColumnFilterSelection_EmptySelectedKeys_ShowsNoRows_UntilCleared()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        var built = vm.TryBuildColumnFilterDialog("diff", out var dialog);
        Assert.True(built);
        Assert.NotNull(dialog);

        var allKeys = dialog!.GetAllKeys();
        vm.ApplyColumnFilterSelection(NotchExportColumnFilterField.Diff, Array.Empty<string>(), allKeys);

        Assert.True(vm.IsDiffColumnFilterActive);
        Assert.Empty(vm.Rows);

        vm.ApplyColumnFilterSelection(NotchExportColumnFilterField.Diff, allKeys, allKeys);
        Assert.False(vm.IsDiffColumnFilterActive);
        Assert.Equal(2, vm.Rows.Count);
    }


    [Fact]
    public void ApplyColumnFilterSelection_UpdatesHeaderFilterSummary_AndChipState()
    {
        var table = BuildTransferOnlyFilterTable();
        var vm = new NotchExportSelectionViewModel(table);
        var allRowsOption = vm.RowDisplayModeOptions.First(option => option.Value == NotchExportRowDisplayMode.AllRows);
        vm.SelectedRowDisplayModeOption = allRowsOption;

        var built = vm.TryBuildColumnFilterDialog("diff", out var dialog);
        Assert.True(built);
        Assert.NotNull(dialog);

        var allKeys = dialog!.GetAllKeys();
        var selectedKeys = new[] { allKeys[0] };
        vm.ApplyColumnFilterSelection(NotchExportColumnFilterField.Diff, selectedKeys, allKeys);

        Assert.Equal("Search by row / diff / mapping / CAD...", vm.SearchWatermarkText);
        Assert.True(vm.HasHeaderFilterChip);
        Assert.Contains("Header:", vm.HeaderFilterChipText, StringComparison.Ordinal);
    }


    [Fact]
    public void ColumnFilterDialog_RangeMode_ReturnsOnlyKeysInsideBounds()
    {
        var options = new List<NotchExportColumnFilterOptionViewModel>
        {
            new NotchExportColumnFilterOptionViewModel("20", "20", isSelected: true),
            new NotchExportColumnFilterOptionViewModel("21", "21", isSelected: true),
            new NotchExportColumnFilterOptionViewModel("24", "24", isSelected: true),
            new NotchExportColumnFilterOptionViewModel("28", "28", isSelected: true),
        };
        var vm = new NotchExportColumnFilterDialogViewModel(
            NotchExportColumnFilterField.Diff,
            "Filter Diff",
            options);

        vm.UseRangeFilter = true;
        vm.RangeFromText = "21";
        vm.RangeToText = "24";
        var selectedKeys = vm.GetSelectedKeys();

        Assert.Equal(_expectedRangeSelectedKeys, selectedKeys);
    }


    [Fact]
    public void ColumnFilterDialog_ExposesReadOnlyOptionsFacade()
    {
        var options = new List<NotchExportColumnFilterOptionViewModel>
        {
            new NotchExportColumnFilterOptionViewModel("1", "1", isSelected: true),
            new NotchExportColumnFilterOptionViewModel("2", "2", isSelected: false),
        };
        var vm = new NotchExportColumnFilterDialogViewModel(
            NotchExportColumnFilterField.Diff,
            "Filter Diff",
            options);

        Assert.IsType<ReadOnlyObservableCollection<NotchExportColumnFilterOptionViewModel>>(vm.Options);
        Assert.Equal(2, vm.Options.Count);
    }
}
