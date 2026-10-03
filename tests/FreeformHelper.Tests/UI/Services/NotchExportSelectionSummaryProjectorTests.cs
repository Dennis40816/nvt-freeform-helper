using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionSummaryProjectorTests
{
    private static readonly int[] LegacyRowPayload = [20, 1, 2];

    [Fact]
    public void Build_ProjectsScopedCountsAndTexts()
    {
        var rows = BuildRows();
        rows[1].IsSelected = false;
        var visibleRows = new[] { rows[0], rows[1] };

        var snapshot = NotchExportSelectionSummaryProjector.Build(
            rows,
            visibleRows,
            NotchAlgorithmVersion.V22);

        Assert.Equal(2, snapshot.AllRowCount);
        Assert.Equal(2, snapshot.VisibleCount);
        Assert.Equal(1, snapshot.SelectedCount);
        Assert.Equal(2, snapshot.V22RowCount);
        Assert.Equal(1, snapshot.TransferRowCount);
        Assert.Equal(1, snapshot.WarningRowCount);
        Assert.Equal(0, snapshot.LegacyRowCount);
        Assert.Equal(1, snapshot.CadLinkedRowCount);
        Assert.Equal(1, snapshot.CadMissingRowCount);
        Assert.Equal("Selected 1/2 rows · shown 2.", snapshot.SummaryText);
        Assert.Equal("Export 1 rows", snapshot.ExportButtonText);
        Assert.True(snapshot.HasRows);
        Assert.True(snapshot.HasSelection);
    }

    [Fact]
    public void Build_NoSelection_UsesDefaultExportText()
    {
        var rows = BuildRows();
        foreach (var row in rows)
        {
            row.IsSelected = false;
        }

        var snapshot = NotchExportSelectionSummaryProjector.Build(
            rows,
            Array.Empty<NotchExportRowItemViewModel>(),
            selectedVersion: null);

        Assert.Equal("Export selected", snapshot.ExportButtonText);
        Assert.False(snapshot.HasRows);
        Assert.False(snapshot.HasSelection);
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
                cadPadId: null,
                v22Node: new NotchV22Node(
                    AnchorDiffIndex: 9,
                    CombinePercent: 95,
                    TargetDiffIndex1: 10,
                    TargetRatioPercent1: 5,
                    TargetDiffIndex2: 65535,
                    TargetRatioPercent2: 0,
                    Flags: 0),
                comment: "warn"),
            new NotchTableRow(NotchAlgorithmVersion.V21, 1, 20, 120, 400, LegacyRowPayload, "legacy"),
        });

        return table.Rows
            .Select((row, index) => new NotchExportRowItemViewModel(index, row))
            .ToList();
    }
}
