using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionWorkspaceLinkServiceTests
{
    private static readonly int[] LinkedWorkspaceCadIds = [200, 201];
    private static readonly int[] MissingWorkspaceCadIds = [9999];

    [Fact]
    public void Build_WithLinkedRows_PrefersFirstLinkedRow()
    {
        var rows = BuildRows();
        var selectedBefore = rows[2];

        var snapshot = NotchExportSelectionWorkspaceLinkService.Build(
            rows,
            workspaceCadIds: LinkedWorkspaceCadIds,
            workspaceRegularIndices: Array.Empty<int>(),
            selectedRow: selectedBefore);

        Assert.Equal(2, snapshot.LinkedRows.Count);
        Assert.Same(rows[0], snapshot.LinkedRows[0]);
        Assert.Same(rows[1], snapshot.LinkedRows[1]);
        Assert.Same(rows[0], snapshot.PreferredSelectedRow);
    }

    [Fact]
    public void Build_WithoutLinkedRows_KeepsCurrentSelectedRow()
    {
        var rows = BuildRows();
        var selectedBefore = rows[2];

        var snapshot = NotchExportSelectionWorkspaceLinkService.Build(
            rows,
            workspaceCadIds: MissingWorkspaceCadIds,
            workspaceRegularIndices: Array.Empty<int>(),
            selectedRow: selectedBefore);

        Assert.Empty(snapshot.LinkedRows);
        Assert.Same(selectedBefore, snapshot.PreferredSelectedRow);
    }

    private static List<NotchExportRowItemViewModel> BuildRows()
    {
        var table = new NotchTable(new[]
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
                icIndex: 1,
                diffIndex: 18,
                regularPadIndex: 110,
                cadPadId: 300,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 18,
                    CombinePercent: 90,
                    TargetDiffIndex1: 19,
                    TargetRatioPercent1: 10,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "linked-b"),
        });

        return table.Rows
            .Select((row, index) => new NotchExportRowItemViewModel(index, row))
            .ToList();
    }
}
