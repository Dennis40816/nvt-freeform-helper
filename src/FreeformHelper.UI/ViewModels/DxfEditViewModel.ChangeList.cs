// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditViewModel
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
            _interaction.SetStatus("DXF edit details: dialog handler not wired.");
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

    internal List<DxfEditChangeListEntry> BuildDxfEditChangeEntries()
    {
        return DxfEditChangeEntryBuilder.Build(
            _cad, _cadEdit, _combinedCad, CadDocument.IsEffectivelyHidden,
            static (left, right) => DxfEditRules.ArePolygonVerticesEquivalent(left, right));
    }

    internal void ApplyDxfEditChangeEntry(DxfEditChangeListEntry entry)
    {
        switch (entry.Kind)
        {
            case DxfEditChangeKind.Hidden:
                _actions.RestoreHiddenEntry(entry.CadPadId);
                break;
            case DxfEditChangeKind.Duplicate:
                _actions.ToggleDuplicateEntry(entry.CadPadId);
                break;
            case DxfEditChangeKind.Combined:
                _actions.ClearCombinedCadGroupForPad(entry.CadPadId);
                break;
            case DxfEditChangeKind.Moved:
                _actions.RestoreLayer(entry.CadPadId);
                break;
            case DxfEditChangeKind.Rotated:
                _actions.RestoreGeometry(entry.CadPadId);
                break;
        }
    }

    private async Task OpenRotatedDxfEditChangeListAsync()
        => await ShowDxfEditChangeListAsync(DxfEditChangeKind.Rotated);

    private void FocusDxfEditChangeEntry(DxfEditChangeListEntry entry)
        => _interaction.FocusCadPad(entry.FocusCadPadId ?? entry.CadPadId);
}
