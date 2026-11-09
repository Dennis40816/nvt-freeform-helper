using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly Step4DuplicateGroupsModel _step4DuplicateGroups = new();

    public IReadOnlyList<Step4DuplicateDiffGroupViewModel> Step4DuplicateDiffGroups => _step4DuplicateGroups.Groups;

    /// <summary>
    /// Gets a value indicating whether duplicate diff groups are currently present in Step 4.
    /// </summary>
    public bool HasStep4DuplicateDiffGroups => _step4DuplicateGroups.HasGroups;

    private void RefreshStep4DuplicateDiffGroups(
        IReadOnlyList<CadPad>? orderedVisiblePads = null,
        WorkflowDataSnapshot? workflowSnapshot = null)
    {
        var orderedPads = orderedVisiblePads ?? CadPads.ToList();
        workflowSnapshot ??= BuildWorkflowDataSnapshot();
        var result = DxfDuplicateGroupBuilder.Build(
            orderedPads,
            new DxfDuplicateGroupInputs(
                workflowSnapshot.CadOutputFwDiffIndexByCadId,
                workflowSnapshot.CadOutputFwDiffAssignmentDecisionsByCadId,
                workflowSnapshot.CadIcIndexByCadId,
                workflowSnapshot.CadToRegularByCadId,
                workflowSnapshot.ActiveRegularVisibilityMaskPadIds),
            _grid);
        _step4DuplicateGroups.Replace(result.Groups);
        Step4Index.DuplicateDiffSummary = result.Summary;
        OnPropertyChanged(nameof(Step4DuplicateDiffGroups));
        OnPropertyChanged(nameof(HasStep4DuplicateDiffGroups));
    }

    private void SelectStep4DuplicateDiffGroup(Step4DuplicateDiffGroupViewModel? group)
    {
        if (group is null || group.CadPadIds.Count == 0)
        {
            return;
        }

        TryLocateSelection(
            group.CadPadIds,
            Array.Empty<int>(),
            focusSelection: true,
            notVisibleStatus: $"Duplicate CAD Output FW Diff IC{group.IcIndex + 1}/diff{group.DiffIndex} is not visible.",
            successStatusBuilder: (cadCount, _) =>
                $"Duplicate CAD Output FW Diff focused: IC{group.IcIndex + 1}/diff{group.DiffIndex} ({cadCount} CAD pad(s)).");
    }
}
