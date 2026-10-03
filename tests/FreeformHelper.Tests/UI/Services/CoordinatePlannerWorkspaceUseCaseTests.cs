using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerWorkspaceUseCaseTests
{
    private static readonly string[] AaAndSignalLayerNames = ["AA", "SIG"];

    [Fact]
    public void BuildSnapshot_WhenOutlineLayerSelected_UsesThatLayerBounds()
    {
        var session = new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[]
            {
                TestGeometryFactory.CreateCadPad(1, "AA", 10d, 20d, 110d, 220d, name: "AA_BOX"),
                TestGeometryFactory.CreateCadPad(2, "SIG", 40d, 60d, 70d, 90d, name: "OTHER"),
            },
            AaAndSignalLayerNames,
            SourceRevision: 0,
            DefaultMachineWidth: 100d,
            DefaultMachineHeight: 200d,
            DefaultPixelWidth: 1000,
            DefaultPixelHeight: 2000);
        var request = new FreeformHelper.Application.Services.CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 200d,
            PixelWidth: 1000,
            PixelHeight: 2000,
            HorizontalGuideCount: 5,
            VerticalGuideCount: 5,
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
            CustomArrayBottomRightMachineY: 200d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 200d);

        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(session, request, "AA");

        Assert.Equal("AA outline: layer AA", result.ActiveAreaSourceText);
        Assert.Equal(CoordinatePlannerActiveAreaSourceKind.LayerBounds, result.ActiveAreaResolution.SourceKind);
        Assert.Equal("AA", result.ActiveAreaResolution.RequestedLayerName);
        Assert.Equal("AA", result.ActiveAreaResolution.ResolvedLayerName);
        Assert.Equal(10d, result.Snapshot.ActiveAreaBounds.MinX, 6);
        Assert.Equal(20d, result.Snapshot.ActiveAreaBounds.MinY, 6);
        Assert.Equal(110d, result.Snapshot.ActiveAreaBounds.MaxX, 6);
        Assert.Equal(220d, result.Snapshot.ActiveAreaBounds.MaxY, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenSelectedOutlineLayerHasNoPads_FallsBackToRegularGridWithDiagnostics()
    {
        var session = new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[]
            {
                TestGeometryFactory.CreateCadPad(1, "SIG", 10d, 20d, 110d, 220d, name: "SIG_BOX"),
            },
            AaAndSignalLayerNames,
            SourceRevision: 0,
            DefaultMachineWidth: 100d,
            DefaultMachineHeight: 200d,
            DefaultPixelWidth: 1000,
            DefaultPixelHeight: 2000);
        var request = BuildDefaultRequest();

        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(session, request, "AA");

        Assert.Equal(
            "AA outline: regular grid bounds (selected layer AA has no CAD pads)",
            result.ActiveAreaSourceText);
        Assert.Equal(
            CoordinatePlannerActiveAreaSourceKind.LayerBoundsFallbackNoCadPads,
            result.ActiveAreaResolution.SourceKind);
        Assert.Equal("AA", result.ActiveAreaResolution.RequestedLayerName);
        Assert.Equal("AA", result.ActiveAreaResolution.ResolvedLayerName);
        Assert.Equal(session.Grid.Bounds.MinX, result.Snapshot.ActiveAreaBounds.MinX, 6);
        Assert.Equal(session.Grid.Bounds.MinY, result.Snapshot.ActiveAreaBounds.MinY, 6);
        Assert.Equal(session.Grid.Bounds.MaxX, result.Snapshot.ActiveAreaBounds.MaxX, 6);
        Assert.Equal(session.Grid.Bounds.MaxY, result.Snapshot.ActiveAreaBounds.MaxY, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenSelectedOutlineLayerNotFound_FallsBackToRegularGridWithNotFoundDiagnostics()
    {
        var session = new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[]
            {
                TestGeometryFactory.CreateCadPad(1, "SIG", 10d, 20d, 110d, 220d, name: "SIG_BOX"),
            },
            AaAndSignalLayerNames,
            SourceRevision: 0,
            DefaultMachineWidth: 100d,
            DefaultMachineHeight: 200d,
            DefaultPixelWidth: 1000,
            DefaultPixelHeight: 2000);
        var request = BuildDefaultRequest();

        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(session, request, "UNKNOWN");

        Assert.Equal(
            "AA outline: regular grid bounds (selected layer UNKNOWN not found)",
            result.ActiveAreaSourceText);
        Assert.Equal(
            CoordinatePlannerActiveAreaSourceKind.LayerBoundsFallbackLayerMissing,
            result.ActiveAreaResolution.SourceKind);
        Assert.Equal("UNKNOWN", result.ActiveAreaResolution.RequestedLayerName);
        Assert.Null(result.ActiveAreaResolution.ResolvedLayerName);
        Assert.Equal(session.Grid.Bounds.MinX, result.Snapshot.ActiveAreaBounds.MinX, 6);
        Assert.Equal(session.Grid.Bounds.MinY, result.Snapshot.ActiveAreaBounds.MinY, 6);
        Assert.Equal(session.Grid.Bounds.MaxX, result.Snapshot.ActiveAreaBounds.MaxX, 6);
        Assert.Equal(session.Grid.Bounds.MaxY, result.Snapshot.ActiveAreaBounds.MaxY, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideBasisIsRegularGrid_UsesRegularBoundsForGuides()
    {
        var session = new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1),
            new[]
            {
                TestGeometryFactory.CreateCadPad(1, "AA", 10d, 20d, 110d, 220d, name: "AA_BOX"),
            },
            AaAndSignalLayerNames,
            SourceRevision: 0,
            DefaultMachineWidth: 100d,
            DefaultMachineHeight: 200d,
            DefaultPixelWidth: 1000,
            DefaultPixelHeight: 2000);
        var request = BuildDefaultRequest();

        var result = CoordinatePlannerWorkspaceUseCase.BuildSnapshot(
            session,
            request,
            "AA",
            CoordinatePlannerGuideBasisKind.RegularGrid);

        Assert.Equal(CoordinatePlannerGuideBasisKind.RegularGrid, result.GuideBasisKind);
        Assert.Contains("regular grid", result.GuideSourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10d, result.Snapshot.ActiveAreaBounds.MinX, 6);
        Assert.Equal(20d, result.Snapshot.ActiveAreaBounds.MinY, 6);
        Assert.Equal(session.Grid.Bounds.MinX, result.GuideBounds.MinX, 6);
        Assert.Equal(session.Grid.Bounds.MaxX, result.GuideBounds.MaxX, 6);

        var firstHorizontalGuide = Assert.Single(result.Snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(session.Grid.Bounds.MinX, firstHorizontalGuide.StartWorld.X, 6);
        Assert.Equal(session.Grid.Bounds.MaxY, firstHorizontalGuide.StartWorld.Y, 6);
        Assert.Equal(session.Grid.Bounds.MaxX, firstHorizontalGuide.EndWorld.X, 6);
        Assert.Equal(session.Grid.Bounds.MaxY, firstHorizontalGuide.EndWorld.Y, 6);
    }

    private static FreeformHelper.Application.Services.CoordinatePlannerRequest BuildDefaultRequest()
    {
        return new FreeformHelper.Application.Services.CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 200d,
            PixelWidth: 1000,
            PixelHeight: 2000,
            HorizontalGuideCount: 5,
            VerticalGuideCount: 5,
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
            CustomArrayBottomRightMachineY: 200d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 200d);
    }
}
