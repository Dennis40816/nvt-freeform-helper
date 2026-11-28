// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Diagnostics;

namespace FreeformHelper.UI.ViewModels.Selection;

/// <summary>Owns accepted membership, summary publication, and the ordered selection workflow. UI thread only.</summary>
internal sealed class SelectionOwner<TSummary> where TSummary : class
{
    private readonly SelectionSummaryPorts<TSummary> _summary;
    private readonly SelectionInteractionPorts _interaction;
    private readonly SelectionManualPorts _manual;
    private readonly SelectionInspectorPorts _inspector;
    private readonly HashSet<int> _selectedCadIds = new();
    private readonly SelectionTimingRecorder _timings = new();
    private TSummary _summaryResult;

    public SelectionOwner(
        SelectionSummaryPorts<TSummary> summary,
        SelectionInteractionPorts interaction,
        SelectionManualPorts manual,
        SelectionInspectorPorts inspector,
        SelectionLocator locator,
        SelectionPreviewRefreshOwner preview)
    {
        _summary = summary;
        _interaction = interaction;
        _manual = manual;
        _inspector = inspector;
        _summaryResult = summary.Initial;
        Locator = locator;
        Preview = preview;
    }

    public SelectionState RegularSelection { get; } = new();
    public SelectionLocator Locator { get; }
    public SelectionPreviewRefreshOwner Preview { get; }
    public IReadOnlySet<int> SelectedCadPadIds => _selectedCadIds;
    public IReadOnlyCollection<int> SelectedRegularPadIndices => RegularSelection.RegularIndices;
    public int SelectedPadCount => _selectedCadIds.Count + RegularSelection.RegularIndices.Count;
    public string CadSelectionText => _summary.Project(_summaryResult).CadSelectionText;
    public string RegularSelectionText => _summary.Project(_summaryResult).RegularSelectionText;
    public string SelectionDetailText => _summary.Project(_summaryResult).SelectionDetailText;

    public TSummary SummaryResult
    {
        get => _summaryResult;
        set
        {
            if (EqualityComparer<TSummary>.Default.Equals(_summaryResult, value))
            {
                return;
            }

            var oldValue = _summaryResult;
            _summary.PropertyChanging("SelectionSummaryResult");
            _summaryResult = value;
            ArgumentNullException.ThrowIfNull(oldValue);
            var previous = _summary.Project(oldValue);
            var current = _summary.Project(value);
            RaiseTextChanged(previous.CadSelectionText, current.CadSelectionText, nameof(CadSelectionText));
            RaiseTextChanged(previous.RegularSelectionText, current.RegularSelectionText, nameof(RegularSelectionText));
            RaiseTextChanged(previous.SelectionDetailText, current.SelectionDetailText, nameof(SelectionDetailText));
            _summary.PropertyChanged("SelectionSummaryResult");
        }
    }

    public string QuickFocusQueryText
    {
        get => _interaction.ReadQuickFocusQuery();
        set
        {
            if (_interaction.ReadQuickFocusQuery() == value)
            {
                return;
            }

            _summary.PropertyChanging(nameof(QuickFocusQueryText));
            _interaction.WriteQuickFocusQuery(value);
            _summary.PropertyChanged(nameof(QuickFocusQueryText));
        }
    }

    public SelectionTimings GetLastSelectionTimings() => _timings.Last;

    public void ApplyCanvasSelection(IReadOnlyList<int> cadPadIds, IReadOnlyList<int> regularPadIndices) =>
        _interaction.ApplyCanvasSelection(cadPadIds, regularPadIndices);

    public void ClearSelection()
    {
        if (!_interaction.CanClearSelection())
        {
            return;
        }

        _interaction.ClearSelection();
    }

