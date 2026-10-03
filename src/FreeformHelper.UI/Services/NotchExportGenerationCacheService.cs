using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.UI.Services;

/// <summary>
/// Caches the Step 5 generation artifact for one source/revision/settings identity.
/// </summary>
public sealed class NotchExportGenerationCacheService
{
    private const ulong FnvOffsetBasis = 1469598103934665603UL;
    private const ulong FnvPrime = 1099511628211UL;
    private const double SignatureCoordinateScale = 1_000_000.0;

    private readonly object _generationEpochGate = new();
    private CacheEntry? _entry;
    private CadAllocationInFlightEntry? _cadAllocationInFlight;
    private long _generationEpoch;
    private long _hitCount;
    private long _missCount;
    private long _storeCount;
    private long _clearCount;

    public bool TryGet(
        CadPadSet cad,
        RegularGrid grid,
        int step3Revision,
        int settingsFingerprint,
        out NotchTable table)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadFingerprint = ComputeCadSignature(cad);
        var gridFingerprint = ComputeGridSignature(grid);
        return TryGet(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            out table);
    }

    public bool TryGet(
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        out NotchTable table)
    {
        return TryGet(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            out table,
            out _);
    }

    public bool TryGet(
        CadPadSet cad,
        RegularGrid grid,
        int step3Revision,
        int settingsFingerprint,
        out NotchTable table,
        out NotchExportGenerationCacheLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadFingerprint = ComputeCadSignature(cad);
        var gridFingerprint = ComputeGridSignature(grid);
        return TryGet(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            out table,
            out lookup);
    }

    public bool TryGet(
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        out NotchTable table,
        out NotchExportGenerationCacheLookup lookup)
    {
        table = null!;

        if (!TryGetMatchingEntry(
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                out var entry,
                out lookup))
        {
            return false;
        }

        if (entry.Table is not { } cachedTable)
        {
            _missCount++;
            lookup = CreateLookup(
                NotchExportGenerationCacheLookupStatus.Empty,
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                entry);
            return false;
        }

        _hitCount++;
        table = cachedTable;
        return true;
    }

    public void Store(
        CadPadSet cad,
        RegularGrid grid,
        int step3Revision,
        int settingsFingerprint,
        NotchTable table)
    {
        ArgumentNullException.ThrowIfNull(cad);
        ArgumentNullException.ThrowIfNull(grid);

        var cadFingerprint = ComputeCadSignature(cad);
        var gridFingerprint = ComputeGridSignature(grid);
        Store(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            table);
    }

    public void Store(
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        NotchTable table)
    {
        _ = TryStore(
            CaptureGenerationEpoch(),
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            table);
    }

    internal long CaptureGenerationEpoch() => Interlocked.Read(ref _generationEpoch);

    internal bool IsGenerationEpochCurrent(long expectedEpoch) =>
        Interlocked.Read(ref _generationEpoch) == expectedEpoch;

    internal NotchCadAllocationBatchLease AcquireCadAllocationResolvedBatch(
        long expectedEpoch,
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        ProjectSettings settings,
        Func<Task<NotchTableGenerator.CadAllocationResolvedBatch>> taskFactory)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(taskFactory);

        lock (_generationEpochGate)
        {
            var status = GetLookupStatus(
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                _entry);
            if (status == NotchExportGenerationCacheLookupStatus.Hit)
            {
                if (_entry is not { Batch: { } storedBatch })
                {
                    status = NotchExportGenerationCacheLookupStatus.Empty;
                }
                else if (!NotchTableGenerator.IsCadAllocationResolvedBatchCompatible(storedBatch, settings))
                {
                    status = NotchExportGenerationCacheLookupStatus.SettingsFingerprintChanged;
                }
            }

            var lookup = CreateLookup(
                status,
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                _entry);
            if (status == NotchExportGenerationCacheLookupStatus.Hit &&
                _entry is { Batch: { } cachedBatch })
            {
                _hitCount++;
                return new NotchCadAllocationBatchLease(cachedBatch, null, false, lookup);
            }

            if (_cadAllocationInFlight is { } inFlight &&
                inFlight.Matches(
                    expectedEpoch,
                    cadFingerprint,
                    gridFingerprint,
                    step3Revision,
                    settingsFingerprint,
                    settings))
            {
                return new NotchCadAllocationBatchLease(null, inFlight.Task, false, lookup);
            }

            _missCount++;
            var entry = new CadAllocationInFlightEntry(
                expectedEpoch,
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                settings);
            _cadAllocationInFlight = entry;
            entry.Task = ResolveAndStoreCadAllocationBatchAsync(entry, taskFactory);
            return new NotchCadAllocationBatchLease(null, entry.Task, true, lookup);
        }
    }

    private async Task<NotchTableGenerator.CadAllocationResolvedBatch> ResolveAndStoreCadAllocationBatchAsync(
        CadAllocationInFlightEntry inFlight,
        Func<Task<NotchTableGenerator.CadAllocationResolvedBatch>> taskFactory)
    {
        try
        {
            var batch = await taskFactory().ConfigureAwait(false);
            lock (_generationEpochGate)
            {
                if (_generationEpoch == inFlight.GenerationEpoch &&
                    ReferenceEquals(_cadAllocationInFlight, inFlight))
                {
                    _entry = new CacheEntry(
                        CadSignature: inFlight.CadSignature,
                        GridSignature: inFlight.GridSignature,
                        Step3Revision: inFlight.Step3Revision,
                        SettingsFingerprint: inFlight.SettingsFingerprint,
                        Table: null,
                        Batch: batch,
                        EntryRowCount: 0);
                    _storeCount++;
                }
            }

            return batch;
        }
        finally
        {
            lock (_generationEpochGate)
            {
                if (ReferenceEquals(_cadAllocationInFlight, inFlight))
                {
                    _cadAllocationInFlight = null;
                }
            }
        }
    }

    internal bool TryStore(
        long expectedEpoch,
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        NotchTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return TryStoreEntry(
            expectedEpoch,
            new CacheEntry(
                CadSignature: cadFingerprint,
                GridSignature: gridFingerprint,
                Step3Revision: step3Revision,
                SettingsFingerprint: settingsFingerprint,
                Table: table,
                Batch: null,
                EntryRowCount: table.Rows.Count));
    }

    internal bool TryUpdateProjectedRowCount(
        long expectedEpoch,
        NotchTableGenerator.CadAllocationResolvedBatch batch,
        int projectedRowCount)
    {
        ArgumentNullException.ThrowIfNull(batch);

        lock (_generationEpochGate)
        {
            if (_generationEpoch != expectedEpoch ||
                _entry is not { Batch: { } cachedBatch } entry ||
                !ReferenceEquals(cachedBatch, batch))
            {
                return false;
            }

            _entry = entry with { EntryRowCount = Math.Max(0, projectedRowCount) };
            return true;
        }
    }

    internal void InvalidateCadAllocationProjectedRowCount()
    {
        lock (_generationEpochGate)
        {
            if (_entry is { Batch: not null } entry)
            {
                _entry = entry with { EntryRowCount = 0 };
            }
        }
    }

    public void Clear()
    {
        lock (_generationEpochGate)
        {
            Interlocked.Increment(ref _generationEpoch);
            _cadAllocationInFlight = null;

            if (_entry is null)
            {
                return;
            }

            _entry = null;
            _clearCount++;
        }
    }

    private bool TryStoreEntry(long expectedEpoch, CacheEntry entry)
    {
        lock (_generationEpochGate)
        {
            if (_generationEpoch != expectedEpoch)
            {
                return false;
            }

            _entry = entry;
            _storeCount++;
            return true;
        }
    }

    public NotchExportGenerationCacheMetricsSnapshot GetSnapshot()
    {
        return _entry is null
            ? new NotchExportGenerationCacheMetricsSnapshot(
                HasEntry: false,
                EntryRowCount: 0,
                CadSignature: 0,
                GridSignature: 0,
                Step3Revision: -1,
                SettingsFingerprint: 0,
                HitCount: _hitCount,
                MissCount: _missCount,
                StoreCount: _storeCount,
                ClearCount: _clearCount)
            : new NotchExportGenerationCacheMetricsSnapshot(
                HasEntry: true,
                EntryRowCount: _entry.EntryRowCount,
                CadSignature: _entry.CadSignature,
                GridSignature: _entry.GridSignature,
                Step3Revision: _entry.Step3Revision,
                SettingsFingerprint: _entry.SettingsFingerprint,
                HitCount: _hitCount,
                MissCount: _missCount,
                StoreCount: _storeCount,
                ClearCount: _clearCount);
    }

    private bool TryGetMatchingEntry(
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        out CacheEntry entry,
        out NotchExportGenerationCacheLookup lookup)
    {
        entry = null!;
        var cachedEntry = _entry;
        if (cachedEntry is null)
        {
            _missCount++;
            lookup = CreateLookup(
                NotchExportGenerationCacheLookupStatus.Empty,
                cadFingerprint,
                gridFingerprint,
                step3Revision,
                settingsFingerprint,
                cachedEntry);
            return false;
        }

        var status = GetLookupStatus(
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            cachedEntry);
        lookup = CreateLookup(
            status,
            cadFingerprint,
            gridFingerprint,
            step3Revision,
            settingsFingerprint,
            cachedEntry);
        if (status != NotchExportGenerationCacheLookupStatus.Hit)
        {
            _missCount++;
            return false;
        }

        entry = cachedEntry;
        return true;
    }

    private static NotchExportGenerationCacheLookup CreateLookup(
        NotchExportGenerationCacheLookupStatus status,
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        CacheEntry? cachedEntry)
    {
        return new NotchExportGenerationCacheLookup(
            Status: status,
            RequestedCadSignature: cadFingerprint,
            RequestedGridSignature: gridFingerprint,
            RequestedStep3Revision: step3Revision,
            RequestedSettingsFingerprint: settingsFingerprint,
            CachedCadSignature: cachedEntry?.CadSignature ?? 0,
            CachedGridSignature: cachedEntry?.GridSignature ?? 0,
            CachedStep3Revision: cachedEntry?.Step3Revision ?? -1,
            CachedSettingsFingerprint: cachedEntry?.SettingsFingerprint ?? 0);
    }

    private static NotchExportGenerationCacheLookupStatus GetLookupStatus(
        ulong cadFingerprint,
        ulong gridFingerprint,
        int step3Revision,
        int settingsFingerprint,
        CacheEntry? cachedEntry)
    {
        return cachedEntry is null
            ? NotchExportGenerationCacheLookupStatus.Empty
            : cachedEntry.CadSignature != cadFingerprint
                ? NotchExportGenerationCacheLookupStatus.CadSignatureChanged
                : cachedEntry.GridSignature != gridFingerprint
                    ? NotchExportGenerationCacheLookupStatus.GridSignatureChanged
                    : cachedEntry.Step3Revision != step3Revision
                        ? NotchExportGenerationCacheLookupStatus.Step3RevisionChanged
                        : cachedEntry.SettingsFingerprint != settingsFingerprint
                            ? NotchExportGenerationCacheLookupStatus.SettingsFingerprintChanged
                            : NotchExportGenerationCacheLookupStatus.Hit;
    }

    private static ulong ComputeCadSignature(CadPadSet cad)
    {
        var hash = FnvOffsetBasis;
        hash = MixHash(hash, ToUnsigned(cad.Pads.Count));
        foreach (var pad in cad.Pads)
        {
            hash = MixHash(hash, ToUnsigned(pad.Id));
            hash = MixHash(hash, ToUnsigned(pad.Polygon.Vertices.Length));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MinX)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MinY)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MaxX)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MaxY)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Area)));
        }

        return hash;
    }

    private static ulong ComputeGridSignature(RegularGrid grid)
    {
        var hash = FnvOffsetBasis;
        hash = MixHash(hash, ToUnsigned(grid.Rows));
        hash = MixHash(hash, ToUnsigned(grid.Cols));
        hash = MixHash(hash, ToUnsigned(grid.XEdges.Count));
        hash = MixHash(hash, ToUnsigned(grid.YEdges.Count));
        hash = MixHash(hash, ToUnsigned(grid.Pads.Count));

        foreach (var edge in grid.XEdges)
        {
            hash = MixHash(hash, ToUnsigned(Quantize(edge)));
        }

        foreach (var edge in grid.YEdges)
        {
            hash = MixHash(hash, ToUnsigned(Quantize(edge)));
        }

        foreach (var pad in grid.Pads)
        {
            hash = MixHash(hash, ToUnsigned(pad.Index));
            hash = MixHash(hash, ToUnsigned(pad.RegularPadId));
            hash = MixHash(hash, ToUnsigned(pad.Row));
            hash = MixHash(hash, ToUnsigned(pad.Col));
            hash = MixHash(hash, ToUnsigned(pad.IcIndex));
            hash = MixHash(hash, ToUnsigned(pad.DiffIndex));
            hash = MixHash(hash, ToUnsigned(pad.MatchedCadPadId ?? -1));
            hash = MixHash(hash, ToUnsigned((int)pad.Freeform));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MinX)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MinY)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MaxX)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.Bounds.MaxY)));
            hash = MixHash(hash, ToUnsigned(Quantize(pad.MatchScore)));
        }

        return hash;
    }

    private static ulong MixHash(ulong hash, ulong value)
    {
        unchecked
        {
            return (hash ^ value) * FnvPrime;
        }
    }

    private static long Quantize(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return (long)Math.Round(value * SignatureCoordinateScale, MidpointRounding.AwayFromZero);
    }

    private static ulong ToUnsigned(int value)
    {
        return unchecked((ulong)(uint)value);
    }

    private static ulong ToUnsigned(long value)
    {
        return unchecked((ulong)value);
    }

    private sealed record CacheEntry(
        ulong CadSignature,
        ulong GridSignature,
        int Step3Revision,
        int SettingsFingerprint,
        NotchTable? Table,
        NotchTableGenerator.CadAllocationResolvedBatch? Batch,
        int EntryRowCount);

    private sealed record CadAllocationInFlightEntry(
        long GenerationEpoch,
        ulong CadSignature,
        ulong GridSignature,
        int Step3Revision,
        int SettingsFingerprint,
        ProjectSettings Settings)
    {
        public Task<NotchTableGenerator.CadAllocationResolvedBatch> Task { get; set; } = null!;

        public bool Matches(
            long expectedEpoch,
            ulong cadFingerprint,
            ulong gridFingerprint,
            int expectedStep3Revision,
            int expectedSettingsFingerprint,
            ProjectSettings expectedSettings) =>
            GenerationEpoch == expectedEpoch &&
            CadSignature == cadFingerprint &&
            GridSignature == gridFingerprint &&
            Step3Revision == expectedStep3Revision &&
            SettingsFingerprint == expectedSettingsFingerprint &&
            NotchTableGenerator.HaveEquivalentCadAllocationResolutionSettings(
                Settings,
                expectedSettings);
    }
}

