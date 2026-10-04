using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.UI.Services;

public sealed class NotchDetailUseCase
{
    private static readonly NotchV22ResolvedResultService ResolvedResultService = new();
    private readonly NotchV22ResolvedResultService _resolvedResultService = ResolvedResultService;

    public static NotchV22CompensationResult BuildCompensation(
        CadPad cadPad,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        bool? enableToRegularOverride = null,
        bool? enableToFullOverride = null,
        double? strictOverlapRatioOverride = null,
        bool? enableToFullRuleEngineOverride = null,
        bool? enableToFullRuleTraceOverride = null)
    {
        var strictOverlapRatio = strictOverlapRatioOverride
            ?? Math.Clamp(settings.Notch.MultiOwnerStrictOverlapPercent / 100.0, 0.0, 1.0);
        var (resolvedEnableToRegular, resolvedEnableToFull) = settings.Notch.ResolveStep3Switches();
        var context = NotchV22CompensationService.CreateContext(
            cadPad,
            grid,
            enableToRegularOverride ?? resolvedEnableToRegular,
            enableToFullOverride ?? resolvedEnableToFull,
            allCadPads,
            activeRegularPadIds,
            strictOverlapRatio,
            enableToFullRuleEngineOverride ?? settings.Notch.EnableToFullRuleEngine,
            enableToFullRuleTraceOverride ?? settings.Notch.EnableToFullRuleTrace,
            settings.Notch.EnableBoundaryVirtualAreaCap,
            settings.Notch.BoundaryVirtualAreaCapRatio);
        var compensation = NotchV22CompensationService.Compute(context);
        var effectiveCombinedRatio = settings.Notch.ResolveCombinedRatio(
            compensation.ToRegularRatio,
            compensation.ToFullRatio);
        return Math.Abs(effectiveCombinedRatio - compensation.CombinedRatio) <= 1e-12
            ? compensation
            : compensation with { CombinedRatio = effectiveCombinedRatio };
    }

    public NotchV22ResolvedResult BuildResolvedResult(
        CadPad cadPad,
        NotchV22CompensationResult compensation,
        double strictOverlapRatio,
        int? anchorIcIndex = null,
        int? anchorDiffIndex = null,
        NotchV22TargetAllocationAreaMode allocationAreaMode = NotchV22TargetAllocationAreaMode.SourceAreaDominant)
    {
        return _resolvedResultService.Build(
            cadPad,
            compensation,
            strictOverlapRatio,
            anchorIcIndex,
            anchorDiffIndex,
            allocationAreaMode);
    }

    internal static NotchV22ResolvedResult BuildResolvedResult(NotchResolvedResultRequest request)
    {
        var compensation = request.PrecomputedCompensation ?? BuildCompensation(
            request.CadPad,
            request.Grid,
            request.Settings,
            request.AllCadPads,
            request.ActiveRegularPadIds,
            request.EnableToRegular,
            request.EnableToFull,
            request.StrictOverlapRatio,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace);
        return ResolvedResultService.Build(
            request.CadPad,
            compensation,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode,
            CreateIdentity(request));
    }

    internal static bool MatchesRequest(NotchV22ResolvedResult result, NotchResolvedResultRequest request) =>
        result.Identity == CreateIdentity(request);

    private static NotchV22ResolvedResultIdentity CreateIdentity(NotchResolvedResultRequest request) =>
        NotchV22ResolvedResultService.CreateIdentity(
            request.CadPad,
            request.Grid,
            request.AllCadPads,
            request.ActiveRegularPadIds,
            request.CompensationModel,
            request.EnableToRegular,
            request.EnableToFull,
            request.EnableToFullRuleEngine,
            request.EnableToFullRuleTrace,
            request.Settings.Notch.EnableBoundaryVirtualAreaCap,
            request.Settings.Notch.BoundaryVirtualAreaCapRatio,
            request.StrictOverlapRatio,
            request.AnchorIcIndex,
            request.AnchorDiffIndex,
            request.AllocationAreaMode);

