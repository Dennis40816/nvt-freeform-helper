namespace FreeformHelper.UI.ViewModels;

/// <summary>Stores the FW diff input for one view model. The grid and layer inputs live in SettingsStateViewModel.</summary>
internal sealed class WorkflowInputState
{
    public FwDiffAssignmentSettings FwDiffAssignment { get; set; } = FwDiffAssignmentSettings.Initial;
}
