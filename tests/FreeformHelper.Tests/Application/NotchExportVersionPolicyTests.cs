// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Notch;
using Xunit;
using VersionCount = FreeformHelper.Application.Services.NotchExportVersionPolicy.VersionCount;

namespace FreeformHelper.Tests;

public sealed class NotchExportVersionPolicyTests
{
    [Fact]
    public void BuildCatalogSortsVersionsNumericallyAndCountsRepeatedRows()
    {
        NotchAlgorithmVersion[] rowVersions =
        [
            NotchAlgorithmVersion.V22,
            NotchAlgorithmVersion.V21,
            NotchAlgorithmVersion.V22,
        ];

        var catalog = NotchExportVersionPolicy.BuildCatalog(rowVersions, []);

        VersionCount[] expected = [new(NotchAlgorithmVersion.V21, 1), new(NotchAlgorithmVersion.V22, 2)];
        Assert.Equal(expected, catalog);
    }

    [Fact]
    public void BuildCatalogIncludesAnOfferedPinWithZeroRows()
    {
        var catalog = NotchExportVersionPolicy.BuildCatalog([NotchAlgorithmVersion.V22], [NotchAlgorithmVersion.V21]);

        VersionCount[] expected = [new(NotchAlgorithmVersion.V21, 0), new(NotchAlgorithmVersion.V22, 1)];
        Assert.Equal(expected, catalog);
    }

    [Fact]
    public void BuildCatalogDeduplicatesPinsWithoutIncreasingRowCounts()
    {
        NotchAlgorithmVersion?[] pinnedVersions =
        [
            NotchAlgorithmVersion.V22,
            NotchAlgorithmVersion.V21,
            NotchAlgorithmVersion.V22,
        ];

        var catalog = NotchExportVersionPolicy.BuildCatalog(
            [NotchAlgorithmVersion.V21, NotchAlgorithmVersion.V21], pinnedVersions);

        VersionCount[] expected = [new(NotchAlgorithmVersion.V21, 2), new(NotchAlgorithmVersion.V22, 0)];
        Assert.Equal(expected, catalog);
    }

    [Fact]
    public void BuildCatalogIgnoresNullPins()
    {
        var catalog = NotchExportVersionPolicy.BuildCatalog([NotchAlgorithmVersion.V22], [null, null]);

        Assert.Equal(new VersionCount(NotchAlgorithmVersion.V22, 1), Assert.Single(catalog));
    }

    [Fact]
    public void BuildCatalogWithEmptyRowsStillSortsOfferedPinsWithZeroCounts()
    {
        var catalog = NotchExportVersionPolicy.BuildCatalog([], [NotchAlgorithmVersion.V22, null, NotchAlgorithmVersion.V21]);

        VersionCount[] expected = [new(NotchAlgorithmVersion.V21, 0), new(NotchAlgorithmVersion.V22, 0)];
        Assert.Equal(expected, catalog);
    }

    [Fact]
    public void BuildCatalogWithEmptyInputsReturnsAnEmptyCatalog()
    {
        var catalog = NotchExportVersionPolicy.BuildCatalog([], []);

        Assert.Empty(catalog);
    }
}
