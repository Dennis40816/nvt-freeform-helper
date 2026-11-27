// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.UI.Presentation;

/// <summary>Formats generation evidence and final export rows; never runs allocation or rounding policy.</summary>
public static class NotchRowExplanationProjector
{
    public static NotchSourcePresentation Build(NotchV22RowExplanation explanation,
        IReadOnlyList<NotchTableRow> finalRows, NotchCompensationModel model, bool includesV21)
    {
        var rows = finalRows.Where(row => row.Version == NotchAlgorithmVersion.V22 &&
            row.CadPadId == explanation.SelectedCadPadId && row.IcIndex == explanation.IcIndex &&
            row.RegularPadIndex == explanation.SelectedRegularPadId)
            .OrderBy(static row => (row.Values[6] & 1) != 0).ToImmutableArray();
        var sourceDiff = rows.Length > 0 ? rows[0].Values[0] : explanation.SourceDiffIndex;
        var coverage = NotchV22TargetAllocationPolicy.UsesTargetRegularCoverage(model);
        var cells = explanation.Targets.OrderBy(target => target.DiffIndex != explanation.SourceDiffIndex)
            .ThenBy(static target => target.DiffIndex)
            .Select(target => BuildCell(explanation, rows, target, sourceDiff, coverage)).ToImmutableArray();
        var total = rows.Sum(static row => (row.Values[6] & 1) == 0
            ? row.Values[1] : row.Values[3] + row.Values[5]);
        var exact = cells.Sum(static cell => cell.ExactPercent);
        var difference = total - exact;
        var targetsByDiff = explanation.Targets.ToDictionary(static target => target.DiffIndex);
        var plainRounding = cells.Sum(static cell => cell.InRow ?? 0) == total &&
            explanation.Guard.All(static guard => guard.BeforePercent == guard.AfterPercent) &&
            cells.All(cell => cell.InRow.HasValue && cell.InRow.Value ==
                targetsByDiff[cell.IsSource ? explanation.SourceDiffIndex : cell.DiffIndex].RoundedPercent);
        var chip = Math.Abs(difference) < 1e-9 ? string.Empty :
            $"{(plainRounding ? "half to even" : "rounding and drops")}: {Signed(difference)}";
        var competition = explanation.Candidates.Length < 2 ? string.Empty :
            $"{explanation.Candidates.Length} CAD pads share diff {sourceDiff}. This one wins: " +
            NotchV22CandidateSelection.DecidingKey(explanation.Candidates);
        return new NotchSourcePresentation(explanation.IcIndex, sourceDiff, explanation.SelectedRegularPadId,
            competition, includesV21 ? "Output rows (V2.2)" : "Output rows",
            coverage ? "Why (% of each cell)" : "Why (share of source area)",
            model == NotchCompensationModel.ConservativeNoGain ? "To Full (support only)" :
                cells.Any(static cell => cell.AddedPercent > cell.CadPercent + 1e-9) ? "To Full added" : "To Full (≤ CAD)", coverage,
            rows.Select(row => BuildRow(row, explanation.NullValue)).ToImmutableArray(), cells, exact, total, chip,
            BuildFormula(explanation, rows, sourceDiff), explanation.PreviewPolygons)
        {
            NullValue = explanation.NullValue,
        };
    }

    private static NotchOutputRow BuildRow(NotchTableRow row, int nullValue)
    {
        var continuation = (row.Values[6] & 1) != 0;
        var values = row.Values.Select((value, index) => new NotchOutputValue(value,
            (continuation && index == 1) || ((index == 2 || index == 4) && value == nullValue) ||
            ((index == 3 || index == 5) && value == 0 && row.Values[index - 1] == nullValue))).ToImmutableArray();
        return new NotchOutputRow(continuation ? "CONT" : "MAIN", values);
    }

