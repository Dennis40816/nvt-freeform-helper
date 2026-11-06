using System.Globalization;
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
    bool HasSelection,
    int HiddenSelectedRowCount)
{
    public bool HasHiddenSelectedRows => HiddenSelectedRowCount > 0;

    public string HiddenSelectedRowsHintText => HiddenSelectedRowCount switch
    {
        0 => string.Empty,
        1 => "1 row hidden but still exported",
        _ => $"{HiddenSelectedRowCount.ToString(CultureInfo.InvariantCulture)} rows hidden but still exported",
    };
}

public static class NotchExportSelectionSummaryProjector
{
    public static NotchExportSelectionSummarySnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> allRows,
        IReadOnlyList<NotchExportRowItemViewModel> visibleRows,
        NotchAlgorithmVersion? selectedVersion)
    {
        var scopedRows = allRows
            .Where(row => NotchExportSelectionProjectionBuilder.MatchesSelectedVersion(row, selectedVersion))
            .ToArray();
        var selectedRows = scopedRows
            .Where(row => NotchExportSelectionProjectionBuilder.IsSelectedForExport(row, selectedVersion))
            .ToArray();
        var visibleRowSet = visibleRows.ToHashSet();
        var hiddenSelectedRowCount = selectedRows.Count(row => !visibleRowSet.Contains(row));
        var allRowCount = scopedRows.Length;
        var visibleCount = visibleRows.Count;
        var selectedCount = selectedRows.Length;
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
            HasSelection: hasSelection,
            HiddenSelectedRowCount: hiddenSelectedRowCount);
    }
}
