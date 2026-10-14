using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what the coordinate planner reports as "safe" when the copper pillar cannot fit
/// (issue #64). A pillar wider than the machine span still gets a safe point, which is the span
/// center, and a skewed custom array keeps less clearance from its edges than the pillar radius.
/// These tests describe the current result, not the wanted one. A fix that rejects an infeasible
/// span or an unsupported quadrilateral changes the expected values here and must say so in its
/// pull request.
/// </summary>
public sealed class CoordinatePlannerSafeFeasibilityCharacterizationTests
{
    private const double PillarDiameter = 12d;

    [Fact]
    public void BuildSnapshot_WhenPillarIsWiderThanTheMachine_ReportsTheSpanCenterAsSafe()
    {
        var request = BuildRequest() with
        {
            MachineWidth = 10d,
            MachineHeight = 100d,
            CopperPillarDiameter = PillarDiameter,
            CustomPoints = [new CoordinateCustomPointRequest("probe", "Probe", 2d, 50d)],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(0d, 0d, 10d, 100d), request);

        var point = Assert.Single(snapshot.Points, static p => p.Kind == CoordinatePlannerPointKind.CustomPoint);
        var radius = PillarDiameter * 0.5d;
        Assert.Equal(5d, point.SafeMachineX, 9);
        // The pillar around the reported safe point reaches past both machine sides.
        Assert.True(point.SafeMachineX - radius < request.MachineOriginX);
        Assert.True(point.SafeMachineX + radius > request.MachineOriginX + request.MachineWidth);
    }

    [Fact]
    public void BuildSnapshot_ForASkewedArray_KeepsLessClearanceFromTheEdgesThanThePillarRadius()
    {
        const double radius = 2d;
        var request = BuildRequest() with
        {
            MachineWidth = 200d,
            MachineHeight = 100d,
            CopperPillarDiameter = radius * 2d,
            ShowCustomArray = true,
            CustomArrayColumnCount = 2,
            CustomArrayRowCount = 2,
            CustomArrayTopLeftMachineX = 10d,
            CustomArrayTopLeftMachineY = 10d,
            CustomArrayTopRightMachineX = 90d,
            CustomArrayTopRightMachineY = 10d,
            CustomArrayBottomRightMachineX = 100d,
            CustomArrayBottomRightMachineY = 20d,
            CustomArrayBottomLeftMachineX = 20d,
            CustomArrayBottomLeftMachineY = 20d,
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(0d, 0d, 200d, 100d), request);

        var safeTopLeft = Assert.Single(snapshot.Points, static p => p.Key == "array-tl");
        Assert.Equal(13.414213562, safeTopLeft.SafeMachineX, 6);
        Assert.Equal(11.414213562, safeTopLeft.SafeMachineY, 6);

        // The top edge runs along y = 10. The left edge runs from (10,10) toward (20,20), so its
        // distance from a point is the cross product over the edge length.
        var topEdgeClearance = safeTopLeft.SafeMachineY - 10d;
        var leftEdgeClearance = Math.Abs(((safeTopLeft.SafeMachineX - 10d) * 10d) - ((safeTopLeft.SafeMachineY - 10d) * 10d))
            / Math.Sqrt(200d);
        Assert.Equal(Math.Sqrt(2d), topEdgeClearance, 6);
        Assert.Equal(Math.Sqrt(2d), leftEdgeClearance, 6);
        Assert.True(topEdgeClearance < radius);
        Assert.True(leftEdgeClearance < radius);
    }

    private static CoordinatePlannerRequest BuildRequest()
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 1000,
            HorizontalGuideCount: 0,
            VerticalGuideCount: 0,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 100d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 100d,
            CustomArrayBottomRightMachineY: 100d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 100d,
            HorizontalGuideMode: CoordinateGuideGenerationMode.Count,
            VerticalGuideMode: CoordinateGuideGenerationMode.Count,
            HorizontalGuidePitch: 0d,
            VerticalGuidePitch: 0d,
            HorizontalGuideInset: 0d,
            VerticalGuideInset: 0d,
            HorizontalGuidePositions: null,
            VerticalGuidePositions: null);
    }
}
