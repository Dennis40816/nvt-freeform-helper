// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchExportGenerationCacheBatchBehaviorTests
{
    [Fact]
    public async Task AcquireBatch_AfterStoringTableWithSameIdentity_ResolvesBatchAndStopsReturningOldTableAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, settings) = CreateScenario();
        var generator = new NotchTableGenerator();
        var generated = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);
        var fingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var cache = new NotchExportGenerationCacheService();
        cache.Store(17UL, 34UL, 3, fingerprint, generated.Table);
        Assert.True(cache.TryGet(17UL, 34UL, 3, fingerprint, out var original));
        Assert.Same(generated.Table, original);

        var lease = cache.AcquireCadAllocationResolvedBatch(
            cache.CaptureGenerationEpoch(), 17UL, 34UL, 3, fingerprint, settings,
            () => Task.FromResult(generated.Batch));
        var batch = await lease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);
        var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);

        Assert.True(lease.IsTaskOwner);
        Assert.Null(lease.CachedBatch);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.Empty, lease.Lookup.Status);
        Assert.Equal(generated.Table.Rows.Select(static row => row.Values),
            projected.Rows.Select(static row => row.Values));
        Assert.NotEmpty(projected.Rows);
        Assert.True(cache.TryUpdateProjectedRowCount(cache.CaptureGenerationEpoch(), batch, projected.Rows.Count));
        Assert.False(cache.TryGet(17UL, 34UL, 3, fingerprint, out var staleTable, out var lookup));
        Assert.Null(staleTable);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.Empty, lookup.Status);
        var snapshot = cache.GetSnapshot();
        Assert.True(snapshot.HasEntry);
        Assert.Equal(projected.Rows.Count, snapshot.EntryRowCount);
        Assert.Equal(2, snapshot.StoreCount);
    }

    [Fact]
    public async Task AcquireBatch_CompletedSettingsCollision_ResolvesCompatibleBatchForTheCurrentRequestAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, originalSettings) = CreateScenario();
        originalSettings.Notch.BoundaryVirtualAreaCapRatio = 7.4079;
        originalSettings.Notch.MultiOwnerStrictOverlapPercent = 66.73;
        var (_, _, requestedSettings) = CreateScenario();
        requestedSettings.Notch.BoundaryVirtualAreaCapRatio = 1.6586;
        requestedSettings.Notch.MultiOwnerStrictOverlapPercent = 79.07;
        var fingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(originalSettings);
        Assert.Equal(fingerprint,
            NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(requestedSettings));
        var generator = new NotchTableGenerator();
        var originalBatch = generator.ResolveCadAllocationBatch(cad, grid, originalSettings);
        var requestedBatch = generator.ResolveCadAllocationBatch(cad, grid, requestedSettings);
        var cache = new NotchExportGenerationCacheService();
        var originalLease = cache.AcquireCadAllocationResolvedBatch(
            cache.CaptureGenerationEpoch(), 17UL, 34UL, 3, fingerprint, originalSettings,
            () => Task.FromResult(originalBatch));
        await originalLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);

        var lease = cache.AcquireCadAllocationResolvedBatch(
            cache.CaptureGenerationEpoch(), 17UL, 34UL, 3, fingerprint, requestedSettings,
            () => Task.FromResult(requestedBatch));

        Assert.True(lease.IsTaskOwner);
        Assert.Null(lease.CachedBatch);
        Assert.Equal(NotchExportGenerationCacheLookupStatus.SettingsFingerprintChanged, lease.Lookup.Status);
        var resolved = await lease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Same(requestedBatch, resolved);
        Assert.True(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(resolved, requestedSettings));
        Assert.False(NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(originalBatch, requestedSettings));
        var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(resolved, requestedSettings);
        var fresh = generator.Generate(cad, grid, requestedSettings);
        Assert.Equal(fresh.Rows.Select(static row => row.Values), projected.Rows.Select(static row => row.Values));
        Assert.Equal(2, cache.GetSnapshot().MissCount);
        Assert.Equal(2, cache.GetSnapshot().StoreCount);
    }

    [Fact]
    public async Task TryUpdateProjectedRowCount_SupersededBatchCannotOverwriteTheNewGridProjectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, settings) = CreateScenario();
        var fingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var generator = new NotchTableGenerator();
        var original = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);
        var cache = new NotchExportGenerationCacheService();
        var epoch = cache.CaptureGenerationEpoch();
        var originalLease = cache.AcquireCadAllocationResolvedBatch(
            epoch, 17UL, 34UL, 3, fingerprint, settings, () => Task.FromResult(original.Batch));
        await originalLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);
        grid.Pads[0].DiffIndex = 27;
        var replacement = generator.GenerateCadAllocationResolvedBatch(cad, grid, settings);
        var replacementLease = cache.AcquireCadAllocationResolvedBatch(
            epoch, 17UL, 35UL, 3, fingerprint, settings, () => Task.FromResult(replacement.Batch));
        await replacementLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);

        var accepted = cache.TryUpdateProjectedRowCount(epoch, replacement.Batch, replacement.Table.Rows.Count);
        var staleAccepted = cache.TryUpdateProjectedRowCount(epoch, original.Batch, original.Table.Rows.Count);

        Assert.True(accepted);
        Assert.False(staleAccepted);
        Assert.NotEmpty(replacement.Table.Rows);
        Assert.Equal(27, Assert.Single(replacement.Table.Rows).DiffIndex);
        var snapshot = cache.GetSnapshot();
        Assert.Equal(35UL, snapshot.GridSignature);
        Assert.Equal(replacement.Table.Rows.Count, snapshot.EntryRowCount);
        Assert.Equal(2, snapshot.StoreCount);
    }

    [Fact]
    public async Task AcquireBatch_GridChangesDuringResolution_KeepsTheNewRequestWhenTheOldOneCompletesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (cad, grid, settings) = CreateScenario();
        var fingerprint = NotchTableGenerator.ComputeCadAllocationResolvedBatchSettingsFingerprint(settings);
        var generator = new NotchTableGenerator();
        var original = generator.ResolveCadAllocationBatch(cad, grid, settings);
        grid.Pads[0].DiffIndex = 27;
        var replacement = generator.ResolveCadAllocationBatch(cad, grid, settings);
        var firstRelease = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementRelease = new TaskCompletionSource<NotchTableGenerator.CadAllocationResolvedBatch>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new NotchExportGenerationCacheService();
        var epoch = cache.CaptureGenerationEpoch();
        var firstLease = cache.AcquireCadAllocationResolvedBatch(
            epoch, 17UL, 34UL, 3, fingerprint, settings,
            () => firstRelease.Task.WaitAsync(TestContext.Current.CancellationToken));
        var replacementLease = cache.AcquireCadAllocationResolvedBatch(
            epoch, 17UL, 35UL, 3, fingerprint, settings,
            () => replacementRelease.Task.WaitAsync(TestContext.Current.CancellationToken));

        try
        {
            Assert.True(replacementLease.IsTaskOwner);
            Assert.NotSame(firstLease.ResolutionTask, replacementLease.ResolutionTask);
            firstRelease.SetResult(original);
            await firstLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(cache.GetSnapshot().HasEntry);

            var joinedLease = cache.AcquireCadAllocationResolvedBatch(
                epoch, 17UL, 35UL, 3, fingerprint, settings,
                static () => throw new InvalidOperationException("Pending replacement must be shared."));
            Assert.False(joinedLease.IsTaskOwner);
            Assert.Same(replacementLease.ResolutionTask, joinedLease.ResolutionTask);

            replacementRelease.SetResult(replacement);
            var batch = await replacementLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken);
            var projected = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);
            Assert.Equal(27, Assert.Single(projected.Rows).DiffIndex);
            var snapshot = cache.GetSnapshot();
            Assert.Equal(35UL, snapshot.GridSignature);
            Assert.Equal(2, snapshot.MissCount);
            Assert.Equal(1, snapshot.StoreCount);
        }
        finally
        {
            firstRelease.TrySetResult(original);
            replacementRelease.TrySetResult(replacement);
            await Task.WhenAll(
                firstLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken),
                replacementLease.ResolutionTask!.WaitAsync(TestContext.Current.CancellationToken));
        }
    }

    private static (CadPadSet Cad, RegularGrid Grid, ProjectSettings Settings) CreateScenario()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10d, cellHeight: 10d);
        var cad = TestGeometryFactory.CreateCadPad(42, "SENSOR", 0d, 0d, 15d, 10d);
        foreach (var pad in grid.Pads)
        {
            pad.Freeform = FreeformType.XWay;
            pad.MatchedCadPadId = cad.Id;
            pad.MatchScore = 1d;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                EnabledVersions = [NotchAlgorithmVersion.V22],
                LinkVersionThresholds = false,
            },
        };
        return (new CadPadSet([cad]), grid, settings);
    }
}
