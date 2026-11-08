// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests.Application.Dxf;

public sealed class CadOutputFwDiffIndexDuplicateReviewMissingSuggestionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildGroups_UnavailableSuggestions_PreservesDuplicatesWithoutInventingMatches(bool supplySuggestions)
    {
        var pads = new[]
        {
            TestGeometryFactory.CreateCadPad(3, "SIG", 2d, 0d, 3d, 1d),
            TestGeometryFactory.CreateCadPad(1, "SIG", 0d, 0d, 1d, 1d),
            TestGeometryFactory.CreateCadPad(2, "SIG", 1d, 0d, 2d, 1d),
        };
        var diffs = new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 10 };
        var icIndices = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 0 };
        Dictionary<int, CadBestMatchSeed>? rawSeeds = supplySuggestions
            ? new() { [2] = new(302, 0, 12, 0.71d, 0.29d) }
            : null;
        Dictionary<int, CadBestMatchSeed>? maskedSeeds = supplySuggestions
            ? new() { [1] = new(401, 0, 11, 0.63d, 0.37d) }
            : null;

        var groups = CadOutputFwDiffIndexDuplicateReviewService.BuildGroups(pads, diffs, icIndices, rawSeeds, maskedSeeds);

        var group = Assert.Single(groups);
        Assert.Equal((0, 10), (group.IcIndex, group.DiffIndex));
        Assert.Equal(new[]
        {
            new CadOutputFwDiffIndexDuplicateReviewEntry(
                1, "SIG", 0, 10, null, null, null,
                supplySuggestions ? 11 : null, supplySuggestions ? 401 : null, supplySuggestions ? 0.63d : null),
            new CadOutputFwDiffIndexDuplicateReviewEntry(
                2, "SIG", 0, 10,
                supplySuggestions ? 12 : null, supplySuggestions ? 302 : null, supplySuggestions ? 0.71d : null,
                null, null, null),
            new CadOutputFwDiffIndexDuplicateReviewEntry(3, "SIG", 0, 10, null, null, null, null, null, null),
        }, group.Entries);
    }
}
