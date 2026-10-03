using System.Globalization;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Application.Services;

internal static class NotchV22FirmwareProjector
{
    internal const int ContinuationFlag = 1 << 0;
    private const int AnchorOverrideFlag = 1 << 1;

    internal static NotchV22FirmwareProjection Project(
        IReadOnlyList<NotchTableRow> rows,
        int nullValue,
        int icCount)
    {
        ArgumentNullException.ThrowIfNull(rows);
        NotchSettings.ValidateNullValueOrThrow(nullValue);

        var sourceRows = rows
            .Select((row, index) => (Row: row, SourceOrdinal: index + 1))
            .Where(static item => item.Row.Version == NotchAlgorithmVersion.V22)
            .Select(item => ProjectRow(item.Row, item.SourceOrdinal, nullValue))
            .ToList();

        return new NotchV22FirmwareProjection(sourceRows, OrderNodesByIc(sourceRows, icCount));
    }

    internal static IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> OrderNodesByIc(
        IReadOnlyList<NotchV22FirmwareRow> sourceRows,
        int icCount)
    {
        var nodesByIc = Enumerable.Range(0, Math.Max(1, icCount))
            .Select(static _ => new List<NotchV22FirmwareRow>())
            .ToList();

        foreach (var row in sourceRows.OrderBy(static row => row.TableDiffIndex))
        {
            if (row.IcIndex >= 0 && row.IcIndex < nodesByIc.Count)
            {
                nodesByIc[row.IcIndex].Add(row);
            }
        }

        foreach (var nodes in nodesByIc)
        {
            nodes.Sort(static (left, right) =>
            {
                var diffCompare = left.SourceDiffIndex.CompareTo(right.SourceDiffIndex);
                return diffCompare != 0
                    ? diffCompare
                    : string.Compare(
                        FormatFlagsLiteral(left.Flags),
                        FormatFlagsLiteral(right.Flags),
                        StringComparison.Ordinal);
            });
        }

        return nodesByIc.Select(static nodes => (IReadOnlyList<NotchV22FirmwareRow>)nodes).ToList();
    }

    private static NotchV22FirmwareRow ProjectRow(
        NotchTableRow row,
        int sourceOrdinal,
        int nullValue)
    {
        if (row.V22Node is not null)
        {
            return BuildProjectedRow(row, sourceOrdinal, row.V22Node, nullValue, NormalizeComment(row.Comment));
        }

        var values = row.Values ?? Array.Empty<int>();
        if (values.Length >= 7)
        {
            return BuildProjectedRow(
                row,
                sourceOrdinal,
                NotchV22Node.FromValues(values),
                nullValue,
                NormalizeComment(row.Comment));
        }

        var legacyToRegular = values.Length > 1 ? values[1] : 100;
        var legacyToFull = values.Length > 2 ? values[2] : 100;
        var legacyCombinedRawPercent = (legacyToRegular * legacyToFull) / 100.0;
        if (legacyCombinedRawPercent > 255.0)
        {
            throw new InvalidOperationException(
                $"v2.2 Combine ratio exceeds 255% at IC{row.IcIndex + 1} diff{row.DiffIndex}: {legacyCombinedRawPercent:F2}%.");
        }

        var legacyCombinedPercent = Math.Clamp((int)Math.Round(legacyCombinedRawPercent), 0, 255);
        var comment = string.IsNullOrWhiteSpace(row.Comment)
            ? $"LEGACY C={legacyCombinedPercent}%"
            : row.Comment;
        return BuildProjectedRow(
            row,
            sourceOrdinal,
            new NotchV22Node(row.DiffIndex, legacyCombinedPercent, nullValue, 0, nullValue, 0, 0),
            nullValue,
            comment);
    }

    private static NotchV22FirmwareRow BuildProjectedRow(
        NotchTableRow row,
        int sourceOrdinal,
        NotchV22Node node,
        int nullValue,
        string comment)
    {
        return new NotchV22FirmwareRow(
            sourceOrdinal,
            row.IcIndex,
            row.DiffIndex,
            row.RegularPadIndex,
            row.CadPadId,
            node.AnchorDiffIndex,
            Math.Clamp(node.CombinePercent, 0, byte.MaxValue),
            node.TargetDiffIndex1 == nullValue ? null : node.TargetDiffIndex1,
            Math.Clamp(node.TargetRatioPercent1, -100, 100),
            node.TargetDiffIndex2 == nullValue ? null : node.TargetDiffIndex2,
            Math.Clamp(node.TargetRatioPercent2, -100, 100),
            node.Flags & byte.MaxValue,
            comment);
    }

    private static string NormalizeComment(string? comment) =>
        string.IsNullOrWhiteSpace(comment) ? "-" : comment;

    internal static string FormatFlagsLiteral(int flags)
    {
        if (flags == 0)
        {
            return "NHC_FLAG_NONE";
        }

        var parts = new List<string>(3);
        if ((flags & ContinuationFlag) != 0)
        {
            parts.Add("NHC_FLAG_CONTINUATION");
        }

        if ((flags & AnchorOverrideFlag) != 0)
        {
            parts.Add("NHC_FLAG_ANCHOR_OVERRIDE");
        }

        var remainingFlags = flags & ~(ContinuationFlag | AnchorOverrideFlag);
        if (remainingFlags != 0)
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "0x{0:X2}u", remainingFlags));
        }

        return string.Join(" | ", parts);
    }
}

internal sealed record NotchV22FirmwareProjection(
    IReadOnlyList<NotchV22FirmwareRow> SourceRows,
    IReadOnlyList<IReadOnlyList<NotchV22FirmwareRow>> NodesByIc);

internal sealed record NotchV22FirmwareRow(
    int SourceOrdinal,
    int IcIndex,
    int TableDiffIndex,
    int RegularPadId,
    int? CadPadId,
    int SourceDiffIndex,
    int CombinePercent,
    int? FirstTargetDiffIndex,
    int FirstRatioPercent,
    int? SecondTargetDiffIndex,
    int SecondRatioPercent,
    int Flags,
    string Comment)
{
    internal bool IsContinuation => (Flags & NotchV22FirmwareProjector.ContinuationFlag) != 0;
}
