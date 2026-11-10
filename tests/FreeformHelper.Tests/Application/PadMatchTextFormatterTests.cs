// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Globalization;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadMatchTextFormatterTests
{
    public static TheoryData<int[], string> CompactCases => new()
    {
        { [], "-" },
        { [7], "7" },
        { [3, 1], "3, 1" },
        { [3, 1, 3], "3, 1, 3" },
        { [4, 1, 3, 2], "4, 1, 3 (+1)" },
        { [5, 4, 3, 2, 1], "5, 4, 3 (+2)" },
    };

    public static TheoryData<int[], int[], string> RegularCases => new()
    {
        { [], [], "Unmatched" },
        { [], [12], "Unmatched" },
        { [7], [12], "CAD idx 12 (CAD id 7)" },
        { [7], [], "CAD id 7" },
        { [1, 2, 3], [10, 20, 30], "CAD idx 10, 20, 30 (CAD id 1, 2, 3)" },
        { [1, 2, 3, 4], [10, 20, 30, 40], "CAD idx 10, 20, 30 (+1) (CAD id 1, 2, 3 (+1))" },
        { [1, 2, 3, 4, 5], [], "CAD id 1, 2, 3 (+2)" },
        { [1, 2, 3], [10, 20], "CAD idx 10, 20 (CAD id 1, 2, 3)" },
        { [9, 3, 7], [10, 20, 30], "CAD idx 10, 20, 30 (CAD id 9, 3, 7)" },
    };

    public static TheoryData<int[], int[], bool, string> CadCases => new()
    {
        { [], [7], true, "Unmatched" },
        { [7], [], true, "IC2/diff107" },
        { [7], [7], false, "regId 7" },
        { [30, 10, 20], [20, 30, 10], true, "IC2/diff120 (+2)" },
        { [1, 2, 3, 4], [4, 3, 2, 1], true, "IC2/diff104 (+3)" },
        { [1, 2, 3, 4, 5], [5, 4, 3, 2, 1], true, "IC2/diff105 (+4)" },
        { [7, 7], [7, 7], true, "IC2/diff107" },
        { [9, 3, 7], [], true, "IC2/diff109 (+2)" },
        { [3], [9, 9], true, "IC2/diff109 (+1)" },
    };

    [Theory]
    [MemberData(nameof(CompactCases))]
    public void CompactListPreservesOrderDuplicatesAndCount(int[] values, string expected)
    {
        using var culture = TestCulture.UseEnUs();

        var actual = PadMatchTextFormatter.CompactList(values);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(RegularCases))]
    public void RegularMatchTextUsesPreparedIndices(int[] cadIds, int[] dxfIndices, string expected)
    {
        using var culture = TestCulture.UseEnUs();

        var actual = PadMatchTextFormatter.RegularMatchText(cadIds, dxfIndices);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(CadCases))]
    public void CadMatchTextPreservesLinkPriorityAndDistinctCount(
        int[] regularIds, int[] linkedIds, bool hasIdentity, string expected)
    {
        using var culture = TestCulture.UseEnUs();

        var actual = PadMatchTextFormatter.CadMatchText(
            regularIds, linkedIds, id => hasIdentity ? (1, id + 100) : null);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CadMatchTextWithoutLookupUsesRegularIdentity()
    {
        var actual = PadMatchTextFormatter.CadMatchText([7], [7], getRegularPadIcDiff: null);

        Assert.Equal("regId 7", actual);
    }

    [Fact]
    public void CadMatchTextWithoutMatchesDoesNotReadLinksOrLookup()
    {
        var linkedIds = Enumerable.Repeat(0, 1).Select<int, int>(static _ => throw new InvalidOperationException());

        var actual = PadMatchTextFormatter.CadMatchText(
            [], linkedIds, static _ => throw new InvalidOperationException());

        Assert.Equal("Unmatched", actual);
    }

    [Fact]
    public void CadMatchTextLooksUpOnlyTheFirstLinkedIdentity()
    {
        var lookedUpIds = new List<int>();

        var actual = PadMatchTextFormatter.CadMatchText([3, 7], [7, 3], id =>
        {
            lookedUpIds.Add(id);
            return (0, 42);
        });

        Assert.Equal("IC1/diff42 (+1)", actual);
        Assert.Equal(7, Assert.Single(lookedUpIds));
    }

    [Theory]
    [InlineData(3, "~1, ~2, ~3")]
    [InlineData(4, "~1, ~2, ~3 (+1)")]
    public void CompactListKeepsCurrentCultureForBothLengthBranches(int count, string expected)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NegativeSign = "~";
        var values = Enumerable.Range(1, count).Select(static value => -value).ToArray();
        try
        {
            CultureInfo.CurrentCulture = culture;

            var actual = PadMatchTextFormatter.CompactList(values);

            Assert.Equal(expected, actual);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
