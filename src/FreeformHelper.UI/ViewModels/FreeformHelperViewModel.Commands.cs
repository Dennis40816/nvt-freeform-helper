using CommunityToolkit.Mvvm.Input;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> defines and initializes
/// all the commands that the UI can bind to, exposing various functionalities to the user.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    // --- Public Properties for Commands ---

    /// <summary>
    /// Gets the command to open a DXF file.
    /// </summary>
    public IAsyncRelayCommand OpenDxfCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to rebuild the regular grid.
    /// </summary>
    public IAsyncRelayCommand RebuildGridCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to perform pad matching.
    /// </summary>
    public IAsyncRelayCommand MatchCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to import a Step 1 regular visibility mask CSV.
    /// </summary>
    public IAsyncRelayCommand ImportRegularVisibilityMaskCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear the imported Step 1 regular visibility mask CSV.
    /// </summary>
    public IRelayCommand ClearRegularVisibilityMaskCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to automatically detect freeform pads.
    /// </summary>
    public IAsyncRelayCommand AutoDetectFreeformsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to save the current project.
    /// </summary>
    public IAsyncRelayCommand SaveProjectCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to load an existing project.
    /// </summary>
    public IAsyncRelayCommand LoadProjectCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to export the notch table in the currently selected format.
    /// </summary>
    public IAsyncRelayCommand ExportNotchCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to export CAD pads (visible layers only) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfVisibleCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to export CAD pads (all non-deleted layers) to DXF.
    /// </summary>
    public IAsyncRelayCommand ExportDxfAllCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to export one DXF layer to image (PNG/BMP/JPG).
    /// </summary>
    public IAsyncRelayCommand ExportDxfLayerImageCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the notch detail view for the selected CAD pad.
    /// </summary>
    public IAsyncRelayCommand ShowNotchDetailCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to refresh Notch 2.2 preview overlays on the main canvas.
    /// </summary>
    public IRelayCommand RefreshNotchCanvasPreviewCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Step 1 result.
    /// </summary>
    public IRelayCommand ClearStep1ResultCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Step 2 result.
    /// </summary>
    public IRelayCommand ClearStep2ResultCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Step 3 result.
    /// </summary>
    public IRelayCommand ClearStep3ResultCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Step 4 result.
    /// </summary>
    public IRelayCommand ClearStep4ResultCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Step 5 result.
    /// </summary>
    public IRelayCommand ClearStep5ResultCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to set notch validation target REG from current selection.
    /// </summary>
    public IRelayCommand UseSelectedRegularForNotchValidationCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to analyze notch rows/flow for current validation REG.
    /// </summary>
    public IRelayCommand AnalyzeNotchValidationCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear notch validation result.
    /// </summary>
    public IRelayCommand ClearNotchValidationCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to focus canvas selection based on one validation row.
    /// </summary>
    public IRelayCommand<NotchValidationDisplayItem> FocusNotchValidationItemCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear Notch 2.2 preview overlays on the main canvas.
    /// </summary>
    public IRelayCommand ClearNotchCanvasPreviewCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to move Notch Stage preview to previous stage.
    /// </summary>
    public IRelayCommand PreviousNotchPreviewStageCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to move Notch Stage preview to next stage.
    /// </summary>
    public IRelayCommand NextNotchPreviewStageCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to fit the canvas content to view.
    /// </summary>
    public IRelayCommand FitCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear the current selection on the canvas.
    /// </summary>
    public IRelayCommand ClearSelectionCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to focus one CAD pad by quick-locate id input.
    /// </summary>
    public IRelayCommand LocateCadByIdCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to focus one regular pad by quick-locate index input.
    /// </summary>
    public IRelayCommand LocateRegularByIndexCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to focus a CAD pad, regular pad, or FW diff from one quick-focus query.
    /// </summary>
    public IRelayCommand QuickFocusCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to undo the last action.
    /// </summary>
    public IRelayCommand UndoCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to None.
    /// </summary>
    public IRelayCommand SetFreeformNoneCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to X-Way.
    /// </summary>
    public IRelayCommand SetFreeformXCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to Y-Way.
    /// </summary>
    public IRelayCommand SetFreeformYCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to XY-Way.
    /// </summary>
    public IRelayCommand SetFreeformXYCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to use the current selection to define a manual sizing range.
    /// </summary>
    public IRelayCommand UseSelectionForManualRangeCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to select all layers for display.
    /// </summary>
    public IRelayCommand SelectAllLayersCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to deselect all layers for display.
    /// </summary>
    public IRelayCommand DeselectAllLayersCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to check the quality of the loaded DXF data.
    /// </summary>
    public IAsyncRelayCommand CheckDxfQualityCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to select CAD pads flagged by DXF overlap checking.
    /// </summary>
    public IRelayCommand SelectDxfOverlapPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear overlap highlight and selection state.
    /// </summary>
    public IRelayCommand ClearDxfOverlapHighlightsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to show overlap details in a dedicated window.
    /// </summary>
    public IAsyncRelayCommand ShowDxfOverlapDetailsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to analyze DXF idx ↔ regular mapping.
    /// </summary>
    public IAsyncRelayCommand AnalyzeIndexMappingCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to shift diff idx for all selected CAD pads by one shared offset.
    /// </summary>
    public IRelayCommand OffsetSelectedCadOutputFwDiffIndicesCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to select and focus one duplicate diff group in Step 4.
    /// </summary>
    public IRelayCommand<Step4DuplicateDiffGroupViewModel?> SelectStep4DuplicateDiffGroupCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to hide selected CAD pads.
    /// </summary>
    public IRelayCommand DeleteSelectedCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to undo the last CAD hide action.
    /// </summary>
    public IRelayCommand RestoreLastDeletedCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to restore all manually hidden CAD pads.
    /// </summary>
    public IRelayCommand RestoreAllHiddenCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to combine selected CAD pads into one synthetic pad.
    /// </summary>
    public IRelayCommand CombineSelectedCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to clear the selected synthetic combined CAD groups.
    /// </summary>
    public IRelayCommand ClearCombinedCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to reset all DXF edits, including hidden, combined, and layer-move changes.
    /// </summary>
    public IRelayCommand ResetAllDxfEditsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the Hidden DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenHiddenDxfEditChangeListCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the Duplicate DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenDuplicateDxfEditChangeListCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the Combined DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenCombinedDxfEditChangeListCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the Moved DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenMovedDxfEditChangeListCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to open the Rotated DXF edit detail dialog.
    /// </summary>
    public IAsyncRelayCommand OpenRotatedDxfEditChangeListCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to move selected CAD pads to an existing DXF layer.
    /// </summary>
    public IRelayCommand MoveSelectedCadPadsToLayerCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to create a new DXF layer and move selected CAD pads into it.
    /// </summary>
    public IRelayCommand CreateLayerAndMoveSelectedCadPadsCommand { get; private set; } = null!;
    /// <summary>
    /// Gets the command to rotate selected CAD pads around their own centroids.
    /// </summary>
    public IRelayCommand RotateSelectedCadPadsCommand { get; private set; } = null!;

    /// <summary>
    /// Initializes all commands used by the ViewModel.
    /// This method is typically called during the ViewModel's construction.
    /// </summary>
    private void InitializeCommands()
    {
        OpenDxfCommand = CreateEditingAsyncCommand(OpenDxfAsync);
        RebuildGridCommand = CreateEditingAsyncCommand(() => TriggerGridRebuildAsync(requestFit: true));
        MatchCommand = CreateEditingAsyncCommand(MatchAsync);
        ImportRegularVisibilityMaskCommand = CreateEditingAsyncCommand(ImportRegularVisibilityMaskAsync);
        ClearRegularVisibilityMaskCommand = CreateEditingCommand(ClearRegularVisibilityMask);
        AutoDetectFreeformsCommand = CreateEditingAsyncCommand(AutoDetectFreeformsAsync);
        SaveProjectCommand = CreateEditingAsyncCommand(SaveProjectAsync);
        LoadProjectCommand = CreateEditingAsyncCommand(LoadProjectAsync);
        ExportNotchCommand = CreateEditingAsyncCommand(ExportNotchAsync);
        ExportDxfVisibleCommand = CreateEditingAsyncCommand(ExportVisibleDxfAsync);
        ExportDxfAllCommand = CreateEditingAsyncCommand(ExportAllDxfAsync);
        ExportDxfLayerImageCommand = CreateEditingAsyncCommand(ExportDxfLayerImageAsync);
        ShowNotchDetailCommand = CreateEditingAsyncCommand(ShowNotchDetailAsync);
        RefreshNotchCanvasPreviewCommand = CreateEditingCommand(ExecuteStep3PreviewWorkflow);
        ClearStep1ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step1Match));
        ClearStep2ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step2Freeform));
        ClearStep3ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step3NotchPreview));
        ClearStep4ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step4IndexDiagnostics));
        ClearStep5ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step5Export));
        UseSelectedRegularForNotchValidationCommand = CreateEditingCommand(UseSelectedRegularForNotchValidation);
        AnalyzeNotchValidationCommand = CreateEditingCommand(AnalyzeNotchValidation);
        ClearNotchValidationCommand = CreateEditingCommand(ClearNotchValidation);
        FocusNotchValidationItemCommand = CreateEditingCommand<NotchValidationDisplayItem>(FocusNotchValidationItem);
        ClearNotchCanvasPreviewCommand = CreateEditingCommand(ClearNotchCanvasPreview);
        PreviousNotchPreviewStageCommand = CreateEditingCommand(() => ShiftNotchPreviewStage(-1));
        NextNotchPreviewStageCommand = CreateEditingCommand(() => ShiftNotchPreviewStage(1));
        FitCommand = CreateEditingCommand(() => CanvasHost?.FitToContent());
        ClearSelectionCommand = CreateEditingCommand(ClearSelection);
        LocateCadByIdCommand = CreateEditingCommand(LocateCadByQuickInput);
        LocateRegularByIndexCommand = CreateEditingCommand(LocateRegularByQuickInput);
        QuickFocusCommand = CreateEditingCommand(ExecuteQuickFocus);
        UndoCommand = CreateEditingCommand(Undo, () => CanUndo); // Undo command with a CanExecute condition.
        SetFreeformNoneCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.None));
        SetFreeformXCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XWay));
        SetFreeformYCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.YWay));
        SetFreeformXYCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XYWay));
        UseSelectionForManualRangeCommand = CreateEditingCommand(ApplySelectionToManualRange);
        SelectAllLayersCommand = CreateEditingCommand(SelectAllLayers);
        DeselectAllLayersCommand = CreateEditingCommand(DeselectAllLayers);
        CheckDxfQualityCommand = CreateEditingAsyncCommand(CheckDxfQualityAsync);
        SelectDxfOverlapPadsCommand = CreateEditingCommand(SelectDxfOverlapPads);
        ClearDxfOverlapHighlightsCommand = CreateEditingCommand(ClearDxfOverlapHighlights);
        ShowDxfOverlapDetailsCommand = CreateEditingAsyncCommand(ShowDxfOverlapDetailsAsync);
        AnalyzeIndexMappingCommand = CreateEditingAsyncCommand(AnalyzeIndexMappingAsync);
        OffsetSelectedCadOutputFwDiffIndicesCommand = CreateEditingCommand(OffsetSelectedCadOutputFwDiffIndices);
        SelectStep4DuplicateDiffGroupCommand = CreateEditingCommand<Step4DuplicateDiffGroupViewModel?>(SelectStep4DuplicateDiffGroup);
        DeleteSelectedCadPadsCommand = CreateEditingCommand(DeleteSelectedCadPads);
        RestoreLastDeletedCadPadsCommand = CreateEditingCommand(RestoreLastDeletedCadPads);
        RestoreAllHiddenCadPadsCommand = CreateEditingCommand(RestoreAllHiddenCadPads);
        CombineSelectedCadPadsCommand = CreateEditingCommand(CombineSelectedCadPads);
        ClearCombinedCadPadsCommand = CreateEditingCommand(ClearCombinedCadPads);
        ResetAllDxfEditsCommand = CreateEditingCommand(ResetAllDxfEdits);
        OpenHiddenDxfEditChangeListCommand = CreateEditingAsyncCommand(OpenHiddenDxfEditChangeListAsync);
        OpenDuplicateDxfEditChangeListCommand = CreateEditingAsyncCommand(OpenDuplicateDxfEditChangeListAsync);
        OpenCombinedDxfEditChangeListCommand = CreateEditingAsyncCommand(OpenCombinedDxfEditChangeListAsync);
        OpenMovedDxfEditChangeListCommand = CreateEditingAsyncCommand(OpenMovedDxfEditChangeListAsync);
        OpenRotatedDxfEditChangeListCommand = CreateEditingAsyncCommand(OpenRotatedDxfEditChangeListAsync);
        MoveSelectedCadPadsToLayerCommand = CreateEditingCommand(MoveSelectedCadPadsToLayer);
        CreateLayerAndMoveSelectedCadPadsCommand = CreateEditingCommand(CreateLayerAndMoveSelectedCadPads);
        RotateSelectedCadPadsCommand = CreateEditingCommand(RotateSelectedCadPads);
    }
}
