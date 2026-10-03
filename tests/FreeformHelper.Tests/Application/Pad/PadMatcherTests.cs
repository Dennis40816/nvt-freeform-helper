using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadMatcherTests
{
    [Fact]
    public void Match_Overlap_ReportsProgressToCompletion()
    {
        var (cad, grid) = BuildPerfectOverlapCase();
        var progress = new List<double>();
        var matcher = new PadMatcher();

        var result = PadMatcher.Match(
            cad,
            grid,
            new MatchingSettings
            {
                Mode = MatchMode.LegacyOverlap,
                MatchThreshold = 0.35,
            },
            reportProgress: value => progress.Add(value));

        Assert.NotNull(result);
        Assert.NotEmpty(progress);
        Assert.InRange(progress[0], 0.0, 1.0);
        Assert.InRange(progress[^1], 0.999, 1.0);
        Assert.All(progress, value => Assert.InRange(value, 0.0, 1.0));
    }

    [Fact]
    public void Match_Overlap_CreatesBidirectionalLinks_WhenCadSpansMultipleRegularPads()
    {
        var grid = new RegularGridBuilder().BuildFromSettings(new GridSettings
        {
            XChannels = 2,
            YChannels = 1,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        });

        var spanningCad = new CadPad(
            id: 1001,
            name: "Cross",
            layer: "L1",
            polygon: new Polygon2(new[]
            {
                new Point2(0.2, 0.1),
                new Point2(1.8, 0.1),
                new Point2(1.8, 0.9),
                new Point2(0.2, 0.9),
            }));
        var cad = new CadPadSet(new[] { spanningCad });

        var matcher = new PadMatcher();

        var result = PadMatcher.Match(cad, grid, new MatchingSettings
        {
            Mode = MatchMode.LegacyOverlap,
        });

        Assert.True(result.CadToRegular.TryGetValue(spanningCad.Id, out var relatedRegulars));
        Assert.Equal(2, relatedRegulars.Count);

        var regularIds = relatedRegulars.Select(link => link.RegularPadId).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { grid.Pads[0].RegularPadId, grid.Pads[1].RegularPadId }, regularIds);

        Assert.True(result.RegularToCad.TryGetValue(grid.Pads[0].RegularPadId, out var firstLinks));
        Assert.Single(firstLinks);
        Assert.Equal(spanningCad.Id, firstLinks[0].CadPadId);

        Assert.True(result.RegularToCad.TryGetValue(grid.Pads[1].RegularPadId, out var secondLinks));
        Assert.Single(secondLinks);
        Assert.Equal(spanningCad.Id, secondLinks[0].CadPadId);

        Assert.Equal(spanningCad.Id, grid.Pads[0].MatchedCadPadId);
        Assert.Equal(spanningCad.Id, grid.Pads[1].MatchedCadPadId);
        Assert.True(grid.Pads[0].MatchScore > 0);
        Assert.True(grid.Pads[1].MatchScore > 0);
    }

    [Fact]
    public void Match_Overlap_IsInvariantToDiagnosticAndRetiredCompatibilitySettings()
    {
        var (lowThresholdCad, lowThresholdGrid) = BuildCompatibilitySettingsCase();
        var (highThresholdCad, highThresholdGrid) = BuildCompatibilitySettingsCase();

        var lowThresholdResult = PadMatcher.Match(
            lowThresholdCad,
            lowThresholdGrid,
            new MatchingSettings
            {
                Mode = MatchMode.RegularToCad,
                MatchThreshold = 0.5,
                EnableCentroidFallback = false,
                NearestK = 1,
            });
        var highThresholdResult = PadMatcher.Match(
            highThresholdCad,
            highThresholdGrid,
            new MatchingSettings
            {
                Mode = MatchMode.LegacyOverlap,
                MatchThreshold = 0.75,
                EnableCentroidFallback = true,
                NearestK = 128,
            });

        Assert.All(lowThresholdResult.Links, static link => Assert.InRange(link.RegularCoverage, 0.500001, 0.749999));
        Assert.Equal(4, lowThresholdResult.Links.Count);
        Assert.DoesNotContain(
            lowThresholdResult.Links,
            link => link.RegularPadId == lowThresholdGrid.Pads[2].RegularPadId);
        Assert.Null(lowThresholdGrid.Pads[2].MatchedCadPadId);
        Assert.Equal(lowThresholdResult.Links, highThresholdResult.Links);
        Assert.Equal(lowThresholdResult.Telemetry, highThresholdResult.Telemetry);
        Assert.Equal(
            lowThresholdGrid.Pads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.MatchScore)),
            highThresholdGrid.Pads.Select(static pad => (pad.RegularPadId, pad.MatchedCadPadId, pad.MatchScore)));
    }

    private static (CadPadSet cad, RegularGrid grid) BuildCompatibilitySettingsCase()
    {
        var grid = new RegularGridBuilder().BuildFromSettings(new GridSettings
        {
            XChannels = 3,
            YChannels = 1,
            ActiveAreaWidth = 3,
            ActiveAreaHeight = 1,
            BoundsPaddingRatio = 0,
        });
        var first = grid.Pads[0].Bounds;
        var second = grid.Pads[1].Bounds;
        var cad = new CadPadSet(new[]
        {
            new CadPad(201, "AmbiguousA", "L1", new Polygon2(new[]
            {
                new Point2(first.MinX + 0.1, first.MinY + 0.1),
                new Point2(second.MaxX - 0.3, first.MinY + 0.1),
                new Point2(second.MaxX - 0.3, first.MaxY - 0.1),
                new Point2(first.MinX + 0.1, first.MaxY - 0.1),
            })),
            new CadPad(202, "AmbiguousB", "L1", new Polygon2(new[]
            {
                new Point2(first.MinX + 0.3, first.MinY + 0.1),
                new Point2(second.MaxX - 0.1, first.MinY + 0.1),
                new Point2(second.MaxX - 0.1, first.MaxY - 0.1),
                new Point2(first.MinX + 0.3, first.MaxY - 0.1),
            })),
        });

        return (cad, grid);
    }

    private static (CadPadSet cad, RegularGrid grid) BuildPerfectOverlapCase()
    {
        return BuildUniformOverlapCase(inset: 0.0);
    }

    private static (CadPadSet cad, RegularGrid grid) BuildUniformOverlapCase(double inset)
    {
        var gridSettings = new GridSettings
        {
            XChannels = 2,
            YChannels = 2,
            ActiveAreaWidth = 2,
            ActiveAreaHeight = 2,
            BoundsPaddingRatio = 0,
        };

        var grid = new RegularGridBuilder().BuildFromSettings(gridSettings);
        var cadPads = new List<CadPad>();
        foreach (var reg in grid.Pads)
        {
            var b = reg.Bounds;
            var poly = new Polygon2(new[]
            {
                new Point2(b.MinX + inset, b.MinY + inset),
                new Point2(b.MaxX - inset, b.MinY + inset),
                new Point2(b.MaxX - inset, b.MaxY - inset),
                new Point2(b.MinX + inset, b.MaxY - inset),
            });
            cadPads.Add(new CadPad(100 + reg.Index, $"C{reg.Index}", "L1", poly));
        }

        return (new CadPadSet(cadPads), grid);
    }
}
