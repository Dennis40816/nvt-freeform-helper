using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel
{
    private NotchExportSelectionFilterState BuildProjectionFilterState()
    {
        return new NotchExportSelectionFilterState(
            SelectedVersionOption.Value,
            SelectedRowDisplayModeOption.Value,
            SelectedRowDisplayModeText,
            SelectedSortModeOption.Value,
            SearchKeyword,
            ShowSelectedOnly,
            _columnFilters);
    }

    public bool TryBuildColumnFilterDialog(
        string? columnKey,
        out NotchExportColumnFilterDialogViewModel? dialogViewModel)
    {
        dialogViewModel = null;
        if (!NotchExportSelectionColumnFilterService.TryParseField(columnKey, out var field))
        {
            return false;
        }

        var values = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(
            _allRows,
            BuildProjectionFilterState(),
            field);

        if (values.Count == 0)
        {
            return false;
        }

        var selectedKeys = GetColumnFilterSet(field);
        var hasActiveFilter = _columnFilters.ContainsKey(field);
        var options = values
            .Select(value => new NotchExportColumnFilterOptionViewModel(
                value,
                value,
                !hasActiveFilter || selectedKeys.Contains(value)))
            .ToArray();

        dialogViewModel = new NotchExportColumnFilterDialogViewModel(
            field,
            NotchExportSelectionColumnFilterService.BuildDialogTitle(field),
            options);
        return true;
    }

    public void ApplyColumnFilterSelection(
        NotchExportColumnFilterField field,
        IReadOnlyList<string> selectedKeys,
        IReadOnlyList<string> allKeys)
    {
        NotchExportSelectionColumnFilterService.ApplySelection(
            _columnFilters,
            field,
            selectedKeys,
            allKeys);
        RebuildVisibleRows();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(IsFilterTransferOnly));
        OnPropertyChanged(nameof(IsFilterAllRows));
        OnPropertyChanged(nameof(IsFilterLinkedOnly));
        OnPropertyChanged(nameof(IsFilterWarningOnly));
        OnPropertyChanged(nameof(IsFilterNoCadOnly));
        OnPropertyChanged(nameof(IsFilterLegacyOnly));
    }

    private HashSet<string> GetColumnFilterSet(NotchExportColumnFilterField field)
    {
        if (!_columnFilters.TryGetValue(field, out var set))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return set;
    }

    private void ClearSearchKeyword()
    {
        var changed = false;

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            SearchKeyword = string.Empty;
            changed = true;
        }

        if (ShowSelectedOnly)
        {
            ShowSelectedOnly = false;
            changed = true;
        }

        if (_columnFilters.Count > 0)
        {
            _columnFilters.Clear();
            RebuildVisibleRows();
            changed = true;
        }

        if (SelectedRowDisplayModeOption.Value != NotchExportRowDisplayMode.AllRows)
        {
            SetRowDisplayMode(NotchExportRowDisplayMode.AllRows);
            changed = true;
        }

        if (!changed)
        {
            OnPropertyChanged(nameof(HasResettableViewFilters));
        }
    }
}
