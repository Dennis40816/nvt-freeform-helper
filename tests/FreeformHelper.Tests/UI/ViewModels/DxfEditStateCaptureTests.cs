// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditStateCaptureTests
{
    private static readonly int[] _expectedSortedSourceCadIds = [1, 2, 5];
    private static readonly int[] _expectedOrderedGroupIds = [2, 7];
    private static readonly int[] _expectedUnrestoredSourceCadIds = [5, 2, 5];
    private static readonly int[] _expectedGroupIdsAfterCollisions = [2, 7, 6, 8, 9, 10];
    private static readonly int[] _expectedGroupIdsWithZeroIds = [0, 0, 1, 2];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LayerOverridesWithMissingCadOrBaselineAreEmpty(bool hasCad)
    {
        var pads = new CadPadSet([CreatePad(1)]);

        var overrides = DxfEditStateCapture.CapturePersistedCadLayerOverrides(
            hasCad ? pads : null, hasCad ? null : pads);

        Assert.Empty(overrides);
    }

    [Theory]
    [InlineData("Original")]
    [InlineData("original")]
    public void LayerOverridesIgnoreUnchangedAndCaseOnlyChangedLayers(string currentLayer)
    {
        var cad = new CadPadSet([CreatePad(1, currentLayer)]);
        var baseline = new CadPadSet([CreatePad(1)]);

        var overrides = DxfEditStateCapture.CapturePersistedCadLayerOverrides(cad, baseline);

        Assert.Empty(overrides);
    }

    [Fact]
    public void LayerOverridesCaptureCurrentLayerForChangedPad()
    {
        var cad = new CadPadSet([CreatePad(1, "Changed")]);
        var baseline = new CadPadSet([CreatePad(1)]);

        var overrides = DxfEditStateCapture.CapturePersistedCadLayerOverrides(cad, baseline);

        var changed = Assert.Single(overrides);
        Assert.Equal(1, changed.Key);
        Assert.Equal("Changed", changed.Value);
    }

    [Fact]
    public void LayerOverridesIgnorePadsAbsentFromBaseline()
    {
        var cad = new CadPadSet([CreatePad(2, "Changed")]);
        var baseline = new CadPadSet([CreatePad(1)]);

        var overrides = DxfEditStateCapture.CapturePersistedCadLayerOverrides(cad, baseline);

        Assert.Empty(overrides);
    }

    [Fact]
    public void LayerOverridesUseFirstBaselinePadWithDuplicateId()
    {
        var cad = new CadPadSet([CreatePad(1, "Changed")]);
        var baseline = new CadPadSet([CreatePad(1), CreatePad(1, "Changed")]);

        var overrides = DxfEditStateCapture.CapturePersistedCadLayerOverrides(cad, baseline);

        var changed = Assert.Single(overrides);
        Assert.Equal(1, changed.Key);
        Assert.Equal("Changed", changed.Value);
    }

    [Fact]
    public void CombinedGroupsWithoutCadOrUnrestoredGroupsAreEmpty()
    {
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([1], [10]);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(null, combined, []);

        Assert.Empty(groups);
    }

    [Fact]
    public void CombinedGroupsCaptureSortedDistinctSourcesAndPresentOutputSnapshotsInOutputOrder()
    {
        var cad = new CadPadSet([CreatePad(10), CreatePad(12, "Combined")]);
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([5, 2, 5, 1], [12, 99, 10]);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(cad, combined, []);

        var group = Assert.Single(groups);
        Assert.Equal(7, group.GroupId);
        Assert.Equal(_expectedSortedSourceCadIds, group.SourceCadIds);
        Assert.Collection(group.OutputPads,
            pad => AssertSnapshot(pad, 12, "Pad 12", "Combined"),
            pad => AssertSnapshot(pad, 10, "Pad 10", "Original"));
    }

    [Fact]
    public void CombinedGroupsAreOrderedByGroupId()
    {
        var cad = new CadPadSet([CreatePad(10), CreatePad(12)]);
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([1], [10]);
        combined.Groups[2] = new CombinedCadGroupState([3], [12]);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(cad, combined, []);

        Assert.Equal(_expectedOrderedGroupIds, groups.Select(static group => group.GroupId));
    }

    [Fact]
    public void CombinedGroupsWithoutSourcesAreSkipped()
    {
        var cad = new CadPadSet([CreatePad(10)]);
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([], [10]);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(cad, combined, []);

        Assert.Empty(groups);
    }

    [Fact]
    public void CombinedGroupsWithAllOutputPadsMissingAreSkipped()
    {
        var cad = new CadPadSet([CreatePad(10)]);
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([1], [98, 99]);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(cad, combined, []);

        Assert.Empty(groups);
    }

    [Fact]
    public void UnrestoredGroupWithFreeIdKeepsIdAndContentsWithoutCad()
    {
        var kept = CreateUnrestoredGroup(6);

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(null, new CombinedCadState(), [kept]);

        var group = Assert.Single(groups);
        Assert.Equal(6, group.GroupId);
        Assert.Equal(_expectedUnrestoredSourceCadIds, group.SourceCadIds);
        AssertSnapshot(Assert.Single(group.OutputPads), 20, "Kept output", "Saved");
    }

    [Fact]
    public void UnrestoredGroupsWithReusedIdsContinueAfterMaximumUsedId()
    {
        var cad = new CadPadSet([CreatePad(10), CreatePad(12)]);
        var combined = new CombinedCadState();
        combined.Groups[7] = new CombinedCadGroupState([1], [10]);
        combined.Groups[2] = new CombinedCadGroupState([3], [12]);
        ProjectDxfCombinedCadGroup[] unrestored =
        [
            CreateUnrestoredGroup(6), CreateUnrestoredGroup(2),
            CreateUnrestoredGroup(7), CreateUnrestoredGroup(8),
        ];

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(cad, combined, unrestored);

        Assert.Equal(_expectedGroupIdsAfterCollisions, groups.Select(static group => group.GroupId));
        foreach (var group in groups.Skip(2))
        {
            Assert.Equal(_expectedUnrestoredSourceCadIds, group.SourceCadIds);
            AssertSnapshot(Assert.Single(group.OutputPads), 20, "Kept output", "Saved");
        }
    }

    [Fact]
    public void UnrestoredGroupsWithZeroIdKeepZeroAndDoNotReserveAnId()
    {
        ProjectDxfCombinedCadGroup[] unrestored =
        [
            CreateUnrestoredGroup(0), CreateUnrestoredGroup(0),
            CreateUnrestoredGroup(1), CreateUnrestoredGroup(1),
        ];

        var groups = DxfEditStateCapture.CapturePersistedCombinedCadGroups(null, new CombinedCadState(), unrestored);

        Assert.Equal(_expectedGroupIdsWithZeroIds, groups.Select(static group => group.GroupId));
        foreach (var group in groups)
        {
            Assert.Equal(_expectedUnrestoredSourceCadIds, group.SourceCadIds);
            AssertSnapshot(Assert.Single(group.OutputPads), 20, "Kept output", "Saved");
        }
    }

    private static CadPad CreatePad(int id, string layer = "Original")
        => new(id, $"Pad {id}", layer, new Polygon2(
        [
            new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1),
        ]));

    private static ProjectDxfCombinedCadGroup CreateUnrestoredGroup(int groupId) => new()
    {
        GroupId = groupId,
        SourceCadIds = [5, 2, 5],
        OutputPads =
        [
            new ProjectCadPadSnapshot
            {
                Id = 20,
                Name = "Kept output",
                Layer = "Saved",
                Vertices = [new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1)],
            },
        ],
    };

    private static void AssertSnapshot(ProjectCadPadSnapshot actual, int expectedId, string expectedName, string expectedLayer)
    {
        Assert.Equal(expectedId, actual.Id);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(expectedLayer, actual.Layer);
        Assert.Equal(new[] { new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1) }, actual.Vertices);
    }
}
