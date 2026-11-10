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
    public IAsyncRelayCommand OpenDxfCommand => _generalCommands.OpenDxfCommand;
    /// <summary>
    /// Gets the command to perform pad matching.
    /// </summary>
    public IAsyncRelayCommand MatchCommand => _generalCommands.MatchCommand;
    /// <summary>
    /// Gets the command to import a Step 1 regular visibility mask CSV.
    /// </summary>
    public IAsyncRelayCommand ImportRegularVisibilityMaskCommand => _generalCommands.ImportRegularVisibilityMaskCommand;
    /// <summary>
    /// Gets the command to clear the imported Step 1 regular visibility mask CSV.
    /// </summary>
    public IRelayCommand ClearRegularVisibilityMaskCommand => _generalCommands.ClearRegularVisibilityMaskCommand;
    /// <summary>
    /// Gets the command to automatically detect freeform pads.
    /// </summary>
    public IAsyncRelayCommand AutoDetectFreeformsCommand => _generalCommands.AutoDetectFreeformsCommand;
    /// <summary>
    /// Gets the command to clear Step 1 result.
    /// </summary>
    public IRelayCommand ClearStep1ResultCommand => _generalCommands.ClearStep1ResultCommand;
    /// <summary>
    /// Gets the command to clear Step 2 result.
    /// </summary>
    public IRelayCommand ClearStep2ResultCommand => _generalCommands.ClearStep2ResultCommand;
    /// <summary>
    /// Gets the command to clear Step 3 result.
    /// </summary>
    public IRelayCommand ClearStep3ResultCommand => _generalCommands.ClearStep3ResultCommand;
    /// <summary>
    /// Gets the command to clear Step 4 result.
    /// </summary>
    public IRelayCommand ClearStep4ResultCommand => _generalCommands.ClearStep4ResultCommand;
    /// <summary>
    /// Gets the command to clear Step 5 result.
    /// </summary>
    public IRelayCommand ClearStep5ResultCommand => _generalCommands.ClearStep5ResultCommand;
    /// <summary>
    /// Gets the command to fit the canvas content to view.
    /// </summary>
    public IRelayCommand FitCommand => _generalCommands.FitCommand;
    /// <summary>
    /// Gets the command to undo the last action.
    /// </summary>
    public IRelayCommand UndoCommand => _generalCommands.UndoCommand;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to None.
    /// </summary>
    public IRelayCommand SetFreeformNoneCommand => _generalCommands.SetFreeformNoneCommand;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to X-Way.
    /// </summary>
    public IRelayCommand SetFreeformXCommand => _generalCommands.SetFreeformXCommand;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to Y-Way.
    /// </summary>
    public IRelayCommand SetFreeformYCommand => _generalCommands.SetFreeformYCommand;
    /// <summary>
    /// Gets the command to set the freeform type of selected pads to XY-Way.
    /// </summary>
    public IRelayCommand SetFreeformXYCommand => _generalCommands.SetFreeformXYCommand;

    private readonly GeneralCommands _generalCommands;

    /// <summary>Creates the general commands once during construction.</summary>
    private GeneralCommands CreateGeneralCommands()
    {
        return new GeneralCommands(
            OpenDxfCommand: CreateEditingAsyncCommand(OpenDxfAsync),
            MatchCommand: CreateEditingAsyncCommand(MatchAsync),
            ImportRegularVisibilityMaskCommand: CreateEditingAsyncCommand(ImportRegularVisibilityMaskAsync),
            ClearRegularVisibilityMaskCommand: CreateEditingCommand(ClearRegularVisibilityMask),
            AutoDetectFreeformsCommand: CreateEditingAsyncCommand(AutoDetectFreeformsAsync),
            ClearStep1ResultCommand: CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step1Match)),
            ClearStep2ResultCommand: CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step2Freeform)),
            ClearStep3ResultCommand: CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step3NotchPreview)),
            ClearStep4ResultCommand: CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step4IndexDiagnostics)),
            ClearStep5ResultCommand: CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step5Export)),
            FitCommand: CreateEditingCommand(() => CanvasHost?.FitToContent()),
            UndoCommand: CreateEditingCommand(Undo, () => CanUndo), // Undo command with a CanExecute condition.
            SetFreeformNoneCommand: CreateEditingCommand(() => SetFreeformForSelection(FreeformType.None)),
            SetFreeformXCommand: CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XWay)),
            SetFreeformYCommand: CreateEditingCommand(() => SetFreeformForSelection(FreeformType.YWay)),
            SetFreeformXYCommand: CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XYWay)));
    }
}
