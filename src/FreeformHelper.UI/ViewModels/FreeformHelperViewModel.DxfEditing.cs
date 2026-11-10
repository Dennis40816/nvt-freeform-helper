using System.Text;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Handles in-app DXF edit operations (hide/restore) and DXF export workflows.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private sealed record CadEditUndoSnapshot(
        CadPadSet? Cad,
        List<int> HiddenCadIds,
        List<int> AutoHiddenDuplicateCadIds,
        List<List<int>> HiddenUndoBatchesTopToBottom,
        Dictionary<int, int> DuplicateCadPadIdToCanonicalId,
        CombinedCadState Combined);

    private CadEditUndoSnapshot CaptureCadEditUndoSnapshot()
    {
        return new CadEditUndoSnapshot(
            _cad,
            _cadEdit.HiddenCadPadIds.ToList(),
            _cadEdit.AutoHiddenDuplicateCadPadIds.ToList(),
            _cadEdit.HiddenUndoStack.Select(batch => batch.ToList()).ToList(),
            _cadEdit.DuplicateCadPadIdToCanonicalId.ToDictionary(static pair => pair.Key, static pair => pair.Value),
            _combinedCad.Clone());
    }

    private void RestoreCadEditUndoSnapshot(CadEditUndoSnapshot snapshot)
    {
        var previous = new CadEditNotificationSnapshot(this);
        _cad = snapshot.Cad;
        _cadEdit.HiddenCadPadIds.Clear();
        foreach (var id in snapshot.HiddenCadIds)
        {
            _cadEdit.HiddenCadPadIds.Add(id);
        }

        _cadEdit.AutoHiddenDuplicateCadPadIds.Clear();
        foreach (var id in snapshot.AutoHiddenDuplicateCadIds)
        {
            _cadEdit.AutoHiddenDuplicateCadPadIds.Add(id);
        }

        _cadEdit.HiddenUndoStack.Clear();
        for (var index = snapshot.HiddenUndoBatchesTopToBottom.Count - 1; index >= 0; index--)
        {
            _cadEdit.HiddenUndoStack.Push(snapshot.HiddenUndoBatchesTopToBottom[index].ToList());
        }

        _cadEdit.DuplicateCadPadIdToCanonicalId.Clear();
        foreach (var pair in snapshot.DuplicateCadPadIdToCanonicalId)
        {
            _cadEdit.DuplicateCadPadIdToCanonicalId[pair.Key] = pair.Value;
        }

        _combinedCad.CopyFrom(snapshot.Combined);
        _cachedFilteredCadForBuild = null;
        SyncRelayeredCadState(previous);
        SyncRotatedCadState(previous);
        InteractionState.ClosePadInfo();
        _selectionCoordinator.ClearSelection(CanvasHost);
        SyncCombinedCadState(previous);
        SyncHiddenCadIdsToProject(previous);
        SyncDuplicateCadState(previous);
        if (_cad is not null)
        {
            var currentLayers = LayerToggles
                .Select(toggle => toggle.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedLayers = BuildCadLayerUiSnapshot(_cad, layerSelections: null).ToggleStates
                .Select(static state => state.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!currentLayers.SetEquals(expectedLayers))
            {
                var layerSelectionSnapshot = CaptureLayerSelectionSnapshot();
                PreserveDxfDependentLayerSelections();
                UpdateLayerToggles(_cad);
                RestoreLayerSelectionAfterCadLayerEdit(layerSelectionSnapshot, ensureVisibleLayerName: string.Empty);
            }
            else
            {
                FilterCadPadsByLayer();
            }
        }
        else
        {
            FilterCadPadsByLayer();
        }
        if (!RecalcBoundsOnLayerFilter)
        {
            _ = TriggerGridRebuildAsync(requestFit: false);
        }

        MarkUnsaved();
    }

    private CadPadSet? BuildActiveCadPadSet()
    {
        if (_cad is null)
        {
            return null;
        }

        if (_cadEdit.HiddenCadPadIds.Count == 0 && _cadEdit.AutoHiddenDuplicateCadPadIds.Count == 0)
        {
            return _cad;
        }

        var visible = _cad.Pads
            .Where(p => !IsCadPadEffectivelyHidden(p.Id))
            .ToList();
        return new CadPadSet(visible);
    }

    private static string SanitizeFileNameToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "layer";
        }

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        }

        return builder.Length == 0 ? "layer" : builder.ToString();
    }

    private void RemoveHiddenCadIdFromUndoBatches(int cadPadId)
    {
        if (_cadEdit.HiddenUndoStack.Count == 0)
        {
            return;
        }

        var batchesTopToBottom = _cadEdit.HiddenUndoStack
            .Select(static batch => batch.ToList())
            .ToList();
        _cadEdit.HiddenUndoStack.Clear();

        for (var index = batchesTopToBottom.Count - 1; index >= 0; index--)
        {
            var batch = batchesTopToBottom[index];
            batch.RemoveAll(id => id == cadPadId);
            if (batch.Count > 0)
            {
                _cadEdit.HiddenUndoStack.Push(batch);
            }
        }
    }

    /// <summary>
    /// Gets the command to export CAD pads (visible layers only) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfVisibleCommand => _dxfEditCommands.ExportDxfVisibleCommand;

    /// <summary>
    /// Gets the command to export CAD pads (all non-deleted layers) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfAllCommand => _dxfEditCommands.ExportDxfAllCommand;

    /// <summary>
    /// Gets the command to export one DXF layer to image (PNG/BMP/JPG).
    /// </summary>
    public IAsyncRelayCommand ExportDxfLayerImageCommand => _dxfEditCommands.ExportDxfLayerImageCommand;

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

    /// <summary>Creates the DXF edit commands once during construction.</summary>
    private DxfEditCommands CreateDxfEditCommands()
    {
        return new DxfEditCommands(
            ExportDxfVisibleCommand: CreateEditingAsyncCommand(ExportVisibleDxfAsync),
            ExportDxfAllCommand: CreateEditingAsyncCommand(ExportAllDxfAsync),
            ExportDxfLayerImageCommand: CreateEditingAsyncCommand(ExportLayerImageAsync),
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
    }
}
