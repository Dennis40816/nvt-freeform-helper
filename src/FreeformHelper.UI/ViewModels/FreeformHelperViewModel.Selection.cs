using System.Diagnostics;
using System.Globalization;
using FreeformHelper.UI.Interaction;

namespace FreeformHelper.UI.ViewModels;

/// <summary>
/// This partial class of <see cref="FreeformHelperViewModel"/> manages the selection state
/// of pads (both CAD and regular) on the canvas, updates UI summaries, and synchronizes
/// with manual sizing inputs.
/// </summary>
public sealed partial class FreeformHelperViewModel
{
    private const double LocateFocusMinZoom = 2.0;
    private const double NotchExportPreviewFocusMinZoom = 2.4;
    private const int SelectionNotchPreviewDeferredDelayMs = 120;

    // HashSets for efficient storage and lookup of selected pad IDs/indices.
    private readonly HashSet<int> _selectedCadIds = new();
    private readonly HashSet<int> _selectedRegIndices = new();
    private CancellationTokenSource? _selectionNotchPreviewCts;
    private long _selectionNotchPreviewRequestId;
    private long _lastSelectionSummaryMs;
    private long _lastSelectionInspectorMs;
    private long _lastSelectionNotchPreviewMs;
    private long _lastSelectionTotalMs;

    private (int minR, int maxR, int minC, int maxC)? _selectedRegRange;

    /// <summary>
    /// Gets a read-only collection of selected CAD pad IDs.
    /// </summary>
    public IReadOnlyCollection<int> SelectedCadPadIds => _selectedCadIds;

    /// <summary>
    /// Gets a read-only collection of selected regular pad indices.
    /// </summary>
    public IReadOnlyCollection<int> SelectedRegularPadIndices => _selectedRegIndices;

    /// <summary>
    /// Gets the rectangular range (min/max row/column) covered by the selected regular pads.
    /// Null if no regular pads are selected or they do not form a contiguous block.
    /// </summary>
    public (int minR, int maxR, int minC, int maxC)? SelectedRegularRange => _selectedRegRange;

    public SelectionTimingSnapshot GetLastSelectionTimingSnapshot()
    {
        return new SelectionTimingSnapshot(
            SummaryMs: Interlocked.Read(ref _lastSelectionSummaryMs),
            InspectorMs: Interlocked.Read(ref _lastSelectionInspectorMs),
            NotchPreviewMs: Interlocked.Read(ref _lastSelectionNotchPreviewMs),
            TotalMs: Interlocked.Read(ref _lastSelectionTotalMs));
    }

    /// <summary>
    /// Applies a selection received from the canvas to the ViewModel selection coordinator.
    /// </summary>
    public void ApplyCanvasSelection(IReadOnlyList<int> cadPadIds, IReadOnlyList<int> regularPadIndices)
    {
        _selectionCoordinator.ApplyCanvasSelection(cadPadIds, regularPadIndices);
    }

