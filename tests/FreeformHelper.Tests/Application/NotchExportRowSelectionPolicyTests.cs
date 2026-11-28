// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using Xunit;
using RowChoice = FreeformHelper.Application.Services.NotchExportRowSelectionPolicy.RowChoice;

namespace FreeformHelper.Tests;

public sealed class NotchExportRowSelectionPolicyTests
{
    [Theory]
    [InlineData(NotchAlgorithmVersion.V21, null, true)]
    [InlineData(NotchAlgorithmVersion.V22, null, true)]
    [InlineData(NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V21, true)]
    [InlineData(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21, false)]
    [InlineData(NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22, false)]
    [InlineData(NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V22, true)]
    public void MatchesVersionAcceptsEveryVersionWithoutScopeAndOnlyTheScopedVersion(
        NotchAlgorithmVersion rowVersion,
        NotchAlgorithmVersion? scope,
        bool expected)
    {
        Assert.Equal(expected, NotchExportRowSelectionPolicy.MatchesVersion(rowVersion, scope));
    }

    [Theory]
    [InlineData(true, NotchAlgorithmVersion.V21, null, true)]
    [InlineData(true, NotchAlgorithmVersion.V22, null, true)]
    [InlineData(true, NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V21, true)]
    [InlineData(true, NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21, false)]
    [InlineData(true, NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22, false)]
    [InlineData(true, NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V22, true)]
    [InlineData(false, NotchAlgorithmVersion.V21, null, false)]
    [InlineData(false, NotchAlgorithmVersion.V22, null, false)]
    [InlineData(false, NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V21, false)]
    [InlineData(false, NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V21, false)]
    [InlineData(false, NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V22, false)]
    [InlineData(false, NotchAlgorithmVersion.V22, NotchAlgorithmVersion.V22, false)]
    public void IsEligibleRequiresCheckedBeforeMatchingTheVersionScope(
        bool isChecked,
        NotchAlgorithmVersion rowVersion,
        NotchAlgorithmVersion? scope,
        bool expected)
    {
        Assert.Equal(expected, NotchExportRowSelectionPolicy.IsEligible(isChecked, rowVersion, scope));
    }

    [Fact]
    public void BuildFiltersUncheckedRowsAndStablyOrdersOriginalInstancesByOrdinal()
    {
        var first = CreateRow(NotchAlgorithmVersion.V22, 30);
        var before = CreateRow(NotchAlgorithmVersion.V21, 50);
        var equal = CreateRow(NotchAlgorithmVersion.V22, 30);
        var uncheckedRow = CreateRow(NotchAlgorithmVersion.V22, 10);
        RowChoice[] choices =
        [
            new(20, first, true),
            new(10, before, true),
            new(20, equal, true),
            new(0, uncheckedRow, false),
        ];

        var table = NotchExportRowSelectionPolicy.Build(choices, scope: null);

        Assert.NotSame(first, equal);
        Assert.Equal(first.Values, equal.Values);
        Assert.Equal(3, table.Rows.Count);
        Assert.Same(before, table.Rows[0]);
        Assert.Same(first, table.Rows[1]);
        Assert.Same(equal, table.Rows[2]);
    }

    [Fact]
    public void BuildWithVersionScopeKeepsOnlyMatchingCheckedRowsInOrdinalOrder()
    {
        var second = CreateRow(NotchAlgorithmVersion.V22, 10);
        var legacy = CreateRow(NotchAlgorithmVersion.V21, 20);
        var first = CreateRow(NotchAlgorithmVersion.V22, 30);
        var uncheckedRow = CreateRow(NotchAlgorithmVersion.V22, 40);
        RowChoice[] choices =
        [
            new(20, second, true),
            new(0, legacy, true),
            new(10, first, true),
            new(5, uncheckedRow, false),
        ];

        var table = NotchExportRowSelectionPolicy.Build(choices, NotchAlgorithmVersion.V22);

        Assert.Equal(2, table.Rows.Count);
        Assert.Same(first, table.Rows[0]);
        Assert.Same(second, table.Rows[1]);
    }

    [Fact]
    public void BuildEmptyInputReturnsEmptyRowsAndDefaultMetadata()
    {
        var table = NotchExportRowSelectionPolicy.Build(Array.Empty<RowChoice>(), scope: null);

        Assert.Empty(table.Rows);
        Assert.Same(NotchToFullCoverageAudit.Empty, table.ToFullCoverageAudit);
        Assert.Same(NotchGenerationPhaseTimings.Empty, table.GenerationPhaseTimings);
    }

    [Fact]
    public void BuildWithNoMatchingCheckedRowsReturnsEmptyTable()
    {
        RowChoice[] choices =
        [
            new(0, CreateRow(NotchAlgorithmVersion.V21, 20), true),
            new(1, CreateRow(NotchAlgorithmVersion.V22, 30), false),
        ];

        var table = NotchExportRowSelectionPolicy.Build(choices, NotchAlgorithmVersion.V22);

        Assert.Empty(table.Rows);
    }

    private static NotchTableRow CreateRow(NotchAlgorithmVersion version, int diff)
    {
        int[] values = version == NotchAlgorithmVersion.V22
            ? [diff, 95, diff + 1, 5, 65535, 0, 0]
            : [diff, 128, 65535, 0, 0, 65535, 0, 0, 0];
        return new NotchTableRow(version, 0, diff, 100 + diff, 200 + diff, values, "row");
    }
}
