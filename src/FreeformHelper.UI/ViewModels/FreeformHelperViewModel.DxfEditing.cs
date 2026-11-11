using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.CadEditing;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// Connects the edit child to existing callers and retains commands for later owner moves.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    internal static PersistedDxfEditProjectState CapturePersistedDxfEditProjectState(ProjectFile file) =>
        DxfEditStateCapture.CaptureProjectState(file);

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

    /// <summary>
    /// Gets the command to export CAD pads (visible layers only) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfVisibleCommand => _dxfWorkspaceCommands.ExportDxfVisibleCommand;

    /// <summary>
    /// Gets the command to export CAD pads (all non-deleted layers) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfAllCommand => _dxfWorkspaceCommands.ExportDxfAllCommand;

    /// <summary>
    /// Gets the command to export one DXF layer to image (PNG/BMP/JPG).
    /// </summary>
    public IAsyncRelayCommand ExportDxfLayerImageCommand => _dxfWorkspaceCommands.ExportDxfLayerImageCommand;

    /// <summary>
    /// Gets the command to check the quality of the loaded DXF data.
    /// </summary>
    public IAsyncRelayCommand CheckDxfQualityCommand => _dxfWorkspaceCommands.CheckDxfQualityCommand;

    /// <summary>
    /// Gets the command to select CAD pads flagged by DXF overlap checking.
    /// </summary>
    public IRelayCommand SelectDxfOverlapPadsCommand => _dxfWorkspaceCommands.SelectDxfOverlapPadsCommand;

    /// <summary>
    /// Gets the command to clear overlap highlight and selection state.
    /// </summary>
    public IRelayCommand ClearDxfOverlapHighlightsCommand => _dxfWorkspaceCommands.ClearDxfOverlapHighlightsCommand;

    /// <summary>
    /// Gets the command to show overlap details in a dedicated window.
    /// </summary>
    public IAsyncRelayCommand ShowDxfOverlapDetailsCommand => _dxfWorkspaceCommands.ShowDxfOverlapDetailsCommand;

    /// <summary>
    /// Gets the command to analyze DXF idx ↔ regular mapping.
    /// </summary>
    public IAsyncRelayCommand AnalyzeIndexMappingCommand => _dxfWorkspaceCommands.AnalyzeIndexMappingCommand;

    /// <summary>
    /// Gets the command to shift diff idx for all selected CAD pads by one shared offset.
    /// </summary>
    public IRelayCommand OffsetSelectedCadOutputFwDiffIndicesCommand => _dxfWorkspaceCommands.OffsetSelectedCadOutputFwDiffIndicesCommand;

    /// <summary>
    /// Gets the command to select and focus one duplicate diff group in Step 4.
    /// </summary>
    public IRelayCommand<Step4DuplicateDiffGroupViewModel?> SelectStep4DuplicateDiffGroupCommand =>
        _dxfWorkspaceCommands.SelectStep4DuplicateDiffGroupCommand;

    private readonly DxfWorkspaceCommands _dxfWorkspaceCommands;

    private DxfWorkspaceCommands CreateDxfWorkspaceCommands()
    {
        return new DxfWorkspaceCommands(
            ExportDxfVisibleCommand: CreateEditingAsyncCommand(ExportVisibleDxfAsync),
            ExportDxfAllCommand: CreateEditingAsyncCommand(ExportAllDxfAsync),
            ExportDxfLayerImageCommand: CreateEditingAsyncCommand(ExportLayerImageAsync),
            CheckDxfQualityCommand: CreateEditingAsyncCommand(CheckDxfQualityAsync),
            SelectDxfOverlapPadsCommand: CreateEditingCommand(SelectDxfOverlapPads),
            ClearDxfOverlapHighlightsCommand: CreateEditingCommand(ClearDxfOverlapHighlights),
            ShowDxfOverlapDetailsCommand: CreateEditingAsyncCommand(ShowDxfOverlapDetailsAsync),
            AnalyzeIndexMappingCommand: CreateEditingAsyncCommand(AnalyzeIndexMappingAsync),
            OffsetSelectedCadOutputFwDiffIndicesCommand: CreateEditingCommand(OffsetSelectedCadOutputFwDiffIndices),
            SelectStep4DuplicateDiffGroupCommand: CreateEditingCommand<Step4DuplicateDiffGroupViewModel?>(SelectStep4DuplicateDiffGroup));
    }

    // Compatibility surface for callers outside the edit panel. All state and commands belong to the child.
    public int DeletedCadPadCount => DxfEdit.DeletedCadPadCount;
    public bool HasDeletedCadPads => DxfEdit.HasDeletedCadPads;
    public int DuplicateCadPadCount => DxfEdit.DuplicateCadPadCount;
    public bool HasDuplicateCadPads => DxfEdit.HasDuplicateCadPads;
    public int CombinedCadPadCount => DxfEdit.CombinedCadPadCount;
    public bool HasCombinedCadPads => DxfEdit.HasCombinedCadPads;
    public int RelayeredCadPadCount { get => DxfEdit.RelayeredCadPadCount; set => DxfEdit.RelayeredCadPadCount = value; }
    public bool HasRelayeredCadPads => DxfEdit.HasRelayeredCadPads;
    public int RotatedCadPadCount { get => DxfEdit.RotatedCadPadCount; set => DxfEdit.RotatedCadPadCount = value; }
    public bool HasRotatedCadPads => DxfEdit.HasRotatedCadPads;
    public bool HasAnyCadEdits => DxfEdit.HasAnyCadEdits;
    public string DxfEditTargetLayerName { get => DxfEdit.DxfEditTargetLayerName; set => DxfEdit.DxfEditTargetLayerName = value; }
    public DxfEditRotationScopeOption SelectedDxfEditRotationScopeOption { get => DxfEdit.SelectedDxfEditRotationScopeOption; set => DxfEdit.SelectedDxfEditRotationScopeOption = value; }
    public string DxfEditRotationLayerName { get => DxfEdit.DxfEditRotationLayerName; set => DxfEdit.DxfEditRotationLayerName = value; }
    public string DxfEditNewLayerName { get => DxfEdit.DxfEditNewLayerName; set => DxfEdit.DxfEditNewLayerName = value; }
    public decimal DxfEditRotationDegrees { get => DxfEdit.DxfEditRotationDegrees; set => DxfEdit.DxfEditRotationDegrees = value; }
    public bool IsDxfEditAdvancedPanelExpanded { get => DxfEdit.IsDxfEditAdvancedPanelExpanded; set => DxfEdit.IsDxfEditAdvancedPanelExpanded = value; }
    public IReadOnlyList<DxfEditRotationScopeOption> DxfEditRotationScopeOptions => DxfEdit.DxfEditRotationScopeOptions;
    public bool HasSelectedCadPadsForDxfEdit => DxfEdit.HasSelectedCadPadsForDxfEdit;
    public bool HasSelectedCombinedCadPadsForDxfEdit => DxfEdit.HasSelectedCombinedCadPadsForDxfEdit;
    public bool HasManualHiddenCadPads => DxfEdit.HasManualHiddenCadPads;
    public bool HasDetectedDuplicateCadPads => DxfEdit.HasDetectedDuplicateCadPads;
    public bool CanMoveSelectedCadPadsToLayer => DxfEdit.CanMoveSelectedCadPadsToLayer;
    public bool CanCreateLayerAndMoveSelectedCadPads => DxfEdit.CanCreateLayerAndMoveSelectedCadPads;
    public bool IsDxfEditRotationLayerScope => DxfEdit.IsDxfEditRotationLayerScope;
    public string DxfEditSelectionSummary => DxfEdit.DxfEditSelectionSummary;
    public string HiddenDxfEditBadgeTooltip => DxfEdit.HiddenDxfEditBadgeTooltip;
    public string CombinedDxfEditBadgeTooltip => DxfEdit.CombinedDxfEditBadgeTooltip;
    public string DuplicateDxfEditBadgeTooltip => DxfEdit.DuplicateDxfEditBadgeTooltip;
    public string MovedDxfEditBadgeTooltip => DxfEdit.MovedDxfEditBadgeTooltip;
    public bool CanRotateSelectedCadPads => DxfEdit.CanRotateSelectedCadPads;
    public string RotatedDxfEditBadgeTooltip => DxfEdit.RotatedDxfEditBadgeTooltip;
    public IRelayCommand DeleteSelectedCadPadsCommand => DxfEdit.DeleteSelectedCadPadsCommand;
    public IRelayCommand RestoreLastDeletedCadPadsCommand => DxfEdit.RestoreLastDeletedCadPadsCommand;
    public IRelayCommand RestoreAllHiddenCadPadsCommand => DxfEdit.RestoreAllHiddenCadPadsCommand;
    public IRelayCommand CombineSelectedCadPadsCommand => DxfEdit.CombineSelectedCadPadsCommand;
    public IRelayCommand ClearCombinedCadPadsCommand => DxfEdit.ClearCombinedCadPadsCommand;
    public IRelayCommand ResetAllDxfEditsCommand => DxfEdit.ResetAllDxfEditsCommand;
    public IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand => DxfEdit.OpenHiddenDxfEditChangeListCommand;
    public IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand => DxfEdit.OpenDuplicateDxfEditChangeListCommand;
    public IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand => DxfEdit.OpenCombinedDxfEditChangeListCommand;
    public IAsyncRelayCommand OpenMovedDxfEditChangeListCommand => DxfEdit.OpenMovedDxfEditChangeListCommand;
    public IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand => DxfEdit.OpenRotatedDxfEditChangeListCommand;
    public IRelayCommand MoveSelectedCadPadsToLayerCommand => DxfEdit.MoveSelectedCadPadsToLayerCommand;
    public IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand => DxfEdit.CreateLayerAndMoveSelectedCadPadsCommand;
    public IRelayCommand RotateSelectedCadPadsCommand => DxfEdit.RotateSelectedCadPadsCommand;
    public ObservableCollection<string> DxfEditLayerOptions { get => DxfEdit.DxfEditLayerOptions; set => DxfEdit.DxfEditLayerOptions = value; }

    private CadEditState _cadEdit => CadDocument.Edit;
    private CadEditNotificationSnapshot CaptureCadEditNotifications() => DxfEdit.CaptureCadEditNotifications();
    private void ResetDxfEditBaseline(CadPadSet? cad) => _cadEditWorkflow.ResetBaseline(cad, CaptureCadEditNotifications());
    private void SyncHiddenCadIdsToProject(CadEditNotificationSnapshot? previous = null) => DxfEdit.SyncHiddenCadIdsToProject(previous);
    private void SyncCombinedCadState(CadEditNotificationSnapshot? previous = null) => DxfEdit.SyncCombinedCadState(previous);
    private void SyncDuplicateCadState(CadEditNotificationSnapshot? previous = null, bool persistToProject = true) => DxfEdit.SyncDuplicateCadState(previous, persistToProject);
    private void SyncRelayeredCadState(CadEditNotificationSnapshot? previous = null) => DxfEdit.SyncRelayeredCadState(previous);
    private void SyncRotatedCadState(CadEditNotificationSnapshot? previous = null) => DxfEdit.SyncRotatedCadState(previous);
    private void RefreshDxfEditLayerOptions(IReadOnlyList<string> layerNames) => DxfEdit.RefreshDxfEditLayerOptions(layerNames);
    private void NotifyDxfEditPanelActionStateChanged() => DxfEdit.NotifyDxfEditPanelActionStateChanged();
}
