using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;
using FreeformHelper.UI.CadExport;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Handles in-app DXF edit operations (hide/restore).
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    internal static PersistedDxfEditProjectState CapturePersistedDxfEditProjectState(ProjectFile file) =>
        DxfEditStateCapture.CaptureProjectState(file);

    private CadEditNotificationSnapshot CaptureCadEditNotifications() =>
        new(DeletedCadPadCount, DuplicateCadPadCount, CombinedCadPadCount, RelayeredCadPadCount, RotatedCadPadCount);

    private bool CadLayersMatchDocument(CadPadSet cad)
    {
        var current = LayerToggles.Select(static toggle => toggle.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = BuildCadLayerUiSnapshot(cad, layerSelections: null).ToggleStates
            .Select(static state => state.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.SetEquals(expected);
    }

    private void RebuildAfterCadEdit()
    {
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }
    }

    /// <summary>
    /// Gets the command to export CAD pads (visible layers only) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfVisibleCommand => CadExport.ExportDxfVisibleCommand;

    /// <summary>
    /// Gets the command to export CAD pads (all non-deleted layers) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfAllCommand => CadExport.ExportDxfAllCommand;

    /// <summary>
    /// Gets the command to export one DXF layer to image (PNG/BMP/JPG).
    /// </summary>
    public IAsyncRelayCommand ExportDxfLayerImageCommand => CadExport.ExportDxfLayerImageCommand;

    /// <summary>
    /// Gets the command to check the quality of the loaded DXF data.
    /// </summary>
    public IAsyncRelayCommand CheckDxfQualityCommand => _dxfEditCommands.CheckDxfQualityCommand;

    /// <summary>
    /// Gets the command to select CAD pads flagged by DXF overlap checking.
    /// </summary>
    public IRelayCommand SelectDxfOverlapPadsCommand => _dxfEditCommands.SelectDxfOverlapPadsCommand;

    /// <summary>
    /// Gets the command to clear overlap highlight and selection state.
    /// </summary>
    public IRelayCommand ClearDxfOverlapHighlightsCommand => _dxfEditCommands.ClearDxfOverlapHighlightsCommand;

    /// <summary>
    /// Gets the command to show overlap details in a dedicated window.
    /// </summary>
    public IAsyncRelayCommand ShowDxfOverlapDetailsCommand => _dxfEditCommands.ShowDxfOverlapDetailsCommand;

    /// <summary>
    /// Gets the command to analyze DXF idx ↔ regular mapping.
    /// </summary>
    public IAsyncRelayCommand AnalyzeIndexMappingCommand => _dxfEditCommands.AnalyzeIndexMappingCommand;

    /// <summary>
    /// Gets the command to shift diff idx for all selected CAD pads by one shared offset.
    /// </summary>
    public IRelayCommand OffsetSelectedCadOutputFwDiffIndicesCommand => _dxfEditCommands.OffsetSelectedCadOutputFwDiffIndicesCommand;

    /// <summary>
    /// Gets the command to select and focus one duplicate diff group in Step 4.
    /// </summary>
    public IRelayCommand<Step4DuplicateDiffGroupViewModel?> SelectStep4DuplicateDiffGroupCommand =>
        _dxfEditCommands.SelectStep4DuplicateDiffGroupCommand;

    /// <summary>
    /// Gets the command to hide selected CAD pads.
    /// </summary>
    public IRelayCommand DeleteSelectedCadPadsCommand => _dxfEditCommands.DeleteSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to undo the last CAD hide action.
    /// </summary>
    public IRelayCommand RestoreLastDeletedCadPadsCommand => _dxfEditCommands.RestoreLastDeletedCadPadsCommand;

    /// <summary>
    /// Gets the command to restore all manually hidden CAD pads.
    /// </summary>
    public IRelayCommand RestoreAllHiddenCadPadsCommand => _dxfEditCommands.RestoreAllHiddenCadPadsCommand;

    /// <summary>
    /// Gets the command to combine selected CAD pads into one synthetic pad.
    /// </summary>
    public IRelayCommand CombineSelectedCadPadsCommand => _dxfEditCommands.CombineSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to clear the selected synthetic combined CAD groups.
    /// </summary>
    public IRelayCommand ClearCombinedCadPadsCommand => _dxfEditCommands.ClearCombinedCadPadsCommand;

    /// <summary>
    /// Gets the command to reset all DXF edits, including hidden, combined, and layer-move changes.
    /// </summary>
    public IRelayCommand ResetAllDxfEditsCommand => _dxfEditCommands.ResetAllDxfEditsCommand;

    /// <summary>
    /// Gets the command to open the Hidden DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand => _dxfEditCommands.OpenHiddenDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Duplicate DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand => _dxfEditCommands.OpenDuplicateDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Combined DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand => _dxfEditCommands.OpenCombinedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Moved DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenMovedDxfEditChangeListCommand => _dxfEditCommands.OpenMovedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to open the Rotated DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand => _dxfEditCommands.OpenRotatedDxfEditChangeListCommand;

    /// <summary>
    /// Gets the command to move selected CAD pads to an existing DXF layer.
    /// </summary>
    public IRelayCommand MoveSelectedCadPadsToLayerCommand => _dxfEditCommands.MoveSelectedCadPadsToLayerCommand;

    /// <summary>
    /// Gets the command to create a new DXF layer and move selected CAD pads into it.
    /// </summary>
    public IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand => _dxfEditCommands.CreateLayerAndMoveSelectedCadPadsCommand;

    /// <summary>
    /// Gets the command to rotate selected CAD pads around their own centroids.
    /// </summary>
    public IRelayCommand RotateSelectedCadPadsCommand => _dxfEditCommands.RotateSelectedCadPadsCommand;

    private readonly DxfEditCommands _dxfEditCommands;

    /// <summary>Creates the CAD export child and the DXF edit commands once during construction. The export commands register first.</summary>
    private (CadExportViewModel Export, DxfEditCommands Edit) CreateDxfEditCommands()
    {
        var export = CreateCadExport();
        var edit = new DxfEditCommands(
            CheckDxfQualityCommand: CreateEditingAsyncCommand(CheckDxfQualityAsync),
            SelectDxfOverlapPadsCommand: CreateEditingCommand(SelectDxfOverlapPads),
            ClearDxfOverlapHighlightsCommand: CreateEditingCommand(ClearDxfOverlapHighlights),
            ShowDxfOverlapDetailsCommand: CreateEditingAsyncCommand(ShowDxfOverlapDetailsAsync),
            AnalyzeIndexMappingCommand: CreateEditingAsyncCommand(AnalyzeIndexMappingAsync),
            OffsetSelectedCadOutputFwDiffIndicesCommand: CreateEditingCommand(OffsetSelectedCadOutputFwDiffIndices),
            SelectStep4DuplicateDiffGroupCommand: CreateEditingCommand<Step4DuplicateDiffGroupViewModel?>(SelectStep4DuplicateDiffGroup),
            DeleteSelectedCadPadsCommand: CreateEditingCommand(DeleteSelectedCadPads),
            RestoreLastDeletedCadPadsCommand: CreateEditingCommand(RestoreLastDeletedCadPads),
            RestoreAllHiddenCadPadsCommand: CreateEditingCommand(RestoreAllHiddenCadPads),
            CombineSelectedCadPadsCommand: CreateEditingCommand(CombineSelectedCadPads),
            ClearCombinedCadPadsCommand: CreateEditingCommand(ClearCombinedCadPads),
            ResetAllDxfEditsCommand: CreateEditingCommand(ResetAllDxfEdits),
            OpenHiddenDxfEditChangeListCommand: CreateEditingAsyncCommand(OpenHiddenDxfEditChangeListAsync),
            OpenDuplicateDxfEditChangeListCommand: CreateEditingAsyncCommand(OpenDuplicateDxfEditChangeListAsync),
            OpenCombinedDxfEditChangeListCommand: CreateEditingAsyncCommand(OpenCombinedDxfEditChangeListAsync),
            OpenMovedDxfEditChangeListCommand: CreateEditingAsyncCommand(OpenMovedDxfEditChangeListAsync),
            OpenRotatedDxfEditChangeListCommand: CreateEditingAsyncCommand(OpenRotatedDxfEditChangeListAsync),
            MoveSelectedCadPadsToLayerCommand: CreateEditingCommand(MoveSelectedCadPadsToLayer),
            CreateLayerAndMoveSelectedCadPadsCommand: CreateEditingCommand(CreateLayerAndMoveSelectedCadPads),
            RotateSelectedCadPadsCommand: CreateEditingCommand(RotateSelectedCadPads));
        return (export, edit);
    }
}
