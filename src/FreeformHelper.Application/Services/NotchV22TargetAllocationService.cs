using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;

namespace FreeformHelper.Application.Services;

/// <summary>
/// Builds Stage3-based target diff allocation summary for one CAD pad.
/// </summary>
public sealed class NotchV22TargetAllocationService
{
    private const double AreaEpsilon = 1e-12;

    public static NotchV22TargetAllocationSummary Build(
        CadPad cadPad,
        NotchV22CompensationResult compensation,
        double strictOverlapRatio,
        int? anchorIcIndex = null,
        int? anchorDiffIndex = null,
        NotchV22TargetAllocationAreaMode areaMode = NotchV22TargetAllocationAreaMode.SourceAreaDominant)
    {
        ArgumentNullException.ThrowIfNull(cadPad);
        ArgumentNullException.ThrowIfNull(compensation);

        var normalizedStrictRatio = NotchV22CompensationService.NormalizeStrictOverlapRatio(strictOverlapRatio);
        var cadArea = Math.Max(cadPad.Area, AreaEpsilon);
        var stage3Area = Math.Max(cadArea, compensation.Stage3Area);
        var groupedTargets = compensation.RegularDebugInfos
            .Select(info =>
            {
                var sourceArea = Math.Max(0.0, info.SourceArea);
                var baseWeightArea = areaMode is NotchV22TargetAllocationAreaMode.Stage3EffectiveArea or NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage
                                     && info.IsToFullApplied
                    ? Math.Max(sourceArea, info.Stage3EffectiveArea)
                    : sourceArea;
                var regularArea = Math.Max(info.RegularArea, AreaEpsilon);
                return new
                {
                    info.IcIndex,
                    info.DiffIndex,
                    info.RegularPadId,
                    info.IsToFullApplied,
                    BaseWeightArea = baseWeightArea,
                    CoverageRatio = areaMode is NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage or NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage
                        ? baseWeightArea / regularArea
                        : 0.0
                };
            })
            .Where(item => item.BaseWeightArea > AreaEpsilon)
            .GroupBy(item => (item.IcIndex, item.DiffIndex))
            .Select(group =>
            {
                return new
                {
                    group.Key.IcIndex,
                    group.Key.DiffIndex,
                    BaseWeightArea = group.Sum(item => item.BaseWeightArea),
                    CoverageRatio = group.Sum(item => item.CoverageRatio),
                    ToFullAppliedRegularCount = group.Count(item => item.IsToFullApplied),
                    RegularCount = group.Count(),
                    RegularAreas = group
                        .GroupBy(item => item.RegularPadId)
                        .Select(regularGroup => new NotchV22TargetRegularArea(
                            RegularPadId: regularGroup.Key,
                            EffectiveArea: regularGroup.Sum(item => item.BaseWeightArea)))
                        .OrderBy(area => area.RegularPadId)
                        .ToArray(),
                    RegularPadIds = group
                        .Select(item => item.RegularPadId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToArray()
                };
            })
            .Where(target => !anchorIcIndex.HasValue || target.IcIndex == anchorIcIndex.Value)
            .ToList();

        var allocationBasisArea = Math.Max(
            AreaEpsilon,
            groupedTargets.Sum(target => target.BaseWeightArea));
        var strictAreaThreshold = Math.Max(AreaEpsilon, allocationBasisArea * normalizedStrictRatio);

        var targets = groupedTargets
            .Select(target =>
            {
                var ratio = areaMode is NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage or NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage
                    ? target.CoverageRatio
                    : target.BaseWeightArea / allocationBasisArea;
                var isAnchorDiff = anchorDiffIndex.HasValue &&
                                   target.DiffIndex == anchorDiffIndex.Value &&
                                   (!anchorIcIndex.HasValue || target.IcIndex == anchorIcIndex.Value);
                return new NotchV22TargetAllocation(
                    IcIndex: target.IcIndex,
                    DiffIndex: target.DiffIndex,
                    EffectiveArea: target.BaseWeightArea,
                    Ratio: ratio,
                    RatioPercentRounded: (int)Math.Round(ratio * 100.0),
                    PassesStrictThreshold: target.BaseWeightArea > strictAreaThreshold,
                    IsAnchorDiff: isAnchorDiff,
                    ToFullAppliedRegularCount: target.ToFullAppliedRegularCount,
                    RegularCount: target.RegularCount,
                    RegularAreas: target.RegularAreas,
                    RegularPadIds: target.RegularPadIds);
            })
            .OrderByDescending(target => Math.Abs(target.Ratio))
            .ThenBy(target => target.IcIndex)
            .ThenBy(target => target.DiffIndex)
            .ToList();
        var targetCoverageProjection = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            targets,
            anchorIcIndex,
            anchorDiffIndex,
            areaMode,
            compensation.CombinedRatio);

        return new NotchV22TargetAllocationSummary(
            CadArea: cadArea,
            Stage3Area: stage3Area,
            StrictOverlapRatio: normalizedStrictRatio,
            StrictAreaThreshold: strictAreaThreshold,
            Targets: targets,
            TargetCoverageProjection: targetCoverageProjection);
    }
}

