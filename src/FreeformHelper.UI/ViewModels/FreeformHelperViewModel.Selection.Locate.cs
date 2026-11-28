using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Selection;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private SelectionLocator CreateSelectionLocator() => new(new SelectionLocatorPorts(
        () => CadPads, () => RegularPads, () => _workflowData.LatestPadMatch,
        () => _cadIcIndexByCadId, () => _cadOutputFwDiffIndexByCadId,
        () => QuickFocusQueryText, ParseSelectionLocateQuery,
        (cad, regular) => _selectionCoordinator.ApplyProgrammaticSelection(cad, regular, CanvasHost),
        minZoom => CanvasHost?.FocusSelectionWithMinZoom(minZoom), SetStatus));

    private static SelectionLocateParseResult ParseSelectionLocateQuery(string text)
    {
        var success = QuickFocusQueryParser.TryParse(text, out var query, out var error);
        var kind = query.TargetKind switch
        {
            QuickFocusTargetKind.Cad => SelectionLocateTargetKind.Cad,
            QuickFocusTargetKind.Regular => SelectionLocateTargetKind.Regular,
            QuickFocusTargetKind.Diff => SelectionLocateTargetKind.Diff,
            _ => SelectionLocateTargetKind.Auto,
        };
        return new SelectionLocateParseResult(success, new SelectionLocateQuery(kind, query.Value, query.IcIndex), error);
    }

    private void ExecuteQuickFocus() => Selection.Locator.ExecuteQuickFocus();

    public IReadOnlyList<int> GetMatchedRegularPadIds(int cadPadId) => Selection.Locator.GetMatchedRegularPadIds(cadPadId);

    public IReadOnlyList<PadMatchLink> GetMatchedRegularLinks(int cadPadId) => Selection.Locator.GetMatchedRegularLinks(cadPadId);

    public (int IcIndex, int DiffIndex)? GetRegularPadIcDiff(int regularPadId) => Selection.Locator.GetRegularPadIcDiff(regularPadId);

    public IReadOnlyList<int> GetMatchedCadPadIds(int regularPadId) => Selection.Locator.GetMatchedCadPadIds(regularPadId);

    public IReadOnlyList<PadMatchLink> GetMatchedCadLinks(int regularPadId) => Selection.Locator.GetMatchedCadLinks(regularPadId);

    public void FocusCadMatches(int cadPadId) => Selection.Locator.FocusCadMatches(cadPadId);

    public void HighlightCadMatches(int cadPadId) => Selection.Locator.HighlightCadMatches(cadPadId);

    public void FocusRegularMatches(int regularPadId) => Selection.Locator.FocusRegularMatches(regularPadId);

    public void HighlightRegularMatches(int regularPadId) => Selection.Locator.HighlightRegularMatches(regularPadId);

    public void HighlightCadPadGroup(IReadOnlyCollection<int> cadPadIds, string? statusPrefix = null) =>
        Selection.Locator.HighlightCadPadGroup(cadPadIds, statusPrefix);

    public void HighlightRegularPadGroup(IReadOnlyCollection<int> regularPadIds, string? statusPrefix = null) =>
        Selection.Locator.HighlightRegularPadGroup(regularPadIds, statusPrefix);

    private bool TryLocateSelection(
        IReadOnlyCollection<int> cadCandidateIds,
        IReadOnlyCollection<int> regularCandidateIds,
        bool focusSelection,
        string notVisibleStatus,
        Func<int, int, string> successStatusBuilder,
        double? minZoomOverride = null) =>
        Selection.Locator.TryLocateSelection(cadCandidateIds, regularCandidateIds, focusSelection,
            notVisibleStatus, successStatusBuilder, minZoomOverride);
}
