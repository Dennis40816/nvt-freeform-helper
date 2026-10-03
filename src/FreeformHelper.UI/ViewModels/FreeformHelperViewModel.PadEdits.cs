using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> handles various
/// editing operations related to pads, including setting custom CAD pad values
/// and applying/resetting manual sizing for regular grid cells.
/// It integrates with the undo/redo system.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Sets custom numeric values for a list of specified CAD pads.
    /// This operation is undoable.
    /// </summary>
    /// <param name="cadIds">A read-only list of CAD pad IDs to update.</param>
    /// <param name="value">The new custom numeric value to assign to these pads.</param>
    public void SetCadPadCustomValues(IReadOnlyList<int> cadIds, decimal value)
    {
        if (cadIds.Count == 0)
        {
            return; // No CAD pads specified.
        }

        // Capture the previous values for undo functionality.
        var previous = cadIds.ToDictionary(
            id => id,
            id => _cadPadCustomValues.TryGetValue(id, out var v) ? (double?)v : null);

        // Apply the new value to each specified CAD pad.
        foreach (var id in cadIds)
        {
            _cadPadCustomValues[id] = (double)value;
        }

        // Push an undo action onto the undo stack.
        PushUndo(() =>
        {
            // The undo action restores each CAD pad's custom value to its state before this operation.
            foreach (var kv in previous)
            {
                if (kv.Value is null)
                {
                    _cadPadCustomValues.Remove(kv.Key); // Remove if it was new.
                }
                else
                {
                    _cadPadCustomValues[kv.Key] = kv.Value.Value; // Restore old value.
                }
            }
        }, "CAD custom value"); // Description for the undo action.

        // Update status text and mark project as unsaved.
        SetStatus(cadIds.Count == 1
            ? $"CAD pad #{cadIds[0]} custom value set to {value:0.####}."
            : $"CAD pads updated: {cadIds.Count} items.");
        MarkUnsaved();
    }

    /// <summary>
    /// Applies manual sizing (width and/or height) to a specified selection of grid cells.
    /// This operation is undoable and triggers a grid rebuild.
    /// </summary>
    /// <param name="rows">A read-only list of row indices to apply sizing to.</param>
    /// <param name="cols">A read-only list of column indices to apply sizing to.</param>
    /// <param name="widthMm">The new width in millimeters (decimal?), or null if not changing width.</param>
    /// <param name="heightMm">The new height in millimeters (decimal?), or null if not changing height.</param>
    public void ApplySizingToCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols, decimal? widthMm, decimal? heightMm)
    {
        if (_grid is null)
        {
            SetStatus("Manual sizing: build grid first.");
            return;
        }

        ApplyUiToSettings(_projectFile.Settings); // Ensure current UI settings are applied to project file.

        var hasWidth = widthMm.HasValue && widthMm.Value > 0;
        var hasHeight = heightMm.HasValue && heightMm.Value > 0;
        if (!hasWidth && !hasHeight)
        {
            return; // No sizing changes requested.
        }

        // Use the ManualSizingService to apply the sizing changes.
        var result = _padEditUseCase.ApplySizing(
            _projectFile.Settings.Grid,
            GridAlignmentMode,
            BuildCadPadSetForBounds(),
            rows,
            cols,
            widthMm,
            heightMm);

        if (!result.Success)
        {
            SetStatus($"Manual sizing: {result.Error}");
            Logger.Warn(CultureInfo.InvariantCulture, "Manual sizing apply failed. Error={0}", result.Error);
            return;
        }

        // Push an undo action onto the undo stack.
        PushUndo(() =>
        {
            // The undo action restores the grid settings from the snapshot and triggers a rebuild.
            Services.ManualSizingService.RestoreSnapshot(_projectFile.Settings.Grid, result.Snapshot);
            _ = TriggerGridRebuildAsync(requestFit: true);
        }, "Manual sizing"); // Description for the undo action.

        SetStatus("Manual sizing applied.");
        var rowLabel = FormatIndexRanges(rows.Select(ToDisplayRow));
        var colLabel = FormatIndexRanges(cols);
        Logger.Info(CultureInfo.InvariantCulture, "Manual sizing apply requested. Rows={0}, Cols={1}, Width={2}, Height={3}.",
            rowLabel,
            colLabel,
            widthMm?.ToString(CultureInfo.InvariantCulture) ?? "unchanged",
            heightMm?.ToString(CultureInfo.InvariantCulture) ?? "unchanged");
        SuppressSelectionClearOnRebuild = true; // Suppress clearing selection during the rebuild.
        _ = TriggerGridRebuildAsync(requestFit: true); // Trigger a grid rebuild to reflect new sizing.
        MarkUnsaved(); // Mark the project as unsaved.
    }

    /// <summary>
    /// Resets manual sizing for a specified selection of grid cells back to automatic distribution.
    /// This operation is undoable and triggers a grid rebuild.
    /// </summary>
    /// <param name="rows">A read-only list of row indices to reset sizing for.</param>
    /// <param name="cols">A read-only list of column indices to reset sizing for.</param>
    public void ResetSizingForCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        if (_grid is null)
        {
            SetStatus("Manual sizing: build grid first.");
            return;
        }

        ApplyUiToSettings(_projectFile.Settings); // Ensure current UI settings are applied to project file.

        // Use the ManualSizingService to reset the sizing for the specified cells.
        var result = _padEditUseCase.ResetSizing(
            _projectFile.Settings.Grid,
            GridAlignmentMode,
            BuildCadPadSetForBounds(),
            rows,
            cols);

        if (!result.Success)
        {
            SetStatus($"Reset sizing: {result.Error}");
            Logger.Warn(CultureInfo.InvariantCulture, "Manual sizing reset failed. Error={0}", result.Error);
            return;
        }

        // Push an undo action onto the undo stack.
        PushUndo(() =>
        {
            // The undo action restores the grid settings from the snapshot and triggers a rebuild.
            Services.ManualSizingService.RestoreSnapshot(_projectFile.Settings.Grid, result.Snapshot);
            _ = TriggerGridRebuildAsync(requestFit: true);
        }, "Reset manual sizing"); // Description for the undo action.

        SetStatus("Manual sizing reset to default.");
        var rowLabel = FormatIndexRanges(rows.Select(ToDisplayRow));
        var colLabel = FormatIndexRanges(cols);
        Logger.Info(CultureInfo.InvariantCulture, "Manual sizing reset requested. Rows={0}, Cols={1}.", rowLabel, colLabel);
        SuppressSelectionClearOnRebuild = true; // Suppress clearing selection during the rebuild.
        _ = TriggerGridRebuildAsync(requestFit: true); // Trigger a grid rebuild to reflect changes.
        MarkUnsaved(); // Mark the project as unsaved.
    }
}

