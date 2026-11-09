using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool CanOffsetSelectedCadOutputFwDiffIndices =>
        DxfDiffShiftService.CanOffset(_selectedCadIds, _cadOutputFwDiffIndexByCadId, Step4Index.ShiftDelta);

    public string Step4DiffShiftSelectionSummary
    {
        get
        {
            var workflowSnapshot = BuildWorkflowDataSnapshot();
            return DxfDiffShiftService.BuildSelectionSummary(
                _selectedCadIds, workflowSnapshot.CadOutputFwDiffIndexByCadId, Step4Index.ShiftDelta);
        }
    }

    private void OnStep4ShiftDeltaChanged()
    {
        OnPropertyChanged(nameof(CanOffsetSelectedCadOutputFwDiffIndices));
        OnPropertyChanged(nameof(Step4DiffShiftSelectionSummary));
    }

    private void OffsetSelectedCadOutputFwDiffIndices()
    {
        var workflowSnapshot = BuildWorkflowDataSnapshot();
        var selectedCadIds = _selectedCadIds
            .Where(id => workflowSnapshot.CadOutputFwDiffIndexByCadId.ContainsKey(id))
            .OrderBy(static id => id)
            .ToList();
        if (selectedCadIds.Count == 0)
        {
            SetStatus("CAD Output FW Diff offset: select CAD output pads first.");
            return;
        }

        var delta = (int)decimal.Truncate(Step4Index.ShiftDelta);
        var result = DxfDiffShiftService.ApplyShift(
            selectedCadIds,
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            delta,
            _projectFile.CadOutputFwDiffIndexOverrides);
        var plan = result.Plan;
        if (!plan.IsSuccessful)
        {
            SetStatus(plan.Message);
            return;
        }

        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        var previousOverrides = result.PreviousOverrides;
        if (result.ChangedCount == 0)
        {
            SetStatus("CAD Output FW Diff offset: selected CAD pads already match the requested offset.");
            return;
        }

        PushUndo(() =>
        {
            overrides.Clear();
            foreach (var pair in previousOverrides)
            {
                overrides[pair.Key] = pair.Value;
            }

            UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        }, "CAD Output FW Diff offset");

        UpdateCadOutputFwDiffIndexing(CadPads.ToList());
        InvalidateDownstreamFromStep4(showStatus: false);
        NotifySimulationWorkspaceSourceChanged();
        MarkUnsaved();
        SetStatus(plan.Message);
        _logger.Info(
            CultureInfo.InvariantCulture,
            "Diff idx batch shift applied. selected={0}, delta={1}, missing={2}.",
            plan.ShiftedDiffByCadId.Count,
            delta,
            plan.MissingCadPadIds.Count);
    }
}
