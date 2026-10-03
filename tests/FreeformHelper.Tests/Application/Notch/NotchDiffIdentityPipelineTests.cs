using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDiffIdentityPipelineTests
{
    private static readonly int[] DiffIndicesDuplicate = [10, 10, 11];
    private static readonly int[] SuppressedRegularPads = [1];

    [Fact]
    public void BuildProjectionContract_WhenRawDiffHasConflicts_TracksConflictAndKeepsRawTarget()
    {
        var rowA = new NotchTableRow(
            icIndex: 0,
            diffIndex: 20,
            regularPadIndex: 0,
            cadPadId: 200,
            v22Node: new NotchV22Node(20, 94, 21, 44, 65535, 0, 0),
            comment: "a");
        var rowB = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 1,
            cadPadId: 201,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "b");
        var rowC = new NotchTableRow(
            icIndex: 0,
            diffIndex: 21,
            regularPadIndex: 2,
            cadPadId: 202,
            v22Node: new NotchV22Node(21, 100, 65535, 0, 65535, 0, 0),
            comment: "c");

        var contract = NotchDiffIdentityPipeline.BuildProjectionContract(
            new[] { rowA, rowB, rowC },
            new Dictionary<int, int>
            {
                [200] = 24,
                [201] = 25,
                [202] = 26,
            });

        Assert.Equal(24, contract.ResolveAnchorDiff(rowA));
        Assert.Equal(1, contract.ConflictedRawDiffKeyCount);
        Assert.Equal(21, contract.ResolveTargetDiff(0, 21));
        Assert.Contains(new NotchDiffKey(0, 21), contract.ConflictedRawDiffKeys);
    }

    [Fact]
    public void BuildActiveDiffBaseline_WhenActiveDiffDuplicatesExist_MergesValuesAndReportsContributors()
    {
        var regularPads = BuildLinearPads(diffIndices: DiffIndicesDuplicate);
        var activeRegularPadIds = regularPads.Select(static pad => pad.RegularPadId).ToHashSet();
        var baseline = NotchDiffIdentityPipeline.BuildActiveDiffBaseline(
            regularPads,
            activeRegularPadIds,
            new Dictionary<int, double>
            {
                [0] = 120d,
                [1] = 80d,
                [2] = 30d,
            });

        Assert.Equal(200d, baseline.ValueByDiffKey[new NotchDiffKey(0, 10)]);
        var duplicate = Assert.Single(baseline.DuplicateResolutions);
        Assert.Equal(new NotchDiffKey(0, 10), duplicate.DiffKey);
        Assert.Equal(0, duplicate.PrimaryRegularPadId);
        Assert.Equal(SuppressedRegularPads, duplicate.SuppressedRegularPadIds);
    }

    private static List<RegularPad> BuildLinearPads(int[] diffIndices)
    {
        var pads = new List<RegularPad>(diffIndices.Length);
        for (var col = 0; col < diffIndices.Length; col++)
        {
            var pad = new RegularPad(
                row: 0,
                col: col,
                index: col,
                polygon: new Polygon2(new[]
                {
                    new Point2(col, 0),
                    new Point2(col + 1, 0),
                    new Point2(col + 1, 1),
                    new Point2(col, 1),
                }))
            {
                IcIndex = 0,
                DiffIndex = diffIndices[col],
            };
            pads.Add(pad);
        }

        return pads;
    }
}