    public NotchDetailViewModel Build(
        CadPad cadPad,
        int? dxfIndex,
        RegularGrid grid,
        ProjectSettings settings,
        IReadOnlyList<CadPad>? allCadPads = null,
        IReadOnlySet<int>? activeRegularPadIds = null,
        bool? enableToRegularOverride = null,
        bool? enableToFullOverride = null,
        bool? enableToFullRuleEngineOverride = null,
        bool? enableToFullRuleTraceOverride = null)
    {
        var compensation = BuildCompensation(
            cadPad,
            grid,
            settings,
            allCadPads,
            activeRegularPadIds,
            enableToRegularOverride,
            enableToFullOverride,
            enableToFullRuleEngineOverride: enableToFullRuleEngineOverride,
            enableToFullRuleTraceOverride: enableToFullRuleTraceOverride);

        var resolved = BuildResolvedResult(
            cadPad,
            compensation,
            Math.Clamp(settings.Notch.MultiOwnerStrictOverlapPercent / 100.0, 0.0, 1.0),
            anchorIcIndex: null,
            anchorDiffIndex: dxfIndex,
            allocationAreaMode: NotchV22TargetAllocationPolicy.ResolveAreaMode(settings.Notch.CompensationModel));

        return BuildFromResolvedResult(cadPad, dxfIndex, grid, settings, resolved);
    }

    internal static NotchDetailViewModel BuildFromResolvedResult(
        CadPad cadPad,
        int? dxfIndex,
        RegularGrid grid,
        ProjectSettings settings,
        NotchV22ResolvedResult resolved)
    {
        var allocations = NotchAllocationService.BuildAllocations(cadPad, grid);
        var freeform = NotchAllocationService.GetFreeformType(cadPad, grid);
        var compensation = resolved.Compensation;
        var items = allocations
            .Select(a => new NotchDetailAllocation(a.Pad.Index, a.Pad.Row, a.Pad.Col, a.Pad.IcIndex, a.Q7, a.Ratio))
            .ToList();

        var maxQ7 = items.Count == 0 ? 0 : items.Max(a => a.Q7);
        var thresholdQ7 = settings.Notch.ThresholdQ7;
        var thresholdSummary = thresholdQ7 > 0
            ? $"TH={thresholdQ7}/128, Max={maxQ7}/128"
            : "TH=0 (disabled)";
        var display = NotchDisplayProjector.Build(
            compensation,
            resolved.TargetAllocation,
            computationMode: settings.Notch.ComputationMode);

        return new NotchDetailViewModel(
            cadPad,
            dxfIndex,
            freeform,
            items,
            allocations.Select(a => a.Pad).ToList(),
            thresholdQ7,
            maxQ7,
            thresholdSummary,
            compensation.ToRegularRatio,
            compensation.ToFullRatio,
            compensation.CombinedRatio,
            compensation.IsToFullEnabled,
            display.ToRegularRatioText,
            display.ToFullRatioText,
            display.CombinedRatioText,
            resolved.Stage1SeedPolygons,
            resolved.Stage2CandidatePolygons,
            compensation.RegularDebugInfos);
    }
}

internal sealed record NotchResolvedResultRequest(
    CadPad CadPad, RegularGrid Grid, ProjectSettings Settings,
    IReadOnlyList<CadPad> AllCadPads,
    IReadOnlySet<int>? ActiveRegularPadIds,
    NotchCompensationModel CompensationModel,
    bool EnableToRegular, bool EnableToFull,
    bool EnableToFullRuleEngine, bool EnableToFullRuleTrace,
    double StrictOverlapRatio, int? AnchorIcIndex, int? AnchorDiffIndex,
    NotchV22TargetAllocationAreaMode AllocationAreaMode,
    NotchV22CompensationResult? PrecomputedCompensation = null);
