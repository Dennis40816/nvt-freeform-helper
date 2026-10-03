using System.Globalization;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages the functionality
/// related to manual sizing adjustments of the regular grid. It handles user input for
/// specifying ranges of rows/columns and applying desired widths/heights.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    // Flag to suppress manual sizing application during initial settings loading or other controlled updates.
    private bool _suppressManualSizingApply;

    /// <summary>
    /// Partial method invoked when the <see cref="ManualRowsRange"/> property changes.
    /// Triggers an update of the manual range status and attempts to apply manual sizing.
    /// </summary>
    /// <param name="value">The new value of <see cref="ManualRowsRange"/>.</param>
    partial void OnManualRowsRangeChanged(string value)
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_isLoadingSettings || _suppressManualSizingApply)
        {
            return;
        }
        UpdateManualRangeStatus(); // Update status text for the manual range input.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    /// <summary>
    /// Partial method invoked when the <see cref="ManualColsRange"/> property changes.
    /// Triggers an update of the manual range status and attempts to apply manual sizing.
    /// </summary>
    /// <param name="value">The new value of <see cref="ManualColsRange"/>.</param>
    partial void OnManualColsRangeChanged(string value)
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_isLoadingSettings || _suppressManualSizingApply)
        {
            return;
        }
        UpdateManualRangeStatus(); // Update status text for the manual range input.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    /// <summary>
    /// Partial method invoked when the <see cref="PendingColumnWidth"/> property changes.
    /// Resets the mixed state for column width and attempts to apply manual sizing.
    /// </summary>
    /// <param name="value">The new value of <see cref="PendingColumnWidth"/>.</param>
    partial void OnPendingColumnWidthChanged(decimal value)
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_isLoadingSettings || _suppressManualSizingApply)
        {
            return;
        }
        IsPendingColumnWidthMixed = false; // Clear mixed state as a specific value is being set.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    /// <summary>
    /// Partial method invoked when the <see cref="PendingRowHeight"/> property changes.
    /// Resets the mixed state for row height and attempts to apply manual sizing.
    /// </summary>
    /// <param name="value">The new value of <see cref="PendingRowHeight"/>.</param>
    partial void OnPendingRowHeightChanged(decimal value)
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_isLoadingSettings || _suppressManualSizingApply)
        {
            return;
        }
        IsPendingRowHeightMixed = false; // Clear mixed state as a specific value is being set.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    /// <summary>
    /// Updates the UI status message related to the manual sizing range input.
    /// Displays parsing errors or a summary of the selected range.
    /// </summary>
    private void UpdateManualRangeStatus()
    {
        ManualRangeError = string.Empty; // Clear previous errors.
        HasManualRangeError = false;

        // If no grid is built, indicate that.
        if (_grid is null)
        {
            ManualRangeStatus = "Grid not built.";
            return;
        }

        // Attempt to parse row and column ranges.
        var rowsOk = TryParseRowRange(ManualRowsRange, out var rows, out _);
        var colsOk = TryParseColRange(ManualColsRange, out var cols, out _);

        // Update status based on parsing results.
        if (!rowsOk && !colsOk)
        {
            ManualRangeStatus = "No manual range.";
            return;
        }
        if (!rowsOk)
        {
            ManualRangeStatus = "Rows: invalid.";
            return;
        }
        if (!colsOk)
        {
            ManualRangeStatus = "Cols: invalid.";
            return;
        }

        // Format and display the selected ranges.
        var rowLabel = FormatIndexRanges(rows.Select(ToDisplayRow)); // Convert to display rows for UI.
        var colLabel = FormatIndexRanges(cols);
        ManualRangeStatus = $"Manual range: Rows {rowLabel}, Cols {colLabel}.";
    }

    /// <summary>
    /// Attempts to apply manual sizing based on the current <see cref="ManualRowsRange"/>,
    /// <see cref="ManualColsRange"/>, <see cref="PendingColumnWidth"/>, and <see cref="PendingRowHeight"/>.
    /// Displays errors if input is invalid.
    /// </summary>
    private async Task TryApplyManualSizingAsync()
    {
        if (_grid is null)
        {
            return; // Cannot apply sizing if no grid is built.
        }

        var rowsEmpty = string.IsNullOrWhiteSpace(ManualRowsRange);
        var colsEmpty = string.IsNullOrWhiteSpace(ManualColsRange);

        // If both ranges are empty, clear any errors and selection.
        if (rowsEmpty && colsEmpty)
        {
            ManualRangeError = string.Empty;
            HasManualRangeError = false;
            _selectionCoordinator.ClearSelection(CanvasHost);
            return;
        }

        // If one range is empty but the other is not, it's an incomplete input, so do nothing.
        if (rowsEmpty || colsEmpty)
        {
            return;
        }

        var plan = _manualSizingUseCase.Evaluate(_grid, ManualRowsRange, ManualColsRange, PendingColumnWidth, PendingRowHeight);
        if (plan.HasError)
        {
            ManualRangeError = plan.ErrorMessage;
            HasManualRangeError = true;
            return;
        }

        ManualRangeError = string.Empty; // Clear errors if parsing successful.
        HasManualRangeError = false;

        if (plan.Action == ManualSizingAction.SyncSelection)
        {
            SyncSelectionFromManualRange(plan.Rows, plan.Cols);
            return;
        }

        if (plan.Action == ManualSizingAction.ApplySizing)
        {
            ApplySizingToCells(plan.Rows, plan.Cols, plan.Width, plan.Height);
            var rowLabel = FormatIndexRanges(plan.Rows.Select(ToDisplayRow));
            var colLabel = FormatIndexRanges(plan.Cols);
            Logger.Debug(CultureInfo.InvariantCulture, "Manual sizing applied. Rows={0}, Cols={1}, Width={2}, Height={3}.", rowLabel, colLabel, PendingColumnWidth, PendingRowHeight);
            SyncSelectionFromManualRange(plan.Rows, plan.Cols); // Update canvas selection.
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Applies the current canvas selection to the manual sizing range input fields.
    /// </summary>
    public void ApplySelectionToManualRange()
    {
        if (_grid is null || _selectedRegIndices.Count == 0)
        {
            SetStatus("Manual sizing: select regular pads first.");
            Logger.Debug(CultureInfo.InvariantCulture, "Manual sizing range fill skipped: no regular selection.");
            return;
        }

        // Get the actual RegularPad objects from the selected indices.
        var pads = _selectedRegIndices
            .Select(i => _grid.Pads.FirstOrDefault(p => p.Index == i))
            .Where(p => p is not null)
            .Cast<RegularPad>()
            .ToList();

        if (pads.Count == 0)
        {
            SetStatus("Manual sizing: selection has no regular pads.");
            Logger.Debug(CultureInfo.InvariantCulture, "Manual sizing range fill skipped: selection had no regular pads.");
            return;
        }

        // Determine the distinct row and column indices from the selected pads.
        var rows = pads.Select(p => ToDisplayRow(p.Row)).Distinct().OrderBy(r => r).ToList();
        var cols = pads.Select(p => p.Col).Distinct().OrderBy(c => c).ToList();

        // Set the UI input fields for manual range.
        SetManualRangeInputs(FormatIndexRanges(rows), FormatIndexRanges(cols));
        Logger.Debug(CultureInfo.InvariantCulture, "Manual sizing range filled from selection. Rows={0}, Cols={1}, Count={2}.",
            FormatIndexRanges(rows),
            FormatIndexRanges(cols),
            pads.Count);
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing after setting the range.
    }

    /// <summary>
    /// Wrapper around <see cref="ManualSizingService.TryParseRowRange"/> that uses the ViewModel's current grid.
    /// </summary>
    private bool TryParseRowRange(string input, out List<int> rows, out string error)
    {
        var totalRows = _grid?.Rows ?? 0;
        return _manualSizingService.TryParseRowRange(input, totalRows, out rows, out error);
    }

    /// <summary>
    /// Wrapper around <see cref="ManualSizingService.TryParseColRange"/> that uses the ViewModel's current grid.
    /// </summary>
    private bool TryParseColRange(string input, out List<int> cols, out string error)
    {
        var totalCols = _grid?.Cols ?? 0;
        return _manualSizingService.TryParseColRange(input, totalCols, out cols, out error);
    }

    /// <summary>
    /// Synchronizes the canvas selection to highlight the pads within the specified manual sizing range.
    /// </summary>
    /// <param name="rows">The list of actual row indices in the manual sizing range.</param>
    /// <param name="cols">The list of column indices in the manual sizing range.</param>
    private void SyncSelectionFromManualRange(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        if (_grid is null)
        {
            return;
        }

        _selectionCoordinator.SelectRegularRange(_grid, rows, cols, CanvasHost);
    }
}

