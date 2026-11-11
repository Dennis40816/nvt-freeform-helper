using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public IReadOnlyList<Step4DuplicateDiffGroupViewModel> Step4DuplicateDiffGroups => IndexMapping.Assignment.Step4DuplicateDiffGroups;

    /// <summary>
    /// Gets a value indicating whether duplicate diff groups are currently present in Step 4.
    /// </summary>
    public bool HasStep4DuplicateDiffGroups => IndexMapping.Assignment.HasStep4DuplicateDiffGroups;

    private void RefreshStep4DuplicateDiffGroups(
        IReadOnlyList<CadPad>? orderedVisiblePads = null,
        WorkflowDataSnapshot? workflowSnapshot = null)
        => IndexMapping.Assignment.RefreshStep4DuplicateDiffGroups(
            orderedVisiblePads, workflowSnapshot is null ? null : ProjectIndexMappingSnapshot(workflowSnapshot));

    private void SelectStep4DuplicateDiffGroup(Step4DuplicateDiffGroupViewModel? group)
        => IndexMapping.Assignment.SelectStep4DuplicateDiffGroup(group);

    private DxfDuplicateGroupInputs ReadIndexMappingSnapshot() => ProjectIndexMappingSnapshot(BuildWorkflowDataSnapshot());

    private static DxfDuplicateGroupInputs ProjectIndexMappingSnapshot(WorkflowDataSnapshot snapshot)
        => new(snapshot.CadOutputFwDiffIndexByCadId, snapshot.CadOutputFwDiffAssignmentDecisionsByCadId,
            snapshot.CadIcIndexByCadId, snapshot.CadToRegularByCadId, snapshot.ActiveRegularVisibilityMaskPadIds);
}
