// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels.Sizing;

internal sealed class ManualSizingOwner
{
    private readonly ManualSizingPorts _ports;

    public ManualSizingOwner(ManualSizingPorts ports) => _ports = ports;

    // UI thread only. The existing holder retains one input record and its latest error.
    private readonly ManualSizingState _state = new();

    public decimal PendingColumnWidth
    {
        get => _state.Input.PendingColumnWidth;
        set
        {
            if (_state.Input.PendingColumnWidth == value)
            {
                return;
            }

            OnPendingColumnWidthChanging(value);
            _ports.Input.Changing(nameof(PendingColumnWidth));
            _state.Input = _state.Input with { PendingColumnWidth = value };
            OnPendingColumnWidthChanged();
            _ports.Input.Changed(nameof(PendingColumnWidth));
        }
    }

    public decimal PendingRowHeight
    {
        get => _state.Input.PendingRowHeight;
        set
        {
            if (_state.Input.PendingRowHeight == value)
            {
                return;
            }

            OnPendingRowHeightChanging(value);
            _ports.Input.Changing(nameof(PendingRowHeight));
            _state.Input = _state.Input with { PendingRowHeight = value };
            OnPendingRowHeightChanged();
            _ports.Input.Changed(nameof(PendingRowHeight));
        }
    }

    public bool IsPendingColumnWidthMixed
    {
        get => _state.Input.IsPendingColumnWidthMixed;
        set
        {
            if (_state.Input.IsPendingColumnWidthMixed == value)
            {
                return;
            }

            _ports.Input.Changing(nameof(IsPendingColumnWidthMixed));
            _state.Input = _state.Input with { IsPendingColumnWidthMixed = value };
            _ports.Input.Changed(nameof(IsPendingColumnWidthMixed));
        }
    }

    public bool IsPendingRowHeightMixed
    {
        get => _state.Input.IsPendingRowHeightMixed;
        set
        {
            if (_state.Input.IsPendingRowHeightMixed == value)
            {
                return;
            }

            _ports.Input.Changing(nameof(IsPendingRowHeightMixed));
            _state.Input = _state.Input with { IsPendingRowHeightMixed = value };
            _ports.Input.Changed(nameof(IsPendingRowHeightMixed));
        }
    }

    public string ManualRowsRange
    {
        get => _state.Input.ManualRowsRange;
        set
        {
            if (_state.Input.ManualRowsRange == value)
            {
                return;
            }

            OnManualRowsRangeChanging(value);
            _ports.Input.Changing(nameof(ManualRowsRange));
            _state.Input = _state.Input with { ManualRowsRange = value };
            OnManualRowsRangeChanged();
            _ports.Input.Changed(nameof(ManualRowsRange));
        }
    }

    public string ManualColsRange
    {
        get => _state.Input.ManualColsRange;
        set
        {
            if (_state.Input.ManualColsRange == value)
            {
                return;
            }

            OnManualColsRangeChanging(value);
            _ports.Input.Changing(nameof(ManualColsRange));
            _state.Input = _state.Input with { ManualColsRange = value };
            OnManualColsRangeChanged();
            _ports.Input.Changed(nameof(ManualColsRange));
        }
    }

    public string ManualRangeStatus
    {
        get => _state.Input.ManualRangeStatus;
        set
        {
            if (_state.Input.ManualRangeStatus == value)
            {
                return;
            }

            _ports.Input.Changing(nameof(ManualRangeStatus));
            _state.Input = _state.Input with { ManualRangeStatus = value };
            _ports.Input.Changed(nameof(ManualRangeStatus));
        }
    }

    public string ManualRangeError
    {
        get => _state.RangeError;
        set
        {
            var hadError = HasManualRangeError;
            if (EqualityComparer<string>.Default.Equals(_state.RangeError, value))
            {
                return;
            }
            _ports.Input.Changing(nameof(ManualRangeError));
            _state.RangeError = value;
            _ports.Input.Changed(nameof(ManualRangeError));
            if (hadError != HasManualRangeError)
            {
                _ports.Input.Changed(nameof(HasManualRangeError));
            }
        }
    }

