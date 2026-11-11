using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using FreeformHelper.UI.Interaction;
using FreeformHelper.UI.ViewModels.Selection;

namespace FreeformHelper.UI.ViewModels;

/// <summary>Keeps the root selection API and command identities while the owner coordinates selection.</summary>
public sealed partial class FreeformHelperViewModel
{
    private const double NotchExportPreviewFocusMinZoom = 2.4;

    // Other workflow partials read the same owner-held membership and rebuild flag.
    private IReadOnlySet<int> _selectedCadIds => Selection.SelectedCadPadIds;
    private SelectionState _selection => Selection.RegularSelection;
    private int SelectedPadCount => Selection.SelectedPadCount;

    /// <summary>Gets the selected CAD pad IDs.</summary>
    public IReadOnlyCollection<int> SelectedCadPadIds => Selection.SelectedCadPadIds;

    /// <summary>Gets the selected Regular pad indices.</summary>
    public IReadOnlyCollection<int> SelectedRegularPadIndices => Selection.SelectedRegularPadIndices;

    public SelectionTimingSnapshot GetLastSelectionTimingSnapshot()
    {
        var last = Selection.GetLastSelectionTimings();
        return new SelectionTimingSnapshot(last.SummaryMs, last.InspectorMs, last.NotchPreviewMs, last.TotalMs);
    }

    /// <summary>Applies the canvas selection through the existing selection coordinator.</summary>
    public void ApplyCanvasSelection(IReadOnlyList<int> cadPadIds, IReadOnlyList<int> regularPadIndices) =>
        Selection.ApplyCanvasSelection(cadPadIds, regularPadIndices);

    private void OnInteractionSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        Selection.ApplySelection(e.CadIds, e.RegularIndices);

    /// <summary>Rebuilds Inspector content from the current selection after edits.</summary>
    public void RefreshInspectorFromSelection() => Selection.RefreshInspectorFromSelection();

    /// <summary>Gets or sets the unified CAD, Regular, or FW diff quick-focus query.</summary>
    public string QuickFocusQueryText
    {
        get => Selection.QuickFocusQueryText;
        set => Selection.QuickFocusQueryText = value;
    }

    private void LogSelectionTimings(SelectionTimings timings)
    {
        var canvasPerfText = BuildCanvasPerfText();
        _logger.Debug(
            CultureInfo.InvariantCulture,
            "Selection updated: CAD={0}, Regular={1}, summary={2}ms, inspector={3}ms, notchPreview={4}ms, total={5}ms. {6} | {7}",
            _selectedCadIds.Count,
            _selection.RegularIndices.Count,
            timings.SummaryMs,
            timings.InspectorMs,
            timings.NotchPreviewMs,
            timings.TotalMs,
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

    /// <summary>
    /// Gets the command to clear the current selection on the canvas.
    /// </summary>
    public IRelayCommand ClearSelectionCommand => _selectionCommands.ClearSelectionCommand;

    /// <summary>
    /// Gets the command to focus a CAD pad, regular pad, or FW diff from one quick-focus query.
    /// </summary>
    public IRelayCommand QuickFocusCommand => _selectionCommands.QuickFocusCommand;

    private readonly SelectionCommands _selectionCommands;

    /// <summary>Creates the selection commands once during construction.</summary>
    private SelectionCommands CreateSelectionCommands()
    {
        return new SelectionCommands(
            ClearSelectionCommand: CreateEditingCommand(ClearSelection),
            QuickFocusCommand: CreateEditingCommand(ExecuteQuickFocus));
    }

    private void ClearSelection() => Selection.ClearSelection();
}
