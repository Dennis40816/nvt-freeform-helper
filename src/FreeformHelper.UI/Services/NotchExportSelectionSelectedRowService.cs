using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public static class NotchExportSelectionSelectedRowService
{
    public static NotchExportRowItemViewModel? ResolveProjectedSelectedRow(
        NotchExportRowItemViewModel? selectedBefore,
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows,
        IReadOnlyList<NotchExportRowItemViewModel> workspaceLinkedRows)
    {
        if (workspaceLinkedRows.Count > 0)
        {
            return workspaceLinkedRows[0];
        }

        if (selectedBefore is not null && visibleRows.Contains(selectedBefore))
        {
            return selectedBefore;
        }

        return visibleRows.Count > 0
            ? visibleRows[0]
            : null;
    }

    public static bool ShouldAutoSelectRowFromSelectionToggle(
        NotchExportRowItemViewModel? currentSelectedRow,
        NotchExportRowItemViewModel changedRow,
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows)
    {
        return currentSelectedRow is null && visibleRows.Contains(changedRow);
    }
}
