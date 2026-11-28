// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class NotchExportSelectionViewModel
{
    public string FooterStatusText
    {
        get
        {
            var counts = string.Create(
                CultureInfo.InvariantCulture,
                $"{SelectedCount} selected · {_summarySnapshot.VisibleCount} shown");
            return HasHiddenSelectedRows ? $"{counts} · {HiddenSelectedRowsHintText}" : counts;
        }
    }

    public string LinkedRowsTitleText =>
        string.Create(CultureInfo.InvariantCulture, $"Linked rows ({WorkspaceLinkedRows.Count})");

    public string PassedChecksTitleText => string.Create(
        CultureInfo.InvariantCulture,
        $"Passed checks ({(IsSimulationSafetyClean ? 1 : 0) + (HasNoToFullCoverageWarning ? 1 : 0)})");

    public string VersionHintText => IsVersionSelectionPinnedByExportType
        ? "Version is set by Export format."
        : "Single-version choice is saved with the next Save Project.";

    public string LinkedFilterText => $"Linked {CadLinkedRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string TransferFilterText => $"Transfer {TransferRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string NoOpFilterText => $"No-op {WarningRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string LegacyFilterText => $"Legacy {LegacyRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string NoCadFilterText => $"No CAD {CadMissingRowCount.ToString(CultureInfo.InvariantCulture)}";
    public string InspectorHeaderText => SelectedRow is null ? string.Empty : $"Row {SelectedRow.RowNumberText}";
    public bool NeedsSimulationCheckAttention => !IsSimulationSafetyClean;
    public bool HasNoSimulationPhysicalAuditWarning => !HasSimulationPhysicalAuditWarning;
    public bool HasNoToFullCoverageWarning => !HasToFullCoverageWarning;
    public bool HasPassedExportChecks => IsSimulationSafetyClean || HasNoToFullCoverageWarning;
    public bool HasNoRows => !HasRows;
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
        OnPropertyChanged(nameof(FooterStatusText));
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
    }
}
