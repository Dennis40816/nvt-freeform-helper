// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchComputationInputsTests
{
    [Fact]
    public void HiddenPadsAreDroppedAndVisiblePadsAreOrderedById()
    {
        var pads = new[] { CreateCadPad(30), CreateCadPad(10), CreateCadPad(20) };
        var indices = new Dictionary<int, int>();

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static id => id == 20, null, null, indices.TryGetValue);

        Assert.Equal([pads[1], pads[0]], selected);
    }

    [Fact]
    public void AllHiddenPadsReturnAnEmptyList()
    {
        var indices = new Dictionary<int, int> { [10] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            [CreateCadPad(10)], static _ => true, CreateGrid(), 10, indices.TryGetValue);

        Assert.Empty(selected);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MissingGridOrTargetReturnsAllVisiblePads(bool useGrid, bool useTarget)
    {
        var pads = new[] { CreateCadPad(30, 10, 20), CreateCadPad(10), CreateCadPad(20) };
        var grid = useGrid ? CreateGrid() : null;
        int? target = useTarget ? 10 : null;
        var indices = new Dictionary<int, int> { [10] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static id => id == 20, grid, target, indices.TryGetValue);

        Assert.Equal([pads[1], pads[0]], selected);
    }

    [Fact]
    public void UnresolvedTargetIcReturnsAllVisiblePads()
    {
        var pads = new[] { CreateCadPad(20, 10, 20), CreateCadPad(10) };
        var indices = new Dictionary<int, int>();

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static _ => false, CreateGrid(), 10, indices.TryGetValue);

        Assert.Equal([pads[1], pads[0]], selected);
    }

    [Fact]
    public void TargetIcKeepsOnlyPadsWithQ7PositiveAllocations()
    {
        var pads = new[] { CreateCadPad(30, 10, 20), CreateCadPad(20, 5, 15), CreateCadPad(10) };
        var indices = new Dictionary<int, int> { [10] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static _ => false, CreateGrid(), 10, indices.TryGetValue);

        Assert.Equal([pads[2], pads[1]], selected);
    }

    [Fact]
    public void NoQ7PositiveAllocationOnTargetIcReturnsAllVisiblePads()
    {
        var pads = new[] { CreateCadPad(30, 10, 20), CreateCadPad(20, 0, 2570), CreateCadPad(10, 10, 20) };
        var indices = new Dictionary<int, int> { [10] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static _ => false, CreateGrid(), 10, indices.TryGetValue);

        Assert.Equal([pads[2], pads[1], pads[0]], selected);
    }

    [Fact]
    public void TargetWithQ7ZeroAllocationIsReaddedAndSortedById()
    {
        var pads = new[] { CreateCadPad(30), CreateCadPad(20, 0, 2570), CreateCadPad(10), CreateCadPad(40, 10, 20) };
        var indices = new Dictionary<int, int> { [20] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static _ => false, CreateGrid(), 20, indices.TryGetValue);

        Assert.Equal([pads[2], pads[1], pads[0]], selected);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(99)]
    public void TargetOutsideVisibleListIsNotAdded(int targetCadPadId)
    {
        var pads = new[] { CreateCadPad(30, 10, 20), CreateCadPad(20, 0, 2570), CreateCadPad(10) };
        var indices = new Dictionary<int, int> { [targetCadPadId] = 0 };

        var selected = NotchComputationInputs.SelectCadPadsForComputation(
            pads, static id => id == 20, CreateGrid(), targetCadPadId, indices.TryGetValue);

        Assert.Same(pads[2], Assert.Single(selected));
    }

    private static CadPad CreateCadPad(int id, double minX = 0, double maxX = 10)
    {
        return TestGeometryFactory.CreateCadPad(id, "AA", minX, 0, maxX, 10);
    }

    private static RegularGrid CreateGrid()
    {
        return TestGeometryFactory.CreateRegularGrid(
            rows: 1, cols: 2, cellWidth: 10, cellHeight: 10, icIndexSelector: static (_, col) => col);
    }
}
