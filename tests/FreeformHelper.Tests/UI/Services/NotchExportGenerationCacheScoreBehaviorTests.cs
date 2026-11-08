// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportGenerationCacheScoreBehaviorTests
{
    [Fact]
    public void TryGet_NonFiniteMatchScores_UsesZeroScoreIdentityAndRejectsFiniteScoreChanges()
    {
        using var culture = TestCulture.UseEnUs();
        var service = new NotchExportGenerationCacheService();
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10,
            diffIndexSelector: static (_, _) => 10);
        var pad = grid.Pads[0];
        var cadPad = TestGeometryFactory.CreateCadPad(42, "PAD", 2, 1, 8, 9);
        var cad = new CadPadSet([cadPad]);
        pad.Freeform = FreeformType.XWay;
        pad.MatchedCadPadId = cadPad.Id;
        pad.MatchScore = 0;
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = [NotchAlgorithmVersion.V21],
            },
        };
        var table = new NotchTableGenerator().Generate(cad, grid, settings);
        Assert.Equal(10, Assert.Single(table.Rows).DiffIndex);
        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        double[] unavailableScores = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        foreach (var score in unavailableScores)
        {
            pad.MatchScore = score;

            Assert.True(service.TryGet(cad, grid, 3, 1201, out var cachedTable, out var lookup));
            Assert.Same(table, cachedTable);
            Assert.Equal(NotchExportGenerationCacheLookupStatus.Hit, lookup.Status);
            Assert.Equal(lookup.CachedGridSignature, lookup.RequestedGridSignature);
        }

        pad.MatchScore = 0.75;

        Assert.False(service.TryGet(cad, grid, 3, 1201, out var staleTable, out var changedLookup));
        Assert.Null(staleTable);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.GridSignatureChanged, changedLookup.Status);
        Assert.NotEqual(changedLookup.CachedGridSignature, changedLookup.RequestedGridSignature);
    }
}
