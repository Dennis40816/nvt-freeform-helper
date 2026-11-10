namespace FreeformHelper.UI.ViewModels;

/// <summary>Stores the grid, layer source and FW diff inputs for one view model.</summary>
internal sealed class WorkflowInputState
{
    public GridBuildOptions GridBuild { get; set; } = GridBuildOptions.Initial;

    public LayerSourceSelection LayerSource { get; set; } = LayerSourceSelection.Initial;

    public FwDiffAssignmentSettings FwDiffAssignment { get; set; } = FwDiffAssignmentSettings.Initial;
}
