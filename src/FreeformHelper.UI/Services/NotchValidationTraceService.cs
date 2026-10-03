using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Maps validation report rows into a single trace model shared by UI and runtime CLI.
/// </summary>
public sealed class NotchValidationTraceService
{
    public static NotchValidationTraceSnapshot BuildTrace(
        NotchValidationReport report,
        IReadOnlyCollection<RegularPad> regularPads,
        int nullDiffValue)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(regularPads);

        var regularByIcDiff = regularPads
            .GroupBy(static pad => (pad.IcIndex, pad.DiffIndex))
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(pad => pad.RegularPadId).First().RegularPadId);

        var directRows = report.DirectRows
            .Select(row => BuildDirectRow(row, nullDiffValue))
            .ToList();
        var incomingRows = report.IncomingRows
            .Select(flow => BuildIncomingRow(flow, regularByIcDiff, nullDiffValue))
            .ToList();
        var outgoingRows = report.OutgoingRows
            .Select(flow => BuildOutgoingRow(flow, regularByIcDiff, nullDiffValue))
            .ToList();

        var allRows = directRows
            .Concat(incomingRows)
            .Concat(outgoingRows)
            .OrderBy(static row => row.IsDirect ? 0 : row.IsIncoming ? 1 : 2)
            .ThenBy(static row => row.IcIndex)
            .ThenBy(static row => row.SourceDiffIndex)
            .ThenBy(static row => row.TargetDiffIndex)
            .ToList();

        return new NotchValidationTraceSnapshot(
            AllRows: allRows,
            DirectRows: directRows,
            IncomingRows: incomingRows,
            OutgoingRows: outgoingRows);
    }

    private static NotchValidationTraceRow BuildDirectRow(
        NotchValidationUseCase.IndexedNotchRow indexedRow,
        int nullDiffValue)
    {
        var row = indexedRow.Row;
        var payload = BuildRowPayload(row, nullDiffValue);
        var sourceText = $"IC{row.IcIndex + 1}/diff{row.DiffIndex}";
        return new NotchValidationTraceRow(
            KindText: "DIRECT",
            RowNumber: indexedRow.RowNumber,
            Version: row.Version,
            IcIndex: row.IcIndex,
            SourceDiffIndex: row.DiffIndex,
            TargetDiffIndex: row.DiffIndex,
            SourceRegularPadId: row.RegularPadIndex,
            TargetRegularPadId: row.RegularPadIndex,
            CadPadId: row.CadPadId ?? -1,
            RatioPercent: null,
            EffectiveArea: null,
            SourceText: sourceText,
            TargetText: sourceText,
            NoteText: $"REG {row.RegularPadIndex}, CAD {(row.CadPadId ?? 0)}",
            ValuesText: payload.ValuesText,
            CommentText: payload.CommentText,
            Values: payload.Values,
            V22: payload.V22,
            IsDirect: true,
            IsIncoming: false,
            IsOutgoing: false);
    }

    private static NotchValidationTraceRow BuildIncomingRow(
        NotchValidationFlowRow flow,
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), int> regularByIcDiff,
        int nullDiffValue)
    {
        var sourceRow = flow.SourceRow.Row;
        var payload = BuildRowPayload(sourceRow, nullDiffValue);
        var targetRegularPadId = ResolveTargetRegularPadId(regularByIcDiff, flow.TargetIcIndex, flow.TargetDiffIndex);

        return new NotchValidationTraceRow(
            KindText: "IN",
            RowNumber: flow.SourceRow.RowNumber,
            Version: sourceRow.Version,
            IcIndex: flow.TargetIcIndex,
            SourceDiffIndex: flow.SourceDiffIndex,
            TargetDiffIndex: flow.TargetDiffIndex,
            SourceRegularPadId: flow.SourceRegularPadId,
            TargetRegularPadId: targetRegularPadId,
            CadPadId: flow.SourceCadPadId,
            RatioPercent: flow.RatioPercent,
            EffectiveArea: double.IsFinite(flow.EffectiveArea) ? flow.EffectiveArea : null,
            SourceText: $"IC{sourceRow.IcIndex + 1}/diff{flow.SourceDiffIndex}",
            TargetText: $"IC{flow.TargetIcIndex + 1}/diff{flow.TargetDiffIndex}",
            NoteText: $"REG {flow.SourceRegularPadId} -> diff {flow.TargetDiffIndex}",
            ValuesText: payload.ValuesText,
            CommentText: payload.CommentText,
            Values: payload.Values,
            V22: payload.V22,
            IsDirect: false,
            IsIncoming: true,
            IsOutgoing: false);
    }

    private static NotchValidationTraceRow BuildOutgoingRow(
        NotchValidationFlowRow flow,
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), int> regularByIcDiff,
        int nullDiffValue)
    {
        var sourceRow = flow.SourceRow.Row;
        var payload = BuildRowPayload(sourceRow, nullDiffValue);
        var targetRegularPadId = ResolveTargetRegularPadId(regularByIcDiff, flow.TargetIcIndex, flow.TargetDiffIndex);

        return new NotchValidationTraceRow(
            KindText: "OUT",
            RowNumber: flow.SourceRow.RowNumber,
            Version: sourceRow.Version,
            IcIndex: flow.TargetIcIndex,
            SourceDiffIndex: flow.SourceDiffIndex,
            TargetDiffIndex: flow.TargetDiffIndex,
            SourceRegularPadId: flow.SourceRegularPadId,
            TargetRegularPadId: targetRegularPadId,
            CadPadId: flow.SourceCadPadId,
            RatioPercent: flow.RatioPercent,
            EffectiveArea: double.IsFinite(flow.EffectiveArea) ? flow.EffectiveArea : null,
            SourceText: $"IC{sourceRow.IcIndex + 1}/diff{flow.SourceDiffIndex}",
            TargetText: $"IC{flow.TargetIcIndex + 1}/diff{flow.TargetDiffIndex}",
            NoteText: $"REG {flow.SourceRegularPadId} -> diff {flow.TargetDiffIndex}",
            ValuesText: payload.ValuesText,
            CommentText: payload.CommentText,
            Values: payload.Values,
            V22: payload.V22,
            IsDirect: false,
            IsIncoming: false,
            IsOutgoing: true);
    }

    private static NotchValidationRowPayload BuildRowPayload(NotchTableRow row, int nullDiffValue)
    {
        var values = row.Values?.ToArray() ?? Array.Empty<int>();
        var valuesText = values.Length == 0
            ? "-"
            : string.Join(", ", values);
        var commentText = string.IsNullOrWhiteSpace(row.Comment)
            ? "-"
            : row.Comment.Trim();
        var v22 = BuildV22Payload(row, values, nullDiffValue);
        return new NotchValidationRowPayload(
            Values: values,
            ValuesText: valuesText,
            CommentText: commentText,
            V22: v22);
    }

    private static NotchValidationTraceV22Node? BuildV22Payload(
        NotchTableRow row,
        int[] values,
        int nullDiffValue)
    {
        NotchV22Node? node = row.V22Node;
        var source = "typed";
        if (node is null)
        {
            if (row.Version != NotchAlgorithmVersion.V22 || values.Length < 7)
            {
                return null;
            }

            node = new NotchV22Node(
                AnchorDiffIndex: values[0],
                CombinePercent: values[1],
                TargetDiffIndex1: values[2],
                TargetRatioPercent1: values[3],
                TargetDiffIndex2: values[4],
                TargetRatioPercent2: values[5],
                Flags: values[6]);
            source = "legacy-values";
        }

        return new NotchValidationTraceV22Node(
            Source: source,
            AnchorDiffIndex: node.AnchorDiffIndex,
            CombinePercent: node.CombinePercent,
            TargetDiffIndex1: node.TargetDiffIndex1,
            TargetRatioPercent1: node.TargetRatioPercent1,
            TargetDiffIndex2: node.TargetDiffIndex2,
            TargetRatioPercent2: node.TargetRatioPercent2,
            Flags: node.Flags,
            Legs: new[]
            {
                new NotchValidationTraceV22Leg(
                    Slot: 1,
                    TargetDiffIndex: node.TargetDiffIndex1,
                    RatioPercent: node.TargetRatioPercent1,
                    IsNone: node.TargetDiffIndex1 == nullDiffValue),
                new NotchValidationTraceV22Leg(
                    Slot: 2,
                    TargetDiffIndex: node.TargetDiffIndex2,
                    RatioPercent: node.TargetRatioPercent2,
                    IsNone: node.TargetDiffIndex2 == nullDiffValue),
            });
    }

    private static int ResolveTargetRegularPadId(
        IReadOnlyDictionary<(int IcIndex, int DiffIndex), int> regularByIcDiff,
        int icIndex,
        int diffIndex)
    {
        return regularByIcDiff.TryGetValue((icIndex, diffIndex), out var regularPadId)
            ? regularPadId
            : -1;
    }

    private sealed record NotchValidationRowPayload(
        IReadOnlyList<int> Values,
        string ValuesText,
        string CommentText,
        NotchValidationTraceV22Node? V22);
}

