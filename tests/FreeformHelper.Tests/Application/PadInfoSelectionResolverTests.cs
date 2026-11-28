// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Verifies pad info selection membership, ordering and instance identity.
/// </summary>
public sealed class PadInfoSelectionResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveCad_NoOrSingleSelection_UsesClickedInstance(bool hasSelection)
    {
        var sourcePad = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 1, 1);
        var clickedPad = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 1, 1);
        CadPad[] sourcePads = [sourcePad];
        IReadOnlyCollection<int> selectedIds = hasSelection ? new[] { sourcePad.Id } : Array.Empty<int>();

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIds, clickedPad);

        Assert.Same(clickedPad, Assert.Single(result));
    }

    [Fact]
    public void ResolveCad_ClickOutsideMultiSelection_UsesOnlyClickedPad()
    {
        var first = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 1, 1);
        var second = TestGeometryFactory.CreateCadPad(2, "Copper", 1, 0, 2, 1);
        var clickedPad = TestGeometryFactory.CreateCadPad(3, "Copper", 2, 0, 3, 1);
        CadPad[] sourcePads = [first, clickedPad, second];
        HashSet<int> selectedIds = [first.Id, second.Id];

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIds, clickedPad);

        Assert.Same(clickedPad, Assert.Single(result));
    }

    [Fact]
    public void ResolveCad_ClickInsideMultiSelection_PreservesSourceOrderAndInstances()
    {
        var first = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 1, 1);
        var second = TestGeometryFactory.CreateCadPad(2, "Copper", 1, 0, 2, 1);
        var unselected = TestGeometryFactory.CreateCadPad(3, "Copper", 2, 0, 3, 1);
        var clickedPad = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 1, 1);
        CadPad[] sourcePads = [second, unselected, first, second];
        HashSet<int> selectedIds = [first.Id, second.Id];

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIds, clickedPad);

        Assert.Collection(
            result,
            pad => Assert.Same(second, pad),
            pad => Assert.Same(first, pad),
            pad => Assert.Same(second, pad));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveRegular_NoOrSingleSelection_UsesClickedInstance(bool hasSelection)
    {
        var sourcePad = TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1);
        var clickedPad = TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1);
        RegularPad[] sourcePads = [sourcePad];
        IReadOnlyCollection<int> selectedIndices = hasSelection ? new[] { sourcePad.Index } : Array.Empty<int>();

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIndices, clickedPad);

        Assert.Same(clickedPad, Assert.Single(result));
    }

    [Fact]
    public void ResolveRegular_ClickOutsideMultiSelection_UsesOnlyClickedPad()
    {
        var first = TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1);
        var second = TestGeometryFactory.CreateRegularPad(0, 1, 2, 1, 0, 2, 1);
        var clickedPad = TestGeometryFactory.CreateRegularPad(0, 2, 3, 2, 0, 3, 1);
        RegularPad[] sourcePads = [first, clickedPad, second];
        HashSet<int> selectedIndices = [first.Index, second.Index];

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIndices, clickedPad);

        Assert.Same(clickedPad, Assert.Single(result));
    }

    [Fact]
    public void ResolveRegular_ClickInsideMultiSelection_PreservesSourceOrderAndInstances()
    {
        var first = TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1);
        var second = TestGeometryFactory.CreateRegularPad(0, 1, 2, 1, 0, 2, 1);
        var unselected = TestGeometryFactory.CreateRegularPad(0, 2, 3, 2, 0, 3, 1);
        var clickedPad = TestGeometryFactory.CreateRegularPad(0, 0, 1, 0, 0, 1, 1);
        RegularPad[] sourcePads = [second, unselected, first, second];
        HashSet<int> selectedIndices = [first.Index, second.Index];

        var result = PadInfoSelectionResolver.Resolve(sourcePads, selectedIndices, clickedPad);

        Assert.Collection(
            result,
            pad => Assert.Same(second, pad),
            pad => Assert.Same(first, pad),
            pad => Assert.Same(second, pad));
    }
}
