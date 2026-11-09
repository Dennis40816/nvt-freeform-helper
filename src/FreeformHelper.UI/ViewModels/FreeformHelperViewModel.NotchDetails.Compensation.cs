using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;

namespace FreeformHelper.UI.ViewModels;

public sealed partial class FreeformHelperViewModel
{
    public int GetNotchStep3Revision()
    {
        return _notchComputationSession.Revision;
    }

    public NotchCompensationCacheMetricsSnapshot GetNotchCompensationCacheMetrics()
    {
        var metrics = _notchComputationSession.GetMetrics();
        var hitCount = metrics.HitCount;
        var missCount = metrics.MissCount;
        var invalidationCount = metrics.InvalidationCount;
        var total = hitCount + missCount;
        var hitRate = total > 0 ? (double)hitCount / total : 0.0;
        return new NotchCompensationCacheMetricsSnapshot(
            Revision: metrics.Revision,
            EntryCount: metrics.EntryCount,
            LastClearSize: metrics.LastClearSize,
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

    private PadInspectorNotchSnapshot? BuildCadInspectorNotch(
        int cadPadId,
        bool includeExpensiveNotchDetails,
        WorkflowDataSnapshot workflowSnapshot,
        NotchV22CompensationResult? precomputedCompensation,
        string? precomputedDiagnostics)
    {
        var resolved = ResolveCadInspectorResolvedResult(
            cadPadId,
            includeExpensiveNotchDetails,
            workflowSnapshot,
            precomputedCompensation);
        if (resolved is not null)
        {
            return BuildCadInspectorNotchSnapshot(resolved, includeExpensiveNotchDetails, precomputedDiagnostics);
        }

        if (!includeExpensiveNotchDetails && _grid is not null)
        {
            _logger.Debug(System.Globalization.CultureInfo.InvariantCulture, "Pad inspector fast snapshot: CAD={0} skipped cold Notch 2.2 compute (cache miss).",
                cadPadId);
        }

        return null;
    }

    private NotchV22ResolvedResult? ResolveCadInspectorResolvedResult(
        int cadPadId,
        bool includeExpensiveNotchDetails,
        WorkflowDataSnapshot workflowSnapshot,
        NotchV22CompensationResult? precomputedCompensation)
    {
        CadPad notchCad = null!;
        IReadOnlyList<CadPad> allCadPads = Array.Empty<CadPad>();
        IReadOnlySet<int>? activeRegularPadIds = null;
        var hasNotchInputs = !includeExpensiveNotchDetails && TryResolveNotchComputationInputs(
            cadPadId,
            out notchCad,
            out allCadPads,
            out activeRegularPadIds);
        var resolved = includeExpensiveNotchDetails
            ? GetCadV22ResolvedResult(cadPadId)
            : hasNotchInputs && _notchComputationSession.Contains(cadPadId)
                ? GetOrBuildCadV22ResolvedResultCached(
                    notchCad,
                    allCadPads,
                    activeRegularPadIds,
                    workflowSnapshot,
                    buildIfMissing: false)
                : null;
        var compensationResult = precomputedCompensation ?? resolved?.Compensation;
        if (compensationResult is not null &&
            resolved is null &&
            (hasNotchInputs ||
             TryResolveNotchComputationInputs(
                 cadPadId,
                 out notchCad,
                 out allCadPads,
                 out activeRegularPadIds)))
        {
            resolved = GetOrBuildCadV22ResolvedResultCached(
                notchCad,
                allCadPads,
                activeRegularPadIds,
                workflowSnapshot,
                precomputedCompensation: compensationResult);
        }

        return resolved;
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
        var request = new NotchComputationRequest(
            _grid,
            cadPad,
            allCadPads,
            activeRegularPadIds,
            _projectFile.Settings,
            SelectedNotchCompensationModelOption.Value,
            EnableToRegular,
            EnableToFull,
            EnableToFullRuleEngine,
            EnableToFullRuleTrace,
            strictOverlapRatio,
            anchorIc,
            anchorDiff,
            allocationAreaMode,
            cadPoolFingerprint,
            activeRegularHash,
            gridFingerprint);
        return _notchComputationSession.GetOrBuild(request, precomputedCompensation, precomputedResolved, buildIfMissing);
    }
}
