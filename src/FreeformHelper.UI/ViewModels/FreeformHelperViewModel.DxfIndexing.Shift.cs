using System.Globalization;
using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public bool CanOffsetSelectedCadOutputFwDiffIndices =>
        _selectedCadIds.Any(id => _cadOutputFwDiffIndexByCadId.ContainsKey(id)) &&
        decimal.Truncate(CadOutputFwDiffIndexShiftDelta) == CadOutputFwDiffIndexShiftDelta &&
        CadOutputFwDiffIndexShiftDelta != 0m;

    public string Step4DiffShiftSelectionSummary
    {
        get
        {
            var workflowSnapshot = BuildWorkflowDataSnapshot();
            var visibleSelected = _selectedCadIds
                .Where(id => workflowSnapshot.CadOutputFwDiffIndexByCadId.ContainsKey(id))
                .OrderBy(id => id)
                .ToList();
            if (visibleSelected.Count == 0)
            {
                return "Diff shift: select CAD output pads in AA first.";
            }

            var diffs = visibleSelected
                .Select(id => workflowSnapshot.CadOutputFwDiffIndexByCadId[id])
                .OrderBy(diff => diff)
                .ToList();
            return FormattableString.Invariant(
                $"Diff shift target: {visibleSelected.Count} CAD pad(s), current range {diffs.First()}-{diffs.Last()}, offset {CadOutputFwDiffIndexShiftDelta:0}.");
        }
    }

    partial void OnCadOutputFwDiffIndexShiftDeltaChanged(decimal value)
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

        var delta = (int)decimal.Truncate(CadOutputFwDiffIndexShiftDelta);
        var plan = CadOutputFwDiffIndexShiftService.BuildShiftPlan(
            selectedCadIds,
            workflowSnapshot.CadOutputFwDiffIndexByCadId,
            workflowSnapshot.CadIcIndexByCadId,
            delta);
        if (!plan.IsSuccessful)
        {
            SetStatus(plan.Message);
            return;
        }

        var overrides = _projectFile.CadOutputFwDiffIndexOverrides;
        var previousOverrides = overrides.ToDictionary(static pair => pair.Key, static pair => pair.Value);
        var changedCount = 0;
        foreach (var pair in plan.ShiftedDiffByCadId.OrderBy(static pair => pair.Key))
        {
            if (overrides.TryGetValue(pair.Key, out var previous) && previous == pair.Value)
            {
                continue;
            }

            overrides[pair.Key] = pair.Value;
            changedCount++;
        }

        if (changedCount == 0)
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
        Logger.Info(
            CultureInfo.InvariantCulture,
            "Diff idx batch shift applied. selected={0}, delta={1}, missing={2}.",
            plan.ShiftedDiffByCadId.Count,
            delta,
            plan.MissingCadPadIds.Count);
    }
}
