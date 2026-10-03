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

        if (_selectedRegIndices.Count == 0)
        {
            SetStatus("Freeform tagging: select regular pads first.");
            return;
        }

        SetFreeformForPadIndices(_selectedRegIndices, type);
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
    /// Sets the width and/or height for a specific regular pad (cell) in the grid.
    /// This delegates to the <see cref="ManualSizingService"/> to apply the changes and redistribute space.
    /// </summary>
    /// <param name="row">The row index of the pad.</param>
    /// <param name="col">The column index of the pad.</param>
    /// <param name="widthMm">The new width in millimeters, or 0 to reset.</param>
    /// <param name="heightMm">The new height in millimeters, or 0 to reset.</param>
    public void SetPadDimensions(int row, int col, double widthMm, double heightMm)
    {
        if (_cad is null)
        {
            SetStatus("Set pad dimensions: import DXF first.");
            return;
        }

        ApplyUiToSettings(_projectFile.Settings); // Ensure current UI settings are applied.

        // Use the ManualSizingService to apply the dimension changes.
        var result = _padEditUseCase.SetPadDimensions(
            _projectFile.Settings.Grid,
            GridAlignmentMode,
            BuildCadPadSetForBounds(),
            row,
            col,
            widthMm,
            heightMm);

        if (!result.Success)
        {
            SetStatus($"Set pad dimensions: {result.Error}");
            return;
        }

        SetStatus($"Pad ({row},{col}) AA updated to {widthMm:0.###} x {heightMm:0.###} mm. Rebuilding grid...");
        _ = TriggerGridRebuildAsync(requestFit: true); // Trigger a grid rebuild with content fitting.
        MarkUnsaved(); // Mark the project as unsaved.
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
    /// Sets a custom numeric value for a specific CAD pad.
    /// </summary>
    /// <param name="cadId">The ID of the CAD pad.</param>
    /// <param name="value">The custom numeric value to set.</param>
    public void SetCadPadCustomValue(int cadId, double value)
    {
        _cadPadCustomValues[cadId] = value;
        SetStatus($"CAD pad #{cadId} note set to {value}.");
        MarkUnsaved(); // Mark the project as unsaved.
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
