using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerActiveAreaResolverTests
{
    private static readonly string[] PreferredLayerNames = ["AA.drawing", "SIG.top"];
    private static readonly string[] FallbackLayerNames = ["sig.top", "AA.drawing"];
    private static readonly string[] SessionLayerNames = ["AA.drawing", "SIG"];

    [Fact]
    public void ResolvePreferredLayerName_WhenPreferredExists_ReturnsCanonicalLayerName()
    {
        var resolved = CoordinatePlannerActiveAreaResolver.ResolvePreferredLayerName(PreferredLayerNames, "sig.TOP");

        Assert.Equal("SIG.top", resolved);
    }

    [Fact]
    public void ResolvePreferredLayerName_WhenPreferredMissing_FallsBackToAaDrawing()
    {
        var resolved = CoordinatePlannerActiveAreaResolver.ResolvePreferredLayerName(FallbackLayerNames, "unknown");

        Assert.Equal("AA.drawing", resolved);
    }

    [Fact]
    public void Resolve_WhenLayerRequestedWithoutPads_ReturnsNoCadPadsFallback()
    {
        var session = new CoordinatePlannerWorkspaceSession(
            TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10d, cellHeight: 5d),
            new[] { TestGeometryFactory.CreateCadPad(1, "SIG", 10d, 20d, 30d, 40d) },
            SessionLayerNames,
            SourceRevision: 0,
            DefaultMachineWidth: 100d,
            DefaultMachineHeight: 50d,
            DefaultPixelWidth: 1000,
            DefaultPixelHeight: 500);

        var resolution = CoordinatePlannerActiveAreaResolver.Resolve(session, "AA.drawing");

        Assert.Equal(CoordinatePlannerActiveAreaSourceKind.LayerBoundsFallbackNoCadPads, resolution.SourceKind);
        Assert.Equal(session.Grid.Bounds, resolution.Bounds);
        Assert.Equal("AA.drawing", resolution.RequestedLayerName);
        Assert.Equal("AA.drawing", resolution.ResolvedLayerName);
    }
}
