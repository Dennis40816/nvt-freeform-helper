using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

internal static class NotchV21FirmwareProjector
{
    private const int LegacyRowWidth = 9;

    internal static NotchV21FirmwareProjection Project(
        IReadOnlyList<NotchTableRow> rows,
        int nullValue,
        int icCount,
        NotchComputationMode computationMode)
    {
        ArgumentNullException.ThrowIfNull(rows);
        NotchSettings.ValidateNullValueOrThrow(nullValue);

        return computationMode == NotchComputationMode.CadAllocation
            ? ProjectCadAllocation(rows, nullValue, icCount)
            : ProjectLegacy(rows, icCount);
    }

    private static NotchV21FirmwareProjection ProjectLegacy(
        IReadOnlyList<NotchTableRow> rows,
        int icCount)
    {
        var nodesByIc = CreateNodeBuckets(icCount);
        foreach (var row in rows)
        {
            if (row.Version != NotchAlgorithmVersion.V21)
            {
                continue;
            }

            var values = PadValues(row.Values);
            _ = TryAddNode(row.IcIndex, nodesByIc, new NotchV21FirmwareNode(
                DestinationDiffIndex: values[0],
                RegularPercent: Math.Clamp(values[1], 0, byte.MaxValue),
                ReguToFullPercent: Math.Clamp(values[2], 0, byte.MaxValue),
                FirstDiffIndex: values[3],
                FirstType: NormalizeType(values[4]),
                FirstRatioQ7: NotchV21Q7Codec.NormalizeMagnitude(values[5]),
                SecondDiffIndex: values[6],
                SecondType: NormalizeType(values[7]),
                SecondRatioQ7: NotchV21Q7Codec.NormalizeMagnitude(values[8]),
                Comment: NormalizeComment(row.Comment)));
        }

        SortNodes(nodesByIc);
        return BuildProjection(nodesByIc, Array.Empty<NotchV21ProjectedSourceRow>());
    }

    private static NotchV21FirmwareProjection ProjectCadAllocation(
        IReadOnlyList<NotchTableRow> rows,
        int nullValue,
        int icCount)
    {
        var termsByDestination = new Dictionary<(int IcIndex, int DestinationDiffIndex), List<FirmwareTerm>>();
        var sourceRows = new List<NotchV21ProjectedSourceRow>();
        var rowNumber = 0;
        foreach (var row in rows)
        {
            if (row.Version != NotchAlgorithmVersion.V21)
            {
                continue;
            }

            rowNumber++;
            var values = PadValues(row.Values);
            var sourceRow = ProjectSourceRow(rowNumber, row, values, nullValue);
            sourceRows.Add(sourceRow);

            if (sourceRow.SelfRatioQ7 != 0)
            {
                AddTerm(
                    row.IcIndex,
                    sourceRow.SourceDiffIndex,
                    sourceRow.SourceDiffIndex,
                    sourceRow.SelfType,
                    sourceRow.SelfRatioQ7,
                    BuildTermComment(row, "SELF"));
            }

            foreach (var leg in sourceRow.Legs)
            {
                AddTerm(
                    row.IcIndex,
                    leg.TargetDiffIndex,
                    sourceRow.SourceDiffIndex,
                    leg.Type,
                    leg.RatioQ7,
                    BuildTermComment(row, "LEG"));
            }
        }

        var nodesByIc = CreateNodeBuckets(icCount);
        foreach (var group in termsByDestination
                     .OrderBy(static item => item.Key.IcIndex)
                     .ThenBy(static item => item.Key.DestinationDiffIndex))
        {
            var terms = group.Value
                .OrderBy(static term => term.RefDiffIndex)
                .ThenBy(static term => term.Type)
                .ThenBy(static term => term.RatioQ7)
                .ToList();
            for (var index = 0; index < terms.Count; index += 2)
            {
                var first = terms[index];
                var second = index + 1 < terms.Count
                    ? terms[index + 1]
                    : new FirmwareTerm(nullValue, NotchV21Q7Codec.TypeNone, 0, "-");
                _ = TryAddNode(group.Key.IcIndex, nodesByIc, new NotchV21FirmwareNode(
                    DestinationDiffIndex: group.Key.DestinationDiffIndex,
                    RegularPercent: 100,
                    ReguToFullPercent: 100,
                    FirstDiffIndex: first.RefDiffIndex,
                    FirstType: first.Type,
                    FirstRatioQ7: first.RatioQ7,
                    SecondDiffIndex: second.RefDiffIndex,
                    SecondType: second.Type,
                    SecondRatioQ7: second.RatioQ7,
                    Comment: MergeTermComments(first, second)));
            }
        }

        SortNodes(nodesByIc);
        return BuildProjection(nodesByIc, sourceRows);

        void AddTerm(
            int icIndex,
            int destinationDiffIndex,
            int refDiffIndex,
            int type,
            int ratioQ7,
            string comment)
        {
            var normalizedRatio = NotchV21Q7Codec.NormalizeMagnitude(ratioQ7);
            if (normalizedRatio == 0 || type == NotchV21Q7Codec.TypeNone)
            {
                return;
            }

            var key = (icIndex, destinationDiffIndex);
            if (!termsByDestination.TryGetValue(key, out var terms))
            {
                terms = new List<FirmwareTerm>();
                termsByDestination[key] = terms;
            }

            terms.Add(new FirmwareTerm(refDiffIndex, type, normalizedRatio, comment));
        }
    }

