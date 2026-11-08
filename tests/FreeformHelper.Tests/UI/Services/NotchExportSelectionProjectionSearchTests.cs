// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportSelectionProjectionSearchTests
{
    [Fact]
    public void Build_EmptyQuotedSearchKeepsVersionModeAndSelectionScope()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        rows[1].IsSelected = false;
        var filter = CreateFilter("\"\"") with
        {
            SelectedVersion = NotchAlgorithmVersion.V22,
            RowDisplayMode = NotchExportRowDisplayMode.TransferOnly,
            RowDisplayModeLabel = "Transfer only",
            ShowSelectedOnly = true,
        };

        var snapshot = NotchExportSelectionProjectionBuilder.Build(rows, filter, [210], [102]);

        Assert.Equal([0, 2], snapshot.VisibleRows.Select(static row => row.RowIndex));
        Assert.Equal(snapshot.VisibleRows, snapshot.WorkspaceLinkedRows);
        Assert.Equal(1, Assert.Single(snapshot.IcGroups).IcIndex);
        Assert.Equal(snapshot.VisibleRows, snapshot.IcGroups[0].Rows);
        Assert.Equal("Search: \"\"", snapshot.FilterViewState.SearchFilterChipText);
        Assert.True(snapshot.FilterViewState.HasSearchFilterChip);
        Assert.Equal("Showing 2 rows", snapshot.FilterViewState.VisibleRowsSummaryText);
        Assert.False(rows[1].IsSelected);
    }

    [Fact]
    public void Build_FieldAliasesResolveDisplayedIdentitiesAndPayloadValues()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        (string Query, int[] ExpectedRows)[] cases =
        [
            ("#=1", [0]),
            ("id=4", [3]),
            ("d=3", []),
            ("regular=10", []),
            ("r=101", [1]),
            ("st=\"linked mapping\"", [0, 1]),
            ("ver=2.1", [3]),
            ("version=2.2", [0, 1, 2, 4]),
            ("anchor=30", [0]),
            ("match=80%", [0]),
            ("combine=80%", [0]),
            ("combine=-", [3]),
            ("target1=31", [0]),
            ("target2=32", [0]),
            ("text=\"edge transfer\"", [0]),
            ("comment=\"edge transfer\"", [0]),
            ("target1=999", []),
            ("target2=999", []),
            ("combine=81%", []),
        ];

        foreach (var (query, expectedRows) in cases)
        {
            var snapshot = Build(rows, query);

            Assert.Equal(expectedRows, snapshot.VisibleRows.Select(static row => row.RowIndex));
        }
    }

    [Fact]
    public void Build_CadSearchSupportsNullAndDashAliasesAndNumericIdentities()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        (string Query, int ExpectedRow)[] cases =
        [
            ("cad=NuLl", 2),
            ("c=-", 2),
            ("c=211", 1),
        ];

        foreach (var (query, expectedRow) in cases)
        {
            var snapshot = Build(rows, query);

            Assert.Same(rows[expectedRow], Assert.Single(snapshot.VisibleRows));
        }
    }

    [Fact]
    public void Build_NumericSearchUsesWholeNumbersAndRejectsNonNumericOperands()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        (string Query, int[] ExpectedRows)[] cases =
        [
            ("diff=3", []),
            ("diff=030", [0]),
            ("diff=30x", []),
        ];

        foreach (var (query, expectedRows) in cases)
        {
            var snapshot = Build(rows, query);

            Assert.Equal(expectedRows, snapshot.VisibleRows.Select(static row => row.RowIndex));
        }
    }

    [Fact]
    public void Build_UnknownSearchFieldsMatchTheirValueAgainstTheRowTokens()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        (string Query, int[] ExpectedRows)[] cases =
        [
            ("label=\"EDGE TRANSFER\"", [0]),
            ("label=210", [0]),
            ("label=missing", []),
        ];

        foreach (var (query, expectedRows) in cases)
        {
            var snapshot = Build(rows, query);

            Assert.Equal(expectedRows, snapshot.VisibleRows.Select(static row => row.RowIndex));
        }
    }

    [Fact]
    public void Build_IncompleteFieldTermsAreSearchedLiterally()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();
        string[] queries = ["note=", "=raw", "\" =raw\"", "\"note= \""];

        foreach (var query in queries)
        {
            var snapshot = Build(rows, query);

            Assert.Same(rows[1], Assert.Single(snapshot.VisibleRows));
        }
    }

    [Fact]
    public void Build_StatusSearch_QuotedValueWithSpacedDash_MatchesTheNoCadRow()
    {
        using var culture = TestCulture.UseEnUs();
        var rows = CreateRows();

        var noCad = Build(rows, "status=\"No - CAD\"");

        Assert.Same(rows[2], Assert.Single(noCad.VisibleRows));
    }

    private static NotchExportSelectionFilterState CreateFilter(string query)
    {
        return new NotchExportSelectionFilterState(
            SelectedVersion: null,
            NotchExportRowDisplayMode.AllRows,
            "All",
            NotchExportSortMode.RowId,
            query,
            ShowSelectedOnly: false,
            new Dictionary<NotchExportColumnFilterField, HashSet<string>>());
    }

    private static NotchExportSelectionProjectionSnapshot Build(
        IReadOnlyList<NotchExportRowItemViewModel> rows,
        string query)
    {
        return NotchExportSelectionProjectionBuilder.Build(rows, CreateFilter(query), Array.Empty<int>(), Array.Empty<int>());
    }

    private static NotchExportRowItemViewModel[] CreateRows()
    {
        NotchTableRow[] rows =
        [
            new(1, 30, 100, 210, new NotchV22Node(30, 80, 31, 10, 32, 10, 0), "edge transfer"),
            new(0, 20, 101, 211, new NotchV22Node(20, 95, 21, 5, 65535, 0, 0), "center transfer note= =raw marker"),
            new(1, 31, 102, null, new NotchV22Node(31, 90, 32, 10, 65535, 0, 0), "orphan transfer raw marker"),
            new(NotchAlgorithmVersion.V21, 0, 20, 103, 212, [20, 128, 65535, 0, 0, 65535, 0, 0, 0], "R=99% legacy transfer"),
            new(0, 22, 104, 213, new NotchV22Node(22, 100, 65535, 0, 65535, 0, 0), "keep anchor"),
        ];
        return rows.Select(static (row, index) => new NotchExportRowItemViewModel(index, row)).ToArray();
    }
}
