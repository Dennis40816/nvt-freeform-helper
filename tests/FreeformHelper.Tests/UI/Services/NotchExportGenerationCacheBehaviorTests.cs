// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportGenerationCacheBehaviorTests
{
    [Fact]
    public void Store_AfterRegularRemapping_ReplacesExportRowsAndRejectsThePreviousGrid()
    {
        using var culture = TestCulture.UseEnUs();
        var service = new NotchExportGenerationCacheService();
        var (cad, originalGrid, settings) = CreateScenario();
        var originalTable = new NotchTableGenerator().Generate(cad, originalGrid, settings);
        service.Store(cad, originalGrid, step3Revision: 3, settingsFingerprint: 1201, originalTable);
        var (_, remappedGrid, _) = CreateScenario();
        remappedGrid.Pads[0].DiffIndex = 27;

        Assert.False(service.TryGet(cad, remappedGrid, 3, 1201, out var staleTable, out var changedLookup));
        Assert.Null(staleTable);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.GridSignatureChanged, changedLookup.Status);
        Assert.NotEqual(changedLookup.CachedGridSignature, changedLookup.RequestedGridSignature);

        var remappedTable = new NotchTableGenerator().Generate(cad, remappedGrid, settings);
        service.Store(cad, remappedGrid, 3, 1201, remappedTable);

        Assert.False(service.TryGet(cad, originalGrid, 3, 1201, out _, out var oldLookup));
        Assert.Equal(NotchExportGenerationCacheLookupStatus.GridSignatureChanged, oldLookup.Status);
        Assert.True(service.TryGet(cad, remappedGrid, 3, 1201, out var cachedTable));
        Assert.Same(remappedTable, cachedTable);
        Assert.Equal(27, Assert.Single(cachedTable.Rows).DiffIndex);
        Assert.Equal(27, Assert.Single(cachedTable.Rows).Values[0]);
        var metrics = service.GetSnapshot();
        Assert.Equal(1, metrics.EntryRowCount);
        Assert.Equal(2, metrics.StoreCount);
        Assert.Equal(2, metrics.MissCount);
        Assert.Equal(1, metrics.HitCount);
    }

    [Fact]
    public void TryGet_ChangedSettingsKey_ReportsBothKeysAndPreservesTheOriginalTable()
    {
        using var culture = TestCulture.UseEnUs();
        var service = new NotchExportGenerationCacheService();
        var (cad, grid, settings) = CreateScenario();
        var table = new NotchTableGenerator().Generate(cad, grid, settings);
        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        var hit = service.TryGet(cad, grid, 3, 1202, out var incompatibleTable, out var lookup);

        Assert.False(hit);
        Assert.Null(incompatibleTable);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.SettingsFingerprintChanged, lookup.Status);
        Assert.Equal(1201, lookup.CachedSettingsFingerprint);
        Assert.Equal(1202, lookup.RequestedSettingsFingerprint);
        Assert.Equal(lookup.CachedCadSignature, lookup.RequestedCadSignature);
        Assert.Equal(lookup.CachedGridSignature, lookup.RequestedGridSignature);
        Assert.Equal(3, lookup.CachedStep3Revision);
        Assert.Equal(3, lookup.RequestedStep3Revision);
        Assert.True(service.TryGet(cad, grid, 3, 1201, out var cachedTable));
        Assert.Same(table, cachedTable);
        Assert.Equal(10, Assert.Single(cachedTable.Rows).DiffIndex);
        var metrics = service.GetSnapshot();
        Assert.Equal(1, metrics.StoreCount);
        Assert.Equal(1, metrics.MissCount);
        Assert.Equal(1, metrics.HitCount);
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings) CreateScenario()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10,
            diffIndexSelector: static (_, _) => 10);
        var cad = TestGeometryFactory.CreateCadPad(42, "PAD", 2, 1, 8, 9);
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Id;
        grid.Pads[0].MatchScore = 1;
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.LegacyRegularAnchor,
                EnabledVersions = [NotchAlgorithmVersion.V21],
            },
        };
        return (new CadPadSet([cad]), grid, settings);
    }
}
