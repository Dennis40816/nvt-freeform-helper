using FreeformHelper.Application.Services;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisplayProjectorTests
{
    [Fact]
    public void Build_FromPadInspectorSnapshot_ProducesSharedDisplayProjection()
    {
        var notch = new PadInspectorNotchSnapshot(
            ToRegularRatio: 0.941,
            ToFullRatio: 1.5,
            CombinedRatio: 1.4115,
            IsToFullEnabled: true,
            Stage3Area: 33.5,
            StrictAreaThreshold: 0.1,
            Targets: new[]
            {
                new PadInspectorNotchTargetSnapshot(
                    IcIndex: 0,
                    DiffIndex: 84,
                    EffectiveArea: 18.09,
                    Ratio: 0.54,
                    RatioPercentRounded: 54,
                    PassesStrictThreshold: true,
                    IsAnchorDiff: false,
                    ToFullAppliedRegularCount: 1,
                    RegularCount: 1,
                    RegularAreas: Array.Empty<PadInspectorNotchTargetRegularAreaSnapshot>(),
                    RegularPadIds: Array.Empty<int>()),
                new PadInspectorNotchTargetSnapshot(
                    IcIndex: 0,
                    DiffIndex: 20,
                    EffectiveArea: 15.41,
                    Ratio: 0.46,
                    RatioPercentRounded: 46,
                    PassesStrictThreshold: true,
                    IsAnchorDiff: true,
                    ToFullAppliedRegularCount: 0,
                    RegularCount: 1,
                    RegularAreas: Array.Empty<PadInspectorNotchTargetRegularAreaSnapshot>(),
                    RegularPadIds: Array.Empty<int>()),
            },
            Diagnostics: "IC1/diff84 reg4808: ov=10, src=10, blk=0, reach=15, blocker=0, " +
                         "owners=123|124, ownerShare=50/50, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)")
        {
            TargetCoverageProjection = new NotchV22TargetCoverageProjection(
                [
                    new NotchV22TargetAllocation(
                        IcIndex: 0,
                        DiffIndex: 84,
                        EffectiveArea: 18.09,
                        Ratio: 0.54,
                        RatioPercentRounded: 54,
                        PassesStrictThreshold: true,
                        IsAnchorDiff: false,
                        ToFullAppliedRegularCount: 1,
                        RegularCount: 1,
                        RegularAreas: Array.Empty<NotchV22TargetRegularArea>(),
                        RegularPadIds: Array.Empty<int>())
                ],
                RawCombinedPercent: 100,
                DisplayCombinedRatio: 1.0,
                HasCombinedOverflowRisk: false)
        };

        var projection = NotchDisplayProjector.Build(notch);

        Assert.Equal("Undo NF (To Regular): 94.10%", projection.ToRegularRatioText);
        Assert.Equal("To Full: 150.00% (enabled)", projection.ToFullRatioText);
        Assert.False(projection.HasCombinedOverflowRisk);
        Assert.Equal("100.00 %", projection.CombinedValueText);
        Assert.Equal("150.0 % (Enabled)", projection.ToFullValueText);
        Assert.Equal("Expansion applied on clear path.", projection.ToFullReasonShortText);
        Assert.Equal("Owners 2: CAD 123, CAD 124", projection.OwnerSummaryText);
        Assert.Equal("Targets: 1 effective / 2 total", projection.TargetAllocationSummaryText);
        Assert.Single(projection.TargetAllocationLines);
    }

    [Fact]
    public void Build_WhenTargetCoverageIsCompatibilityMode_PreservesStrictOnlyDisplay()
    {
        var target = new NotchV22TargetAllocation(
            IcIndex: 0,
            DiffIndex: 84,
            EffectiveArea: 18.09,
            Ratio: 0.54,
            RatioPercentRounded: 54,
            PassesStrictThreshold: false,
            IsAnchorDiff: false,
            ToFullAppliedRegularCount: 1,
            RegularCount: 1,
            RegularAreas: [new NotchV22TargetRegularArea(4809, 18.09)],
            RegularPadIds: [4809]);
        var targetCoverage = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            [target],
            anchorIcIndex: 0,
            sourceDiffIndex: 83,
            NotchV22TargetAllocationAreaMode.SourceAreaDominant,
            fallbackCombinedRatio: 0.54);

        var projection = NotchDisplayProjector.Build(
            toRegularRatio: 1.0,
            toFullRatio: 1.0,
            combinedRatio: 0.54,
            isToFullEnabled: true,
            stage3Area: null,
            targets:
            [
                new NotchDisplayTargetInput(
                    target.IcIndex,
                    target.DiffIndex,
                    target.EffectiveArea,
                    target.Ratio,
                    target.RatioPercentRounded,
                    target.PassesStrictThreshold,
                    target.IsAnchorDiff,
                    target.ToFullAppliedRegularCount,
                    target.RegularCount,
                    [new NotchDisplayRegularAreaInput(4809, 18.09)],
                    target.RegularPadIds)
            ],
            diagnostics: null,
            targetCoverageProjection: targetCoverage);

        Assert.Null(targetCoverage.RawCombinedPercent);
        Assert.Single(targetCoverage.EmittedTargets);
        Assert.Equal("Targets: 0 effective / 1 total", projection.TargetAllocationSummaryText);
        Assert.Empty(projection.TargetAllocationLines);
        Assert.Equal("Below gate", Assert.Single(projection.TargetAllocationItems).RoleText);
    }
}
