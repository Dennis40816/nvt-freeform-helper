// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel
{
    public string ExportSummaryText => $"Export {SelectedCount.ToString(CultureInfo.InvariantCulture)} rows";
    public string LinkedFilterText => $"Linked {CadLinkedRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string TransferFilterText => $"Transfer {TransferRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string NoOpFilterText => $"No-op {WarningRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string LegacyFilterText => $"Legacy {LegacyRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string NoCadFilterText => $"No CAD {CadMissingRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string InspectorHeaderText => SelectedRow is null ? string.Empty : $"Row {SelectedRow.RowNumberText}";
    public bool IsSelectedRowLegacy => SelectedRow?.IsStatusLegacy == true;
    public bool NeedsSimulationCheckAttention => !IsSimulationSafetyClean;
    public bool HasNoSimulationPhysicalAuditWarning => !HasSimulationPhysicalAuditWarning;
    public bool HasNoToFullCoverageWarning => !HasToFullCoverageWarning;
    public bool HasPassedExportChecks => IsSimulationSafetyClean || HasNoToFullCoverageWarning;
    public bool HasNoRows => !HasRows;
    public string SelectedRowAnalysisText => SelectedRow is null
        ? string.Empty
        : $"Analysis: target ratio sum {SelectedRowAnalysisRatioSumText}";

    public bool? VisibleRowsSelection
    {
        get => AreAllVisibleRowsSelected ? true : Rows.Any(static row => row.IsSelected) ? null : false;
        set
        {
            if (value.HasValue)
            {
                AreAllVisibleRowsSelected = VisibleRowsSelection is null || value.Value;
            }
        }
    }

    private void NotifyDisplayCountsChanged()
    {
        OnPropertyChanged(nameof(ExportSummaryText));
        OnPropertyChanged(nameof(LinkedFilterText));
        OnPropertyChanged(nameof(TransferFilterText));
        OnPropertyChanged(nameof(NoOpFilterText));
        OnPropertyChanged(nameof(LegacyFilterText));
        OnPropertyChanged(nameof(NoCadFilterText));
        OnPropertyChanged(nameof(HasNoRows));
        OnPropertyChanged(nameof(VisibleRowsSelection));
    }

    private void NotifyInspectorDisplayChanged()
    {
        OnPropertyChanged(nameof(InspectorHeaderText));
        OnPropertyChanged(nameof(IsSelectedRowLegacy));
        OnPropertyChanged(nameof(SelectedRowAnalysisText));
    }
}
