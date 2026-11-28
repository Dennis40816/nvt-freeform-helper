// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorMatchCacheEntryBuilderTests
{
    [Fact]
    public void BuildCadPreservesMatchOrderingAndProjection()
    {
        PadMatchLink[] links =
        [
            new(11, 10, 4.0, 0.4, 0.95),
            new(11, 30, 8.0, 0.8, 0.6),
            new(11, 20, 8.0, 0.8, 0.9),
            new(11, 5, 8.0, 0.8, 0.9),
        ];
        int[] matchedIds = [30, 10, 5, 20, 30, 10];
        List<int> expectedIds = [5, 10, 20, 30];
        List<PadMatchLink> expectedLinks = [links[3], links[2], links[1], links[0]];
        Func<int, (int IcIndex, int DiffIndex)?> lookup = static id => (1, id + 100);

        var entry = PadInspectorMatchCacheEntryBuilder.BuildCad(17, links, matchedIds, lookup);

        Assert.Equal(expectedLinks, entry.MatchedLinks);
        Assert.Equal(expectedIds, entry.MatchedRegularIds);
        Assert.Equal(expectedIds, entry.MatchedRegularDetails.Select(detail => detail.RegularPadId));
        Assert.Equal(0.95, entry.Confidence);
        Assert.Equal(17, entry.Revision);
        Assert.Equal(PadInspectorMatchFormatter.BuildCadMatchText(expectedIds, expectedLinks, lookup), entry.MatchText);
        Assert.Equal(PadInspectorMatchFormatter.BuildCadMatchDetailsText(expectedIds, expectedLinks, lookup), entry.MatchDetailsText);
    }

    [Fact]
    public void BuildCadEmptyInputHasNoMatchesAndZeroConfidence()
    {
        var entry = PadInspectorMatchCacheEntryBuilder.BuildCad(23, [], [], static _ => null);

        Assert.Empty(entry.MatchedLinks);
        Assert.Empty(entry.MatchedRegularIds);
        Assert.Empty(entry.MatchedRegularDetails);
        Assert.Equal(0.0, entry.Confidence);
        Assert.Equal(23, entry.Revision);
    }

    [Fact]
    public void BuildCadMissingIdentityKeepsTheMatchedCoverage()
    {
        var link = new PadMatchLink(11, 7, 5.0, 0.5, 0.75);

        var entry = PadInspectorMatchCacheEntryBuilder.BuildCad(1, [link], [7], static _ => null);

        var detail = Assert.Single(entry.MatchedRegularDetails);
        Assert.Equal(new PadInspectorMatchedRegularSnapshot(7, null, null, 0.75, 0.5), detail);
        Assert.Equal(0.75, entry.Confidence);
        Assert.Equal("regId 7", entry.MatchText);
    }

    [Fact]
    public void BuildRegularPreservesMatchOrderingAndProjection()
    {
        var regularPad = TestGeometryFactory.CreateRegularPad(0, 0, 42, 0, 0, 10, 10, diffIndex: 12);
        PadMatchLink[] links =
        [
            new(9, 42, 4.0, 0.95, 0.4),
            new(7, 42, 8.0, 0.2, 0.8),
            new(3, 42, 8.0, 0.4, 0.8),
        ];
        int[] matchedIds = [9, 3, 7, 9, 3];
        int[] expectedIds = [3, 7, 9];
        PadMatchLink[] expectedLinks = [links[2], links[1], links[0]];
        var outputIndices = new Dictionary<int, int> { [3] = 12, [7] = 13 };

        var entry = PadInspectorMatchCacheEntryBuilder.BuildRegular(
            29, regularPad, links, matchedIds, outputIndices.TryGetValue, new Dictionary<int, FreeformType>());

        Assert.Equal(expectedLinks, entry.MatchedLinks);
        Assert.Equal(expectedIds, entry.MatchedCadIds);
        Assert.Equal(expectedIds, entry.MatchedCadDetails.Select(detail => detail.CadPadId));
        Assert.Equal(12, entry.MatchedCadDetails[0].DxfIndex);
        Assert.Equal(13, entry.MatchedCadDetails[1].DxfIndex);
        Assert.Null(entry.MatchedCadDetails[2].DxfIndex);
        Assert.Equal(0.8, entry.Confidence);
        Assert.Equal(29, entry.Revision);
    }

    [Fact]
    public void BuildRegularEmptyInputHasNoMatchesAndZeroConfidence()
    {
        var regularPad = TestGeometryFactory.CreateRegularPad(0, 0, 42, 0, 0, 10, 10);
        var outputIndices = new Dictionary<int, int>();

        var entry = PadInspectorMatchCacheEntryBuilder.BuildRegular(
            31, regularPad, [], [], outputIndices.TryGetValue, new Dictionary<int, FreeformType>());

        Assert.Empty(entry.MatchedLinks);
        Assert.Empty(entry.MatchedCadIds);
        Assert.Empty(entry.MatchedCadDetails);
        Assert.Equal(0.0, entry.Confidence);
        Assert.Equal(31, entry.Revision);
    }

    [Theory]
    [InlineData(FreeformType.None, 41)]
    [InlineData(FreeformType.XWay, 42)]
    [InlineData(FreeformType.YWay, 41)]
    public void BuildRegularSourcesAndRuleTraceMatchTheExistingHelpers(FreeformType freeform, int overridePadId)
    {
        var regularPad = TestGeometryFactory.CreateRegularPad(0, 0, 42, 0, 0, 10, 10, diffIndex: 12, icIndex: 1);
        regularPad.Freeform = freeform;
        List<PadMatchLink> links = [new(11, 42, 5.0, 0.5, 0.75)];
        List<int> matchedIds = [11];
        var outputIndices = new Dictionary<int, int> { [11] = 12 };
        var overrides = new Dictionary<int, FreeformType> { [overridePadId] = freeform };
        var details = PadInspectorMatchDetailBuilder.BuildMatchedCadDetails(matchedIds, links, outputIndices.TryGetValue);
        var diffSource = PadInspectorMatchFormatter.BuildRegularDiffSource(regularPad.DiffIndex, details);
        var (freeformSource, freeformSourceDetail) = PadInspectorDisplayProjector.BuildRegularFreeformSource(regularPad, overrides);
        var ruleTrace = PadInspectorRuleTraceBuilder.BuildRegularRuleTrace(
            regularPad, details, diffSource, freeformSource, freeformSourceDetail);

        var entry = PadInspectorMatchCacheEntryBuilder.BuildRegular(
            1, regularPad, links, matchedIds, outputIndices.TryGetValue, overrides);

        Assert.Equal(details, entry.MatchedCadDetails);
        Assert.Equal(diffSource, entry.DiffSource);
        Assert.Equal(freeformSource, entry.FreeformSource);
        Assert.Equal(freeformSourceDetail, entry.FreeformSourceDetail);
        Assert.Equal(ruleTrace, entry.RuleTrace);
        Assert.Equal(PadInspectorMatchFormatter.BuildRegularMatchText(details, matchedIds), entry.MatchText);
        Assert.Equal(PadInspectorMatchFormatter.BuildRegularMatchDetailsText(details, links), entry.MatchDetailsText);
    }
}
