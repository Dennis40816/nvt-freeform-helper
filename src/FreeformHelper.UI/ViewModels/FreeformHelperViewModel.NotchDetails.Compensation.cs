using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public int GetNotchStep3Revision()
    {
        return _notchCompensationCacheRevision;
    }

    public NotchCompensationCacheMetricsSnapshot GetNotchCompensationCacheMetrics()
    {
        var hitCount = Interlocked.Read(ref _notchCompensationCacheHitCount);
        var missCount = Interlocked.Read(ref _notchCompensationCacheMissCount);
        var invalidationCount = Interlocked.Read(ref _notchCompensationCacheInvalidationCount);
        var total = hitCount + missCount;
        var hitRate = total > 0 ? (double)hitCount / total : 0.0;
        return new NotchCompensationCacheMetricsSnapshot(
            Revision: _notchCompensationCacheRevision,
            EntryCount: _notchResolvedResultCacheByCadId.Count,
            LastClearSize: _notchCompensationCacheLastClearSize,
            HitCount: hitCount,
            MissCount: missCount,
            InvalidationCount: invalidationCount,
            HitRate: hitRate);
    }

    public NotchExportGenerationCacheMetricsSnapshot GetNotchExportGenerationCacheMetrics()
    {
        return _notchExportGenerationCacheService.GetSnapshot();
    }

    public double GetNotchMultiOwnerStrictOverlapPercent()
    {
        return Math.Clamp((double)ToFullStrictOverlapPercent, 0.0, 100.0);
    }

    public (double ToRegularRatio, double ToFullRatio, double CombinedRatio, bool IsToFullEnabled)? GetCadV22CompensationPreview(int cadPadId)
    {
        var compensation = GetCadV22CompensationResult(cadPadId);
        return compensation is null
            ? null
            : (compensation.ToRegularRatio, compensation.ToFullRatio, compensation.CombinedRatio, compensation.IsToFullEnabled);
    }

    public string? GetCadV22CompensationDiagnostics(int cadPadId)
    {
        var compensation = GetCadV22CompensationResult(cadPadId);
        return compensation is null ? null : BuildToFullDiagnosticsText(compensation);
    }

    public IReadOnlyList<(int CadId, double RatioPercent)> GetCrossIcOwnerSharesForRegularPad(
        int regularPadId,
        double? strictOverlapPercentOverride = null)
    {
        if (_grid is null)
        {
            return Array.Empty<(int CadId, double RatioPercent)>();
        }

        var regular = _grid.Pads.FirstOrDefault(pad => pad.RegularPadId == regularPadId);
        if (regular is null)
        {
            return Array.Empty<(int CadId, double RatioPercent)>();
        }

        var visibleCadPads = CadPads
            .Where(pad => !IsCadPadEffectivelyHidden(pad.Id))
            .ToList();
        var strictOverlapRatio = NotchV22CompensationService.NormalizeStrictOverlapRatio(
            (strictOverlapPercentOverride.HasValue
                ? Math.Clamp(strictOverlapPercentOverride.Value, 0.0, 100.0) / 100.0
                : GetStrictOverlapRatioForNotchCompensation()));
        return ComputeOwnerInfosForRegular(regular, visibleCadPads, strictOverlapRatio);
    }

    public NotchV22CompensationResult? GetCadV22CompensationResult(int cadPadId, double? strictOverlapRatioOverride = null)
    {
        if (!TryResolveNotchComputationInputs(cadPadId, out var cad, out var allCadPads, out var activeRegularPadIds))
        {
            return null;
        }

        return GetOrBuildCadV22ResolvedResultCached(
            cad,
            allCadPads,
            activeRegularPadIds,
            BuildWorkflowDataSnapshot(),
            strictOverlapRatioOverride)?.Compensation;
    }

    public NotchV22TargetAllocationSummary? GetCadV22TargetAllocationSummary(int cadPadId, double? strictOverlapRatioOverride = null)
    {
        return GetCadV22ResolvedResult(cadPadId, strictOverlapRatioOverride)?.TargetAllocation;
    }

    public NotchV22ResolvedResult? GetCadV22ResolvedResult(int cadPadId, double? strictOverlapRatioOverride = null)
    {
        if (!TryResolveNotchComputationInputs(cadPadId, out var cad, out var allCadPads, out var activeRegularPadIds))
        {
            return null;
        }

        var workflowSnapshot = BuildWorkflowDataSnapshot();
        return GetOrBuildCadV22ResolvedResultCached(
            cad,
            allCadPads,
            activeRegularPadIds,
            workflowSnapshot,
            strictOverlapRatioOverride);
    }

    public (bool IsToFullEnabled, IReadOnlyList<Polygon2> Stage1Seed, IReadOnlyList<Polygon2> Stage2Candidate, IReadOnlyList<Polygon2> Stage3Final)? GetCadV22StageOverlays(int cadPadId)
    {
        var resolved = GetCadV22ResolvedResult(cadPadId);
        if (resolved is null)
        {
            return null;
        }

        return (
            resolved.Compensation.IsToFullEnabled,
            resolved.Stage1SeedPolygons,
            resolved.Stage2CandidatePolygons,
            resolved.Stage3FinalOutlinePolygons);
    }

    private NotchV22ResolvedResult? GetOrBuildCadV22ResolvedResultCached(
        CadPad cadPad,
        IReadOnlyList<CadPad> allCadPads,
        IReadOnlySet<int>? activeRegularPadIds,
        WorkflowDataSnapshot workflowSnapshot,
        double? strictOverlapRatioOverride = null,
        NotchV22CompensationResult? precomputedCompensation = null,
        NotchV22ResolvedResult? precomputedResolved = null,
        bool buildIfMissing = true)
    {
        var cadPadId = cadPad.Id;
        if (_grid is null)
        {
            throw new InvalidOperationException("Resolved notch result requires an active regular grid.");
        }

        var activeRegularHash = ComputeActiveRegularHash(activeRegularPadIds);
        var cadPoolFingerprint = ComputeCadPoolFingerprint(allCadPads);
        var gridFingerprint = BuildNotchComputationGridFingerprint(_grid);
        var strictOverlapRatio = NotchV22CompensationService.NormalizeStrictOverlapRatio(
            strictOverlapRatioOverride ?? GetStrictOverlapRatioForNotchCompensation());
        var anchorDiff = workflowSnapshot.TryGetCadOutputFwDiffIndex(cadPadId, out var outputFwDiffIndex)
            ? outputFwDiffIndex
            : (int?)null;
        var anchorIc = workflowSnapshot.TryGetCadIcIndex(cadPadId, out var icIndex)
            ? icIndex
            : (int?)null;
        var allocationAreaMode = NotchV22TargetAllocationPolicy.ResolveAreaMode(SelectedNotchCompensationModelOption.Value);
        if (_notchResolvedResultCacheByCadId.TryGetValue(cadPadId, out var cached) &&
            cached.Revision == _notchCompensationCacheRevision &&
            cached.CadPoolFingerprint == cadPoolFingerprint &&
            cached.ActiveRegularHash == activeRegularHash &&
            cached.GridFingerprint == gridFingerprint &&
            cached.EnableToRegular == EnableToRegular &&
            cached.EnableToFull == EnableToFull &&
            cached.EnableToFullRuleEngine == EnableToFullRuleEngine &&
            cached.EnableToFullRuleTrace == EnableToFullRuleTrace &&
            Math.Abs(cached.StrictOverlapRatio - strictOverlapRatio) <= 1e-12 &&
            cached.AnchorIcIndex == anchorIc &&
            cached.AnchorDiffIndex == anchorDiff &&
            cached.TargetAllocationAreaMode == allocationAreaMode)
        {
            Interlocked.Increment(ref _notchCompensationCacheHitCount);
            return cached.Resolved;
        }

        if (!buildIfMissing)
        {
            return null;
        }

        Interlocked.Increment(ref _notchCompensationCacheMissCount);
        var resolutionRequest = new NotchResolvedResultRequest(
            cadPad,
            _grid,
            _projectFile.Settings,
            allCadPads,
            activeRegularPadIds,
            SelectedNotchCompensationModelOption.Value,
            EnableToRegular,
            EnableToFull,
            EnableToFullRuleEngine,
            EnableToFullRuleTrace,
            strictOverlapRatio,
            anchorIc,
            anchorDiff,
            allocationAreaMode,
            precomputedCompensation);
        var resolved = precomputedResolved is not null &&
                       NotchDetailUseCase.MatchesRequest(precomputedResolved, resolutionRequest)
            ? precomputedResolved
            : NotchDetailUseCase.BuildResolvedResult(resolutionRequest);

        _notchResolvedResultCacheByCadId[cadPadId] = (
            _notchCompensationCacheRevision,
            cadPoolFingerprint,
            activeRegularHash,
            gridFingerprint,
            EnableToRegular,
            EnableToFull,
            EnableToFullRuleEngine,
            EnableToFullRuleTrace,
            strictOverlapRatio,
            anchorIc,
            anchorDiff,
            allocationAreaMode,
            resolved);
        return resolved;
    }
}
