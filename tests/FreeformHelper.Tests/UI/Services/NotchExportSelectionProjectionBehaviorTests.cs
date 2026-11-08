// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Domain.Notch;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionProjectionBehaviorTests : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

    public NotchExportSelectionProjectionBehaviorTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [Fact]
    public void Build_SearchCombinesFieldValuesQuotedPhrasesAndPlainTerms()
    {
        var rows = CreateRows();
        (string Query, int[] ExpectedRows)[] cases =
        [
            ("ic=2 diff=30", [0, 5]),
            ("ic=1 diff=30", []),
            ("row=1 reg=100 cad=210", [0]),
            ("v=2.1 note=\"legacy transfer\"", [3]),
            ("m=80% t1=31 t2=32", [0]),
            ("status=\"Linked mapping\"", [0, 1, 5]),
            ("\"edge transfer\" reversed", [5]),
            ("cad=210 note=\"center transfer\"", []),
        ];

        foreach (var (query, expectedRows) in cases)
        {
            var snapshot = Build(rows, CreateFilter() with { SearchKeyword = query });

            Assert.Equal(expectedRows, snapshot.VisibleRows.Select(static row => row.RowIndex));
            Assert.True(snapshot.FilterViewState.HasSearchFilterChip);
            Assert.Equal($"Search: {query}", snapshot.FilterViewState.SearchFilterChipText);
        }
    }

    [Fact]
    public void Build_StatusModesLinkVisibleRowsByEitherCadIdOrRegularIndex()
    {
        var rows = CreateRows().Reverse().ToArray();
        (NotchExportRowDisplayMode Mode, int[] Visible, int[] Linked, int[] RegularOnly, int[] IcGroups)[] cases =
        [
            (NotchExportRowDisplayMode.LinkedOnly, [0, 1, 5], [0, 1], [0], [0, 1]),
            (NotchExportRowDisplayMode.NoCadOnly, [2], [2], [2], [1]),
            (NotchExportRowDisplayMode.LegacyOnly, [3], [3], [3], [0]),
        ];

        foreach (var (mode, visible, linked, regularOnly, icGroups) in cases)
        {
            var filter = CreateFilter() with { RowDisplayMode = mode, RowDisplayModeLabel = mode.ToString() };
            var snapshot = NotchExportSelectionProjectionBuilder.Build(rows, filter, [211], [100, 102, 103]);

            Assert.Equal(visible, snapshot.VisibleRows.Select(static row => row.RowIndex));
            Assert.Equal(linked, snapshot.WorkspaceLinkedRows.Select(static row => row.RowIndex));
            Assert.Equal(icGroups, snapshot.IcGroups.Select(static group => group.IcIndex));
            Assert.True(snapshot.FilterViewState.HasModeFilterChip);
            Assert.Equal($"Showing {visible.Length} rows", snapshot.FilterViewState.VisibleRowsSummaryText);

            var regularSnapshot = NotchExportSelectionProjectionBuilder.Build(rows, filter, [], [100, 102, 103]);

            Assert.Equal(regularOnly, regularSnapshot.WorkspaceLinkedRows.Select(static row => row.RowIndex));
        }
    }

    [Fact]
    public void Build_MatchSortOrdersLegacyRowsByTheirDisplayedMatch()
    {
        var rows = CreateRows().Reverse().ToArray();
        var byMatch = Build(rows, CreateFilter() with { SortMode = NotchExportSortMode.MatchDescending });
        var byIcAndDiff = Build(rows, CreateFilter() with { SortMode = NotchExportSortMode.IcThenDiff });

        Assert.Equal([4, 3, 1, 2, 0, 5], byMatch.VisibleRows.Select(static row => row.RowIndex));
        Assert.Equal("99%", byMatch.VisibleRows[1].MatchSummaryText);
        Assert.Equal("80%", byMatch.VisibleRows[^2].MatchSummaryText);
        Assert.Equal([1, 3, 4, 0, 5, 2], byIcAndDiff.VisibleRows.Select(static row => row.RowIndex));
    }

    [Fact]
    public void Build_ColumnFiltersUseDisplayedValuesAndSummarizeEveryActiveHeader()
    {
        var rows = CreateRows();
        (NotchExportColumnFilterField Field, string Value, int[] ExpectedRows)[] cases =
        [
            (NotchExportColumnFilterField.Status, "Linked", [0, 1, 5]),
            (NotchExportColumnFilterField.Mapping, "REG 100 -> CAD 210", [0]),
            (NotchExportColumnFilterField.Match, "80%", [0, 5]),
            (NotchExportColumnFilterField.Diff, "30", [0, 5]),
            (NotchExportColumnFilterField.Ic, "IC 2", [0, 2, 5]),
            (NotchExportColumnFilterField.RowNumber, "#1", [0]),
        ];
        var filters = cases.ToDictionary(
            static item => item.Field,
            static item => new HashSet<string>(StringComparer.Ordinal) { item.Value });

        foreach (var (field, _, expectedRows) in cases)
        {
            var singleFilter = CreateFilter() with
            {
                ColumnFilters = new Dictionary<NotchExportColumnFilterField, HashSet<string>> { [field] = filters[field] },
            };
            var filtered = Build(rows, singleFilter);

            Assert.Equal(expectedRows, filtered.VisibleRows.Select(static row => row.RowIndex));
        }

        var filter = CreateFilter() with { ColumnFilters = filters };

        var snapshot = Build(rows, filter);

        Assert.Same(rows[0], Assert.Single(snapshot.VisibleRows));
        var state = snapshot.FilterViewState;
        Assert.Equal("#(1), IC(1), Diff(1), Match(1), REG->CAD(1), Status(1)", state.ActiveColumnFilterSummaryText);
        Assert.Equal($"Header: {state.ActiveColumnFilterSummaryText}", state.HeaderFilterChipText);
        Assert.True(state.IsRowColumnFilterActive);
        Assert.True(state.IsIcColumnFilterActive);
        Assert.True(state.IsDiffColumnFilterActive);
        Assert.True(state.IsMatchColumnFilterActive);
        Assert.True(state.IsMappingColumnFilterActive);
        Assert.True(state.IsStatusColumnFilterActive);
        Assert.True(state.HasResettableViewFilters);

        foreach (var (field, values) in filters)
        {
            var options = NotchExportSelectionProjectionBuilder.BuildColumnFilterValues(rows, filter, field);

            Assert.Equal(Assert.Single(values), Assert.Single(options));
        }
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
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

    private static NotchExportSelectionProjectionSnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> rows,
        NotchExportSelectionFilterState filter)
    {
        return NotchExportSelectionProjectionBuilder.Build(rows, filter, Array.Empty<int>(), Array.Empty<int>());
    }

    private static NotchExportRowItemViewModel[] CreateRows()
    {
        NotchTableRow[] rows =
        [
            new(1, 30, 100, 210, new NotchV22Node(30, 80, 31, 10, 32, 10, 0), "edge transfer"),
            new(0, 20, 101, 211, new NotchV22Node(20, 95, 21, 5, 65535, 0, 0), "center transfer"),
            new(1, 31, 102, null, new NotchV22Node(31, 90, 32, 10, 65535, 0, 0), "orphan transfer"),
            new(NotchAlgorithmVersion.V21, 0, 20, 103, 212, [20, 99, 0, 65535, 0, 0, 65535, 0, 0], "R=99% legacy transfer"),
            new(0, 22, 104, 213, new NotchV22Node(22, 100, 65535, 0, 65535, 0, 0), "keep anchor"),
            new(1, 30, 105, 214, new NotchV22Node(30, 80, 33, 10, 34, 10, 0), "edge transfer reversed"),
        ];
        return rows.Select(static (row, index) => new NotchExportRowItemViewModel(index, row)).ToArray();
    }
}
