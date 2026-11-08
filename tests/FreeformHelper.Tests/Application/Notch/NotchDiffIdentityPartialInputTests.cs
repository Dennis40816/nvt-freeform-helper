// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDiffIdentityPartialInputTests
{
    private static readonly int[] _suppressedPadThree = [3];
    private static readonly int[] _suppressedPadSeven = [7];

    [Fact]
    public void BuildProjectionContract_EmptyOrPartialEvidence_PreservesAvailableAnchorsAndRawFallbacks()
    {
        var mapped = CreateRow(diffIndex: 10, cadPadId: 100);
        var withoutCad = CreateRow(diffIndex: 11, cadPadId: null);
        var unmapped = CreateRow(diffIndex: 12, cadPadId: 101);
        var cadDiffs = new Dictionary<int, int> { [100] = 42 };

        var withoutRows = NotchDiffIdentityPipeline.BuildProjectionContract([], cadDiffs);
        Assert.Empty(withoutRows.ConflictedRawDiffKeys);
        Assert.Equal(42, withoutRows.ResolveAnchorDiff(mapped));

        var withoutAssignments = NotchDiffIdentityPipeline.BuildProjectionContract([mapped], new Dictionary<int, int>());
        Assert.Empty(withoutAssignments.ConflictedRawDiffKeys);
        Assert.Equal(10, withoutAssignments.ResolveAnchorDiff(mapped));

        var partial = NotchDiffIdentityPipeline.BuildProjectionContract([mapped, withoutCad, unmapped], cadDiffs);
        Assert.Empty(partial.ConflictedRawDiffKeys);
        Assert.Equal(42, partial.ResolveAnchorDiff(mapped));
        Assert.Equal(11, partial.ResolveAnchorDiff(withoutCad));
        Assert.Equal(12, partial.ResolveAnchorDiff(unmapped));
    }

    [Fact]
    public void BuildActiveDiffBaseline_ExcludedOrZeroPads_DoNotBecomeDuplicateContributors()
    {
        RegularPad[] pads =
        [
            CreatePad(0, 10), CreatePad(1, 10), CreatePad(2, 10), CreatePad(3, 10), CreatePad(4, 10),
            CreatePad(5, 20), CreatePad(6, 20), CreatePad(7, 20), CreatePad(8, 20),
        ];
        var activeIds = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7 };
        var values = new Dictionary<int, double>
        {
            [0] = 999,
            [1] = 120,
            [2] = 0,
            [3] = 80,
            [4] = 5e-10,
            [6] = 30,
            [7] = 70,
            [8] = 999,
        };

        var baseline = NotchDiffIdentityPipeline.BuildActiveDiffBaseline(pads, activeIds, values);

        Assert.Equal(2, baseline.ValueByDiffKey.Count);
        Assert.Equal(200d, baseline.ValueByDiffKey[new NotchDiffKey(0, 10)]);
        Assert.Equal(100d, baseline.ValueByDiffKey[new NotchDiffKey(0, 20)]);
        Assert.Collection(baseline.DuplicateResolutions,
            resolution =>
            {
                Assert.Equal(new NotchDiffKey(0, 10), resolution.DiffKey);
                Assert.Equal(1, resolution.PrimaryRegularPadId);
                Assert.Equal(_suppressedPadThree, resolution.SuppressedRegularPadIds);
            },
            resolution =>
            {
                Assert.Equal(new NotchDiffKey(0, 20), resolution.DiffKey);
                Assert.Equal(6, resolution.PrimaryRegularPadId);
                Assert.Equal(_suppressedPadSeven, resolution.SuppressedRegularPadIds);
            });
    }

    private static NotchTableRow CreateRow(int diffIndex, int? cadPadId)
    {
        return new NotchTableRow(
            icIndex: 0, diffIndex: diffIndex, regularPadIndex: diffIndex, cadPadId: cadPadId,
            v22Node: new NotchV22Node(diffIndex, 100, 65535, 0, 65535, 0, 0), comment: "partial mapping");
    }

    private static RegularPad CreatePad(int id, int diffIndex)
    {
        return new RegularPad(0, id, id, new Polygon2(
        [
            new Point2(id, 0), new Point2(id + 1, 0), new Point2(id + 1, 1), new Point2(id, 1),
        ]))
        { IcIndex = 0, DiffIndex = diffIndex };
    }
}
