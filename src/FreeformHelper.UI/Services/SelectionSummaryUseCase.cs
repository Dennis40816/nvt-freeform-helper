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

        // The cases below only change what they know more about than this summary of a selection without regular pads.
        var emptySummary = BuildEmptySummary(cadCount, regularCount);

        if (grid is null || regularCount == 0)
        {
            return emptySummary;
        }

        var pads = ResolveSelectedPads(grid, selectedRegIndices);
        if (pads.Count == 0)
        {
            return emptySummary;
        }

        var regularRange = (
            minR: pads.Min(p => p.Row),
            maxR: pads.Max(p => p.Row),
            minC: pads.Min(p => p.Col),
            maxC: pads.Max(p => p.Col));

        var rowIndices = pads.Select(p => p.Row).Distinct().ToList();
        var colIndices = pads.Select(p => p.Col).Distinct().ToList();
        var isRectangular = pads.Count == rowIndices.Count * colIndices.Count;

        var rowLabel = FormatIndexRanges(pads.Select(p => ToDisplayRow(p.Row, grid.Rows)));
        var colLabel = FormatIndexRanges(pads.Select(p => p.Col));

        var (widthValue, widthMixed) = MeasurePendingSize(pads.Select(p => p.Bounds.Width));
        var (heightValue, heightMixed) = MeasurePendingSize(pads.Select(p => p.Bounds.Height));

        var selectedSummary = emptySummary with
        {
            SelectionDetailText = BuildSelectionDetailText(cadCount, regularCount, rowLabel, colLabel),
            RegularRange = regularRange,
            PendingWidth = widthValue,
            PendingHeight = heightValue,
            WidthMixed = widthMixed,
            HeightMixed = heightMixed,
        };

        if (isRectangular)
        {
            return selectedSummary with
            {
                RegularSelectionText = $"Regular: Rows {rowLabel}, Cols {colLabel} ({regularCount})",
                ManualRowsRange = rowLabel,
                ManualColsRange = colLabel,
                IsRectangular = true,
            };
        }

        return selectedSummary with
        {
            RegularSelectionText = $"Regular: {regularCount} pads (non-rectangular)",
            ManualRangeError = "Selection is not a full rectangle; rows/cols range not applied.",
        };
    }

    private static SelectionSummary BuildEmptySummary(int cadCount, int regularCount)
    {
        var totalCount = cadCount + regularCount;
        return new SelectionSummary(
            cadCount,
            regularCount,
            BuildSelectionSummary(cadCount, regularCount),
            totalCount == 0 ? "No selection." : $"Selected: {totalCount} pads.",
            $"CAD: {cadCount} pads.",
            $"Regular: {regularCount} pads.",
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            false,
            string.Empty,
            0m,
            0m,
            false,
            false);
    }

    private static List<RegularPad> ResolveSelectedPads(RegularGrid grid, IReadOnlyCollection<int> selectedRegIndices)
    {
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

        return pads;
    }

    // The size shown for a selection: the middle of the smallest and largest value, or "mixed" with value 0
    // when they differ by more than SizeEpsilon.
    private static (decimal Value, bool Mixed) MeasurePendingSize(IEnumerable<double> sizes)
    {
        var sizeList = sizes.ToList();
        var min = sizeList.Min();
        var max = sizeList.Max();
        var mixed = (max - min) > SizeEpsilon;
        return (mixed ? 0m : (decimal)((min + max) * 0.5), mixed);
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
    (int minR, int maxR, int minC, int maxC)? RegularRange,
    string ManualRowsRange,
    string ManualColsRange,
    bool IsRectangular,
    string ManualRangeError,
    decimal PendingWidth,
    decimal PendingHeight,
    bool WidthMixed,
    bool HeightMixed)
{
    /// <summary>
    /// Gets the summary shown before the first selection update.
    /// It differs from the computed summary of an empty selection in the grid range text.
    /// </summary>
    public static SelectionSummary Initial { get; } = new(
        0,
        0,
        "No selection.",
        "Select regular pads to edit size.",
        "CAD: 0 pads.",
        "Regular: 0 pads.",
        string.Empty,
        null,
        string.Empty,
        string.Empty,
        false,
        string.Empty,
        0m,
        0m,
        false,
        false);
}
