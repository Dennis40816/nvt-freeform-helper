using CommunityToolkit.Mvvm.ComponentModel;
using FreeformHelper.UI.Services;
using SelectionSummaryModel = FreeformHelper.UI.Services.SelectionSummary;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    /// <summary>
    /// Gets or sets the result of the last selection summary computation.
    /// The text properties below read from this one result.
    /// </summary>
    [ObservableProperty]
    private SelectionSummaryModel _selectionSummaryResult = SelectionSummaryModel.Initial;

    /// <summary>
    /// Gets a concise summary of the current selection (e.g., "Selected: CAD=5, Regular=12").
    /// </summary>
    public string SelectionSummary => SelectionSummaryResult.SelectionSummaryText;

    /// <summary>
    /// Gets a descriptive summary of the currently selected regular grid range.
    /// </summary>
    public string SelectedGridRangeSummary => SelectionSummaryResult.SelectedGridRangeSummary;

    /// <summary>
    /// Gets a string indicating the count of selected CAD pads.
    /// </summary>
    public string CadSelectionText => SelectionSummaryResult.CadSelectionText;

    /// <summary>
    /// Gets a string indicating the count of selected regular pads.
    /// </summary>
    public string RegularSelectionText => SelectionSummaryResult.RegularSelectionText;

    /// <summary>
    /// Gets a detailed text description of the current selection, including row/col ranges.
    /// </summary>
    public string SelectionDetailText => SelectionSummaryResult.SelectionDetailText;

    private void UpdateSelectedRangeSummary()
    {
        var summary = SelectionSummaryUseCase.Compute(_grid, _selectedCadIds, _selection.RegularIndices);

        SelectionSummaryResult = summary;
        SetManualRangeInputs(summary.ManualRowsRange, summary.ManualColsRange);
        ManualRangeError = summary.ManualRangeError;
        SetManualSizeInputs(summary.PendingWidth, summary.PendingHeight);
        IsPendingColumnWidthMixed = summary.WidthMixed;
        IsPendingRowHeightMixed = summary.HeightMixed;
        SetManualSizeDisplay(summary.PendingWidth, summary.PendingHeight, summary.WidthMixed, summary.HeightMixed);
        OnPropertyChanged(nameof(SelectedRegularRange));
    }

    partial void OnSelectionSummaryResultChanged(SelectionSummaryModel? oldValue, SelectionSummaryModel newValue)
    {
        ArgumentNullException.ThrowIfNull(oldValue);
        RaiseWhenChanged(oldValue.SelectionSummaryText, newValue.SelectionSummaryText, nameof(SelectionSummary));
        RaiseWhenChanged(oldValue.SelectedGridRangeSummary, newValue.SelectedGridRangeSummary, nameof(SelectedGridRangeSummary));
        RaiseWhenChanged(oldValue.CadSelectionText, newValue.CadSelectionText, nameof(CadSelectionText));
        RaiseWhenChanged(oldValue.RegularSelectionText, newValue.RegularSelectionText, nameof(RegularSelectionText));
        RaiseWhenChanged(oldValue.SelectionDetailText, newValue.SelectionDetailText, nameof(SelectionDetailText));
    }

    private void SetManualRangeInputs(string rows, string cols)
    {
        if (ManualRowsRange == rows && ManualColsRange == cols)
        {
            return;
        }

        using (_settingsSuppression.EnterManualSizingApply())
        {
            using (_undoSuppression.Enter())
            {
                ManualRowsRange = rows;
                ManualColsRange = cols;
            }
        }
        UpdateManualRangeStatus();
    }

    private void SetManualSizeInputs(decimal width, decimal height)
    {
        if (PendingColumnWidth == width && PendingRowHeight == height)
        {
            return;
        }

        using (_settingsSuppression.EnterManualSizingApply())
        {
            using (_undoSuppression.Enter())
            {
                PendingColumnWidth = width;
                PendingRowHeight = height;
            }
        }
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
