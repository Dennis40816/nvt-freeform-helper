using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public readonly record struct NotchExportSelectionSummarySnapshot(
    int AllRowCount,
    int VisibleCount,
    int SelectedCount,
    int V22RowCount,
    int TransferRowCount,
    int WarningRowCount,
    int LegacyRowCount,
    int CadLinkedRowCount,
    int CadMissingRowCount,
    string SummaryText,
    string DistributionText,
    string ExportButtonText,
    bool HasRows,
    bool HasSelection);

public static class NotchExportSelectionSummaryProjector
{
    public static NotchExportSelectionSummarySnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows,
        NotchAlgorithmVersion? selectedVersion)
    {
        static bool MatchesVersion(NotchExportRowItemViewModel row, NotchAlgorithmVersion? version) =>
            !version.HasValue || row.Row.Version == version.Value;

        var scopedRows = allRows.Where(row => MatchesVersion(row, selectedVersion)).ToArray();
        var allRowCount = scopedRows.Length;
        var visibleCount = visibleRows.Count;
        var selectedCount = scopedRows.Count(static row => row.IsSelected);
        var v22RowCount = scopedRows.Count(static row => row.Row.Version == NotchAlgorithmVersion.V22);
        var transferRowCount = scopedRows.Count(static row => row.IsTransferRow);
        var warningRowCount = scopedRows.Count(static row => row.IsStatusWarning);
        var legacyRowCount = scopedRows.Count(static row => row.IsStatusLegacy);
        var cadLinkedRowCount = scopedRows.Count(static row => row.Row.CadPadId.HasValue);
        var cadMissingRowCount = allRowCount - cadLinkedRowCount;

        var summaryText = $"Selected {selectedCount}/{allRowCount} rows · shown {visibleCount}.";
        var distributionText =
            $"v2.2 {v22RowCount} · warning {warningRowCount} · legacy {legacyRowCount} · CAD linked {cadLinkedRowCount} · no CAD {cadMissingRowCount}";
        var hasSelection = selectedCount > 0;
        var exportButtonText = hasSelection ? $"Export {selectedCount} rows" : "Export selected";

        return new NotchExportSelectionSummarySnapshot(
            AllRowCount: allRowCount,
            VisibleCount: visibleCount,
            SelectedCount: selectedCount,
            V22RowCount: v22RowCount,
            TransferRowCount: transferRowCount,
            WarningRowCount: warningRowCount,
            LegacyRowCount: legacyRowCount,
            CadLinkedRowCount: cadLinkedRowCount,
            CadMissingRowCount: cadMissingRowCount,
            SummaryText: summaryText,
            DistributionText: distributionText,
            ExportButtonText: exportButtonText,
            HasRows: visibleCount > 0,
            HasSelection: hasSelection);
    }
}