public sealed record NotchValidationTraceSnapshot(
    IReadOnlyList<NotchValidationTraceRow> AllRows,
    IReadOnlyList<NotchValidationTraceRow> DirectRows,
    IReadOnlyList<NotchValidationTraceRow> IncomingRows,
    IReadOnlyList<NotchValidationTraceRow> OutgoingRows);

public sealed record NotchValidationTraceRow(
    string KindText,
    int RowNumber,
    NotchAlgorithmVersion Version,
    int IcIndex,
    int SourceDiffIndex,
    int TargetDiffIndex,
    int SourceRegularPadId,
    int TargetRegularPadId,
    int CadPadId,
    double? RatioPercent,
    double? EffectiveArea,
    string SourceText,
    string TargetText,
    string NoteText,
    string ValuesText,
    string CommentText,
    IReadOnlyList<int> Values,
    NotchValidationTraceV22Node? V22,
    bool IsDirect,
    bool IsIncoming,
    bool IsOutgoing);

public sealed record NotchValidationTraceV22Node(
    string Source,
    int AnchorDiffIndex,
    int CombinePercent,
    int TargetDiffIndex1,
    int TargetRatioPercent1,
    int TargetDiffIndex2,
    int TargetRatioPercent2,
    int Flags,
    IReadOnlyList<NotchValidationTraceV22Leg> Legs);

public sealed record NotchValidationTraceV22Leg(
    int Slot,
    int TargetDiffIndex,
    int RatioPercent,
    bool IsNone);