    /// <summary>
    /// Handles selection updates from workspace interaction state.
    /// </summary>
    private void OnInteractionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplySelection(e.CadIds, e.RegularIndices);
    }

    /// <summary>
    /// Updates internal selection state and synchronizes dependent UI state.
    /// </summary>
    private void ApplySelection(IReadOnlyList<int> cadPadIds, IReadOnlyList<int> regularPadIndices)
    {
        var selectionRevision = Interlocked.Increment(ref _padInspectorSelectionRevision);
        var selectionSw = Stopwatch.StartNew();
        _selectedCadIds.Clear();
        foreach (var id in cadPadIds)
        {
            _selectedCadIds.Add(id);
        }

        _selectedRegIndices.Clear();
        foreach (var idx in regularPadIndices)
        {
            _selectedRegIndices.Add(idx);
        }

        var summaryStartMs = selectionSw.ElapsedMilliseconds;
        UpdateSelectedRangeSummary();
        var summaryDurationMs = selectionSw.ElapsedMilliseconds - summaryStartMs;

        OnPropertyChanged(nameof(SelectedCadPadIds));
        OnPropertyChanged(nameof(SelectedRegularPadIndices));
        OnPropertyChanged(nameof(SelectedRegularRange));
        NotifyDxfEditPanelActionStateChanged();
        var inspectorStartMs = selectionSw.ElapsedMilliseconds;
        UpdateInspectorSnapshotFromSelection(selectionRevision);
        var inspectorDurationMs = selectionSw.ElapsedMilliseconds - inspectorStartMs;

        long notchPreviewDurationMs = 0;
        if (_grid is not null && _selectedCadIds.Count > 0)
        {
            var previewStartMs = selectionSw.ElapsedMilliseconds;
            if (_selectedCadIds.Count == 1)
            {
                CancelDeferredSelectionNotchPreviewRefresh();
                RefreshNotchCanvasPreview(showStatus: false);
            }
            else
            {
                QueueDeferredSelectionNotchPreviewRefresh();
            }

            notchPreviewDurationMs = selectionSw.ElapsedMilliseconds - previewStartMs;
        }
        else
        {
            CancelDeferredSelectionNotchPreviewRefresh();
            ClearNotchCanvasPreview();
        }

        var totalDurationMs = selectionSw.ElapsedMilliseconds;
        Interlocked.Exchange(ref _lastSelectionSummaryMs, summaryDurationMs);
        Interlocked.Exchange(ref _lastSelectionInspectorMs, inspectorDurationMs);
        Interlocked.Exchange(ref _lastSelectionNotchPreviewMs, notchPreviewDurationMs);
        Interlocked.Exchange(ref _lastSelectionTotalMs, totalDurationMs);
        var canvasPerfText = BuildCanvasPerfText();

        Logger.Debug(
            CultureInfo.InvariantCulture,
            "Selection updated: CAD={0}, Regular={1}, summary={2}ms, inspector={3}ms, notchPreview={4}ms, total={5}ms. {6} | {7}",
            _selectedCadIds.Count,
            _selectedRegIndices.Count,
            summaryDurationMs,
            inspectorDurationMs,
            notchPreviewDurationMs,
            totalDurationMs,
            SelectionDetailText,
            canvasPerfText);
    }

    private string BuildCanvasPerfText()
    {
        if (CanvasHost is null)
        {
            return "canvasPerf=n/a";
        }

        var selectionPerf = CanvasHost.GetSelectionPerfSnapshot();
        var drawPerf = CanvasHost.GetVisibleDrawPerfSnapshot();
        return string.Format(
            CultureInfo.InvariantCulture,
            "canvasSelect(rev={0},ms={1},cadCand={2},regCand={3},cadIdx={4},regIdx={5}) draw(rev={6},ms={7},cadCand={8},regCand={9},visCad={10},visReg={11},stepCad={12},stepReg={13},decCad={14},decReg={15},qHit={16},listHit={17})",
            selectionPerf.Revision,
            selectionPerf.ElapsedMs,
            selectionPerf.CadCandidates,
            selectionPerf.RegularCandidates,
            selectionPerf.UsedCadIndex ? "Y" : "N",
            selectionPerf.UsedRegularIndex ? "Y" : "N",
            drawPerf.Revision,
            drawPerf.ElapsedMs,
            drawPerf.CadCandidates,
            drawPerf.RegularCandidates,
            drawPerf.VisibleCadSelected + drawPerf.VisibleCadUnselected,
            drawPerf.VisibleRegularSelected + drawPerf.VisibleRegularUnselected,
            drawPerf.CadDecimationStep,
            drawPerf.RegularDecimationStep,
            drawPerf.CadDecimatedCount,
            drawPerf.RegularDecimatedCount,
            drawPerf.QueryCacheHit ? "Y" : "N",
            drawPerf.DrawListCacheHit ? "Y" : "N");
    }

    private void UpdateInspectorSnapshotFromSelection(long selectionRevision)
    {
        if (_selectedCadIds.Count == 1)
        {
            CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: true);
            var cadId = _selectedCadIds.First();
            CurrentPadInspectorSnapshot = BuildCadPadInspectorSnapshot(
                cadId,
                includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false,
                includeRuleTrace: false);
            QueueDeferredCadInspectorSnapshotRefresh(cadId, selectionRevision);
            return;
        }

        if (_selectedCadIds.Count > 1)
        {
            // Multi-select can generate high-frequency updates during box drag.
            // Keep inspector lightweight and avoid blocking the selection interaction.
            CancelDeferredCadInspectorSnapshotRefresh(clearPendingState: true);
            CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: true);
            CurrentPadInspectorSnapshot = null;
            return;
        }

        CancelDeferredCadInspectorSnapshotRefresh(clearPendingState: true);
        if (_selectedRegIndices.Count == 1)
        {
            var selectedRegularIndex = _selectedRegIndices.First();
            var primaryRegular = RegularPads.FirstOrDefault(pad => pad.Index == selectedRegularIndex);
            if (primaryRegular is not null)
            {
                CurrentPadInspectorSnapshot = BuildRegularPadInspectorSnapshot(
                    primaryRegular.RegularPadId,
                    includeMatchDetails: false,
                    includeRuleTrace: false);
                QueueDeferredRegularInspectorSnapshotRefresh(primaryRegular.RegularPadId, selectionRevision);
                return;
            }
        }
        else if (_selectedRegIndices.Count > 1)
        {
            CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: true);
            CurrentPadInspectorSnapshot = null;
            return;
        }

        CancelDeferredRegularInspectorSnapshotRefresh(clearPendingState: true);
        CurrentPadInspectorSnapshot = null;
    }
}

