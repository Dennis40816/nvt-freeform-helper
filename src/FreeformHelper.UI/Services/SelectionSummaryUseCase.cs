using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Computes selection summaries and derived data for the UI from the current pad selection.
/// </summary>
public sealed class SelectionSummaryUseCase
{
    private const double SizeEpsilon = 1e-4;

    public static SelectionSummary Compute(
        RegularGrid? grid,
        IReadOnlyCollection<int> selectedCadIds,
        IReadOnlyCollection<int> selectedRegIndices)
    {
        var cadCount = selectedCadIds.Count;
        var regularCount = selectedRegIndices.Count;
        var totalCount = cadCount + regularCount;

        var selectionSummary = BuildSelectionSummary(cadCount, regularCount);
        var selectedGridRangeSummary = totalCount == 0 ? "No selection." : $"Selected: {totalCount} pads.";
        var cadSelectionText = $"CAD: {cadCount} pads.";
        var regularSelectionText = $"Regular: {regularCount} pads.";

        if (grid is null || regularCount == 0)
        {
            return new SelectionSummary(
                cadCount,
                regularCount,
                selectionSummary,
                selectedGridRangeSummary,
                cadSelectionText,
                regularSelectionText,
                string.Empty,
                false,
                null,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                false,
                0m,
                0m,
                false,
                false);
        }

        Dictionary<int, RegularPad>? fallbackPadMap = null;
        var pads = new List<RegularPad>(selectedRegIndices.Count);
        foreach (var index in selectedRegIndices)
        {
            var pad = ResolveRegularPadByIndex(grid, index, ref fallbackPadMap);
            if (pad is not null)
            {
                pads.Add(pad);
            }
        }

        if (pads.Count == 0)
        {
            return new SelectionSummary(
                cadCount,
                regularCount,
                selectionSummary,
                selectedGridRangeSummary,
                cadSelectionText,
                regularSelectionText,
                string.Empty,
                false,
                null,
                string.Empty,
                string.Empty,
                false,
                string.Empty,
                false,
                0m,
                0m,
                false,
                false);
        }

        var minR = pads.Min(p => p.Row);
        var maxR = pads.Max(p => p.Row);
        var minC = pads.Min(p => p.Col);
        var maxC = pads.Max(p => p.Col);
        var regularRange = (minR, maxR, minC, maxC);

        var rowIndices = pads.Select(p => p.Row).Distinct().ToList();
        var colIndices = pads.Select(p => p.Col).Distinct().ToList();
        var isRectangular = pads.Count == rowIndices.Count * colIndices.Count;

        var displayRows = pads
            .Select(p => ToDisplayRow(p.Row, grid.Rows))
            .ToList();
        var columns = pads
            .Select(p => p.Col)
            .ToList();

        var rowLabel = FormatIndexRanges(displayRows);
        var colLabel = FormatIndexRanges(columns);

        var widthValues = pads.Select(p => p.Bounds.Width).ToList();
        var heightValues = pads.Select(p => p.Bounds.Height).ToList();
        var widthMin = widthValues.Min();
        var widthMax = widthValues.Max();
        var heightMin = heightValues.Min();
        var heightMax = heightValues.Max();
        var widthMixed = (widthMax - widthMin) > SizeEpsilon;
        var heightMixed = (heightMax - heightMin) > SizeEpsilon;
        var widthValue = widthMixed ? 0m : (decimal)((widthMin + widthMax) * 0.5);
        var heightValue = heightMixed ? 0m : (decimal)((heightMin + heightMax) * 0.5);

        var detailText = BuildSelectionDetailText(cadCount, regularCount, rowLabel, colLabel);
        var hasDetail = !string.IsNullOrWhiteSpace(detailText);

        if (isRectangular)
        {
            regularSelectionText = $"Regular: Rows {rowLabel}, Cols {colLabel} ({regularCount})";
            return new SelectionSummary(
                cadCount,
                regularCount,
                selectionSummary,
                selectedGridRangeSummary,
                cadSelectionText,
                regularSelectionText,
                detailText,
                hasDetail,
                regularRange,
                rowLabel,
                colLabel,
                true,
                string.Empty,
                false,
                widthValue,
                heightValue,
                widthMixed,
                heightMixed);
        }

        regularSelectionText = $"Regular: {regularCount} pads (non-rectangular)";
        return new SelectionSummary(
            cadCount,
            regularCount,
            selectionSummary,
            selectedGridRangeSummary,
            cadSelectionText,
            regularSelectionText,
            detailText,
            hasDetail,
            regularRange,
            string.Empty,
            string.Empty,
            false,
            "Selection is not a full rectangle; rows/cols range not applied.",
            true,
            widthValue,
            heightValue,
            widthMixed,
            heightMixed);
    }

    private static string BuildSelectionSummary(int cadCount, int regularCount)
    {
        if (cadCount == 0 && regularCount == 0) return "No selection.";
        if (cadCount > 0 && regularCount == 0) return $"Selected: CAD={cadCount}.";
        if (cadCount == 0 && regularCount > 0) return $"Selected: Regular={regularCount}.";
        return $"Selected: CAD={cadCount}, Regular={regularCount}.";
    }

    private static string BuildSelectionDetailText(int cadCount, int regularCount, string rowLabel, string colLabel)
    {
        var parts = new List<string>();
        if (cadCount > 0)
        {
            parts.Add($"CAD: {cadCount} pads");
        }

        if (regularCount > 0 && !string.IsNullOrWhiteSpace(rowLabel) && !string.IsNullOrWhiteSpace(colLabel))
        {
            parts.Add($"Regular: Rows {rowLabel}, Cols {colLabel} ({regularCount})");
        }

        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
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

    private static int ToDisplayRow(int actualRow, int totalRows)
    {
        if (totalRows <= 0)
        {
            return actualRow;
        }

        return Math.Clamp((totalRows - 1) - actualRow, 0, totalRows - 1);
    }

    private static RegularPad? ResolveRegularPadByIndex(
        RegularGrid grid,
        int index,
        ref Dictionary<int, RegularPad>? fallbackPadMap)
    {
        if ((uint)index < (uint)grid.Pads.Count)
        {
            var candidate = grid.Pads[index];
            if (candidate.Index == index)
            {
                return candidate;
            }
        }

        fallbackPadMap ??= grid.Pads.ToDictionary(static pad => pad.Index);
        return fallbackPadMap.TryGetValue(index, out var resolved) ? resolved : null;
    }
}

public sealed record SelectionSummary(
    int CadCount,
    int RegularCount,
    string SelectionSummaryText,
    string SelectedGridRangeSummary,
    string CadSelectionText,
    string RegularSelectionText,
    string SelectionDetailText,
    bool HasSelectionDetail,
    (int minR, int maxR, int minC, int maxC)? RegularRange,
    string ManualRowsRange,
    string ManualColsRange,
    bool IsRectangular,
    string ManualRangeError,
    bool HasManualRangeError,
    decimal PendingWidth,
    decimal PendingHeight,
    bool WidthMixed,
    bool HeightMixed);