public sealed record NotchV22TargetAllocationSummary(
    double CadArea,
    double Stage3Area,
    double StrictOverlapRatio,
    double StrictAreaThreshold,
    IReadOnlyList<NotchV22TargetAllocation> Targets,
    NotchV22TargetCoverageProjection TargetCoverageProjection);

public sealed record NotchV22TargetCoverageProjection(
    IReadOnlyList<NotchV22TargetAllocation> EmittedTargets,
    int? RawCombinedPercent,
    double DisplayCombinedRatio,
    bool HasCombinedOverflowRisk);

public sealed record NotchV22TargetAllocation(
    int IcIndex,
    int DiffIndex,
    double EffectiveArea,
    double Ratio,
    int RatioPercentRounded,
    bool PassesStrictThreshold,
    bool IsAnchorDiff,
    int ToFullAppliedRegularCount,
    int RegularCount,
    IReadOnlyList<NotchV22TargetRegularArea> RegularAreas,
    IReadOnlyList<int> RegularPadIds);

public sealed record NotchV22TargetRegularArea(
    int RegularPadId,
    double EffectiveArea);

public enum NotchV22TargetAllocationAreaMode
{
    SourceAreaDominant = 0,
    Stage3EffectiveArea = 1,
    TargetRegularSourceCoverage = 2,
    TargetRegularStage3Coverage = 3,
}

public static class NotchV22TargetAllocationPolicy
{
    public static NotchV22TargetCoverageProjection ProjectTargetCoverage(
        IReadOnlyList<NotchV22TargetAllocation> targets,
        int? anchorIcIndex,
        int? sourceDiffIndex,
        NotchV22TargetAllocationAreaMode areaMode,
        double fallbackCombinedRatio)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (!anchorIcIndex.HasValue || !sourceDiffIndex.HasValue)
        {
            return ProjectCompatibilityDisplay(targets, [], fallbackCombinedRatio);
        }

        var anchorIc = anchorIcIndex.Value;
        var sourceDiff = sourceDiffIndex.Value;
        var emittedTargets = targets
            .Where(target => target.IcIndex == anchorIc)
            .Where(target => !target.IsAnchorDiff && target.DiffIndex != sourceDiff)
            .Where(target => target.PassesStrictThreshold || target.ToFullAppliedRegularCount > 0)
            .Where(target => target.RatioPercentRounded != 0)
            .ToArray();
        if (areaMode is not (NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage or
            NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage))
        {
            return ProjectCompatibilityDisplay(targets, emittedTargets, fallbackCombinedRatio);
        }

        var anchorPercent = targets
            .Where(target => target.IcIndex == anchorIc)
            .Where(target => target.IsAnchorDiff || target.DiffIndex == sourceDiff)
            .Select(static target => Math.Max(0, target.RatioPercentRounded))
            .DefaultIfEmpty(0)
            .Max();
        var rawCombinedPercent = anchorPercent +
                                 emittedTargets.Sum(static target => Math.Max(0, target.RatioPercentRounded));

        return new NotchV22TargetCoverageProjection(
            emittedTargets,
            rawCombinedPercent,
            rawCombinedPercent / 100.0,
            HasCombinedOverflowRisk(rawCombinedPercent));
    }

    internal static bool HasCombinedOverflowRisk(double rawCombinedPercent) =>
        rawCombinedPercent > byte.MaxValue;

    private static NotchV22TargetCoverageProjection ProjectCompatibilityDisplay(
        IReadOnlyList<NotchV22TargetAllocation> targets,
        IReadOnlyList<NotchV22TargetAllocation> emittedTargets,
        double fallbackCombinedRatio)
    {
        var displayCombinedRatio = targets.Count > 0
            ? targets.Sum(static target => Math.Max(0.0, target.Ratio))
            : fallbackCombinedRatio;
        return new NotchV22TargetCoverageProjection(
            emittedTargets,
            RawCombinedPercent: null,
            displayCombinedRatio,
            HasCombinedOverflowRisk(displayCombinedRatio * 100.0));
    }

    public static NotchV22TargetAllocationAreaMode ResolveAreaMode(NotchCompensationModel model)
    {
        return model switch
        {
            NotchCompensationModel.CurrentGain => NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
            NotchCompensationModel.ConservativeNoGain => NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
            _ => NotchV22TargetAllocationAreaMode.SourceAreaDominant,
        };
    }

    public static bool UsesTargetRegularCoverage(NotchCompensationModel model)
    {
        return model is NotchCompensationModel.CurrentGain or NotchCompensationModel.ConservativeNoGain;
    }

    public static string DescribeRule(NotchCompensationModel model)
    {
        return model switch
        {
            NotchCompensationModel.CurrentGain => "Target-regular coverage; legs use stage3/regular-area coverage, not source-wide R share.",
            NotchCompensationModel.ConservativeNoGain => "Target-regular coverage; legs use overlap/regular-area coverage, ToFull is support/cap/allowance.",
            NotchCompensationModel.Disabled => "SourceArea-dominant baseline.",
            _ => "SourceArea-dominant; ToFull is support/cap/allowance.",
        };
    }
}