    private static NotchV21ProjectedSourceRow ProjectSourceRow(
        int rowNumber,
        NotchTableRow row,
        int[] values,
        int nullValue)
    {
        var sourceDiffIndex = values[0];
        var combinePercent = Math.Clamp(values[2], 0, byte.MaxValue);
        var legs = new List<NotchV21ProjectedLeg>(2);
        AddLeg(values[3], values[4], values[5]);
        AddLeg(values[6], values[7], values[8]);

        var sourceRetainedPercent = combinePercent - legs.Sum(static leg => leg.SignedPercent);
        sourceRetainedPercent = Math.Max(0, sourceRetainedPercent);
        var sourceDeltaPercent = sourceRetainedPercent - 100;
        var selfType = sourceDeltaPercent switch
        {
            > 0 => NotchV21Q7Codec.TypeAdd,
            < 0 => NotchV21Q7Codec.TypeSub,
            _ => NotchV21Q7Codec.TypeNone,
        };
        var selfRatioQ7 = sourceDeltaPercent == 0
            ? 0
            : NotchV21Q7Codec.EncodePercentMagnitude(sourceDeltaPercent);

        return new NotchV21ProjectedSourceRow(
            rowNumber,
            row.IcIndex,
            sourceDiffIndex,
            row.RegularPadIndex,
            row.CadPadId,
            combinePercent,
            selfType,
            selfRatioQ7,
            legs);

        void AddLeg(int targetDiffIndex, int type, int ratioQ7)
        {
            if (targetDiffIndex == nullValue || type == NotchV21Q7Codec.TypeNone || ratioQ7 == 0)
            {
                return;
            }

            var normalizedRatio = NotchV21Q7Codec.NormalizeMagnitude(ratioQ7);
            var signedPercent = NotchV21Q7Codec.DecodeSignedPercent(type, normalizedRatio);
            if (signedPercent == 0)
            {
                return;
            }

            legs.Add(new NotchV21ProjectedLeg(targetDiffIndex, type, normalizedRatio, signedPercent));
        }
    }

    private static NotchV21FirmwareProjection BuildProjection(
        List<List<NotchV21FirmwareNode>> nodesByIc,
        IReadOnlyList<NotchV21ProjectedSourceRow> sourceRows)
    {
        return new NotchV21FirmwareProjection(
            nodesByIc.Select(static nodes => (IReadOnlyList<NotchV21FirmwareNode>)nodes).ToList(),
            sourceRows);
    }

    private static List<List<NotchV21FirmwareNode>> CreateNodeBuckets(int icCount)
    {
        return Enumerable.Range(0, Math.Max(1, icCount))
            .Select(static _ => new List<NotchV21FirmwareNode>())
            .ToList();
    }

    private static bool TryAddNode(
        int icIndex,
        List<List<NotchV21FirmwareNode>> nodesByIc,
        NotchV21FirmwareNode node)
    {
        if (icIndex < 0 || icIndex >= nodesByIc.Count)
        {
            return false;
        }

        nodesByIc[icIndex].Add(node);
        return true;
    }

    private static void SortNodes(IEnumerable<List<NotchV21FirmwareNode>> nodesByIc)
    {
        foreach (var nodes in nodesByIc)
        {
            nodes.Sort(static (left, right) =>
            {
                var diffCompare = left.DestinationDiffIndex.CompareTo(right.DestinationDiffIndex);
                if (diffCompare != 0)
                {
                    return diffCompare;
                }

                var firstCompare = left.FirstDiffIndex.CompareTo(right.FirstDiffIndex);
                return firstCompare != 0
                    ? firstCompare
                    : left.SecondDiffIndex.CompareTo(right.SecondDiffIndex);
            });
        }
    }

    private static int[] PadValues(int[]? source)
    {
        var values = new int[LegacyRowWidth];
        if (source is not null)
        {
            Array.Copy(source, values, Math.Min(source.Length, values.Length));
        }

        return values;
    }

    private static int NormalizeType(int type)
    {
        return type is NotchV21Q7Codec.TypeAdd or NotchV21Q7Codec.TypeSub
            ? type
            : NotchV21Q7Codec.TypeNone;
    }

    private static string BuildTermComment(NotchTableRow row, string kind)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1} srcD={2}",
            kind,
            NormalizeComment(row.Comment),
            row.DiffIndex);
    }

    private static string NormalizeComment(string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? "-" : comment;
    }

    private static string MergeTermComments(FirmwareTerm first, FirmwareTerm second)
    {
        if (second.Type == NotchV21Q7Codec.TypeNone)
        {
            return first.Comment;
        }

        return string.Equals(first.Comment, second.Comment, StringComparison.Ordinal)
            ? first.Comment
            : string.Concat(first.Comment, " | ", second.Comment);
    }

    private sealed record FirmwareTerm(int RefDiffIndex, int Type, int RatioQ7, string Comment);
}

internal sealed record NotchV21FirmwareProjection(
    IReadOnlyList<IReadOnlyList<NotchV21FirmwareNode>> NodesByIc,
    IReadOnlyList<NotchV21ProjectedSourceRow> SourceRows);

internal sealed record NotchV21ProjectedSourceRow(
    int RowNumber,
    int IcIndex,
    int SourceDiffIndex,
    int RegularPadId,
    int? CadPadId,
    int CombinePercent,
    int SelfType,
    int SelfRatioQ7,
    IReadOnlyList<NotchV21ProjectedLeg> Legs);

internal sealed record NotchV21ProjectedLeg(
    int TargetDiffIndex,
    int Type,
    int RatioQ7,
    int SignedPercent);

internal sealed record NotchV21FirmwareNode(
    int DestinationDiffIndex,
    int RegularPercent,
    int ReguToFullPercent,
    int FirstDiffIndex,
    int FirstType,
    int FirstRatioQ7,
    int SecondDiffIndex,
    int SecondType,
    int SecondRatioQ7,
    string Comment);
