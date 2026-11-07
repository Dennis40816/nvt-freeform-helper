// Copyright (c) 2026 Dennis Liu. All rights reserved.

namespace FreeformHelper.UI.ViewModels;

public sealed partial class DxfEditChangeListViewModel
{
    public string HiddenCategoryText => $"{GetKindDisplayText(DxfEditChangeKind.Hidden)} {HiddenCount}";
    public string DuplicateCategoryText => $"{GetKindDisplayText(DxfEditChangeKind.Duplicate)} {DuplicateCount}";
    public string CombinedCategoryText => $"{GetKindDisplayText(DxfEditChangeKind.Combined)} {CombinedCount}";
    public string MovedCategoryText => $"{GetKindDisplayText(DxfEditChangeKind.Moved)} {MovedCount}";
    public string RotatedCategoryText => $"{GetKindDisplayText(DxfEditChangeKind.Rotated)} {RotatedCount}";
    public bool HasSelectedEntry => SelectedEntry is not null;
    public int BulkActionCount => GetApplicableSelectedRows().Count;
    public int RestoreSelectedDuplicateCount => GetApplicableSelectedRows(IsRestorableDuplicate).Count;
    public int RehideSelectedDuplicateCount => GetApplicableSelectedRows(IsRehidableDuplicate).Count;
    public string BulkActionCountText => $"{BulkActionText} ({BulkActionCount})";
    public string RestoreSelectedDuplicateCountText => $"Restore selected ({RestoreSelectedDuplicateCount})";
    public string RehideSelectedDuplicateCountText => $"Hide selected ({RehideSelectedDuplicateCount})";
    public string BulkActionToolTip => FormatBulkActionToolTip(BulkActionCount, "cannot be processed");
    public string RestoreSelectedDuplicateToolTip => FormatDuplicateActionToolTip(IsRestorableDuplicate, "already restored");
    public string RehideSelectedDuplicateToolTip => FormatDuplicateActionToolTip(IsRehidableDuplicate, "already hidden");

    private string FormatBulkActionToolTip(int processedCount, string reason)
    {
        var skippedCount = SelectedVisibleCount - processedCount;
        return skippedCount == 0
            ? "Bulk action follows current tab scope."
            : $"{skippedCount} checked row(s) {reason}. Only applicable rows in this category are processed.";
    }

    private string FormatDuplicateActionToolTip(Func<DxfEditChangeListEntry, bool> predicate, string reason)
    {
        var selectedRows = GetSelectedVisibleRows();
        var skippedCount = selectedRows.Count(row => !predicate(row));
        var unavailableCount = selectedRows.Count(row => predicate(row) && !row.CanApply);
        if (skippedCount == 0)
        {
            return FormatBulkActionToolTip(selectedRows.Count - unavailableCount, "cannot be processed");
        }

        var unavailableText = unavailableCount == 0 ? string.Empty : $" {unavailableCount} checked row(s) cannot be processed.";
        return $"{skippedCount} checked row(s) are {reason}.{unavailableText} Only applicable rows in this category are processed.";
    }

    private void NotifyPresentationChanged()
    {
        OnPropertyChanged(nameof(HiddenCategoryText));
        OnPropertyChanged(nameof(DuplicateCategoryText));
        OnPropertyChanged(nameof(CombinedCategoryText));
        OnPropertyChanged(nameof(MovedCategoryText));
        OnPropertyChanged(nameof(RotatedCategoryText));
        OnPropertyChanged(nameof(BulkActionCount));
        OnPropertyChanged(nameof(RestoreSelectedDuplicateCount));
        OnPropertyChanged(nameof(RehideSelectedDuplicateCount));
        OnPropertyChanged(nameof(BulkActionCountText));
        OnPropertyChanged(nameof(RestoreSelectedDuplicateCountText));
        OnPropertyChanged(nameof(RehideSelectedDuplicateCountText));
        OnPropertyChanged(nameof(BulkActionToolTip));
        OnPropertyChanged(nameof(RestoreSelectedDuplicateToolTip));
        OnPropertyChanged(nameof(RehideSelectedDuplicateToolTip));
    }
}
