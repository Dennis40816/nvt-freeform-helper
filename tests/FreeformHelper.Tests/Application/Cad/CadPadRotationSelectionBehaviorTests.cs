// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadRotationSelectionBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rotate_EmptyOrStaleSelection_ReturnsOriginalCad(bool useStaleSelection)
    {
        var pad = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 4, 2, name: "Selected");
        var cad = new CadPadSet([pad]);
        var targetIds = useStaleSelection ? new HashSet<int> { 99 } : new HashSet<int>();

        var result = CadPadRotationService.Rotate(cad, targetIds, 90);

        Assert.Same(cad, result.Cad);
        Assert.Same(pad, Assert.Single(result.Cad.Pads));
        Assert.Equal(0, result.ChangedCount);
        Assert.Equal(90, result.AppliedDegrees);
    }

    [Theory]
    [InlineData(-270.0, 90.0, 3.0, -1.0)]
    [InlineData(270.0, -90.0, 1.0, 3.0)]
    public void Rotate_WrappedAngle_PreservesDirectionAndUnselectedPads(
        double degrees, double expectedDegrees, double expectedFirstX, double expectedFirstY)
    {
        var selected = TestGeometryFactory.CreateCadPad(1, "Copper", 0, 0, 4, 2, name: "Selected");
        var unselected = TestGeometryFactory.CreateCadPad(2, "Other", 20, 10, 22, 12, name: "Unselected");
        var cad = new CadPadSet([selected, unselected]);

        var result = CadPadRotationService.Rotate(cad, new HashSet<int> { 1 }, degrees);

        Assert.Equal(1, result.ChangedCount);
        Assert.Equal(expectedDegrees, result.AppliedDegrees);
        Assert.Equal(2, result.Cad.Pads.Count);
        var rotated = Assert.Single(result.Cad.Pads, pad => pad.Id == selected.Id);
        Assert.Equal(new Point2(expectedFirstX, expectedFirstY), rotated.Polygon.Vertices[0]);
        Assert.Equal(new Rect2(1, -1, 3, 3), rotated.Bounds);
        Assert.Equal(selected.Area, rotated.Area, 6);
        Assert.Equal(selected.Centroid, rotated.Centroid);
        Assert.Equal(selected.Name, rotated.Name);
        Assert.Equal(selected.Layer, rotated.Layer);
        Assert.Same(unselected, Assert.Single(result.Cad.Pads, pad => pad.Id == unselected.Id));
        Assert.Equal(new Point2(0, 0), selected.Polygon.Vertices[0]);
    }
}
