// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchExportSelectionViewModelTests
{
    [Fact]
    public void SelectPreviewRow_LegacyPayload_ShowsRawColumnsAndUnavailableV22Analysis()
    {
        using var culture = TestCulture.UseEnUs();
        var row = new NotchTableRow(NotchAlgorithmVersion.V21, 1, 20, 120, 220,
            [20, 128, 21, 64, 0, 65535, 0, 0, 0], "legacy transfer");
        var vm = new NotchExportSelectionViewModel(new NotchTable([row]));

        vm.SelectPreviewRowCommand.Execute(Assert.Single(vm.Rows));

        Assert.Equal("Legacy", vm.SelectedRow!.StatusText);
        Assert.Equal("Legacy mapping", vm.SelectedRowMappingStatusText);
        Assert.Equal("Legacy row: interpreted by legacy firmware path.", vm.SelectedRowOutcomeText);
        Assert.Equal("Legacy payload: use Values row for detail.", vm.SelectedRowPayloadText);
        Assert.Equal("Legacy raw columns: [20, 128, 21, 64, 0, 65535, 0, 0, 0]", vm.SelectedRowColumnBreakdownText);
        Assert.Equal("{ 20, 128, 21, 64, 0, 65535, 0, 0, 0 }", vm.SelectedRowCodePreviewText);
        Assert.Equal("20", vm.SelectedRowPayloadAnchorText);
        Assert.Equal("-", vm.SelectedRowPayloadCombineText);
        Assert.Equal("-", vm.SelectedRowPayloadTarget1Text);
        Assert.Equal("-", vm.SelectedRowPayloadTarget2Text);
        Assert.Equal("n/a", vm.SelectedRowFlagsText);
        Assert.Equal("-", vm.SelectedRowAnalysisRatioSumText);
        Assert.Equal("-", vm.SelectedRowAnalysisDeltaText);
        Assert.Same(row, Assert.Single(vm.BuildSelectedTable().Rows));
    }

    [Fact]
    public void LegacyMatchPercent_UsesCompleteNumericCommentTokenOrShowsUnavailable()
    {
        using var culture = TestCulture.UseEnUs();
        (string Comment, double? Percent, string Summary)[] cases =
        [
            (" ", null, "-"),
            ("legacy transfer", null, "-"),
            ("R=91.25", null, "-"),
            ("R=%", null, "-"),
            ("R=review%", null, "-"),
            ("r= 91.25 %; legacy transfer", 91.25, "91.25%"),
        ];
        foreach (var (comment, percent, summary) in cases)
        {
            var row = new NotchTableRow(NotchAlgorithmVersion.V21, 0, 20, 120, 220,
                [20, 128, 21, 64, 0, 65535, 0, 0, 0], comment);
            var item = new NotchExportRowItemViewModel(0, row);

            var actualPercent = item.MatchPercent;
            var actualSummary = item.MatchSummaryText;

            Assert.Equal(percent, actualPercent);
            Assert.Equal(summary, actualSummary);
        }
    }

    [Fact]
    public void SelectPreviewRow_V22Payload_FormatsSignedTargetsAndTestsContinuationBit()
    {
        using var culture = TestCulture.UseEnUs();
        (int Flags, string Display)[] cases = [(0, "NONE"), (1, "CONTINUATION"), (2, "NONE")];
        foreach (var (flags, display) in cases)
        {
            var node = new NotchV22Node(20, 95, 21, -5, 65535, 0, flags);
            var row = new NotchTableRow(1, 20, 120, 220, node, "R=12% diagnostic");
            var vm = new NotchExportSelectionViewModel(new NotchTable([row]));

            vm.SelectPreviewRowCommand.Execute(Assert.Single(vm.Rows));

            Assert.Equal("95%", vm.SelectedRow!.MatchSummaryText);
            Assert.Equal("Anchor 20, combine 95%, target1 21 (-5%), target2 none (0%).", vm.SelectedRowOutcomeText);
            Assert.Equal("20", vm.SelectedRowPayloadAnchorText);
            Assert.Equal("95%", vm.SelectedRowPayloadCombineText);
            Assert.Equal("21 (-5%)", vm.SelectedRowPayloadTarget1Text);
            Assert.Equal("none (0%)", vm.SelectedRowPayloadTarget2Text);
            Assert.Equal("-5%", vm.SelectedRowAnalysisRatioSumText);
            Assert.Equal("100%", vm.SelectedRowAnalysisDeltaText);
            Assert.Equal(display, vm.SelectedRowFlagsText);
            Assert.Same(row, Assert.Single(vm.BuildSelectedTable().Rows));
        }
    }

    [Fact]
    public void SelectPreviewRow_NoOpPayload_PrioritizesMissingCadAndKeepsTheRowInExport()
    {
        using var culture = TestCulture.UseEnUs();
        (int? CadId, string Mapping, string Status, string Relation)[] cases =
        [
            (220, "Linked, but payload is no-op", "No-op", "REG 120 -> CAD 220"),
            (null, "No CAD mapping", "No CAD", "REG 120 -> CAD -"),
        ];
        foreach (var (cadId, mapping, status, relation) in cases)
        {
            var row = new NotchTableRow(1, 20, 120, cadId,
                new NotchV22Node(20, 100, 65535, 0, 65535, 0, 0), " ");
            var vm = new NotchExportSelectionViewModel(new NotchTable([row]));
            Assert.Empty(vm.Rows);
            vm.SetViewFilterModeCommand.Execute(NotchExportRowDisplayMode.AllRows);
            var item = Assert.Single(vm.Rows);

            vm.SelectPreviewRowCommand.Execute(item);

            Assert.True(item.IsNoOpV22Node);
            Assert.False(item.IsTransferRow);
            Assert.Equal(status, item.StatusText);
            Assert.Equal(cadId.HasValue, item.IsStatusWarning);
            Assert.False(item.IsStatusLinked);
            Assert.Equal(mapping, vm.SelectedRowMappingStatusText);
            Assert.Equal("No-op: keep anchor diff only (no target switch).", vm.SelectedRowOutcomeText);
            Assert.Equal("#1 · v2.2 · IC 2 · Diff 20", item.CompactHeaderText);
            Assert.Equal(relation, item.CompactMetaText);
            Assert.Equal("#1 · IC 2 · Diff 20", item.WorkspaceLinkText);
            Assert.Equal(relation, item.WorkspaceLinkDetailText);
            Assert.Equal(relation, vm.SelectedRowPadRelationText);
            Assert.Equal("-", vm.SelectedRowCommentText);
            Assert.Same(row, Assert.Single(vm.BuildSelectedTable().Rows));
        }
    }
}
