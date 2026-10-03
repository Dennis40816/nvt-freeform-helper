using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Builds regular-centric notch validation report from the latest generated table.
/// </summary>
public sealed class NotchValidationUseCase
{
    public static NotchValidationBucket BuildBucket(
        NotchTable table,
        int nullDiffValue)
    {
        ArgumentNullException.ThrowIfNull(table);

        var indexedRows = table.Rows
            .Select((row, index) => new IndexedNotchRow(index + 1, row))
            .ToList();

        var directRowsByRegularPadId = new Dictionary<int, List<IndexedNotchRow>>();
        var incomingRowsByIcDiff = new Dictionary<(int IcIndex, int DiffIndex), List<NotchValidationFlowRow>>();
        var outgoingRowsByRegularPadId = new Dictionary<int, List<NotchValidationFlowRow>>();

        foreach (var item in indexedRows)
        {
            var row = item.Row;
            if (!directRowsByRegularPadId.TryGetValue(row.RegularPadIndex, out var directRows))
            {
                directRows = new List<IndexedNotchRow>();
                directRowsByRegularPadId[row.RegularPadIndex] = directRows;
            }

            directRows.Add(item);

            if (row.Version != NotchAlgorithmVersion.V22 || !row.CadPadId.HasValue)
            {
                continue;
            }

            var cadId = row.CadPadId!.Value;
            foreach (var leg in DecodeV22Legs(row, nullDiffValue))
            {
                if (leg.RatioPercent == 0)
                {
                    continue;
                }

                var flow = new NotchValidationFlowRow(
                    SourceRegularPadId: row.RegularPadIndex,
                    SourceCadPadId: cadId,
                    SourceDiffIndex: row.DiffIndex,
                    TargetDiffIndex: leg.TargetDiffIndex,
                    TargetIcIndex: row.IcIndex,
                    RatioPercent: leg.RatioPercent,
                    EffectiveArea: double.NaN,
                    SourceRow: item);

                var incomingKey = (row.IcIndex, leg.TargetDiffIndex);
                if (!incomingRowsByIcDiff.TryGetValue(incomingKey, out var incomingRows))
                {
                    incomingRows = new List<NotchValidationFlowRow>();
                    incomingRowsByIcDiff[incomingKey] = incomingRows;
                }

                incomingRows.Add(flow);

                if (!outgoingRowsByRegularPadId.TryGetValue(row.RegularPadIndex, out var outgoingRows))
                {
                    outgoingRows = new List<NotchValidationFlowRow>();
                    outgoingRowsByRegularPadId[row.RegularPadIndex] = outgoingRows;
                }

                outgoingRows.Add(flow);
            }
        }

        foreach (var rows in directRowsByRegularPadId.Values)
        {
            rows.Sort(static (left, right) => left.RowNumber.CompareTo(right.RowNumber));
        }

        foreach (var rows in incomingRowsByIcDiff.Values)
        {
            rows.Sort(static (left, right) =>
            {
                var cmp = Math.Abs(right.RatioPercent).CompareTo(Math.Abs(left.RatioPercent));
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = left.SourceRegularPadId.CompareTo(right.SourceRegularPadId);
                if (cmp != 0)
                {
                    return cmp;
                }

                return left.SourceCadPadId.CompareTo(right.SourceCadPadId);
            });
        }

        foreach (var rows in outgoingRowsByRegularPadId.Values)
        {
            rows.Sort(static (left, right) =>
            {
                var cmp = Math.Abs(right.RatioPercent).CompareTo(Math.Abs(left.RatioPercent));
                if (cmp != 0)
                {
                    return cmp;
                }

                return left.TargetDiffIndex.CompareTo(right.TargetDiffIndex);
            });
        }

        return new NotchValidationBucket(directRowsByRegularPadId, incomingRowsByIcDiff, outgoingRowsByRegularPadId);
    }