    public bool HasManualRangeError => !string.IsNullOrEmpty(_state.RangeError);

    // --- Derived Read-Only Properties ---

    public double PitchSizeX => _ports.XChannels() <= 0 ? 0.0 : (double)_ports.ActiveAreaWidth() / (double)_ports.XChannels();
    public double PitchSizeY => _ports.YChannels() <= 0 ? 0.0 : (double)_ports.ActiveAreaHeight() / (double)_ports.YChannels();

    public string PitchSizeXSummary => BuildPitchSizeSummary("Pitch X", "X", PitchSizeX, useWidth: true);
    public string PitchSizeYSummary => BuildPitchSizeSummary("Pitch Y", "Y", PitchSizeY, useWidth: false);

    private const double PitchDistinctToleranceMm = 0.0001;

    private string BuildPitchSizeSummary(string title, string indexPrefix, double fallbackValue, bool useWidth)
    {
        var values = GetDistinctPitchValues(useWidth);
        if (values.Count == 0)
        {
            return $"{title}: {indexPrefix}1={fallbackValue:0.###} mm";
        }

        var entries = values
            .Select((value, index) => $"{indexPrefix}{index + 1}={value:0.###} mm");
        return $"{title}: {string.Join(", ", entries)}";
    }

    private List<double> GetDistinctPitchValues(bool useWidth)
    {
        if (_ports.Grid() is null || _ports.Grid()!.Pads.Count == 0)
        {
            return new List<double>();
        }

        var orderedValues = _ports.Grid()!.Pads
            .Select(pad => useWidth ? pad.Bounds.Width : pad.Bounds.Height)
            .Where(value => value > 0)
            .OrderBy(value => value)
            .ToList();

        if (orderedValues.Count == 0)
        {
            return new List<double>();
        }

        var distinct = new List<double>();
        foreach (var value in orderedValues)
        {
            if (distinct.Count == 0 || Math.Abs(value - distinct[^1]) > PitchDistinctToleranceMm)
            {
                distinct.Add(value);
            }
        }

        return distinct;
    }

