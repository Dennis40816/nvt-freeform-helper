// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfCombinedGroupRulesTests
{
    [Fact]
    public void CollectGroupMemberIdsUnionsKnownGroupsAndSkipsUnknownGroups()
    {
        var combinedCad = new CombinedCadState();
        combinedCad.Groups[1] = new CombinedCadGroupState([1, 2], [10, 11]);
        combinedCad.Groups[3] = new CombinedCadGroupState([2, 4], [11, 12]);

        var (outputIds, sourceIds) = DxfCombinedGroupRules.CollectGroupMemberIds(combinedCad, [99, 3, 1, 3]);

        Assert.Collection(outputIds.Order(),
            static id => Assert.Equal(10, id),
            static id => Assert.Equal(11, id),
            static id => Assert.Equal(12, id));
        Assert.Collection(sourceIds.Order(),
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(2, id),
            static id => Assert.Equal(4, id));
    }

    [Fact]
    public void CollectGroupMemberIdsWithNoGroupsReturnsEmptySets()
    {
        var combinedCad = new CombinedCadState();
        combinedCad.Groups[1] = new CombinedCadGroupState([1], [10]);

        var (outputIds, sourceIds) = DxfCombinedGroupRules.CollectGroupMemberIds(combinedCad, []);

        Assert.Empty(outputIds);
        Assert.Empty(sourceIds);
    }

    [Fact]
    public void RemoveGroupMembersRemovesMapsAndCountsOnlyHiddenSources()
    {
        var combinedCad = new CombinedCadState();
        combinedCad.GroupIdByOutputCadId[10] = 1;
        combinedCad.GroupIdByOutputCadId[11] = 1;
        combinedCad.GroupIdByOutputCadId[99] = 7;
        combinedCad.GroupIdBySourceCadId[1] = 1;
        combinedCad.GroupIdBySourceCadId[2] = 1;
        combinedCad.GroupIdBySourceCadId[5] = 7;
        HashSet<int> hiddenCadPadIds = [10, 1, 99];

        var restored = DxfCombinedGroupRules.RemoveGroupMembers(combinedCad, hiddenCadPadIds, [10, 11], [1, 2]);

        Assert.Equal(1, restored);
        Assert.Equal(99, Assert.Single(hiddenCadPadIds));
        var remainingOutput = Assert.Single(combinedCad.GroupIdByOutputCadId);
        Assert.Equal(99, remainingOutput.Key);
        Assert.Equal(7, remainingOutput.Value);
        var remainingSource = Assert.Single(combinedCad.GroupIdBySourceCadId);
        Assert.Equal(5, remainingSource.Key);
        Assert.Equal(7, remainingSource.Value);
    }

    [Fact]
    public void AllocatePadIdWithoutCadReturnsAndIncrementsNextPadId()
    {
        var combinedCad = new CombinedCadState { NextPadId = 10 };

        var id = DxfCombinedGroupRules.AllocatePadId(null, combinedCad);

        Assert.Equal(10, id);
        Assert.Equal(11, combinedCad.NextPadId);
    }

    [Fact]
    public void AllocatePadIdSkipsIdsAlreadyUsedByCad()
    {
        var cad = new CadPadSet([CreatePad(10), CreatePad(11), CreatePad(13)]);
        var combinedCad = new CombinedCadState { NextPadId = 10 };

        var id = DxfCombinedGroupRules.AllocatePadId(cad, combinedCad);

        Assert.Equal(12, id);
        Assert.Equal(13, combinedCad.NextPadId);
    }

    [Fact]
    public void CreatePadsAllocatesNamesLayersAndGroupMappingsInPolygonOrder()
    {
        var cad = new CadPadSet([CreatePad(10), CreatePad(12), CreatePad(13)]);
        var combinedCad = new CombinedCadState { NextPadId = 10 };
        var firstPolygon = CreatePolygon();
        var secondPolygon = CreatePolygon(3);

        var pads = DxfCombinedGroupRules.CreatePads("Combined", [firstPolygon, secondPolygon], 7, cad, combinedCad);

        Assert.Collection(pads,
            pad =>
            {
                Assert.Equal(11, pad.Id);
                Assert.Equal("COMB_11", pad.Name);
                Assert.Equal("Combined", pad.Layer);
                Assert.Same(firstPolygon, pad.Polygon);
            },
            pad =>
            {
                Assert.Equal(14, pad.Id);
                Assert.Equal("COMB_14", pad.Name);
                Assert.Equal("Combined", pad.Layer);
                Assert.Same(secondPolygon, pad.Polygon);
            });
        Assert.Equal(15, combinedCad.NextPadId);
        Assert.Equal(2, combinedCad.GroupIdByOutputCadId.Count);
        Assert.Equal(7, combinedCad.GroupIdByOutputCadId[11]);
        Assert.Equal(7, combinedCad.GroupIdByOutputCadId[14]);
    }

    private static Polygon2 CreatePolygon(double x = 0) => new(
    [
        new Point2(x, 0), new Point2(x + 2, 0), new Point2(x + 2, 1), new Point2(x, 1),
    ]);

    private static CadPad CreatePad(int id) => new(id, "Pad", "Source", CreatePolygon());
}
