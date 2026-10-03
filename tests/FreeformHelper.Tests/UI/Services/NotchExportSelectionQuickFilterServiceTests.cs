using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionQuickFilterServiceTests
{
    [Fact]
    public void ResolveAction_RowTarget_TogglesToken()
    {
        var add = NotchExportSelectionQuickFilterService.ResolveAction(
            "row",
            string.Empty,
            NotchExportRowDisplayMode.TransferOnly);

        Assert.True(add.ShouldUpdateSearchKeyword);
        Assert.Equal("row=", add.SearchKeyword);
        Assert.False(add.ShouldUpdateRowDisplayMode);

        var remove = NotchExportSelectionQuickFilterService.ResolveAction(
            "row",
            "row=",
            NotchExportRowDisplayMode.TransferOnly);

        Assert.True(remove.ShouldUpdateSearchKeyword);
        Assert.Equal(string.Empty, remove.SearchKeyword);
        Assert.False(remove.ShouldUpdateRowDisplayMode);
    }

    [Fact]
    public void ResolveAction_RowTarget_RemovesTokenInsideCompoundQuery()
    {
        var action = NotchExportSelectionQuickFilterService.ResolveAction(
            "row",
            "diff=9 row=",
            NotchExportRowDisplayMode.TransferOnly);

        Assert.True(action.ShouldUpdateSearchKeyword);
        Assert.Equal("diff=9", action.SearchKeyword);
        Assert.False(action.ShouldUpdateRowDisplayMode);
    }

    [Fact]
    public void ResolveAction_StatusTarget_CyclesDisplayMode()
    {
        var fromTransfer = NotchExportSelectionQuickFilterService.ResolveAction(
            "status",
            "diff=9",
            NotchExportRowDisplayMode.TransferOnly);

        Assert.False(fromTransfer.ShouldUpdateSearchKeyword);
        Assert.True(fromTransfer.ShouldUpdateRowDisplayMode);
        Assert.Equal(NotchExportRowDisplayMode.LinkedOnly, fromTransfer.RowDisplayMode);

        var fromAllRows = NotchExportSelectionQuickFilterService.ResolveAction(
            "status",
            string.Empty,
            NotchExportRowDisplayMode.AllRows);

        Assert.True(fromAllRows.ShouldUpdateRowDisplayMode);
        Assert.Equal(NotchExportRowDisplayMode.TransferOnly, fromAllRows.RowDisplayMode);
    }

    [Fact]
    public void ResolveAction_UnknownTarget_ReturnsNoOp()
    {
        var action = NotchExportSelectionQuickFilterService.ResolveAction(
            "unknown",
            "diff=9",
            NotchExportRowDisplayMode.WarningOnly);

        Assert.False(action.ShouldUpdateSearchKeyword);
        Assert.False(action.ShouldUpdateRowDisplayMode);
    }
}
