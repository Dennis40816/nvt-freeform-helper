using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class NotchExportSelectionRowSelectionService
{
    public static void SelectAll(IReadOnlyList<NotchExportRowItemViewModel> rows)
    {
        SetRowsSelected(rows, selected: true);
    }

    public static void SelectNone(IReadOnlyList<NotchExportRowItemViewModel> rows)
    {
        SetRowsSelected(rows, selected: false);
    }

    public static void SelectVisibleOnly(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows)
    {
        var visibleSet = new HashSet<NotchExportRowItemViewModel>(visibleRows);
        foreach (var row in allRows)
        {
            row.IsSelected = visibleSet.Contains(row);
        }
    }

    public static void SetGroupSelection(
        IReadOnlyList<NotchExportRowItemViewModel> groupRows,
        bool selected)
    {
        SetRowsSelected(groupRows, selected);
    }

    public static void SetWorkspaceLinkedRowsSelected(
        IReadOnlyList<NotchExportRowItemViewModel> rows,
        bool selected)
    {
        SetRowsSelected(rows, selected);
    }

    private static void SetRowsSelected(
        IReadOnlyList<NotchExportRowItemViewModel> rows,
        bool selected)
    {
        foreach (var row in rows)
        {
            row.IsSelected = selected;
        }
    }
}