    private static NotchCellPresentation BuildCell(NotchV22RowExplanation explanation,
        ImmutableArray<NotchTableRow> rows, NotchV22TargetExplanation target, int sourceDiff, bool coverage)
    {
        var source = target.DiffIndex == explanation.SourceDiffIndex;
        var regulars = explanation.Regulars.Where(regular => regular.IcIndex == target.IcIndex &&
            regular.DiffIndex == target.DiffIndex).ToImmutableArray();
        var cad = regulars.Sum(static regular => regular.RegularArea > 0 ? regular.SourceArea / regular.RegularArea * 100 : 0);
        var exact = target.RatioBeforeRounding * 100;
        var inRow = ReadInRow(rows, target.DiffIndex, source);
        var added = exact - cad;
        var limit = !coverage || added <= 1e-9 ? string.Empty : DescribeLimit(regulars);
        return new NotchCellPresentation(source ? sourceDiff : target.DiffIndex, source, cad, added, exact, inRow,
            limit, inRow.HasValue ? string.Empty : DescribeDisposition(target.Disposition), coverage,
            regulars.Select(static regular => regular.RegularPadId).ToImmutableArray());
    }

    private static int? ReadInRow(ImmutableArray<NotchTableRow> rows, int diff, bool source)
    {
        if (source)
        {
            var main = rows.FirstOrDefault(static row => (row.Values[6] & 1) == 0);
            return main is null ? null : main.Values[1] - main.Values[3] - main.Values[5];
        }

        var legs = rows.SelectMany(static row => new[] { (Diff: row.Values[2], Percent: row.Values[3]),
            (Diff: row.Values[4], Percent: row.Values[5]) }).Where(leg => leg.Diff == diff && leg.Percent != 0).ToArray();
        return legs.Length == 0 ? null : legs.Sum(static leg => leg.Percent);
    }

    private static string DescribeLimit(ImmutableArray<NotchV22RegularExplanation> regulars)
    {
        if (regulars.Any(static regular => regular.AdditionLimit == NotchV22ToFullLimit.VirtualAreaCap))
        {
            return "capped";
        }

        var limited = regulars.Where(static regular => regular.AdditionLimit == NotchV22ToFullLimit.ReachableFreeArea).ToArray();
        return limited.Length == 0 ? string.Empty : "reach " + limited.Sum(static regular =>
            regular.RegularArea > 0 ? regular.ReachableArea / regular.RegularArea * 100 : 0).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string DescribeDisposition(NotchV22TargetDisposition disposition) => disposition switch
    {
        NotchV22TargetDisposition.BelowStrictThreshold => "below TH",
        NotchV22TargetDisposition.RoundedToZero => "rounds to 0",
        NotchV22TargetDisposition.GuardRoundedToZero => "guard rounded to 0",
        _ => "omitted by export profile",
    };

    private static ImmutableArray<string> BuildFormula(NotchV22RowExplanation explanation,
        ImmutableArray<NotchTableRow> rows, int sourceDiff)
    {
        var lines = ImmutableArray.CreateBuilder<string>();
        var main = rows.FirstOrDefault(static row => (row.Values[6] & 1) == 0);
        if (main is null)
        {
            return [];
        }

        var kept = main.Values[1] - main.Values[3] - main.Values[5];
        var subtraction = string.Concat(new[] { main.Values[3], main.Values[5] }.Where(static percent => percent != 0)
            .Select(static percent => $" − {percent}"));
        lines.Add($"diff {sourceDiff} keeps {kept} = {main.Values[1]}{subtraction}");
        var continuation = rows.Where(static row => (row.Values[6] & 1) != 0).ToArray();
        if (continuation.Length > 0)
        {
            lines.Add($"MAIN combine {main.Values[1]} = {explanation.Combine.AfterGuard} total − {explanation.Combine.ContinuationSubtraction} sent by CONT");
            lines.AddRange(continuation.SelectMany(static row => new[] { (row.Values[2], row.Values[3]), (row.Values[4], row.Values[5]) })
                .Where(static leg => leg.Item2 != 0).Select(static leg =>
                    $"CONT sends {leg.Item2} to diff {leg.Item1}: one row holds two targets"));
        }

        lines.AddRange(explanation.Guard.Where(static guard => guard.BeforePercent != guard.AfterPercent)
            .Select(static guard => FormattableString.Invariant(
                $"guard x{guard.Scale:0.00}: diff {guard.TargetDiffIndex} {guard.BeforePercent} → {guard.AfterPercent}")));
        return lines.ToImmutable();
    }

    private static string Signed(double value) => value.ToString("+0.00;−0.00;0.00", CultureInfo.InvariantCulture);
}