    private void OnManualRowsRangeChanged()
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_ports.Input.IsLoading() || _ports.Input.IsApplySuppressed())
        {
            return;
        }
        UpdateManualRangeStatus(); // Update status text for the manual range input.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    private void OnManualColsRangeChanged()
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_ports.Input.IsLoading() || _ports.Input.IsApplySuppressed())
        {
            return;
        }
        UpdateManualRangeStatus(); // Update status text for the manual range input.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    private void OnPendingColumnWidthChanged()
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_ports.Input.IsLoading() || _ports.Input.IsApplySuppressed())
        {
            return;
        }
        IsPendingColumnWidthMixed = false; // Clear mixed state as a specific value is being set.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    private void OnPendingRowHeightChanged()
    {
        // Suppress updates if settings are loading or manual sizing is temporarily suppressed.
        if (_ports.Input.IsLoading() || _ports.Input.IsApplySuppressed())
        {
            return;
        }
        IsPendingRowHeightMixed = false; // Clear mixed state as a specific value is being set.
        _ = TryApplyManualSizingAsync(); // Attempt to apply sizing asynchronously.
    }

    public void UpdateManualRangeStatus()
    {
        ManualRangeError = string.Empty; // Clear previous errors.

        // If no grid is built, indicate that.
        if (_ports.Grid() is null)
        {
            ManualRangeStatus = "Grid not built.";
            return;
        }

        // Attempt to parse row and column ranges.
        var rowsOk = _ports.Range.ParseRows(ManualRowsRange, out var rows, out _);
        var colsOk = _ports.Range.ParseCols(ManualColsRange, out var cols, out _);

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
        var rowLabel = _ports.Range.FormatRanges(rows.Select(ToDisplayRow)); // Convert to display rows for UI.
        var colLabel = _ports.Range.FormatRanges(cols);
        ManualRangeStatus = $"Manual range: Rows {rowLabel}, Cols {colLabel}.";
    }

    public async Task TryApplyManualSizingAsync()
    {
        if (_ports.Grid() is null)
        {
            return; // Cannot apply sizing if no grid is built.
        }

        var rowsEmpty = string.IsNullOrWhiteSpace(ManualRowsRange);
        var colsEmpty = string.IsNullOrWhiteSpace(ManualColsRange);

        // If both ranges are empty, clear any errors and selection.
        if (rowsEmpty && colsEmpty)
        {
            ManualRangeError = string.Empty;
            _ports.Range.ClearSelection();
            return;
        }

        // If one range is empty but the other is not, it's an incomplete input, so do nothing.
        if (rowsEmpty || colsEmpty)
        {
            return;
        }

        var plan = _ports.Range.Evaluate(_ports.Grid()!, ManualRowsRange, ManualColsRange, PendingColumnWidth, PendingRowHeight);
        if (plan.HasError)
        {
            ManualRangeError = plan.Error;
            return;
        }

        ManualRangeError = string.Empty; // Clear errors if parsing successful.

        if (plan.SyncSelection)
        {
            _ports.Range.SelectRange(plan.Rows, plan.Cols);
            return;
        }

        if (plan.ApplySizing)
        {
            ApplySizingToCells(plan.Rows, plan.Cols, plan.Width, plan.Height);
            var rowLabel = _ports.Range.FormatRanges(plan.Rows.Select(ToDisplayRow));
            var colLabel = _ports.Range.FormatRanges(plan.Cols);
            _ports.Edit.Debug("Manual sizing applied. Rows={0}, Cols={1}, Width={2}, Height={3}.", rowLabel, colLabel, PendingColumnWidth, PendingRowHeight);
            _ports.Range.SelectRange(plan.Rows, plan.Cols); // Update canvas selection.
        }
        await Task.CompletedTask;
    }

    public void SetCadPadCustomValues(IReadOnlyList<int> cadIds, decimal value)
    {
        if (cadIds.Count == 0)
        {
            return; // No CAD pads specified.
        }

        // Capture the previous values for undo functionality.
        var previous = cadIds.ToDictionary(
            id => id,
            id => _ports.Edit.CustomValues().TryGetValue(id, out var v) ? (double?)v : null);

        // Apply the new value to each specified CAD pad.
        foreach (var id in cadIds)
        {
            _ports.Edit.CustomValues()[id] = (double)value;
        }

        // Push an undo action onto the undo stack.
        _ports.Edit.PushUndo(() =>
        {
            // The undo action restores each CAD pad's custom value to its state before this operation.
            foreach (var kv in previous)
            {
                if (kv.Value is null)
                {
                    _ports.Edit.CustomValues().Remove(kv.Key); // Remove if it was new.
                }
                else
                {
                    _ports.Edit.CustomValues()[kv.Key] = kv.Value.Value; // Restore old value.
                }
            }
        }, "CAD custom value"); // Description for the undo action.

        // Update status text and mark project as unsaved.
        _ports.Edit.SetStatus(cadIds.Count == 1
            ? $"CAD pad #{cadIds[0]} custom value set to {value:0.####}."
            : $"CAD pads updated: {cadIds.Count} items.");
        _ports.Edit.MarkUnsaved();
    }

    public void ApplySizingToCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols, decimal? widthMm, decimal? heightMm)
    {
        if (_ports.Grid() is null)
        {
            _ports.Edit.SetStatus("Manual sizing: build grid first.");
            return;
        }

        // A rejected request must not mark the project unsaved. The success path calls MarkUnsaved below.
        _ports.Edit.ApplyUiSettings(); // Ensure current UI settings are applied to project file.

        var hasWidth = widthMm.HasValue && widthMm.Value > 0;
        var hasHeight = heightMm.HasValue && heightMm.Value > 0;
        if (!hasWidth && !hasHeight)
        {
            return; // No sizing changes requested.
        }

        // Use the ManualSizingService to apply the sizing changes.
        var result = _ports.Edit.Apply(
            rows,
            cols,
            widthMm,
            heightMm);

        if (!result.Success)
        {
            _ports.Edit.SetStatus($"Manual sizing: {result.Error}");
            _ports.Edit.Warn("Manual sizing apply failed. Error={0}", result.Error);
            return;
        }

        // Push an undo action onto the undo stack.
        _ports.Edit.PushUndo(() =>
        {
            // The undo action restores the grid settings from the snapshot and triggers a rebuild.
            result.Restore();
            _ = _ports.Edit.RebuildAsync(true);
        }, "Manual sizing"); // Description for the undo action.

        _ports.Edit.SetStatus("Manual sizing applied.");
        var rowLabel = _ports.Range.FormatRanges(rows.Select(ToDisplayRow));
        var colLabel = _ports.Range.FormatRanges(cols);
        _ports.Edit.Info("Manual sizing apply requested. Rows={0}, Cols={1}, Width={2}, Height={3}.",
            rowLabel,
            colLabel,
            widthMm?.ToString(CultureInfo.InvariantCulture) ?? "unchanged",
            heightMm?.ToString(CultureInfo.InvariantCulture) ?? "unchanged");
        _ports.Edit.PreserveSelection(); // Suppress clearing selection during the rebuild.
        _ = _ports.Edit.RebuildAsync(true); // Trigger a grid rebuild to reflect new sizing.
        _ports.Edit.MarkUnsaved(); // Mark the project as unsaved.
    }

    public void ResetSizingForCells(IReadOnlyList<int> rows, IReadOnlyList<int> cols)
    {
        if (_ports.Grid() is null)
        {
            _ports.Edit.SetStatus("Manual sizing: build grid first.");
            return;
        }

        // A rejected request must not mark the project unsaved. The success path calls MarkUnsaved below.
        _ports.Edit.ApplyUiSettings(); // Ensure current UI settings are applied to project file.

        // Use the ManualSizingService to reset the sizing for the specified cells.
        var result = _ports.Edit.Reset(
            rows,
            cols);

        if (!result.Success)
        {
            _ports.Edit.SetStatus($"Reset sizing: {result.Error}");
            _ports.Edit.Warn("Manual sizing reset failed. Error={0}", result.Error);
            return;
        }

        // Push an undo action onto the undo stack.
        _ports.Edit.PushUndo(() =>
        {
            // The undo action restores the grid settings from the snapshot and triggers a rebuild.
            result.Restore();
            _ = _ports.Edit.RebuildAsync(true);
        }, "Reset manual sizing"); // Description for the undo action.

        _ports.Edit.SetStatus("Manual sizing reset to default.");
        var rowLabel = _ports.Range.FormatRanges(rows.Select(ToDisplayRow));
        var colLabel = _ports.Range.FormatRanges(cols);
        _ports.Edit.Info("Manual sizing reset requested. Rows={0}, Cols={1}.", rowLabel, colLabel);
        _ports.Edit.PreserveSelection(); // Suppress clearing selection during the rebuild.
        _ = _ports.Edit.RebuildAsync(true); // Trigger a grid rebuild to reflect changes.
        _ports.Edit.MarkUnsaved(); // Mark the project as unsaved.
    }

    public double GetCadPadCustomValue(int cadId) =>
        _ports.Edit.CustomValues().TryGetValue(cadId, out var value) ? value : 0.0;

    public int ToDisplayRow(int actualRow) => _ports.Range.ToDisplayRow(actualRow);

    private void OnPendingColumnWidthChanging(decimal value) =>
        _ports.Input.TrackDecimalUndo(PendingColumnWidth, value, v => PendingColumnWidth = v, "Manual width");

    private void OnPendingRowHeightChanging(decimal value) =>
        _ports.Input.TrackDecimalUndo(PendingRowHeight, value, v => PendingRowHeight = v, "Manual height");

    private void OnManualRowsRangeChanging(string value) =>
        _ports.Input.TrackStringUndo(ManualRowsRange, value, v => ManualRowsRange = v, "Manual rows range");

    private void OnManualColsRangeChanging(string value) =>
        _ports.Input.TrackStringUndo(ManualColsRange, value, v => ManualColsRange = v, "Manual cols range");
}
