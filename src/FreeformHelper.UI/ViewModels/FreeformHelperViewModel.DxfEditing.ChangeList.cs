using FreeformHelper.Application.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private async Task OpenHiddenDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Hidden);

    private async Task OpenDuplicateDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Duplicate);

    private async Task OpenCombinedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Combined);

    private async Task OpenMovedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Moved);

    private async Task ShowDxfEditChangeListAsync(DxfEditChangeKind initialKind)
    {
        if (OpenDxfEditChangeListAsync is null)
        {
            SetStatus("DXF edit details: dialog handler not wired.");
            return;
        }

        var viewModel = new DxfEditChangeListViewModel(
            initialKind,
            BuildDxfEditChangeEntries,
            FocusDxfEditChangeEntry,
            ApplyDxfEditChangeEntry);
        viewModel.Editing.AttachProject(this);
        await OpenDxfEditChangeListAsync(viewModel);
    }

    private List<DxfEditChangeListEntry> BuildDxfEditChangeEntries()
    {
        return DxfEditChangeEntryBuilder.Build(
            _cad, _cadEdit, _combinedCad, CadDocument.IsEffectivelyHidden,
            static (left, right) => DxfEditRules.ArePolygonVerticesEquivalent(left, right));
    }

    private void FocusDxfEditChangeEntry(DxfEditChangeListEntry entry)
    {
        var focusCadPadId = entry.FocusCadPadId ?? entry.CadPadId;
        TryLocateSelection(
            new[] { focusCadPadId },
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"CAD {focusCadPadId} is not visible.",
            successStatusBuilder: (cadCount, _) =>
                $"DXF edit focused: CAD {focusCadPadId} (CAD output={cadCount}).");
    }

    private void ApplyDxfEditChangeEntry(DxfEditChangeListEntry entry)
    {
        switch (entry.Kind)
        {
            case DxfEditChangeKind.Hidden:
                if (_combinedCad.GroupIdBySourceCadId.ContainsKey(entry.CadPadId))
                {
                    ClearCombinedCadGroupForPad(entry.CadPadId);
                }
                else
                {
                    RestoreHiddenCadPad(entry.CadPadId);
                }

                break;
            case DxfEditChangeKind.Duplicate:
                if (_cadEdit.AutoHiddenDuplicateCadPadIds.Contains(entry.CadPadId))
                {
                    RestoreDuplicateCadPad(entry.CadPadId);
                }
                else
                {
                    RehideDuplicateCadPad(entry.CadPadId);
                }

                break;
            case DxfEditChangeKind.Combined:
                ClearCombinedCadGroupForPad(entry.CadPadId);
                break;
            case DxfEditChangeKind.Moved:
                RestoreMovedCadPadLayer(entry.CadPadId);
                break;
            case DxfEditChangeKind.Rotated:
                RestoreRotatedCadPadGeometry(entry.CadPadId);
                break;
        }
    }

    private void RestoreHiddenCadPad(int cadPadId)
    {
        if (_cad is null || !_cadEdit.HiddenCadPadIds.Contains(cadPadId))
        {
            return;
        }

        RestoreHiddenCadPadsCore(
            [cadPadId],
            undoLabel: "DXF restore hidden CAD pad",
            statusTextFactory: _ => $"DXF edit: restored CAD {cadPadId}.",
            logContext: $"DXF edit restored CAD pad {cadPadId}",
            removeFromUndoBatches: true);
    }

    private void ClearCombinedCadGroupForPad(int cadPadId)
    {
        if (!TryResolveCombinedCadGroupId(cadPadId, out var groupId) || _cad is null)
        {
            return;
        }

        ClearCombinedCadGroups(
            [groupId],
            undoLabel: "DXF clear combined group",
            statusPrefix: $"DXF combine: cleared group {groupId}",
            logContext: "DXF combine cleared group");
    }

    private void RestoreMovedCadPadLayer(int cadPadId) => _cadEditWorkflow.RestoreLayer(cadPadId);

    private bool TryResolveCombinedCadGroupId(int cadPadId, out int groupId)
    {
        if (_combinedCad.GroupIdByOutputCadId.TryGetValue(cadPadId, out groupId))
        {
            return true;
        }

        return _combinedCad.GroupIdBySourceCadId.TryGetValue(cadPadId, out groupId);
    }
}
