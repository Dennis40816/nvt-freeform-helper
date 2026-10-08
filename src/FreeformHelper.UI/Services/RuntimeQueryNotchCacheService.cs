using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.RuntimeQuery;

namespace FreeformHelper.UI.Services;

internal sealed class RuntimeQueryNotchCacheService
{
    private readonly Dictionary<int, RuntimeQueryNotchCacheEntry> _cacheByCadId = new();
    private int _revision = -1;
    private long _hitCount;
    private long _missCount;
    private long _revisionResetCount;
    private int _lastClearSize;

    public bool TryGetCadNotchCacheEntry(
        FreeformHelperViewModel helper,
        int cadPadId,
        out RuntimeQueryNotchCacheEntry entry,
        out RuntimeQueryResponseEnvelope? error)
    {
        entry = default!;
        error = null;

        var currentRevision = helper.GetNotchStep3Revision();
        if (_revision != currentRevision)
        {
            _lastClearSize = _cacheByCadId.Count;
            Interlocked.Increment(ref _revisionResetCount);
            _revision = currentRevision;
            _cacheByCadId.Clear();
        }

        if (_cacheByCadId.TryGetValue(cadPadId, out var cachedEntry))
        {
            Interlocked.Increment(ref _hitCount);
            entry = cachedEntry;
            return true;
        }

        Interlocked.Increment(ref _missCount);

        var snapshot = helper.BuildCadPadInspectorSnapshot(cadPadId);
        if (snapshot?.Cad is not CadPadInspectorSnapshot cad)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "PAD_NOT_FOUND",
                message: $"CAD pad {cadPadId} is not visible.");
            return false;
        }

        var resolved = helper.GetCadV22ResolvedResult(cadPadId);
        if (resolved is null)
        {
            error = RuntimeQueryResponseEnvelope.Failure(
                code: "NOT_READY",
                message: "Notch compensation is unavailable. Build grid and ensure CAD is visible.");
            return false;
        }

        entry = new RuntimeQueryNotchCacheEntry(
            Revision: currentRevision,
            Cad: cad,
            Resolved: resolved,
            DiagnosticsText: cad.Notch?.Diagnostics);
        _cacheByCadId[cadPadId] = entry;
        return true;
    }

    public RuntimeQueryNotchCacheMetrics GetMetrics()
    {
        var hitCount = Interlocked.Read(ref _hitCount);
        var missCount = Interlocked.Read(ref _missCount);
        var total = hitCount + missCount;
        var hitRate = total > 0 ? (double)hitCount / total : 0.0;
        return new RuntimeQueryNotchCacheMetrics(
            Revision: _revision,
            EntryCount: _cacheByCadId.Count,
            LastClearSize: _lastClearSize,
            HitCount: hitCount,
            MissCount: missCount,
            RevisionResetCount: Interlocked.Read(ref _revisionResetCount),
            HitRate: hitRate);
    }
}

internal sealed record RuntimeQueryNotchCacheEntry(
    int Revision,
    CadPadInspectorSnapshot Cad,
    NotchV22ResolvedResult Resolved,
    string? DiagnosticsText);

internal readonly record struct RuntimeQueryNotchCacheMetrics(
    int Revision,
    int EntryCount,
    int LastClearSize,
    long HitCount,
    long MissCount,
    long RevisionResetCount,
    double HitRate);
