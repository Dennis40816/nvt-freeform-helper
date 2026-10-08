using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel
{
    public void ApplyWorkspaceSelection(IReadOnlyList<int> cadIds, IReadOnlyList<int> regularIndices)
    {
        _workspaceCadIds = cadIds ?? Array.Empty<int>();
        _workspaceRegularIndices = regularIndices ?? Array.Empty<int>();
        RefreshWorkspaceLinkedRows();
    }

    private void RefreshWorkspaceLinkedRows()
    {
        var snapshot = NotchExportSelectionWorkspaceLinkService.Build(
            Rows,
            _workspaceCadIds,
            _workspaceRegularIndices,
            SelectedRow);

        ApplyWorkspaceLinkedRows(snapshot.LinkedRows);
        ApplyWorkspaceLinkedSelectedRow(snapshot.PreferredSelectedRow);
    }

    private void SetWorkspaceLinkedRowsSelected(bool selected)
    {
        NotchExportSelectionRowSelectionService.SetWorkspaceLinkedRowsSelected(WorkspaceLinkedRows, selected);
    }

    private string BuildWorkspaceSelectionSummaryText()
    {
        var selectedCadCount = _workspaceCadIds.Count;
        var selectedRegularCount = _workspaceRegularIndices.Count;
        return $"{WorkspaceLinkedRows.Count} linked row(s) · CAD {selectedCadCount} · REG {selectedRegularCount}";
    }

    private void NotifyPreviewRowChanged(NotchExportRowItemViewModel row)
    {
        if (_suppressPreviewCallback || !Editing.IsProjectEditingEnabled)
        {
            return;
        }

        _previewRowChanged?.Invoke(row.Row);
    }

    private void ApplyWorkspaceLinkedRows(IReadOnlyList<NotchExportRowItemViewModel> linkedRows)
    {
        _workspaceLinkedRows.Clear();
        foreach (var row in linkedRows)
        {
            _workspaceLinkedRows.Add(row);
        }

        OnPropertyChanged(nameof(HasWorkspaceLinkedRows));
        OnPropertyChanged(nameof(HasNoWorkspaceLinkedRows));
        OnPropertyChanged(nameof(WorkspaceSelectionSummaryText));
    }

    private void ApplyWorkspaceLinkedSelectedRow(NotchExportRowItemViewModel? preferredSelectedRow)
    {
        if (preferredSelectedRow is null || ReferenceEquals(SelectedRow, preferredSelectedRow))
        {
            return;
        }

        // Workspace selection sync should only update row highlight/context.
        // Do not trigger preview callback (which performs locate+zoom).
        _suppressPreviewCallback = true;
        SelectedRow = preferredSelectedRow;
        _suppressPreviewCallback = false;
    }
}