    private static List<DecodedV22Leg> DecodeV22Legs(NotchTableRow row, int nullDiffValue)
    {
        if (row.V22Node is not null)
        {
            var typed = row.V22Node;
            return BuildLegs(
                typed.TargetDiffIndex1,
                typed.TargetRatioPercent1,
                typed.TargetDiffIndex2,
                typed.TargetRatioPercent2,
                nullDiffValue);
        }

        return DecodeV22Legs(row.Values, nullDiffValue);
    }

    private static List<DecodedV22Leg> DecodeV22Legs(int[]? values, int nullDiffValue)
    {
        if (values is null || values.Length < 7)
        {
            return new List<DecodedV22Leg>(0);
        }

        return BuildLegs(values[2], values[3], values[4], values[5], nullDiffValue);
    }

    private static List<DecodedV22Leg> BuildLegs(
        int targetDiff1,
        int ratioPercent1,
        int targetDiff2,
        int ratioPercent2,
        int nullDiffValue)
    {
        var result = new List<DecodedV22Leg>(2);
        AppendLeg(targetDiff1, ratioPercent1);
        AppendLeg(targetDiff2, ratioPercent2);
        return result;

        void AppendLeg(int targetDiff, int ratioPercent)
        {
            if (targetDiff == nullDiffValue)
            {
                return;
            }

            result.Add(new DecodedV22Leg(
                TargetDiffIndex: targetDiff,
                RatioPercent: Math.Clamp(ratioPercent, -100, 100)));
        }
    }

    public static NotchValidationReport BuildRegularReportFromBucket(
        int regularPadId,
        RegularPad regularPad,
        NotchValidationBucket bucket)
    {
        ArgumentNullException.ThrowIfNull(regularPad);
        ArgumentNullException.ThrowIfNull(bucket);

        IReadOnlyList<IndexedNotchRow> directRows = bucket.DirectRowsByRegularPadId.TryGetValue(regularPadId, out var directRowsForRegular)
            ? directRowsForRegular
            : Array.Empty<IndexedNotchRow>();

        IReadOnlyList<NotchValidationFlowRow> incomingRows = bucket.IncomingRowsByIcDiff.TryGetValue((regularPad.IcIndex, regularPad.DiffIndex), out var incomingRowsForDiff)
            ? incomingRowsForDiff.Where(row => row.SourceRegularPadId != regularPadId).ToList()
            : new List<NotchValidationFlowRow>();

        IReadOnlyList<NotchValidationFlowRow> outgoingRows = bucket.OutgoingRowsByRegularPadId.TryGetValue(regularPadId, out var outgoingRowsForRegular)
            ? outgoingRowsForRegular
            : Array.Empty<NotchValidationFlowRow>();

        return new NotchValidationReport(
            RegularPadId: regularPadId,
            IcIndex: regularPad.IcIndex,
            DiffIndex: regularPad.DiffIndex,
            DirectRows: directRows,
            IncomingRows: incomingRows,
            OutgoingRows: outgoingRows);
    }

    public sealed record IndexedNotchRow(int RowNumber, NotchTableRow Row);

    private sealed record DecodedV22Leg(int TargetDiffIndex, int RatioPercent);
}

public sealed record NotchValidationBucket(
    IReadOnlyDictionary<int, List<NotchValidationUseCase.IndexedNotchRow>> DirectRowsByRegularPadId,
    IReadOnlyDictionary<(int IcIndex, int DiffIndex), List<NotchValidationFlowRow>> IncomingRowsByIcDiff,
    IReadOnlyDictionary<int, List<NotchValidationFlowRow>> OutgoingRowsByRegularPadId);

public sealed record NotchValidationReport(
    int RegularPadId,
    int IcIndex,
    int DiffIndex,
    IReadOnlyList<NotchValidationUseCase.IndexedNotchRow> DirectRows,
    IReadOnlyList<NotchValidationFlowRow> IncomingRows,
    IReadOnlyList<NotchValidationFlowRow> OutgoingRows);

public sealed record NotchValidationFlowRow(
    int SourceRegularPadId,
    int SourceCadPadId,
    int SourceDiffIndex,
    int TargetDiffIndex,
    int TargetIcIndex,
    double RatioPercent,
    double EffectiveArea,
    NotchValidationUseCase.IndexedNotchRow SourceRow);
