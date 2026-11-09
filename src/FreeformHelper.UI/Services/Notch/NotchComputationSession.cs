// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Services;

namespace FreeformHelper.UI.Services;

/// <summary>Owns the resolved Notch cache and its invalidation lifecycle. Access is UI-thread-only.</summary>
internal sealed class NotchComputationSession
{
    private readonly Dictionary<int, NotchResolvedCacheEntry> _resolvedResultByCadId = new();
    private readonly NotchCompensationCacheCounters _counters = new();

    internal int Revision { get; private set; }

    internal void Invalidate()
    {
        _counters.RecordInvalidation(_resolvedResultByCadId.Count);
        Revision++;
        _resolvedResultByCadId.Clear();
    }

    internal NotchComputationCacheMetrics GetMetrics()
    {
        var hitCount = _counters.HitCount;
        var missCount = _counters.MissCount;
        var invalidationCount = _counters.InvalidationCount;
        return new NotchComputationCacheMetrics(
            Revision,
            _resolvedResultByCadId.Count,
            _counters.LastClearSize,
            hitCount,
            missCount,
            invalidationCount);
    }

    internal bool Contains(int cadPadId) => _resolvedResultByCadId.ContainsKey(cadPadId);

    internal NotchV22ResolvedResult? GetOrBuild(
        NotchComputationRequest request,
        NotchV22CompensationResult? precomputedCompensation = null,
        NotchV22ResolvedResult? precomputedResolved = null,
        bool buildIfMissing = true)
    {
        if (_resolvedResultByCadId.TryGetValue(request.CadPad.Id, out var cached) && Matches(cached, request))
        {
            _counters.RecordHit();
            return cached.Resolved;
        }

        if (!buildIfMissing)
        {
            return null;
        }

        _counters.RecordMiss();
        var resolved = BuildCadV22ResolvedResult(request, precomputedCompensation, precomputedResolved);
        _resolvedResultByCadId[request.CadPad.Id] = new NotchResolvedCacheEntry(
            Revision,
            request.CadPoolFingerprint,
            request.ActiveRegularHash,
            request.GridFingerprint,
            request.EnableToRegular,
            request.EnableToFull,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode,
            resolved);
        return resolved;
    }

    private bool Matches(NotchResolvedCacheEntry cached, NotchComputationRequest request) =>
        cached.Revision == Revision &&
        cached.CadPoolFingerprint == request.CadPoolFingerprint &&
        cached.ActiveRegularHash == request.ActiveRegularHash &&
        cached.GridFingerprint == request.GridFingerprint &&
        cached.EnableToRegular == request.EnableToRegular &&
        cached.EnableToFull == request.EnableToFull &&
        cached.EnableToFullRuleEngine == request.EnableToFullRuleEngine &&
        cached.EnableToFullRuleTrace == request.EnableToFullRuleTrace &&
        Math.Abs(cached.StrictOverlapRatio - request.StrictOverlapRatio) <= 1e-12 &&
        cached.AnchorIcIndex == request.AnchorIcIndex &&
        cached.AnchorDiffIndex == request.AnchorDiffIndex &&
        cached.TargetAllocationAreaMode == request.AllocationAreaMode;

    private static NotchV22ResolvedResult BuildCadV22ResolvedResult(
        NotchComputationRequest request,
        NotchV22CompensationResult? precomputedCompensation,
        NotchV22ResolvedResult? precomputedResolved)
    {
        var resolutionRequest = new NotchResolvedResultRequest(
            request.CadPad,
            request.Grid,
            request.Settings,
            request.AllCadPads,
            request.ActiveRegularPadIds,
            request.CompensationModel,
            request.EnableToRegular,
            request.EnableToFull,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode,
            precomputedCompensation);
        return precomputedResolved is not null &&
               NotchDetailUseCase.MatchesRequest(precomputedResolved, resolutionRequest)
            ? precomputedResolved
            : NotchDetailUseCase.BuildResolvedResult(resolutionRequest);
    }
}
