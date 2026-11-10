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
    /// Gets the command to fit the canvas content to view.
    /// </summary>
    public IRelayCommand FitCommand { get; private set; } = null!;
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
    /// Initializes Notch, project, and selection commands during construction.
    /// DXF edit and general commands are initialized next by the constructor.
    /// </summary>
    private void InitializeCommands()
    {
        InitializeNotchCommands();
        InitializeProjectCommands();
        InitializeSelectionCommands();
    }

    /// <summary>Initializes general commands after the DXF edit commands during construction.</summary>
    private void InitializeGeneralCommands()
    {
        OpenDxfCommand = CreateEditingAsyncCommand(OpenDxfAsync);
        MatchCommand = CreateEditingAsyncCommand(MatchAsync);
        ImportRegularVisibilityMaskCommand = CreateEditingAsyncCommand(ImportRegularVisibilityMaskAsync);
        ClearRegularVisibilityMaskCommand = CreateEditingCommand(ClearRegularVisibilityMask);
        AutoDetectFreeformsCommand = CreateEditingAsyncCommand(AutoDetectFreeformsAsync);
        ClearStep1ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step1Match));
        ClearStep2ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step2Freeform));
        ClearStep3ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step3NotchPreview));
        ClearStep4ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step4IndexDiagnostics));
        ClearStep5ResultCommand = CreateEditingCommand(() => ClearWorkflowStep(WorkflowStepId.Step5Export));
        FitCommand = CreateEditingCommand(() => CanvasHost?.FitToContent());
        UndoCommand = CreateEditingCommand(Undo, () => CanUndo); // Undo command with a CanExecute condition.
        SetFreeformNoneCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.None));
        SetFreeformXCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XWay));
        SetFreeformYCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.YWay));
        SetFreeformXYCommand = CreateEditingCommand(() => SetFreeformForSelection(FreeformType.XYWay));
    }
}
