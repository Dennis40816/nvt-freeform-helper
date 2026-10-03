using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportGenerationCacheServiceTests
{
    private static readonly double[] GridXEdges = [0.0, 10.0];
    private static readonly double[] GridYEdges = [0.0, 10.0];
    private static readonly int[] CacheTableValues = [10, 100, 65535, 0, 65535, 0, 0];

    [Fact]
    public void TryGet_WithSameReferencesAndKey_ReturnsCachedTable()
    {
        var service = new NotchExportGenerationCacheService();
        var cad = BuildCadSet();
        var grid = BuildGrid();
        var table = BuildTable();

        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        var hit = service.TryGet(cad, grid, step3Revision: 3, settingsFingerprint: 1201, out var cachedTable);

        Assert.True(hit);
        Assert.Same(table, cachedTable);
        var metrics = service.GetSnapshot();
        Assert.True(metrics.HasEntry);
        Assert.Equal(1, metrics.HitCount);
        Assert.Equal(0, metrics.MissCount);
        Assert.Equal(1, metrics.StoreCount);
    }

    [Fact]
    public void TryGet_WithDifferentRevision_Misses()
    {
        var service = new NotchExportGenerationCacheService();
        var cad = BuildCadSet();
        var grid = BuildGrid();
        var table = BuildTable();

        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        var hit = service.TryGet(cad, grid, step3Revision: 4, settingsFingerprint: 1201, out _);

        Assert.False(hit);
        var metrics = service.GetSnapshot();
        Assert.Equal(0, metrics.HitCount);
        Assert.Equal(1, metrics.MissCount);
        Assert.Equal(1, metrics.StoreCount);
    }

    [Fact]
    public void TryGet_WithEquivalentClonedCadAndGrid_HitsByContentSignature()
    {
        var service = new NotchExportGenerationCacheService();
        var cad = BuildCadSet();
        var grid = BuildGrid();
        var table = BuildTable();

        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        var clonedCad = BuildCadSet();
        var clonedGrid = BuildGrid();
        var hit = service.TryGet(
            clonedCad,
            clonedGrid,
            step3Revision: 3,
            settingsFingerprint: 1201,
            out var cachedTable,
            out var lookup);

        Assert.True(hit);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.Hit, lookup.Status);
        Assert.Same(table, cachedTable);
    }

    [Fact]
    public void TryGet_WithDifferentCadContent_MissesWithCadSignatureReason()
    {
        var service = new NotchExportGenerationCacheService();
        var cad = BuildCadSet();
        var grid = BuildGrid();
        var table = BuildTable();

        service.Store(cad, grid, step3Revision: 3, settingsFingerprint: 1201, table);

        var changedCad = BuildCadSet(offsetX: 1.0);
        var hit = service.TryGet(
            changedCad,
            grid,
            step3Revision: 3,
            settingsFingerprint: 1201,
            out _,
            out var lookup);

        Assert.False(hit);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.CadSignatureChanged, lookup.Status);
    }

    [Fact]
    public void TryGet_WithFingerprintOverload_HitsWithoutCadGridSignatureScanPath()
    {
        var service = new NotchExportGenerationCacheService();
        var table = BuildTable();

        service.Store(
            cadFingerprint: 0x11UL,
            gridFingerprint: 0x22UL,
            step3Revision: 3,
            settingsFingerprint: 1201,
            table: table);

        var hit = service.TryGet(
            cadFingerprint: 0x11UL,
            gridFingerprint: 0x22UL,
            step3Revision: 3,
            settingsFingerprint: 1201,
            out var cachedTable,
            out var lookup);

        Assert.True(hit);
        Assert.Same(table, cachedTable);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.Hit, lookup.Status);
    }

    [Fact]
    public void Clear_RemovesEntryAndTracksClearCount()
    {
        var service = new NotchExportGenerationCacheService();
        service.Store(BuildCadSet(), BuildGrid(), step3Revision: 1, settingsFingerprint: 10, BuildTable());

        service.Clear();
        var metrics = service.GetSnapshot();

        Assert.False(metrics.HasEntry);
        Assert.Equal(1, metrics.ClearCount);
    }

    [Fact]
    public void Clear_WithoutEntry_InvalidatesCapturedGenerationEpoch()
    {
        var service = new NotchExportGenerationCacheService();
        var generationEpoch = service.CaptureGenerationEpoch();

        service.Clear();

        Assert.False(service.IsGenerationEpochCurrent(generationEpoch));
        Assert.False(service.TryStore(
            generationEpoch,
            cadFingerprint: 0x11UL,
            gridFingerprint: 0x22UL,
            step3Revision: 3,
            settingsFingerprint: 1201,
            BuildTable()));
        var metrics = service.GetSnapshot();
        Assert.False(metrics.HasEntry);
        Assert.Equal(0, metrics.StoreCount);
        Assert.Equal(0, metrics.ClearCount);
    }

    [Fact]
    public async Task CadAllocationBatchAndLegacyTable_AreTypeIsolatedAndTrackLatestProjectedRowCount()
    {
        const ulong cadFingerprint = 0x11UL;
        const ulong gridFingerprint = 0x22UL;
        const int step3Revision = 3;
        var service = new NotchExportGenerationCacheService();
        var cad = BuildCadSet();
        var grid = BuildGrid();
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        var settings = new ProjectSettings();
        settings.Notch.BoundaryVirtualAreaCapRatio = 7.4079;
        settings.Notch.MultiOwnerStrictOverlapPercent = 66.73;
        var settingsFingerprint = NotchTableGenerator
            .ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var generation = new NotchTableGenerator().GenerateCadAllocationResolvedBatch(cad, grid, settings);

        var generationEpoch = service.CaptureGenerationEpoch();
        var initialLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            () => Task.FromResult(generation.Batch));
        Assert.Same(generation.Batch, await initialLease.ResolutionTask!);

        var cachedLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            static () => throw new InvalidOperationException("Cache hit must not run the factory."));
        Assert.Same(generation.Batch, cachedLease.CachedBatch);
        Assert.Null(cachedLease.ResolutionTask);

        Assert.False(service.TryGet(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            out _));

        Assert.True(service.TryUpdateProjectedRowCount(
            generationEpoch,
            generation.Batch,
            projectedRowCount: 3));

        Assert.Equal(3, service.GetSnapshot().EntryRowCount);

        var table = BuildTable();
        service.Store(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            table);

        Assert.True(service.TryGet(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            out var cachedTable));
        Assert.Same(table, cachedTable);
    }

    [Fact]
    public async Task AcquireCadAllocationResolvedBatch_FaultedTaskIsRemovedAndCanRetry()
    {
        const ulong cadFingerprint = 0x11UL;
        const ulong gridFingerprint = 0x22UL;
        const int step3Revision = 3;
        var service = new NotchExportGenerationCacheService();
        var settings = new ProjectSettings();
        var settingsFingerprint = NotchTableGenerator
            .ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var generationEpoch = service.CaptureGenerationEpoch();

        var failedLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            static () => Task.FromException<NotchTableGenerator.CadAllocationResolvedBatch>(
                new InvalidOperationException("synthetic resolution failure")));

        await Assert.ThrowsAsync<InvalidOperationException>(() => failedLease.ResolutionTask!);

        var batch = BuildCadAllocationBatch(settings);
        var retryLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            () => Task.FromResult(batch));

        Assert.True(retryLease.IsTaskOwner);
        Assert.Same(batch, await retryLease.ResolutionTask!);
        var metrics = service.GetSnapshot();
        Assert.Equal(2, metrics.MissCount);
        Assert.Equal(1, metrics.StoreCount);
    }

    [Fact]
    public async Task AcquireCadAllocationResolvedBatch_FinalOnlyChangeJoinsPendingResolution()
    {
        const ulong cadFingerprint = 0x11UL;
        const ulong gridFingerprint = 0x22UL;
        const int step3Revision = 3;
        var service = new NotchExportGenerationCacheService();
        var firstSettings = new ProjectSettings();
        firstSettings.Notch.EnableTargetCoverageGuard = false;
        var finalOnlySettings = new ProjectSettings();
        finalOnlySettings.Notch.EnableTargetCoverageGuard = true;
        finalOnlySettings.Notch.TargetCoverageCapPercent = 50;
        var settingsFingerprint = NotchTableGenerator
            .ComputeCadAllocationResolvedBatchSettingsFingerprint(firstSettings);
        Assert.Equal(
            settingsFingerprint,
            NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(finalOnlySettings));

        var batch = BuildCadAllocationBatch(firstSettings);
        var release = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var generationEpoch = service.CaptureGenerationEpoch();
        var firstLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            firstSettings,
            () => release.Task);
        var joinedLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            finalOnlySettings,
            static () => throw new InvalidOperationException("Join must not run a second factory."));

        Assert.True(firstLease.IsTaskOwner);
        Assert.False(joinedLease.IsTaskOwner);
        Assert.Same(firstLease.ResolutionTask, joinedLease.ResolutionTask);
        release.SetResult(batch);
        await Task.WhenAll(firstLease.ResolutionTask!, joinedLease.ResolutionTask!);

        var metrics = service.GetSnapshot();
        Assert.Equal(1, metrics.MissCount);
        Assert.Equal(1, metrics.StoreCount);
    }

    [Fact]
    public async Task Clear_DetachesPendingResolutionAndRejectsItsStoreBeforeRetry()
    {
        const ulong cadFingerprint = 0x11UL;
        const ulong gridFingerprint = 0x22UL;
        const int step3Revision = 3;
        var service = new NotchExportGenerationCacheService();
        var settings = new ProjectSettings();
        var settingsFingerprint = NotchTableGenerator
            .ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var batch = BuildCadAllocationBatch(settings);
        var release = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var staleEpoch = service.CaptureGenerationEpoch();
        var staleLease = service.AcquireCadAllocationResolvedBatch(
            staleEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            () => release.Task);

        service.Clear();
        release.SetResult(batch);
        Assert.Same(batch, await staleLease.ResolutionTask!);
        Assert.False(service.IsGenerationEpochCurrent(staleEpoch));
        var staleMetrics = service.GetSnapshot();
        Assert.False(staleMetrics.HasEntry);
        Assert.Equal(0, staleMetrics.StoreCount);

        var retryLease = service.AcquireCadAllocationResolvedBatch(
            service.CaptureGenerationEpoch(),
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            settings,
            () => Task.FromResult(batch));
        Assert.True(retryLease.IsTaskOwner);
        Assert.Same(batch, await retryLease.ResolutionTask!);
        var retryMetrics = service.GetSnapshot();
        Assert.Equal(2, retryMetrics.MissCount);
        Assert.Equal(1, retryMetrics.StoreCount);
    }

    [Fact]
    public async Task AcquireCadAllocationResolvedBatch_FingerprintCollisionDoesNotJoinIncompatibleTask()
    {
        const ulong cadFingerprint = 0x11UL;
        const ulong gridFingerprint = 0x22UL;
        const int step3Revision = 3;
        var service = new NotchExportGenerationCacheService();
        var firstSettings = new ProjectSettings();
        firstSettings.Notch.BoundaryVirtualAreaCapRatio = 7.4079;
        firstSettings.Notch.MultiOwnerStrictOverlapPercent = 66.73;
        var collidingSettings = new ProjectSettings();
        collidingSettings.Notch.BoundaryVirtualAreaCapRatio = 1.6586;
        collidingSettings.Notch.MultiOwnerStrictOverlapPercent = 79.07;
        var settingsFingerprint = NotchTableGenerator
            .ComputeCadAllocationResolvedBatchSettingsFingerprint(firstSettings);
        Assert.Equal(
            settingsFingerprint,
            NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(collidingSettings));

        var firstBatch = BuildCadAllocationBatch(firstSettings);
        var collidingBatch = BuildCadAllocationBatch(collidingSettings);
        var firstRelease = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var collidingRelease = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var generationEpoch = service.CaptureGenerationEpoch();
        var firstLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            firstSettings,
            () => firstRelease.Task);
        var collidingLease = service.AcquireCadAllocationResolvedBatch(
            generationEpoch,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            collidingSettings,
            () => collidingRelease.Task);

        try
        {
            Assert.True(firstLease.IsTaskOwner);
            Assert.True(collidingLease.IsTaskOwner);
            Assert.NotSame(firstLease.ResolutionTask, collidingLease.ResolutionTask);
        }
        finally
        {
            firstRelease.TrySetResult(firstBatch);
            collidingRelease.TrySetResult(collidingBatch);
            await Task.WhenAll(firstLease.ResolutionTask!, collidingLease.ResolutionTask!);
        }

        var metrics = service.GetSnapshot();
        Assert.Equal(2, metrics.MissCount);
        Assert.Equal(1, metrics.StoreCount);
    }

    private static CadPadSet BuildCadSet(double offsetX = 0.0)
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0 + offsetX, 0),
            new Point2(10 + offsetX, 0),
            new Point2(10 + offsetX, 10),
            new Point2(0 + offsetX, 10),
        });
        var cadPad = new CadPad(10, "CAD10", "L1", polygon);
        return new CadPadSet(new[] { cadPad });
    }

    private static RegularGrid BuildGrid()
    {
        var polygon = new Polygon2(new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        });
        var pad = new RegularPad(0, 0, 100, polygon);
        return new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: GridXEdges,
            yEdges: GridYEdges,
            pads: new[] { pad });
    }

    private static NotchTableGenerator.CadAllocationResolvedBatch BuildCadAllocationBatch(
        ProjectSettings settings)
    {
        var cad = BuildCadSet();
        var grid = BuildGrid();
        grid.Pads[0].Freeform = FreeformType.XWay;
        grid.Pads[0].MatchedCadPadId = cad.Pads[0].Id;
        grid.Pads[0].MatchScore = 1.0;
        return new NotchTableGenerator().ResolveCadAllocationBatch(cad, grid, settings);
    }

    private static NotchTable BuildTable()
    {
        return new NotchTable(new[]
        {
            new NotchTableRow(
                NotchAlgorithmVersion.V22,
                icIndex: 0,
                diffIndex: 10,
                regularPadIndex: 100,
                cadPadId: 10,
                values: CacheTableValues,
                comment: "cache")
        });
    }
}
