// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.CadEditing;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfVisibilityRulesTests
{
    [Fact]
    public void SelectVisibleSelectedCadIdsPreservesSelectionOrderAndDuplicates()
    {
        int[] selectedCadIds = [8, 3, 8, 1, 3, 5];

        var ids = DxfVisibilityRules.SelectVisibleSelectedCadIds(selectedCadIds, static id => id == 3);

        Assert.Collection(ids,
            static id => Assert.Equal(8, id),
            static id => Assert.Equal(8, id),
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(5, id));
    }

    [Fact]
    public void SelectManualHiddenCadPadIdsDropsCombinedSourcesAndReturnsDistinctAscendingIds()
    {
        int[] hiddenCadPadIds = [8, 3, 8, 1, 3, 5];
        var combinedCad = new CombinedCadState();
        combinedCad.GroupIdBySourceCadId[3] = 7;

        var ids = DxfVisibilityRules.SelectManualHiddenCadPadIds(hiddenCadPadIds, combinedCad);

        Assert.Collection(ids,
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(5, id),
            static id => Assert.Equal(8, id));
    }

    [Fact]
    public void SelectRestorableHiddenCadPadIdsKeepsDistinctAscendingManualHiddenIds()
    {
        int[] cadPadIds = [8, 3, 8, 1, 5, 2];
        HashSet<int> hiddenCadPadIds = [1, 2, 3, 8];
        var combinedCad = new CombinedCadState();
        combinedCad.GroupIdBySourceCadId[3] = 7;

        var ids = DxfVisibilityRules.SelectRestorableHiddenCadPadIds(cadPadIds, hiddenCadPadIds, combinedCad);

        Assert.Collection(ids,
            static id => Assert.Equal(1, id),
            static id => Assert.Equal(2, id),
            static id => Assert.Equal(8, id));
    }

    [Theory]
    [InlineData(true, 2, 6)]
    [InlineData(false, 4, 8)]
    public void SelectChangedDuplicateCadPadIdsKeepsDistinctAscendingIdsOutsideTargetState(bool autoHide, int first, int second)
    {
        int[] cadPadIds = [8, 6, 2, 4, 6, 8, 2, 99];
        var duplicates = new Dictionary<int, int> { [2] = 1, [4] = 1, [6] = 1, [8] = 1 };
        HashSet<int> autoHiddenIds = [4, 8, 99];

        var ids = DxfVisibilityRules.SelectChangedDuplicateCadPadIds(cadPadIds, duplicates, autoHiddenIds, autoHide);

        Assert.Collection(ids,
            id => Assert.Equal(first, id),
            id => Assert.Equal(second, id));
        Assert.Collection(autoHiddenIds.Order(),
            static id => Assert.Equal(4, id),
            static id => Assert.Equal(8, id),
            static id => Assert.Equal(99, id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectChangedDuplicateCadPadIdsWithoutDuplicateIdsIsEmpty(bool autoHide)
    {
        int[] cadPadIds = [99, 99];
        var duplicates = new Dictionary<int, int> { [2] = 1 };
        HashSet<int> autoHiddenIds = [2];

        var ids = DxfVisibilityRules.SelectChangedDuplicateCadPadIds(cadPadIds, duplicates, autoHiddenIds, autoHide);

        Assert.Empty(ids);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 2)]
    public void SelectChangedDuplicateCadPadIdsAlreadyInTargetStateIsEmpty(bool autoHide, int cadPadId)
    {
        int[] cadPadIds = [cadPadId, cadPadId];
        var duplicates = new Dictionary<int, int> { [2] = 1, [4] = 1 };
        HashSet<int> autoHiddenIds = [4];

        var ids = DxfVisibilityRules.SelectChangedDuplicateCadPadIds(cadPadIds, duplicates, autoHiddenIds, autoHide);

        Assert.Empty(ids);
    }

    [Fact]
    public void SelectRegularSourceLayerPadsToAppendFiltersInCadOrderWithoutChangingVisiblePads()
    {
        var visiblePad = CreatePad(1, "Regular");
        var firstAppend = CreatePad(8, "rEgUlAr");
        var secondAppend = CreatePad(2, "REGULAR");
        var thirdAppend = CreatePad(4, "Regular");
        var visiblePads = new List<CadPad> { visiblePad };
        var cad = new CadPadSet(
        [
            CreatePad(1, "Regular"), CreatePad(3, "Regular"), CreatePad(5, "Other"),
            firstAppend, secondAppend, CreatePad(8, "Regular"), thirdAppend,
        ]);

        var pads = DxfVisibilityRules.SelectRegularSourceLayerPadsToAppend(visiblePads, cad, "Regular", static id => id == 3);

        Assert.Collection(pads,
            pad => Assert.Same(firstAppend, pad),
            pad => Assert.Same(secondAppend, pad),
            pad => Assert.Same(thirdAppend, pad));
        Assert.Same(visiblePad, Assert.Single(visiblePads));
    }

    private static CadPad CreatePad(int id, string layer) => new(id, "Pad", layer, new Polygon2(
    [
        new Point2(0, 0), new Point2(2, 0), new Point2(2, 1), new Point2(0, 1),
    ]));
}