internal readonly record struct NotchCadAllocationBatchLease(
    NotchTableGenerator.CadAllocationResolvedBatch? CachedBatch,
    Task<NotchTableGenerator.CadAllocationResolvedBatch>? ResolutionTask,
    bool IsTaskOwner,
    NotchExportGenerationCacheLookup Lookup);

public readonly record struct NotchExportGenerationCacheLookup(
    NotchExportGenerationCacheLookupStatus Status,
    ulong RequestedCadSignature,
    ulong RequestedGridSignature,
    int RequestedStep3Revision,
    int RequestedSettingsFingerprint,
    ulong CachedCadSignature,
    ulong CachedGridSignature,
    int CachedStep3Revision,
    int CachedSettingsFingerprint)
{
    public bool IsHit => Status == NotchExportGenerationCacheLookupStatus.Hit;
}

public enum NotchExportGenerationCacheLookupStatus
{
    Hit,
    Empty,
    CadSignatureChanged,
    GridSignatureChanged,
    Step3RevisionChanged,
    SettingsFingerprintChanged,
}

public readonly record struct NotchExportGenerationCacheMetricsSnapshot(
    bool HasEntry,
    int EntryRowCount,
    ulong CadSignature,
    ulong GridSignature,
    int Step3Revision,
    int SettingsFingerprint,
    long HitCount,
    long MissCount,
    long StoreCount,
    long ClearCount);
