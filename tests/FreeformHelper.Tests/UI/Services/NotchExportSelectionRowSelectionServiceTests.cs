using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionRowSelectionServiceTests
{
    private static readonly int[] LegacyRowPayload = [20, 1, 2];

    [Fact]
    public void SelectAll_MarksAllRowsSelected()
    {
        var rows = BuildRows();
        rows[0].IsSelected = false;
        rows[2].IsSelected = false;

        NotchExportSelectionRowSelectionService.SelectAll(rows);

        Assert.All(rows, row => Assert.True(row.IsSelected));
    }

    [Fact]
    public void SelectNone_ClearsAllSelections()
    {
        var rows = BuildRows();

        NotchExportSelectionRowSelectionService.SelectNone(rows);

        Assert.All(rows, row => Assert.False(row.IsSelected));
    }

    [Fact]
    public void SelectVisibleOnly_KeepsOnlyVisibleRowsSelected()
    {
        var allRows = BuildRows();
        var visibleRows = new[] { allRows[1], allRows[3] };

        NotchExportSelectionRowSelectionService.SelectVisibleOnly(allRows, visibleRows);

        Assert.False(allRows[0].IsSelected);
        Assert.True(allRows[1].IsSelected);
        Assert.False(allRows[2].IsSelected);
        Assert.True(allRows[3].IsSelected);
    }

    [Fact]
    public void SetGroupSelection_AppliesSelectionFlagToGroupRows()
    {
        var rows = BuildRows();
        rows[0].IsSelected = false;
        rows[1].IsSelected = false;

        NotchExportSelectionRowSelectionService.SetGroupSelection(rows.Take(2).ToArray(), selected: true);

        Assert.True(rows[0].IsSelected);
        Assert.True(rows[1].IsSelected);
        Assert.True(rows[2].IsSelected);
        Assert.True(rows[3].IsSelected);
    }

    [Fact]
    public void SetWorkspaceLinkedRowsSelected_OnlyTouchesWorkspaceRows()
    {
        var rows = BuildRows();
        var linkedRows = new[] { rows[0], rows[2] };

        NotchExportSelectionRowSelectionService.SetWorkspaceLinkedRowsSelected(linkedRows, selected: false);

        Assert.False(rows[0].IsSelected);
        Assert.True(rows[1].IsSelected);
        Assert.False(rows[2].IsSelected);
        Assert.True(rows[3].IsSelected);
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
            new NotchTableRow(NotchAlgorithmVersion.V21, 1, 20, 120, 400, LegacyRowPayload, "legacy"),
        });

        return table.Rows
            .Select((row, index) => new NotchExportRowItemViewModel(index, row))
            .ToList();
    }
}
