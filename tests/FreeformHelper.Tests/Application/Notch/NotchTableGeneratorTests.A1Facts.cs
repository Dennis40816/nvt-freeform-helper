using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class NotchTableGeneratorTests
{
    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void A1Fact2_CadAllocation_AnchorToFullInfluenceMatchesOwnedCounts(NotchCompensationModel model)
    {
        var baseline = NotchV22CompensationService.Compute(
            CreateCad(CreateGrid()).Pads[0], CreateGrid(), true, false);
        var info = Assert.Single(baseline.RegularDebugInfos);
        var roundedZero = info with
        {
            DiffIndex = 11,
            SourceArea = 0.1,
            Stage3EffectiveArea = 0.1,
            IsToFullApplied = true,
        };
        var toFullOnly = roundedZero with { SourceArea = 0, Stage3EffectiveArea = 10 };
        var cases = new (NotchV22RegularDebugInfo[] Infos, int Ic0Count, int Ic1Count)[]
        {
            ([], 0, 0),
            ([info], 0, 0),
            ([info, roundedZero, roundedZero with { IcIndex = 1 }], 1, 1),
            ([info, toFullOnly, toFullOnly with { IcIndex = 1 }],
                model == NotchCompensationModel.CurrentGain ? 1 : 0,
                model == NotchCompensationModel.CurrentGain ? 1 : 0),
            ([info, roundedZero, roundedZero with { RegularPadId = 2 }], 2, 0),
            ([info, roundedZero with { IcIndex = 1 }], 0, 1),
            ([roundedZero with { SourceArea = 5e-13, Stage3EffectiveArea = 5e-13 }], 0, 0),
        };
        foreach (var (infos, ic0Count, ic1Count) in cases)
        {
            foreach (var anchorIc in new[] { 0, 1 })
            {
                var (candidate, resolved) = CreateA1ResolvedCandidate(
                    model, baseline with { RegularDebugInfos = infos }, anchorIc);
                var count = resolved.TargetAllocation.Targets.Sum(target => target.ToFullAppliedRegularCount);
                Assert.Equal(anchorIc == 0 ? ic0Count : ic1Count, count);
                Assert.All(resolved.TargetAllocation.Targets, target => Assert.Equal(anchorIc, target.IcIndex));
                Assert.Equal(count > 0, candidate.GetType().GetProperty("HasToFullInfluence")!.GetValue(candidate));
                if (infos.Contains(roundedZero) && anchorIc == 0)
                {
                    Assert.Equal(0, Assert.Single(resolved.TargetAllocation.Targets,
                        target => target.DiffIndex == 11).RatioPercentRounded);
                    Assert.Empty(resolved.TargetAllocation.TargetCoverageProjection.EmittedTargets);
                }
            }
        }
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void A1Fact3_CadAllocation_SourceScoreMatchesOwnedAreaAndFallback(NotchCompensationModel model)
    {
        var baseline = NotchV22CompensationService.Compute(
            CreateCad(CreateGrid()).Pads[0], CreateGrid(), true, false);
        var info = Assert.Single(baseline.RegularDebugInfos);
        var cases = new (NotchV22RegularDebugInfo[] Infos, double Overlap, int Ic, int Source, double Score)[]
        {
            ([info], 100, 0, 0, 100),
            ([info with { SourceArea = 30 }, info with { RegularPadId = 2, SourceArea = 40 }], 70, 0, 0, 70),
            ([info with { DiffIndex = 11, SourceArea = 30 }], 30, 0, 0, 30),
            ([info], 100, 0, 99, 100),
            ([], 0, 0, 0, 0),
            ([], 5e-13, 0, 0, 5e-13),
            ([info with { SourceArea = 5e-13, Stage3EffectiveArea = 5e-13 }], 0, 0, 0, 0),
            ([info with { SourceArea = 0, Stage3EffectiveArea = 10, IsToFullApplied = true }], 0, 0, 0,
                model == NotchCompensationModel.CurrentGain ? 10 : 0),
            ([info with { SourceArea = 40 }, info with { IcIndex = 1, SourceArea = 70 }], 110, 1, 0, 70),
        };
        foreach (var (infos, overlap, ic, source, score) in cases)
        {
            var (candidate, resolved) = CreateA1ResolvedCandidate(
                model, baseline with { RegularDebugInfos = infos, OverlapAreaTotal = overlap }, ic, source);
            var ownedAnchor = resolved.TargetAllocation.Targets.SingleOrDefault(target => target.IsAnchorDiff);
            var ownedArea = ownedAnchor?.EffectiveArea ?? resolved.Compensation.OverlapAreaTotal;
            Assert.Equal(score, ownedArea);
            Assert.Equal(ownedArea, (double)candidate.GetType().GetProperty("SourceAreaScore")!.GetValue(candidate)!);
        }

        var (larger, _) = CreateA1ResolvedCandidate(model, baseline);
        var (smaller, _) = CreateA1ResolvedCandidate(model, baseline with
        {
            RegularDebugInfos = [info with { SourceArea = 40, RegularArea = 20 }],
            OverlapAreaTotal = 40,
        });
        Assert.Same(larger, NotchTableGeneratorPrivateAccess.SelectPrimaryV22Candidate(null, CreatePrivateList(larger.GetType(), smaller, larger)));
        Assert.Same(larger, NotchTableGeneratorPrivateAccess.SelectPrimaryV22Candidate(null, CreatePrivateList(larger.GetType(), larger, smaller)));
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void A1Fact4_CadAllocation_CombinedFallbackAndOverflowMatchOwnedProjection(NotchCompensationModel model)
    {
        var switches = new NotchSettings { CompensationModel = model }.ResolveStep3Switches();
        var baseline = NotchV22CompensationService.Compute(
            CreateCad(CreateGrid()).Pads[0], CreateGrid(), switches.EnableToRegular, switches.EnableToFull);
        var info = Assert.Single(baseline.RegularDebugInfos);
        var zero = baseline with
        {
            RegularDebugInfos = [],
            OverlapAreaTotal = 0,
            ToRegularRatio = switches.EnableToRegular ? 0 : 1,
            CombinedRatio = switches.EnableToRegular ? 0 : 1,
        };
        var cases = new[]
        {
            baseline,
            zero,
            baseline with { RegularDebugInfos = [info with { DiffIndex = 11, SourceArea = 0.1,
                Stage3EffectiveArea = 0.1, IsToFullApplied = false }] },
            baseline with { RegularDebugInfos = [info with { SourceArea = 5e-13,
                Stage3EffectiveArea = 5e-13, IsToFullApplied = false }] },
            baseline with { RegularDebugInfos = [info, info with { RegularPadId = 2 }, info with { RegularPadId = 3 }] },
        };
        foreach (var compensation in cases)
        {
            foreach (var source in new[] { 0, 99 })
            {
                var (candidate, resolved) = CreateA1ResolvedCandidate(model, compensation, sourceDiff: source);
                var projection = resolved.TargetAllocation.TargetCoverageProjection;
                var ownedPercent = projection.RawCombinedPercent ??
                    (int)Math.Round(projection.DisplayCombinedRatio * 100);
                Assert.Equal(Math.Clamp(ownedPercent, 0, 255),
                    candidate.GetType().GetProperty("CombinePercent")!.GetValue(candidate));
                var error = (string?)candidate.GetType().GetProperty("ProjectionError")!.GetValue(candidate);
                Assert.Equal(projection.HasCombinedOverflowRisk, error is not null);
                if (projection.RawCombinedPercent is null)
                {
                    Assert.Equal(NotchCompensationModel.Disabled, model);
                    Assert.Equal(100, ownedPercent);
                    Assert.Equal(100, new NotchSettings { CompensationModel = model }.ResolveCombinedPercent(0, 999));
                }
                else if (ownedPercent > 255)
                {
                    Assert.Contains($"raw={ownedPercent}%", error!, StringComparison.Ordinal);
                }
            }
        }
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void A1Fact6_CadAllocation_EmittedLegChunksAndFinalOrderMatchOwner(NotchCompensationModel model)
    {
        var baseline = NotchV22CompensationService.Compute(
            CreateCad(CreateGrid()).Pads[0], CreateGrid(), true, false);
        var info = Assert.Single(baseline.RegularDebugInfos);
        var compensation = baseline with
        {
            RegularDebugInfos =
            [
                info with { SourceArea = 10, Stage3EffectiveArea = 10 },
                info with { DiffIndex = 2, SourceArea = 150, Stage3EffectiveArea = 150 },
                info with { DiffIndex = 3, SourceArea = 20, Stage3EffectiveArea = 20 },
                info with { DiffIndex = 1, SourceArea = 20, Stage3EffectiveArea = 20 },
                info with { DiffIndex = 4, SourceArea = 0.1, Stage3EffectiveArea = 0.1, IsToFullApplied = true },
                info with { DiffIndex = 5, SourceArea = 0, Stage3EffectiveArea = 25, IsToFullApplied = true },
                info with { IcIndex = 1, DiffIndex = 6, SourceArea = 200, Stage3EffectiveArea = 200 },
            ],
        };
        var (candidate, resolved) = CreateA1ResolvedCandidate(model, compensation);
        var targets = resolved.TargetAllocation.TargetCoverageProjection.EmittedTargets;
        var expected = new List<(int Diff, int Percent, bool ToFull)>();
        foreach (var target in targets)
        {
            var percent = target.RatioPercentRounded;
            var chunks = NotchV22TargetAllocationPolicy.UsesTargetRegularCoverage(model)
                ? Enumerable.Repeat(100, percent / 100).Concat(percent % 100 == 0 ? [] : new[] { percent % 100 })
                : new[] { Math.Clamp(percent, -100, 100) };
            expected.AddRange(chunks.Select(chunk => (target.DiffIndex, chunk, target.ToFullAppliedRegularCount > 0)));
        }
        Assert.DoesNotContain(targets, target => target.DiffIndex is 0 or 4 or 6);
        var legs = (System.Collections.IEnumerable)candidate.GetType().GetProperty("Legs")!.GetValue(candidate)!;
        var actual = legs.Cast<object>().Select(leg => (
            (int)leg.GetType().GetProperty("TargetDiffIndex")!.GetValue(leg)!,
            (int)leg.GetType().GetProperty("RatioPercent")!.GetValue(leg)!,
            (bool)leg.GetType().GetProperty("HasToFullCoverage")!.GetValue(leg)!)).ToArray();
        Assert.Equal(expected, actual);

        var rows = NotchTableGeneratorPrivateAccess.BuildV22DiffCentricRows(
            null,
            CreateCandidateDictionary(candidate.GetType(), ((0, 0), new[] { candidate })),
            65535,
            false,
            120);
        var ordered = expected.OrderByDescending(leg => Math.Abs(leg.Percent)).ThenBy(leg => leg.Diff)
            .Select(leg => (leg.Diff, leg.Percent)).ToArray();
        var nodeLegs = rows.SelectMany(row => new[]
        {
            (row.V22Node!.TargetDiffIndex1, row.V22Node.TargetRatioPercent1),
            (row.V22Node.TargetDiffIndex2, row.V22Node.TargetRatioPercent2),
        }).Where(leg => leg.Item2 != 0).ToArray();
        Assert.Equal(ordered, nodeLegs);
        Assert.Equal((ordered.Length + 1) / 2, rows.Count);
        Assert.All(rows.Skip(1), row =>
        {
            Assert.Equal(100, row.V22Node!.CombinePercent);
            Assert.Equal(1, row.V22Node.Flags);
        });
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void A1Fact6_CadAllocation_SignedAbiBoundariesOnlyClampOrChunkOwnedPercent(NotchCompensationModel model)
    {
        var cases = new (int Percent, int Clamped, int[] Chunks)[]
        {
            (-250, -100, [-100, -100, -50]), (-101, -100, [-100, -1]), (-100, -100, [-100]),
            (-1, -1, [-1]), (0, 0, []), (1, 1, [1]), (100, 100, [100]),
            (101, 100, [100, 1]), (250, 100, [100, 100, 50]),
        };
        foreach (var (percent, clamped, chunks) in cases)
        {
            // Signed diagnostic inputs characterize the ABI boundary, not geometry reachability.
            var target = new NotchV22TargetAllocation(0, 11, 1, percent / 100.0, percent,
                true, false, 1, 1, [], []);
            var projection = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
                [target], 0, 0, NotchV22TargetAllocationPolicy.ResolveAreaMode(model), 1);
            var legs = (System.Collections.IEnumerable)NotchTableGeneratorPrivateAccess.BuildV22DiffLegs(null, projection.EmittedTargets, model);
            var actual = legs.Cast<object>().Select(leg =>
                (int)leg.GetType().GetProperty("RatioPercent")!.GetValue(leg)!).ToArray();
            Assert.Equal(NotchV22TargetAllocationPolicy.UsesTargetRegularCoverage(model)
                ? chunks : percent == 0 ? [] : new[] { clamped }, actual);
            foreach (var (ic, source) in new (int?, int?)[] { (null, 0), (0, null) })
            {
                var missingAnchor = NotchV22TargetAllocationPolicy.ProjectTargetCoverage(
                    [target], ic, source, NotchV22TargetAllocationPolicy.ResolveAreaMode(model), 1);
                var missingLegs = (System.Collections.IEnumerable)NotchTableGeneratorPrivateAccess.BuildV22DiffLegs(null, missingAnchor.EmittedTargets, model);
                Assert.Empty(missingLegs.Cast<object>());
            }
        }
    }

    // Feed boundary diagnostics through the existing normal CadAllocation sparse-result reader.

    [Theory]
    [InlineData(NotchAlgorithmVersion.V21)]
    [InlineData(NotchAlgorithmVersion.V22)]
    public void A1Fact5_CadAllocation_Q7ZeroAndPositiveAllocationsExportIdenticallyWarmAndCold(
        NotchAlgorithmVersion version)
    {
        var cad = CreateCadWithBounds(0, 0, 2570, 10);
        var anchor = new RegularPad(0, 1, 1, CreateCadWithBounds(10, 0, 2570, 10).Pads[0].Polygon)
        {
            IcIndex = 0,
            DiffIndex = 1,
            Freeform = FreeformType.XWay,
            MatchedCadPadId = cad.Pads[0].Id,
            MatchScore = 1.0,
        };
        var xEdges = new[] { 0.0, 10.0, 2570.0 };
        var yEdges = new[] { 0.0, 10.0 };
        var grid = new RegularGrid(1, 2, xEdges, yEdges, new[] { CreateGrid().Pads[0], anchor });
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                MultiOwnerStrictOverlapPercent = 0.1,
                ThresholdQ7 = 0,
                ThresholdPercentV22 = 0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { version },
            },
        };
        const double strictOverlapRatio = 0.001;
        var target = cad.Pads[0];
        var context = NotchV22CompensationService.CreateContext(
            target, grid, enableToRegular: true, enableToFull: true,
            allCadPads: cad.Pads, strictOverlapRatioOverride: strictOverlapRatio,
            enableToFullRuleEngine: settings.Notch.EnableToFullRuleEngine,
            enableToFullRuleTrace: settings.Notch.EnableToFullRuleTrace,
            enableBoundaryVirtualAreaCap: settings.Notch.EnableBoundaryVirtualAreaCap,
            boundaryVirtualAreaCapRatio: settings.Notch.BoundaryVirtualAreaCapRatio);
        var warmResolved = NotchV22ResolvedResultService.Build(
            target,
            NotchV22CompensationService.Compute(context),
            strictOverlapRatio,
            anchorIcIndex: anchor.IcIndex,
            anchorDiffIndex: anchor.DiffIndex,
            allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage,
            identity: NotchV22ResolvedResultService.CreateIdentity(
                target, grid, cad.Pads, activeRegularPadIds: null,
                compensationModel: settings.Notch.CompensationModel,
                enableToRegular: true, enableToFull: true,
                enableToFullRuleEngine: settings.Notch.EnableToFullRuleEngine,
                enableToFullRuleTrace: settings.Notch.EnableToFullRuleTrace,
                enableBoundaryVirtualAreaCap: settings.Notch.EnableBoundaryVirtualAreaCap,
                boundaryVirtualAreaCapRatio: settings.Notch.BoundaryVirtualAreaCapRatio,
                strictOverlapRatio: strictOverlapRatio,
                anchorIcIndex: anchor.IcIndex,
                anchorDiffIndex: anchor.DiffIndex,
                allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage));
        var outputDiffs = new Dictionary<int, int> { [target.Id] = anchor.DiffIndex };
        var warmBatch = new NotchTableGenerator().ResolveCadAllocationBatch(
            cad, grid, settings,
            cadOutputFwDiffIndexByCadId: outputDiffs,
            selectedSparseResultRequest: new NotchTableGenerator.CadAllocationSparseResultRequest(
                target.Id, anchor.IcIndex, anchor.DiffIndex, warmResolved));

        Assert.Same(warmResolved, warmBatch.SelectedSparseResult!.ResolvedResult);
        var warm = NotchTableGenerator.ProjectCadAllocationResolvedBatch(warmBatch, settings);
        var cold = new NotchTableGenerator().Generate(
            cad, grid, settings, cadOutputFwDiffIndexByCadId: outputDiffs);

        Assert.Equal(cold.Rows.Select(BuildRowSnapshot), warm.Rows.Select(BuildRowSnapshot));
        Assert.Equal(100, warmResolved.TargetAllocation.TargetCoverageProjection.RawCombinedPercent);
    }

    [Fact]
    public void A1Fact5_CadAllocation_Q7ZeroPartialOverlapDoesNotExpandStage3()
    {
        var cad = CreateCadWithBounds(5, 0, 2575, 10);
        var context = NotchV22CompensationService.CreateContext(
            cad.Pads[0], CreateGrid(), enableToRegular: true, enableToFull: true,
            allCadPads: cad.Pads, strictOverlapRatioOverride: 0.001,
            enableBoundaryVirtualAreaCap: true, boundaryVirtualAreaCapRatio: 1.0);

        var compensation = NotchV22CompensationService.Compute(context);

        Assert.Equal(25700.0, compensation.Stage3Area, 6);
        Assert.False(compensation.IsToFullEnabled);
    }
}
