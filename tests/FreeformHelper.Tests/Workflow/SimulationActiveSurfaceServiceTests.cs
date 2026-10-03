using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationActiveSurfaceServiceTests
{
    [Fact]
    public void BuildActiveRegularPadIds_UsesBestMatchedRegularPerCad()
    {
        var regularA = CreatePad(72, diffIndex: 540);
        var regularB = CreatePad(73, diffIndex: 541);
        var orderedCadPads = new[] { CreateCad(341) };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [341] = new[]
            {
                new PadMatchLink(341, 72, 0.60, 0.55, 0.60),
                new PadMatchLink(341, 73, 0.20, 0.18, 0.20),
            },
        };
        var regularPadById = new Dictionary<int, RegularPad>
        {
            [72] = regularA,
            [73] = regularB,
        };
        var cadIcIndexByCadId = new Dictionary<int, int>
        {
            [341] = 0,
        };

        var active = SimulationActiveSurfaceService.BuildActiveRegularPadIds(
            orderedCadPads,
            cadToRegular,
            regularPadById,
            cadIcIndexByCadId);

        Assert.Contains(72, active);
        Assert.DoesNotContain(73, active);
    }

    [Fact]
    public void BuildActiveRegularPadIds_RespectsAllowedRegularMask()
    {
        var regularA = CreatePad(72, diffIndex: 540);
        var regularB = CreatePad(73, diffIndex: 541);
        var orderedCadPads = new[] { CreateCad(341) };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [341] = new[]
            {
                new PadMatchLink(341, 72, 0.60, 0.55, 0.60),
                new PadMatchLink(341, 73, 0.20, 0.18, 0.20),
            },
        };
        var regularPadById = new Dictionary<int, RegularPad>
        {
            [72] = regularA,
            [73] = regularB,
        };
        var cadIcIndexByCadId = new Dictionary<int, int>
        {
            [341] = 0,
        };

        var active = SimulationActiveSurfaceService.BuildActiveRegularPadIds(
            orderedCadPads,
            cadToRegular,
            regularPadById,
            cadIcIndexByCadId,
            allowedRegularPadIds: new HashSet<int> { 73 });

        Assert.DoesNotContain(72, active);
        Assert.Contains(73, active);
    }

    [Fact]
    public void BuildActiveRegularPadIds_ExcludesCadWhenNoMaskedBestRegularExists()
    {
        var regularA = CreatePad(72, diffIndex: 540);
        var orderedCadPads = new[] { CreateCad(341) };
        var cadToRegular = new Dictionary<int, IReadOnlyList<PadMatchLink>>
        {
            [341] = new[]
            {
                new PadMatchLink(341, 72, 0.60, 0.55, 0.60),
            },
        };
        var regularPadById = new Dictionary<int, RegularPad>
        {
            [72] = regularA,
        };
        var cadIcIndexByCadId = new Dictionary<int, int>
        {
            [341] = 0,
        };

        var active = SimulationActiveSurfaceService.BuildActiveRegularPadIds(
            orderedCadPads,
            cadToRegular,
            regularPadById,
            cadIcIndexByCadId,
            allowedRegularPadIds: new HashSet<int>());

        Assert.Empty(active);
    }

    private static CadPad CreateCad(int id)
    {
        return TestGeometryFactory.CreateCadPad(id, "L1", 0, 0, 1, 1);
    }

    private static RegularPad CreatePad(int regularPadId, int diffIndex)
    {
        var pad = TestGeometryFactory.CreateRegularPad(
            row: 15,
            col: regularPadId - 72,
            regularPadId,
            minX: 0,
            minY: 15,
            maxX: 1,
            maxY: 16,
            diffIndex: diffIndex);
        pad.MatchedCadPadId = 341;
        return pad;
    }
}
