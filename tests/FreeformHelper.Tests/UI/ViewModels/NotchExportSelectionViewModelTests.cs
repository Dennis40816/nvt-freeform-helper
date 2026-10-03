using FreeformHelper.Domain.Notch;

namespace FreeformHelper.Tests;

public sealed partial class NotchExportSelectionViewModelTests
{

    private static NotchTable BuildSampleTable()
    {
        var row0Values = new[] { 21, 1, 2 };
        var row1Values = new[] { 30, 3, 4 };
        var row2Values = new[] { 21, 5, 6 };
        var rows = new[]
        {
            new NotchTableRow(NotchAlgorithmVersion.V21, 0, 10, 100, 200, row0Values, "r0"),
            new NotchTableRow(NotchAlgorithmVersion.V22, 0, 11, 101, 201, row1Values, "r1"),
            new NotchTableRow(NotchAlgorithmVersion.V21, 1, 12, 102, 202, row2Values, "r2"),
        };
        return new NotchTable(rows);
    }

    private static NotchTable BuildTransferOnlyFilterTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 8,
                regularPadIndex: 100,
                cadPadId: 200,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 8,
                    CombinePercent: 100,
                    TargetDiffIndex1: 65535,
                    TargetRatioPercent1: 0,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "noop"),
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 9,
                regularPadIndex: 101,
                cadPadId: 201,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 9,
                    CombinePercent: 100,
                    TargetDiffIndex1: 10,
                    TargetRatioPercent1: 25,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "transfer"),
        });
    }

    private static NotchTable BuildCadStatusFilterTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 12,
                regularPadIndex: 200,
                cadPadId: 300,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 12,
                    CombinePercent: 90,
                    TargetDiffIndex1: 13,
                    TargetRatioPercent1: 10,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "linked"),
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 13,
                regularPadIndex: 201,
                cadPadId: null,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 13,
                    CombinePercent: 90,
                    TargetDiffIndex1: 14,
                    TargetRatioPercent1: 10,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "no cad"),
        });
    }

    private static NotchTable BuildColumnFilterProjectionTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 8,
                regularPadIndex: 100,
                cadPadId: 200,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 8,
                    CombinePercent: 100,
                    TargetDiffIndex1: 65535,
                    TargetRatioPercent1: 0,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "noop"),
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 9,
                regularPadIndex: 101,
                cadPadId: 201,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 9,
                    CombinePercent: 95,
                    TargetDiffIndex1: 10,
                    TargetRatioPercent1: 5,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "linked-a"),
            new NotchTableRow(
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 102,
                cadPadId: 202,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 10,
                    CombinePercent: 90,
                    TargetDiffIndex1: 11,
                    TargetRatioPercent1: 10,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "linked-b"),
        });
    }
}
