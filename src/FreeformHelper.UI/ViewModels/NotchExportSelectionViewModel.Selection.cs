using System.ComponentModel;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel
{
    private void RebuildVisibleRows()
    {
        var selectedBefore = SelectedRow;
        var snapshot = NotchExportSelectionProjectionBuilder.Build(
            _allRows,
            BuildProjectionFilterState(),
            _workspaceCadIds,
            _workspaceRegularIndices);

        ApplyVisibleRows(snapshot.VisibleRows);
        ApplyIcGroups(snapshot.IcGroups);
        ApplyWorkspaceLinkedRows(snapshot.WorkspaceLinkedRows);
        ApplyProjectedSelectedRow(selectedBefore, snapshot);
        _filterViewState = snapshot.FilterViewState;

        UpdateCounts();
        OnPropertyChanged(nameof(SelectedRowPositionText));
        OnPropertyChanged(nameof(AreAllVisibleRowsSelected));
        NotifyProjectionViewStateChanged();
    }

    private void SetViewFilterMode(NotchExportRowDisplayMode mode)
    {
        var current = SelectedRowDisplayModeOption.Value;
        if (current == mode && mode != NotchExportRowDisplayMode.AllRows)
        {
            SetRowDisplayMode(NotchExportRowDisplayMode.AllRows);
            return;
        }

        SetRowDisplayMode(mode);
    }

    private void SetRowDisplayMode(NotchExportRowDisplayMode mode)
    {
        var option = RowDisplayModeOptions.FirstOrDefault(item => item.Value == mode);
        if (string.IsNullOrWhiteSpace(option.Display))
        {
            return;
        }

        if (!Equals(SelectedRowDisplayModeOption, option))
        {
            SelectedRowDisplayModeOption = option;
        }
    }

    private void ApplyColumnFilter(string? target)
    {
        var action = NotchExportSelectionQuickFilterService.ResolveAction(
            target,
            SearchKeyword,
            SelectedRowDisplayModeOption.Value);
        if (action.ShouldUpdateSearchKeyword)
        {
            SearchKeyword = action.SearchKeyword;
        }

        if (action.ShouldUpdateRowDisplayMode)
        {
            SetRowDisplayMode(action.RowDisplayMode);
        }
    }

    private NotchExportIcGroupViewModel CreateGroup(int icIndex, IReadOnlyList<NotchExportRowItemViewModel> rows)
    {
        var group = new NotchExportIcGroupViewModel(
            icIndex,
            rows,
            onSelectAll: () => SetGroupSelection(icIndex, true),
            onSelectNone: () => SetGroupSelection(icIndex, false));
        _groupByIcIndex[icIndex] = group;
        return group;
    }

    private void SetGroupSelection(int icIndex, bool selected)
    {
        if (!_groupByIcIndex.TryGetValue(icIndex, out var group))
        {
            return;
        }

        NotchExportSelectionRowSelectionService.SetGroupSelection(group.Rows, selected);
    }

    private void SelectPreviewRow(NotchExportRowItemViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedRow, row))
        {
            NotifyPreviewRowChanged(row);
            return;
        }

        SelectedRow = row;
    }

    private void SelectAll()
    {
        NotchExportSelectionRowSelectionService.SelectAll(_allRows);
    }

    private void SelectNone()
    {
        NotchExportSelectionRowSelectionService.SelectNone(_allRows);
    }

    private void UseShownOnly()
    {
        NotchExportSelectionRowSelectionService.SelectVisibleOnly(_allRows, Rows);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchExportRowItemViewModel.IsSelected))
        {
            UpdateCounts();
            OnPropertyChanged(nameof(AreAllVisibleRowsSelected));

            if (sender is NotchExportRowItemViewModel row &&
                NotchExportSelectionSelectedRowService.ShouldAutoSelectRowFromSelectionToggle(
                    SelectedRow,
                    row,
                    Rows))
            {
                SelectedRow = row;
            }
        }
    }

    private void UpdateCounts()
    {
        _summarySnapshot = NotchExportSelectionSummaryProjector.Build(
            _allRows,
            Rows,
            SelectedVersionOption.Value);
        SelectedCount = _summarySnapshot.SelectedCount;
        foreach (var group in IcGroups)
        {
            group.UpdateCounts();
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(HasRows));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(AllRowCount));
        OnPropertyChanged(nameof(V22RowCount));
        OnPropertyChanged(nameof(TransferRowCount));
        OnPropertyChanged(nameof(WarningRowCount));
        OnPropertyChanged(nameof(LegacyRowCount));
        OnPropertyChanged(nameof(CadLinkedRowCount));
        OnPropertyChanged(nameof(CadMissingRowCount));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(DistributionText));
        OnPropertyChanged(nameof(ExportButtonText));
        OnPropertyChanged(nameof(CanConfirmExport));
        OnPropertyChanged(nameof(AreAllVisibleRowsSelected));
    }

    private void NotifyProjectionViewStateChanged()
    {
        OnPropertyChanged(nameof(HasSearchKeyword));
        OnPropertyChanged(nameof(HasActiveColumnFilters));
        OnPropertyChanged(nameof(IsRowColumnFilterActive));
        OnPropertyChanged(nameof(IsIcColumnFilterActive));
        OnPropertyChanged(nameof(IsDiffColumnFilterActive));
        OnPropertyChanged(nameof(IsMatchColumnFilterActive));
        OnPropertyChanged(nameof(IsMappingColumnFilterActive));
        OnPropertyChanged(nameof(IsStatusColumnFilterActive));
        OnPropertyChanged(nameof(HasSearchFilterChip));
        OnPropertyChanged(nameof(SearchFilterChipText));
        OnPropertyChanged(nameof(HasModeFilterChip));
        OnPropertyChanged(nameof(ModeFilterChipText));
        OnPropertyChanged(nameof(HasSelectionFilterChip));
        OnPropertyChanged(nameof(SelectionFilterChipText));
        OnPropertyChanged(nameof(HasHeaderFilterChip));
        OnPropertyChanged(nameof(HeaderFilterChipText));
        OnPropertyChanged(nameof(HasNoActiveFilterChips));
        OnPropertyChanged(nameof(HasResettableViewFilters));
        OnPropertyChanged(nameof(ActiveColumnFilterSummaryText));
        OnPropertyChanged(nameof(CurrentSearchScopeText));
        OnPropertyChanged(nameof(VisibleRowsSummaryText));
    }

    private void ApplyVisibleRows(IReadOnlyList<NotchExportRowItemViewModel> visibleRows)
    {
        _rows.Clear();
        foreach (var row in visibleRows)
        {
            _rows.Add(row);
        }
    }

    private void ApplyIcGroups(IReadOnlyList<NotchExportSelectionIcGroupProjection> groups)
    {
        _icGroups.Clear();
        _groupByIcIndex.Clear();
        foreach (var group in groups)
        {
            _icGroups.Add(CreateGroup(group.IcIndex, group.Rows));
        }
    }

    private void ApplyProjectedSelectedRow(
        NotchExportRowItemViewModel? selectedBefore,
        NotchExportSelectionProjectionSnapshot snapshot)
    {
        _suppressPreviewCallback = true;
        SelectedRow = NotchExportSelectionSelectedRowService.ResolveProjectedSelectedRow(
            selectedBefore,
            snapshot.VisibleRows,
            snapshot.WorkspaceLinkedRows);
        _suppressPreviewCallback = false;
    }
}
