using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles user-driven
/// overrides to pad properties, such as setting freeform types and individual pad dimensions.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Sets the <see cref="FreeformType"/> for all currently selected regular pads.
    /// </summary>
    /// <param name="type">The <see cref="FreeformType"/> to apply.</param>
    private void SetFreeformForSelection(FreeformType type)
    {
        if (_grid is null)
        {
            SetStatus("Freeform tagging: build grid first.");
            return;
        }

        if (_selection.RegularIndices.Count == 0)
        {
            SetStatus("Freeform tagging: select regular pads first.");
            return;
        }

        SetFreeformForPadIndices(_selection.RegularIndices, type);
    }

    internal void SetFreeformForPadIndices(IReadOnlyCollection<int> padIndices, FreeformType type)
    {
        if (_grid is null)
        {
            SetStatus("Freeform tagging: build grid first.");
            return;
        }

        if (padIndices.Count == 0)
        {
            SetStatus("Freeform tagging: no regular pads selected.");
            return;
        }

        var targetPads = _grid.Pads
            .Where(pad => padIndices.Contains(pad.Index))
            .ToList();
        var touched = FreeformTaggingUseCase.ApplyManual(_grid, padIndices, type);
        SyncManualFreeformOverrides(targetPads, type);
        BumpNotchExportGridFingerprint();
        OnWorkflowStepChanged(WorkflowStepId.Step2Freeform, showStatus: false);
        RefreshFreeformAutoDetectStats("manual override");
        if (_selectedCadIds.Count > 0)
        {
            RefreshNotchCanvasPreview(showStatus: false);
        }
        OnWorkflowStepCompleted(
            WorkflowStepId.Step2Freeform,
            invalidateDownstream: false,
            moveToNextStep: true);
        SetStatus($"Freeform set to {type} for {touched} regular pads.");
        NotifySimulationWorkspaceSourceChanged();
        CanvasHost?.Invalidate();
        MarkUnsaved();
    }

    private void SyncManualFreeformOverrides(IEnumerable<RegularPad> pads, FreeformType type)
    {
        foreach (var pad in pads)
        {
            if (type == FreeformType.None)
            {
                _projectFile.FreeformOverrides.Remove(pad.Index);
                continue;
            }

            _projectFile.FreeformOverrides[pad.Index] = type;
        }
    }

    /// <summary>
    /// Retrieves a custom numeric value associated with a specific CAD pad.
    /// </summary>
    /// <param name="cadId">The ID of the CAD pad.</param>
    /// <returns>The custom value, or 0.0 if not found.</returns>
    public double GetCadPadCustomValue(int cadId)
    {
        return _cadPadCustomValues.TryGetValue(cadId, out var v) ? v : 0.0;
    }

    /// <summary>
    /// Converts an actual internal row index to a display row index (top-to-bottom).
    /// </summary>
    /// <param name="actualRow">The internal zero-based row index.</param>
    /// <returns>The zero-based display row index.</returns>
    public int ToDisplayRow(int actualRow)
    {
        var totalRows = _grid?.Rows ?? 0; // Get total rows from the current grid.
        return ManualSizingService.ToDisplayRow(actualRow, totalRows);
    }
}
