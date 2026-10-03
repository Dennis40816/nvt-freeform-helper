using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

internal static class NotchExportSelectionWorkspaceLinkService
{
    public static NotchExportSelectionWorkspaceLinkSnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows,
        IReadOnlyList<int> workspaceCadIds,
        IReadOnlyList<int> workspaceRegularIndices,
        NotchExportRowItemViewModel? selectedRow)
    {
        var linkedRows = NotchExportSelectionProjectionBuilder.BuildWorkspaceLinkedRows(
            visibleRows,
            workspaceCadIds,
            workspaceRegularIndices);
        var preferredSelectedRow = ResolvePreferredSelectedRow(linkedRows, selectedRow);
        return new NotchExportSelectionWorkspaceLinkSnapshot(linkedRows, preferredSelectedRow);
    }

    private static NotchExportRowItemViewModel? ResolvePreferredSelectedRow(
        IReadOnlyList<NotchExportRowItemViewModel> linkedRows,
        NotchExportRowItemViewModel? selectedRow)
    {
        if (linkedRows.Count == 0)
        {
            return selectedRow;
        }

        if (ReferenceEquals(selectedRow, linkedRows[0]))
        {
            return selectedRow;
        }

        return linkedRows[0];
    }
}

internal sealed record NotchExportSelectionWorkspaceLinkSnapshot(
    IReadOnlyList<NotchExportRowItemViewModel> LinkedRows,
    NotchExportRowItemViewModel? PreferredSelectedRow);
