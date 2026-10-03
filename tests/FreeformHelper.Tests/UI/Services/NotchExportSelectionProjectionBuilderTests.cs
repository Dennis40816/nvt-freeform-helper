using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionProjectionBuilderTests
{
    private static readonly int[] WorkspaceCadIds = [200, 201, 202];
    private static readonly int[] CadOutputFwDiffIndices = [8, 9];
    private static readonly string[] ExpectedDiffFilterValues = ["10", "9"];
    private static readonly string[] ExpectedStatusFilterValues = ["Linked"];
    private static readonly int[] LegacyRowPayload = [20, 1, 2];

    [Fact]
    public void Build_ProjectsVisibleRowsGroupsAndWorkspaceLinks_FromSingleScope()
    {
        var rows = BuildProjectionRows();
        rows[2].IsSelected = false;
        var filterState = new NotchExportSelectionFilterState(
            NotchAlgorithmVersion.V22,
            NotchExportRowDisplayMode.AllRows,
            "All",
            NotchExportSortMode.DiffAscending,
            string.Empty,
            ShowSelectedOnly: true,
            new Dictionary<NotchExportColumnFilterField, HashSet<string>>
            {
                [NotchExportColumnFilterField.Status] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Linked",
                    "No-op",
                },
            });

        var snapshot = NotchExportSelectionProjectionBuilder.Build(
            rows,
            filterState,
            WorkspaceCadIds,
            Array.Empty<int>());

        Assert.Equal(CadOutputFwDiffIndices, snapshot.VisibleRows.Select(row => row.Row.DiffIndex).ToArray());
        Assert.Single(snapshot.IcGroups);
        Assert.Equal(CadOutputFwDiffIndices, snapshot.IcGroups[0].Rows.Select(row => row.Row.DiffIndex).ToArray());
        Assert.Equal(CadOutputFwDiffIndices, snapshot.WorkspaceLinkedRows.Select(row => row.Row.DiffIndex).ToArray());
        Assert.True(snapshot.FilterViewState.HasSelectionFilterChip);
        Assert.True(snapshot.FilterViewState.HasHeaderFilterChip);
        Assert.Equal("Showing 2 rows", snapshot.FilterViewState.VisibleRowsSummaryText);
    }

    [Fact]
    public void BuildColumnFilterValues_ExcludeTargetFieldFilter_ButKeepSharedScope()
    {
        var rows = BuildProjectionRows();
        var filterState = new NotchExportSelectionFilterState(
            NotchAlgorithmVersion.V22,
            NotchExportRowDisplayMode.AllRows,
            "All",
            NotchExportSortMode.RowId,
            string.Empty,
            ShowSelectedOnly: false,
            new Dictionary<NotchExportColumnFilterField, HashSet<string>>
            {
                [NotchExportColumnFilterField.Status] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Linked",
                },
                [NotchExportColumnFilterField.Diff] = new HashSet<string>(StringComparer.Ordinal)
                {
                    "9",
                },
            });

        var diffValues = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(
            rows,
            filterState,
            NotchExportColumnFilterField.Diff);
        var statusValues = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(
            rows,
            filterState,
            NotchExportColumnFilterField.Status);

        Assert.Equal(ExpectedDiffFilterValues, diffValues);
        Assert.Equal(ExpectedStatusFilterValues, statusValues);
    }

    private static List<NotchExportRowItemViewModel> BuildProjectionRows()
    {
        var rows = new NotchTableRow[]
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
            new NotchTableRow(NotchAlgorithmVersion.V21, 1, 20, 300, 400, LegacyRowPayload, "legacy"),
        };
        var table = new NotchTable(rows);

        return table.Rows
            .Select((row, index) => new NotchExportRowItemViewModel(index, row))
            .ToList();
    }
}
