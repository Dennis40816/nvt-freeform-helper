using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionSelectedRowServiceTests
{
    [Fact]
    public void ResolveProjectedSelectedRow_PrefersWorkspaceLinkedFirst()
    {
        var rows = BuildRows();
        var selected = NotchExportSelectionSelectedRowService.ResolveProjectedSelectedRow(
            selectedBefore: rows[2],
            visibleRows: rows,
            workspaceLinkedRows: new[] { rows[1], rows[2] });

        Assert.Same(rows[1], selected);
    }

    [Fact]
    public void ResolveProjectedSelectedRow_KeepsSelectedWhenStillVisible()
    {
        var rows = BuildRows();
        var selected = NotchExportSelectionSelectedRowService.ResolveProjectedSelectedRow(
            selectedBefore: rows[2],
            visibleRows: rows,
            workspaceLinkedRows: Array.Empty<NotchExportRowItemViewModel>());

        Assert.Same(rows[2], selected);
    }

    [Fact]
    public void ResolveProjectedSelectedRow_FallsBackToFirstVisible()
    {
        var rows = BuildRows();
        var visible = new[] { rows[1], rows[2] };
        var selected = NotchExportSelectionSelectedRowService.ResolveProjectedSelectedRow(
            selectedBefore: rows[0],
            visibleRows: visible,
            workspaceLinkedRows: Array.Empty<NotchExportRowItemViewModel>());

        Assert.Same(rows[1], selected);
    }

    [Fact]
    public void ShouldAutoSelectRowFromSelectionToggle_OnlyWhenNoCurrentSelectionAndVisible()
    {
        var rows = BuildRows();
        var shouldAutoSelect = NotchExportSelectionSelectedRowService.ShouldAutoSelectRowFromSelectionToggle(
            currentSelectedRow: null,
            changedRow: rows[0],
            visibleRows: rows);

        Assert.True(shouldAutoSelect);

        var noAutoSelectWithCurrent = NotchExportSelectionSelectedRowService.ShouldAutoSelectRowFromSelectionToggle(
            currentSelectedRow: rows[1],
            changedRow: rows[0],
            visibleRows: rows);
        Assert.False(noAutoSelectWithCurrent);

        var noAutoSelectWhenHidden = NotchExportSelectionSelectedRowService.ShouldAutoSelectRowFromSelectionToggle(
            currentSelectedRow: null,
            changedRow: rows[0],
            visibleRows: new[] { rows[1], rows[2] });
        Assert.False(noAutoSelectWhenHidden);
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