    public void ApplySelection(IReadOnlyList<int> cadPadIds, IReadOnlyList<int> regularPadIndices)
    {
        var selectionRevision = _inspector.NextSelectionRevision();
        var selectionSw = Stopwatch.StartNew();
        _selectedCadIds.Clear();
        foreach (var id in cadPadIds)
        {
            _selectedCadIds.Add(id);
        }

        RegularSelection.RegularIndices.Clear();
        foreach (var index in regularPadIndices)
        {
            RegularSelection.RegularIndices.Add(index);
        }

        var summaryStartMs = selectionSw.ElapsedMilliseconds;
        UpdateSelectedRangeSummary();
        var summaryDurationMs = selectionSw.ElapsedMilliseconds - summaryStartMs;

        _summary.PropertyChanged(nameof(SelectedCadPadIds));
        _summary.PropertyChanged(nameof(SelectedRegularPadIndices));
        _summary.PropertyChanged("HasPadInspectorMultiSelection");
        _summary.PropertyChanged("PadInspectorSelectionSummaryText");
        _summary.PropertyChanged("HasNoPadInspectorSummary");
        _interaction.NotifyDxfEditActions();
        var inspectorStartMs = selectionSw.ElapsedMilliseconds;
        UpdateInspectorSnapshotFromSelection(selectionRevision);
        var inspectorDurationMs = selectionSw.ElapsedMilliseconds - inspectorStartMs;

        long notchPreviewDurationMs = 0;
        if (_interaction.ReadGrid() is not null && _selectedCadIds.Count > 0)
        {
            var previewStartMs = selectionSw.ElapsedMilliseconds;
            if (_selectedCadIds.Count == 1)
            {
                Preview.Cancel();
                _interaction.RefreshPreview();
            }
            else
            {
                Preview.Queue();
            }

            notchPreviewDurationMs = selectionSw.ElapsedMilliseconds - previewStartMs;
        }
        else
        {
            Preview.Cancel();
            _interaction.ClearPreview();
        }

        var totalDurationMs = selectionSw.ElapsedMilliseconds;
        _timings.Record(summaryDurationMs, inspectorDurationMs, notchPreviewDurationMs, totalDurationMs);
        _interaction.LogTimings(_timings.Last);
    }

    public void UpdateSelectedRangeSummary()
    {
        var summary = _summary.Compute(_interaction.ReadGrid(), _selectedCadIds, RegularSelection.RegularIndices);
        SummaryResult = summary;
        var fields = _summary.Project(summary);
        SetManualRangeInputs(fields.ManualRowsRange, fields.ManualColsRange);
        _manual.SetRangeError(fields.ManualRangeError);
        SetManualSizeInputs(fields.PendingWidth, fields.PendingHeight);
        _manual.SetWidthMixed(fields.WidthMixed);
        _manual.SetHeightMixed(fields.HeightMixed);
    }

    public void RefreshInspectorFromSelection()
    {
        var selectionRevision = _inspector.NextSelectionRevision();
        UpdateInspectorSnapshotFromSelection(selectionRevision);
    }

    private void UpdateInspectorSnapshotFromSelection(long selectionRevision)
    {
        if (_inspector.HasMultiSelection())
        {
            // Keep multi-selection updates lightweight during box drag.
            _inspector.CancelCad(true);
            _inspector.CancelRegular(true);
            _inspector.SetSnapshot(null);
            return;
        }

        if (_selectedCadIds.Count == 1)
        {
            _inspector.CancelRegular(true);
            var cadId = _selectedCadIds.First();
            _inspector.SetSnapshot(_inspector.BuildCad(cadId));
            _inspector.QueueCad(cadId, selectionRevision);
            return;
        }

        _inspector.CancelCad(true);
        if (RegularSelection.RegularIndices.Count == 1)
        {
            var selectedRegularIndex = RegularSelection.RegularIndices.First();
            var primaryRegular = _interaction.ReadRegularPads().FirstOrDefault(pad => pad.Index == selectedRegularIndex);
            if (primaryRegular is not null)
            {
                _inspector.SetSnapshot(_inspector.BuildRegular(primaryRegular.RegularPadId));
                _inspector.QueueRegular(primaryRegular.RegularPadId, selectionRevision);
                return;
            }
        }

        _inspector.CancelRegular(true);
        _inspector.SetSnapshot(null);
    }

    private void SetManualRangeInputs(string rows, string cols)
    {
        var current = _manual.ReadRanges();
        if (current.Rows == rows && current.Cols == cols)
        {
            return;
        }

        using (_manual.SuppressManualSizing())
        {
            using (_manual.SuppressUndo())
            {
                _manual.SetRows(rows);
                _manual.SetCols(cols);
            }
        }

        _manual.UpdateRangeStatus();
    }

    private void SetManualSizeInputs(decimal width, decimal height)
    {
        var current = _manual.ReadSizes();
        if (current.Width == width && current.Height == height)
        {
            return;
        }

        using (_manual.SuppressManualSizing())
        {
            using (_manual.SuppressUndo())
            {
                _manual.SetWidth(width);
                _manual.SetHeight(height);
            }
        }

        _manual.SetWidthMixed(false);
        _manual.SetHeightMixed(false);
    }

    private void RaiseTextChanged(string oldValue, string newValue, string propertyName)
    {
        if (!EqualityComparer<string>.Default.Equals(oldValue, newValue))
        {
            _summary.PropertyChanged(propertyName);
        }
    }
}
