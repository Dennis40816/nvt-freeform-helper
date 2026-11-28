// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using System.Globalization;
using FreeformHelper.Domain.Geometry;

namespace FreeformHelper.UI.Presentation;

public sealed record NotchOutputValue(int Value, bool IsMuted)
{
    public string Text => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record NotchOutputRow(string Label, ImmutableArray<NotchOutputValue> Values);

public sealed record NotchCellPresentation(int DiffIndex, bool IsSource, double CadPercent, double AddedPercent,
    double ExactPercent, int? InRow, string LimitText, string OmissionReason, bool ShowCoverage,
    ImmutableArray<int> RegularPadIds)
{
    public string CellText => $"diff {DiffIndex}";
    public string SourceText => IsSource ? "source" : string.Empty;
    public string CadText => CadPercent.ToString("F2", CultureInfo.InvariantCulture);
    public string AddedText => AddedPercent.ToString("+0.00;−0.00;0.00", CultureInfo.InvariantCulture);
    public string ExactText => ExactPercent.ToString("F2", CultureInfo.InvariantCulture);
    public string InRowText => InRow?.ToString(CultureInfo.InvariantCulture) ?? "—";
    public string PreviewText => IsSource ? $"keep {InRowText}" : InRowText;
}

public sealed record NotchSourcePresentation(int IcIndex, int SourceDiffIndex, int SourceRegularPadId,
    string CompetitionText, string RowsTitle, string WhyTitle, string AddedTitle, bool ShowCoverage,
    ImmutableArray<NotchOutputRow> Rows, ImmutableArray<NotchCellPresentation> Cells,
    double ExactTotal, int RowTotal, string DifferenceText, ImmutableArray<string> FormulaLines,
    ImmutableArray<Polygon2> PreviewPolygons)
{
    public int NullValue { get; init; } = 65535;
    public string Footnote => $"grey: not applied · {NullValue}: no target · flags 1: continuation row";
    public string SelectorText => $"IC {IcIndex + 1} · source diff {SourceDiffIndex}";
    public string ShareLegend => $"share sent from diff {SourceDiffIndex}";
    public string ExactTotalText => ExactTotal.ToString("F2", CultureInfo.InvariantCulture);
    public string RowTotalText => RowTotal.ToString(CultureInfo.InvariantCulture);
    public bool HasDifference => DifferenceText.Length > 0;
    public bool HasCompetition => CompetitionText.Length > 0;
}
