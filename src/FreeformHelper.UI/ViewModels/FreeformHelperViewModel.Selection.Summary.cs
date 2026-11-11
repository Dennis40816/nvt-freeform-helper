using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels.Selection;
using SelectionSummaryModel = FreeformHelper.UI.Services.SelectionSummary;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private readonly Lazy<SelectionOwner<SelectionSummaryModel>> _selectionOwner;
    private SelectionOwner<SelectionSummaryModel> Selection => _selectionOwner.Value;

    private SelectionOwner<SelectionSummaryModel> CreateSelectionOwner()
    {
        return new SelectionOwner<SelectionSummaryModel>(
            new SelectionSummaryPorts<SelectionSummaryModel>(
                SelectionSummaryModel.Initial, SelectionSummaryUseCase.Compute, ProjectSelectionSummary,
                OnPropertyChanging, OnPropertyChanged),
            new SelectionInteractionPorts(
                () => _grid, () => RegularPads,
                (cad, regular) => _selectionCoordinator.ApplyCanvasSelection(cad, regular),
                CanProceedWithPendingEdits, () => _selectionCoordinator.ClearSelection(CanvasHost),
                () => _inspectorPanel.QuickFocusQueryText,
                value => _inspectorPanel = _inspectorPanel with { QuickFocusQueryText = value },
                NotifyDxfEditPanelActionStateChanged, () => RefreshNotchCanvasPreview(showStatus: false),
                ClearNotchCanvasPreview, LogSelectionTimings),
            new SelectionManualPorts(
                () => (ManualRowsRange, ManualColsRange),
                value => ManualRowsRange = value, value => ManualColsRange = value,
                () => (PendingColumnWidth, PendingRowHeight),
                value => PendingColumnWidth = value, value => PendingRowHeight = value,
                value => ManualRangeError = value,
                value => IsPendingColumnWidthMixed = value, value => IsPendingRowHeightMixed = value,
                () => _settingsSuppression.EnterManualSizingApply(), () => _undoSuppression.Enter(),
                UpdateManualRangeStatus),
            new SelectionInspectorPorts(
                () => _padInspectorRequests.NextSelectionRevision(), () => HasPadInspectorMultiSelection,
                CancelDeferredCadInspectorSnapshotRefresh, CancelDeferredRegularInspectorSnapshotRefresh,
                cadId => BuildCadPadInspectorSnapshot(cadId, includeExpensiveNotchDetails: false,
                    includeNotchRowEligibilityDetails: false, includeMatchDetails: false, includeRuleTrace: false),
                regularId => BuildRegularPadInspectorSnapshot(regularId, includeMatchDetails: false, includeRuleTrace: false),
                QueueDeferredCadInspectorSnapshotRefresh, QueueDeferredRegularInspectorSnapshotRefresh,
                snapshot => PadInspectorUi.Snapshot = snapshot),
            CreateSelectionLocator(), CreateSelectionPreviewRefreshOwner());
    }

    private static SelectionSummaryFields ProjectSelectionSummary(SelectionSummaryModel summary) => new(
        summary.CadSelectionText, summary.RegularSelectionText, summary.SelectionDetailText,
        summary.ManualRowsRange, summary.ManualColsRange, summary.ManualRangeError,
        summary.PendingWidth, summary.PendingHeight, summary.WidthMixed, summary.HeightMixed);

    /// <summary>Gets or sets the single accepted selection summary.</summary>
    public SelectionSummaryModel SelectionSummaryResult
    {
        get => Selection.SummaryResult;
        set => Selection.SummaryResult = value;
    }

    public string CadSelectionText => Selection.CadSelectionText;
    public string RegularSelectionText => Selection.RegularSelectionText;
    public string SelectionDetailText => Selection.SelectionDetailText;

    private void UpdateSelectedRangeSummary() => Selection.UpdateSelectedRangeSummary();

    private static string FormatIndexRanges(IEnumerable<int> indices)
    {
        var list = indices
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (list.Count == 0)
        {
            return "-";
        }

        var parts = new List<string>();
        var start = list[0];
        var prev = list[0];

        for (var i = 1; i < list.Count; i++)
        {
            var current = list[i];
            if (current == prev + 1)
            {
                prev = current;
                continue;
            }

            parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
            start = current;
            prev = current;
        }

        parts.Add(start == prev ? $"{start}" : $"{start}-{prev}");
        return string.Join(", ", parts);
    }
}
