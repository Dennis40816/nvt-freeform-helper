using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    private void UpdateSelectedRangeSummary()
    {
        var summary = SelectionSummaryUseCase.Compute(_grid, _selectedCadIds, _selectedRegIndices);

        _selectedRegRange = summary.RegularRange;
        SelectionSummary = summary.SelectionSummaryText;
        SelectedGridRangeSummary = summary.SelectedGridRangeSummary;
        CadSelectionText = summary.CadSelectionText;
        RegularSelectionText = summary.RegularSelectionText;
        SelectionDetailText = summary.SelectionDetailText;
        HasSelectionDetail = summary.HasSelectionDetail;
        SetManualRangeInputs(summary.ManualRowsRange, summary.ManualColsRange);
        ManualRangeError = summary.ManualRangeError;
        HasManualRangeError = summary.HasManualRangeError;
        SetManualSizeInputs(summary.PendingWidth, summary.PendingHeight);
        IsPendingColumnWidthMixed = summary.WidthMixed;
        IsPendingRowHeightMixed = summary.HeightMixed;
        SetManualSizeDisplay(summary.PendingWidth, summary.PendingHeight, summary.WidthMixed, summary.HeightMixed);
        OnPropertyChanged(nameof(SelectedRegularRange));
    }

    private void SetManualRangeInputs(string rows, string cols)
    {
        if (ManualRowsRange == rows && ManualColsRange == cols)
        {
            return;
        }

        _suppressManualSizingApply = true;
        _suppressUndo = true;
        ManualRowsRange = rows;
        ManualColsRange = cols;
        _suppressUndo = false;
        _suppressManualSizingApply = false;
        UpdateManualRangeStatus();
    }

    private void SetManualSizeInputs(decimal width, decimal height)
    {
        if (PendingColumnWidth == width && PendingRowHeight == height)
        {
            return;
        }

        _suppressManualSizingApply = true;
        _suppressUndo = true;
        PendingColumnWidth = width;
        PendingRowHeight = height;
        _suppressUndo = false;
        _suppressManualSizingApply = false;
        ClearPendingMixedFlags();
    }

    private void ClearPendingMixedFlags()
    {
        IsPendingColumnWidthMixed = false;
        IsPendingRowHeightMixed = false;
    }

    private void SetManualSizeDisplay(decimal width, decimal height, bool widthMixed, bool heightMixed)
    {
        PendingColumnWidthDisplay = widthMixed
            ? "*"
            : width.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        PendingRowHeightDisplay = heightMixed
            ? "*"
            : height.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
    }

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
