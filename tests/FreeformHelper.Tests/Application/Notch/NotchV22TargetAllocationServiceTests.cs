using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchV22TargetAllocationServiceTests
{
    [Fact]
    public void TargetAllocationPolicy_MapsEveryCompensationModelToItsCoveragePath()
    {
        var expected = new Dictionary<
            NotchCompensationModel,
            (NotchV22TargetAllocationAreaMode AreaMode, bool UsesTargetRegularCoverage)>
        {
            [NotchCompensationModel.CurrentGain] = (
                NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
                true),
            [NotchCompensationModel.ConservativeNoGain] = (
                NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
                true),
            [NotchCompensationModel.Disabled] = (
                NotchV22TargetAllocationAreaMode.SourceAreaDominant,
                false),
        };
        var models = Enum.GetValues<NotchCompensationModel>();

        Assert.Equal(expected.Count, models.Length);
        Assert.All(models, model =>
        {
            Assert.True(expected.TryGetValue(model, out var contract));
            Assert.Equal(contract.AreaMode, NotchV22TargetAllocationPolicy.ResolveAreaMode(model));
            Assert.Equal(
                contract.UsesTargetRegularCoverage,
                NotchV22TargetAllocationPolicy.UsesTargetRegularCoverage(model));
        });
    }

    [Fact]
    public void ProjectTargetCoverage_EmitsStrictOrToFullTargetsWithoutMutatingDiagnostics()
    {
        var anchor = CreateTarget(diffIndex: 10, ratioPercent: 100, isAnchorDiff: true);
        var eligible = CreateTarget(diffIndex: 11, ratioPercent: 70);
        var strictRejected = CreateTarget(
            diffIndex: 12,
            ratioPercent: 99,
            passesStrictThreshold: false);
        var toFullExempt = CreateTarget(
            diffIndex: 13,
            ratioPercent: 30,
            passesStrictThreshold: false,
            toFullAppliedRegularCount: 1);
        var otherIc = CreateTarget(icIndex: 1, diffIndex: 14, ratioPercent: 99);
        var targets = new List<NotchV22TargetAllocation>
        {
            anchor,
            eligible,
            strictRejected,
            toFullExempt,
            otherIc,
        };
        var projection = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            targets,
            anchorIcIndex: 0,
            sourceDiffIndex: 10,
            areaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
            fallbackCombinedRatio: 9.99);

        Assert.Equal([eligible, toFullExempt], projection.EmittedTargets);
        Assert.Equal(200, projection.RawCombinedPercent);
        Assert.Equal(2.0, projection.DisplayCombinedRatio, 6);
    }

    [Fact]
    public void ProjectTargetCoverage_UsesRoundedLegsAndMaximumSourcePercentOnce()
    {
        var anchor = CreateTarget(
            diffIndex: 10,
            ratioPercent: 50,
            isAnchorDiff: true,
            rawRatio: 0.504);
        var duplicateSource = CreateTarget(
            diffIndex: 10,
            ratioPercent: 49,
            isAnchorDiff: false,
            rawRatio: 0.494);
        var firstLeg = CreateTarget(diffIndex: 11, ratioPercent: 50, rawRatio: 0.504);
        var secondLeg = CreateTarget(diffIndex: 12, ratioPercent: 50, rawRatio: 0.504);

        var projection = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            [anchor, duplicateSource, firstLeg, secondLeg],
            anchorIcIndex: 0,
            sourceDiffIndex: 10,
            areaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage,
            fallbackCombinedRatio: 9.99);

        Assert.Equal([firstLeg, secondLeg], projection.EmittedTargets);
        Assert.Equal(150, projection.RawCombinedPercent);
        Assert.Equal(1.5, projection.DisplayCombinedRatio, 6);
    }

    [Fact]
    public void ProjectTargetCoverage_CompatibilityPathsPreserveAllTargetDisplayRatio()
    {
        var anchor = CreateTarget(diffIndex: 10, ratioPercent: 75, isAnchorDiff: true);
        var target = CreateTarget(diffIndex: 11, ratioPercent: 100);
        var targets = new[] { anchor, target };

        var unanchored = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            targets,
            anchorIcIndex: null,
            sourceDiffIndex: 10,
            NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
            fallbackCombinedRatio: 9.99);
        var sourceArea = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            targets,
            anchorIcIndex: 0,
            sourceDiffIndex: 10,
            NotchV22TargetAllocationAreaMode.SourceAreaDominant,
            fallbackCombinedRatio: 9.99);

        Assert.Empty(unanchored.EmittedTargets);
        Assert.Equal([target], sourceArea.EmittedTargets);
        Assert.All(new[] { unanchored, sourceArea }, static projection =>
        {
            Assert.Null(projection.RawCombinedPercent);
            Assert.Equal(1.75, projection.DisplayCombinedRatio, 6);
            Assert.False(projection.HasCombinedOverflowRisk);
        });
    }

    [Theory]
    [InlineData(155, 255, false)]
    [InlineData(156, 256, true)]
    public void ProjectTargetCoverage_OverflowRiskStartsAbove255Percent(
        int anchorPercent,
        int expectedCombinedPercent,
        bool expectedOverflowRisk)
    {
        var projection = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
            [
                CreateTarget(diffIndex: 10, ratioPercent: anchorPercent, isAnchorDiff: true),
                CreateTarget(diffIndex: 11, ratioPercent: 100),
            ],
            anchorIcIndex: 0,
            sourceDiffIndex: 10,
            areaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
            fallbackCombinedRatio: 9.99);

        Assert.Equal(expectedCombinedPercent, projection.RawCombinedPercent);
        Assert.Equal(expectedCombinedPercent / 100.0, projection.DisplayCombinedRatio, 6);
        Assert.Equal(expectedOverflowRisk, projection.HasCombinedOverflowRisk);
    }

    [Fact]
    public void Build_WhenToFullApplies_UsesSourceAreaAsAllocationWeight()
    {
        var cad = CreateRectangleCadPad(id: 1, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            new NotchV22RegularDebugInfo(
                RegularPadId: 100,
                RegularIndex: 0,
                IcIndex: 0,
                DiffIndex: 72,
                Row: 0,
                Col: 0,
                RegularArea: 100,
                OverlapArea: 90,
                SourceArea: 90,
                BlockedArea: 0,
                ReachableArea: 90,
                Stage3EffectiveArea: 90,
                SourceCellCount: 0,
                BlockedCellCount: 0,
                ReachableCellCount: 0,
                TotalCellCount: 0,
                BlockerCandidateCount: 0,
                OwnerCadPadCount: 1,
                OwnerCadPadIds: [1],
                IsBoundaryRegular: false,
                IsToFullBoundaryCandidate: false,
                HasEffectiveExpansion: false,
                HasDirectionalBlocker: false,
                ToFullRuleTrace: Array.Empty<NotchToFullRuleTraceEntry>(),
                ToFullRuleCode: "NO_EXPANSION_NEEDED",
                IsToFullApplied: false),
            new NotchV22RegularDebugInfo(
                RegularPadId: 101,
                RegularIndex: 1,
                IcIndex: 0,
                DiffIndex: 8,
                Row: 1,
                Col: 0,
                RegularArea: 100,
                OverlapArea: 10,
                SourceArea: 10,
                BlockedArea: 0,
                ReachableArea: 90,
                Stage3EffectiveArea: 90,
                SourceCellCount: 0,
                BlockedCellCount: 0,
                ReachableCellCount: 0,
                TotalCellCount: 0,
                BlockerCandidateCount: 0,
                OwnerCadPadCount: 1,
                OwnerCadPadIds: [1],
                IsBoundaryRegular: true,
                IsToFullBoundaryCandidate: true,
                HasEffectiveExpansion: true,
                HasDirectionalBlocker: false,
                ToFullRuleTrace: Array.Empty<NotchToFullRuleTraceEntry>(),
                ToFullRuleCode: "EXPAND_CLEAR_PATH",
                IsToFullApplied: true));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 72);

        Assert.Equal(2, allocation.Targets.Count);

        var anchor = Assert.Single(allocation.Targets, static target => target.DiffIndex == 72);
        var target = Assert.Single(allocation.Targets, static target => target.DiffIndex == 8);

        Assert.True(anchor.IsAnchorDiff);
        Assert.False(target.IsAnchorDiff);
        Assert.Equal(90, anchor.RatioPercentRounded);
        Assert.Equal(10, target.RatioPercentRounded);
        Assert.Equal(90, anchor.EffectiveArea, 6);
        Assert.Equal(10, target.EffectiveArea, 6);
        Assert.Equal(1, target.ToFullAppliedRegularCount);
    }

    [Fact]
    public void Build_WhenStage3EffectiveAreaMode_UsesStage3EffectiveAreaAsAllocationWeight()
    {
        var cad = CreateRectangleCadPad(id: 1, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            CreateDebugInfo(regularPadId: 100, regularIndex: 0, icIndex: 0, diffIndex: 72, sourceArea: 90),
            CreateDebugInfo(regularPadId: 101, regularIndex: 1, icIndex: 0, diffIndex: 8, sourceArea: 10, isToFullApplied: true));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 72,
            areaMode: NotchV22TargetAllocationAreaMode.Stage3EffectiveArea);

        var anchor = Assert.Single(allocation.Targets, static target => target.DiffIndex == 72);
        var target = Assert.Single(allocation.Targets, static target => target.DiffIndex == 8);

        Assert.Equal(90, anchor.EffectiveArea, 6);
        Assert.Equal(100, target.EffectiveArea, 6);
        Assert.Equal(47, anchor.RatioPercentRounded);
        Assert.Equal(53, target.RatioPercentRounded);
    }

    [Fact]
    public void Build_WhenStage3EffectiveAreaMode_UsesCappedStage3EffectiveArea()
    {
        var cad = CreateRectangleCadPad(id: 1, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            CreateDebugInfo(regularPadId: 100, regularIndex: 0, icIndex: 0, diffIndex: 72, sourceArea: 80),
            CreateDebugInfo(
                regularPadId: 101,
                regularIndex: 1,
                icIndex: 0,
                diffIndex: 8,
                sourceArea: 20,
                isToFullApplied: true,
                stage3EffectiveArea: 40));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 72,
            areaMode: NotchV22TargetAllocationAreaMode.Stage3EffectiveArea);

        var anchor = Assert.Single(allocation.Targets, static target => target.DiffIndex == 72);
        var target = Assert.Single(allocation.Targets, static target => target.DiffIndex == 8);

        Assert.Equal(80, anchor.EffectiveArea, 6);
        Assert.Equal(40, target.EffectiveArea, 6);
        Assert.Equal(67, anchor.RatioPercentRounded);
        Assert.Equal(33, target.RatioPercentRounded);
    }

    [Fact]
    public void Build_WhenTargetRegularSourceCoverageMode_UsesPerTargetRegularCoverage()
    {
        var cad = CreateRectangleCadPad(id: 1, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            CreateDebugInfo(
                regularPadId: 384,
                regularIndex: 0,
                icIndex: 0,
                diffIndex: 1472,
                sourceArea: 11.34,
                regularArea: 11.50),
            CreateDebugInfo(
                regularPadId: 385,
                regularIndex: 1,
                icIndex: 0,
                diffIndex: 1473,
                sourceArea: 12.78,
                regularArea: 21.73));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 1473,
            areaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage);

        var left = Assert.Single(allocation.Targets, static target => target.DiffIndex == 1472);
        var anchor = Assert.Single(allocation.Targets, static target => target.DiffIndex == 1473);

        Assert.Equal(99, left.RatioPercentRounded);
        Assert.Equal(59, anchor.RatioPercentRounded);
        Assert.Equal(11.34, left.EffectiveArea, 2);
        Assert.Equal(12.78, anchor.EffectiveArea, 2);
    }

    [Fact]
    public void Build_WhenTargetRegularStage3CoverageMode_UsesPerTargetStage3Coverage()
    {
        var cad = CreateRectangleCadPad(id: 1, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            CreateDebugInfo(
                regularPadId: 384,
                regularIndex: 0,
                icIndex: 0,
                diffIndex: 1472,
                sourceArea: 11.34,
                regularArea: 11.50,
                isToFullApplied: true,
                stage3EffectiveArea: 11.50),
            CreateDebugInfo(
                regularPadId: 385,
                regularIndex: 1,
                icIndex: 0,
                diffIndex: 1473,
                sourceArea: 12.78,
                regularArea: 21.73,
                isToFullApplied: true,
                stage3EffectiveArea: 21.73));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 1473,
            areaMode: NotchV22TargetAllocationAreaMode.TargetRegularStage3Coverage);

        Assert.All(allocation.Targets, static target => Assert.Equal(100, target.RatioPercentRounded));
    }

    [Fact]
    public void Build_WhenAnchorIcProvided_NormalizesWithinAnchorIcAndDropsOtherIcTargets()
    {
        var cad = CreateRectangleCadPad(id: 2, minX: 0, minY: 0, maxX: 10, maxY: 10);
        var compensation = CreateCompensationResult(
            CreateDebugInfo(regularPadId: 200, regularIndex: 0, icIndex: 0, diffIndex: 20, sourceArea: 30),
            CreateDebugInfo(regularPadId: 201, regularIndex: 1, icIndex: 0, diffIndex: 21, sourceArea: 70, isToFullApplied: true),
            CreateDebugInfo(regularPadId: 300, regularIndex: 2, icIndex: 1, diffIndex: 40, sourceArea: 100, isToFullApplied: true));

        var allocation = NotchV22TargetAllocationService.Build(
            cad,
            compensation,
            strictOverlapRatio: 0.001,
            anchorIcIndex: 0,
            anchorDiffIndex: 20);

        Assert.Equal([20, 21], allocation.Targets.Select(static target => target.DiffIndex).OrderBy(static diff => diff).ToArray());

        var anchor = Assert.Single(allocation.Targets, static target => target.DiffIndex == 20);
        var target = Assert.Single(allocation.Targets, static target => target.DiffIndex == 21);

        Assert.Equal(30, anchor.RatioPercentRounded);
        Assert.Equal(70, target.RatioPercentRounded);
    }

    private static CadPad CreateRectangleCadPad(int id, double minX, double minY, double maxX, double maxY)
    {
        return new CadPad(id, $"CAD{id}", "PAD", new Polygon2(
            new[]
            {
                new Point2(minX, minY),
                new Point2(maxX, minY),
                new Point2(maxX, maxY),
                new Point2(minX, maxY),
            }));
    }

    private static NotchV22TargetAllocation CreateTarget(
        int diffIndex,
        int ratioPercent,
        bool passesStrictThreshold = true,
        bool isAnchorDiff = false,
        int toFullAppliedRegularCount = 0,
        int icIndex = 0,
        double? rawRatio = null)
    {
        return new NotchV22TargetAllocation(
            IcIndex: icIndex,
            DiffIndex: diffIndex,
            EffectiveArea: 1.0,
            Ratio: rawRatio ?? ratioPercent / 100.0,
            RatioPercentRounded: ratioPercent,
            PassesStrictThreshold: passesStrictThreshold,
            IsAnchorDiff: isAnchorDiff,
            ToFullAppliedRegularCount: toFullAppliedRegularCount,
            RegularCount: 1,
            RegularAreas: Array.Empty<NotchV22TargetRegularArea>(),
            RegularPadIds: Array.Empty<int>());
    }

    private static NotchV22CompensationResult CreateCompensationResult(params NotchV22RegularDebugInfo[] infos)
    {
        var overlapAreaTotal = infos.Sum(static info => info.OverlapArea);
        return new NotchV22CompensationResult(
            ToRegularRatio: 1.0,
            ToFullRatio: 1.0,
            CombinedRatio: 1.0,
            IsToFullEnabled: infos.Any(static info => info.IsToFullApplied),
            Stage3Area: Math.Max(1.0, overlapAreaTotal),
            OverlapAreaTotal: overlapAreaTotal,
            OverlappedRegularAreaTotal: infos.Sum(static info => info.RegularArea),
            OverlapRegularCount: infos.Length,
            ToFullSeedPolygons: Array.Empty<Polygon2>(),
            ToFullCandidatePolygons: Array.Empty<Polygon2>(),
            ToFullPolygons: Array.Empty<Polygon2>(),
            RegularDebugInfos: infos);
    }

    private static NotchV22RegularDebugInfo CreateDebugInfo(
        int regularPadId,
        int regularIndex,
        int icIndex,
        int diffIndex,
        double sourceArea,
        bool isToFullApplied = false,
        double? stage3EffectiveArea = null,
        double regularArea = 100)
    {
        var reachableArea = isToFullApplied ? 100 : sourceArea;
        return new NotchV22RegularDebugInfo(
            RegularPadId: regularPadId,
            RegularIndex: regularIndex,
            IcIndex: icIndex,
            DiffIndex: diffIndex,
            Row: regularIndex,
            Col: regularIndex,
            RegularArea: regularArea,
            OverlapArea: sourceArea,
            SourceArea: sourceArea,
            BlockedArea: 0,
            ReachableArea: reachableArea,
            Stage3EffectiveArea: stage3EffectiveArea ?? reachableArea,
            SourceCellCount: 0,
            BlockedCellCount: 0,
            ReachableCellCount: 0,
            TotalCellCount: 0,
            BlockerCandidateCount: 0,
            OwnerCadPadCount: 1,
            OwnerCadPadIds: [999],
            IsBoundaryRegular: isToFullApplied,
            IsToFullBoundaryCandidate: isToFullApplied,
            HasEffectiveExpansion: isToFullApplied,
            HasDirectionalBlocker: false,
            ToFullRuleTrace: Array.Empty<NotchToFullRuleTraceEntry>(),
            ToFullRuleCode: isToFullApplied ? "EXPAND_CLEAR_PATH" : "NO_EXPANSION_NEEDED",
            IsToFullApplied: isToFullApplied);
    }
}
