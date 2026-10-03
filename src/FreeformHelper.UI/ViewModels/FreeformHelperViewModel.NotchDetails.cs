using System.Globalization;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void RefreshNotchCanvasPreview(bool showStatus)
    {
        if (!ShowNotchCanvasPreview)
        {
            ClearNotchCanvasPreview();
            return;
        }

        if (_grid is null)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: build grid first.");
            }
            return;
        }

        var selectedCadIds = GetSelectedCadIdsForPreview();
        if (selectedCadIds.Count == 0)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: select at least one CAD pad.");
            }
            return;
        }

        if (!TryEnsureWorkflowStep(WorkflowStepId.Step3NotchPreview, suppressStatus: !showStatus))
        {
            ClearNotchCanvasPreview();
            return;
        }

        var items = BuildSelectedCadNotchPreviewItems(selectedCadIds);
        if (items.Count == 0)
        {
            ClearNotchCanvasPreview();
            if (showStatus)
            {
                SetStatus("Notch 2.2 preview: selected CAD has no effective To Full result.");
            }
            return;
        }

        NotchCanvasPreviewItems = new System.Collections.ObjectModel.ObservableCollection<NotchCanvasPreviewItem>(items);
        CanvasHost?.Invalidate();

        if (showStatus)
        {
            var enabledCount = items.Count(static item => item.IsToFullEnabled);
            var disabledCount = items.Count - enabledCount;
            var scopeText = items.Count == 1
                ? $"CAD {items[0].CadPadId}"
                : $"{items.Count} selected CAD pads";
            var toFullState = disabledCount == 0
                ? "To Full effective ON"
                : $"To Full mixed ({enabledCount} ON, {disabledCount} OFF)";
            SetStatus($"Notch 2.2 preview updated: {scopeText}, {toFullState}.");
            OnWorkflowStepCompleted(
                WorkflowStepId.Step3NotchPreview,
                invalidateDownstream: false,
                moveToNextStep: true);
        }
    }

    private List<int> GetSelectedCadIdsForPreview()
    {
        if (_selectedCadIds.Count == 0)
        {
            return [];
        }

        var orderedIds = new List<int>();
        foreach (var pad in CadPads)
        {
            if (_selectedCadIds.Contains(pad.Id))
            {
                orderedIds.Add(pad.Id);
            }
        }

        if (orderedIds.Count > 0)
        {
            return orderedIds;
        }

        return _selectedCadIds.OrderBy(static id => id).ToList();
    }

    private List<NotchCanvasPreviewItem> BuildSelectedCadNotchPreviewItems(List<int> selectedCadIds)
    {
        var workflowSnapshot = BuildWorkflowDataSnapshot();
        var items = new List<NotchCanvasPreviewItem>(selectedCadIds.Count);
        foreach (var cadId in selectedCadIds)
        {
            if (!TryResolveNotchComputationInputs(cadId, out var targetCad, out var sourceCad, out var activeRegularPadIds))
            {
                continue;
            }

            var resolved = GetOrBuildCadV22ResolvedResultCached(
                targetCad,
                sourceCad,
                activeRegularPadIds,
                workflowSnapshot)!;
            var compensation = resolved.Compensation;
            if (!compensation.IsToFullEnabled)
            {
                continue;
            }

            items.Add(
                new NotchCanvasPreviewItem(
                    CadPadId: targetCad.Id,
                    ToRegularRatio: compensation.ToRegularRatio,
                    ToFullRatio: compensation.ToFullRatio,
                    IsToFullEnabled: compensation.IsToFullEnabled,
                    ToFullSeedPolygons: resolved.Stage1SeedPolygons,
                    ToFullCandidatePolygons: resolved.Stage2CandidatePolygons,
                    ToFullPolygons: compensation.ToFullPolygons,
                    ToFullFinalOutlinePolygons: resolved.Stage3FinalOutlinePolygons));
        }

        return items;
    }

    private void ClearNotchCanvasPreview()
    {
        if (NotchCanvasPreviewItems.Count == 0)
        {
            return;
        }

        NotchCanvasPreviewItems = new System.Collections.ObjectModel.ObservableCollection<NotchCanvasPreviewItem>();
        CanvasHost?.Invalidate();
    }

    private async Task ShowNotchDetailAsync()
    {
        if (OpenNotchDetailAsync is null)
        {
            SetStatus("Notch detail: dialog handler not wired.");
            Logger.Warn(CultureInfo.InvariantCulture, "Notch detail requested but dialog handler is not wired.");
            return;
        }

        if (_cad is null || _grid is null)
        {
            SetStatus("Notch detail: import DXF and build grid first.");
            Logger.Debug(CultureInfo.InvariantCulture, "Notch detail skipped: cad/grid not ready.");
            return;
        }

        if (_selectedCadIds.Count == 0)
        {
            SetStatus("Notch detail: select a CAD pad first.");
            Logger.Debug(CultureInfo.InvariantCulture, "Notch detail skipped: no CAD selected.");
            return;
        }

        var cadId = _selectedCadIds.First();
        var cad = _cad.Pads.FirstOrDefault(p => p.Id == cadId)
            ?? CadPads.FirstOrDefault(p => p.Id == cadId);

        if (cad is null)
        {
            SetStatus("Notch detail: selected CAD pad not found.");
            Logger.Warn(CultureInfo.InvariantCulture, "Notch detail skipped: selected CAD pad not found (id={0}).", cadId);
            return;
        }

        var workflowSnapshot = BuildWorkflowDataSnapshot();
        int? dxfIndex = workflowSnapshot.TryGetCadOutputFwDiffIndex(cad.Id, out var idx) ? idx : null;
        var resolved = GetOrBuildCadV22ResolvedResultCached(
            cad,
            GetCadPadsForNotchComputation(cad.Id),
            GetActiveRegularPadIdsForNotchComputation(),
            workflowSnapshot)!;

        var detail = NotchDetailUseCase.BuildFromResolvedResult(
            cad,
            dxfIndex,
            _grid,
            _projectFile.Settings,
            resolved);
        Logger.Info(
            CultureInfo.InvariantCulture,
            "Notch detail opened: cadId={0}, dxfIndex={1}, allocations={2}.",
            cad.Id,
            dxfIndex?.ToString(CultureInfo.InvariantCulture) ?? "-",
            detail.Allocations.Count);
        await OpenNotchDetailAsync(detail);
    }
}
