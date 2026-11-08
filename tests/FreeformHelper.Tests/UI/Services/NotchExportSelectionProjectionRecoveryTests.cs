// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionProjectionRecoveryTests
{
    [Fact]
    public void BuildColumnFilterValues_EmptyStatusSelectionCanBeReopenedWithinTheOtherHeaderScope()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        var filter = CreateFilter() with
        {
            ColumnFilters = new Dictionary<NotchExportColumnFilterField, HashSet<string>>
            {
                [NotchExportColumnFilterField.Ic] = new(StringComparer.Ordinal) { "IC 2" },
                [NotchExportColumnFilterField.Status] = new(StringComparer.Ordinal),
            },
        };

        var snapshot = NotchExportSelectionProjectionBuilder.Build(rows, filter, [210], [100]);
        var statusOptions = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(
            rows, filter, NotchExportColumnFilterField.Status);
        var icOptions = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(
            rows, filter, NotchExportColumnFilterField.Ic);

        Assert.Empty(snapshot.VisibleRows);
        Assert.Empty(snapshot.IcGroups);
        Assert.Empty(snapshot.WorkspaceLinkedRows);
        Assert.Equal("IC(1), Status(0)", snapshot.FilterViewState.ActiveColumnFilterSummaryText);
        Assert.True(snapshot.FilterViewState.HasResettableViewFilters);
        Assert.Equal("Showing 0 rows", snapshot.FilterViewState.VisibleRowsSummaryText);
        Assert.Equal(["Legacy", "Linked"], statusOptions);
        Assert.Empty(icOptions);
        Assert.All(rows, static row => Assert.True(row.IsSelected));
    }

    [Fact]
    public void Build_MatchSortPlacesUnknownLegacyMatchWithZeroMatchesAndKeepsRowOrderForTies()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        var filter = CreateFilter() with { SortMode = NotchExportSortMode.MatchDescending };

        var snapshot = NotchExportSelectionProjectionBuilder.Build(rows.Reverse().ToArray(), filter, [], []);

        Assert.Equal([1, 0, 2, 3], snapshot.VisibleRows.Select(static row => row.RowIndex));
        Assert.Null(rows[0].MatchPercent);
        Assert.Equal("-", rows[0].MatchSummaryText);
        Assert.Equal("0%", rows[2].MatchSummaryText);
        Assert.Equal("0%", rows[3].MatchSummaryText);
        Assert.Equal([0, 1], snapshot.IcGroups.Select(static group => group.IcIndex));
    }

    private static NotchExportSelectionFilterState CreateFilter()
    {
        return new NotchExportSelectionFilterState(
            SelectedVersion: null,
            NotchExportRowDisplayMode.AllRows,
            "All",
            NotchExportSortMode.RowId,
            string.Empty,
            ShowSelectedOnly: false,
            new Dictionary<NotchExportColumnFilterField, HashSet<string>>());
    }

    private static NotchExportRowItemViewModel[] CreateRows()
    {
        NotchTableRow[] rows =
        [
            new(NotchAlgorithmVersion.V21, 1, 30, 100, 210,
                [30, 100, 100, 65535, 0, 0, 65535, 0, 0], "legacy transfer without match metadata"),
            new(1, 31, 101, 211, new NotchV22Node(31, 90, 32, 10, 65535, 0, 0), "linked transfer"),
            new(0, 12, 102, null, new NotchV22Node(12, 0, 65535, 0, 65535, 0, 0), "unmapped source"),
            new(NotchAlgorithmVersion.V21, 1, 32, 103, 212,
                [32, 100, 100, 65535, 0, 0, 65535, 0, 0], "R=0% legacy transfer"),
        ];
        return rows.Select(static (row, index) => new NotchExportRowItemViewModel(index, row)).ToArray();
    }
}
