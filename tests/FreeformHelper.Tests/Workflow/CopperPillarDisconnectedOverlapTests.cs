// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CopperPillarDisconnectedOverlapTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProjectCadOutputToRegularGrid_UShapedPad_AccumulatesBothDisconnectedCopperRegions(bool reverseVertices)
    {
        var vertices = new[]
        {
            new Point2(-2d, -2d), new Point2(2d, -2d), new Point2(2d, 2d), new Point2(0.5d, 2d),
            new Point2(0.5d, -1.5d), new Point2(-0.5d, -1.5d), new Point2(-0.5d, 2d), new Point2(-2d, 2d),
        };
        if (reverseVertices)
        {
            Array.Reverse(vertices);
        }

        var result = CopperPillarSimulationService.ProjectCadOutputToRegularGrid(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[] { new CadPad(100, "U-shaped copper", "SENSOR", new Polygon2(vertices)) },
            new Dictionary<int, int> { [100] = 0 }, new Dictionary<int, int> { [100] = 0 },
            new CopperPillarSimulationRequest(0d, 0d, 2d, 100d, BaselineValue: 7d, CircleSegmentCount: 256));

        Assert.True(result.IsCompatible);
        Assert.Null(result.Diagnostic);
        var cell = Assert.Single(result.Cells);
        Assert.Equal(0, cell.RegularPadId);
        // The connector is outside the circle. Its two caps cover about 39.10% together;
        // allow for the inscribed circle approximation and six-digit clipping precision.
        Assert.InRange(cell.Value, 46.08d, 46.11d);
    }
}
