using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class NotchDisplayProjectorTests
{
    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain)]
    [InlineData(NotchCompensationModel.ConservativeNoGain)]
    [InlineData(NotchCompensationModel.Disabled)]
    public void Build_NormalCadAllocation_WithoutAnchorAndSourcePreservesStrictPassingTargetDisplay(
        NotchCompensationModel model)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 1, cellWidth: 10, cellHeight: 10);
        var cad = TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 10, 10);
        var compensation = NotchV22CompensationService.Compute(
            cad, grid, enableToRegular: true, enableToFull: false);
        var resolved = new NotchV22ResolvedResultService().Build(
            cad,
            compensation,
            strictOverlapRatio: 0.1,
            anchorIcIndex: null,
            anchorDiffIndex: null,
            allocationAreaMode: NotchV22TargetAllocationPolicy.ResolveAreaMode(model));
        var allocation = resolved.TargetAllocation;
        var target = Assert.Single(allocation.Targets);
        var display = NotchDisplayProjector.Build(resolved.Compensation, allocation);

        Assert.True(target.PassesStrictThreshold);
        Assert.False(target.IsAnchorDiff);
        Assert.Equal(100, target.RatioPercentRounded);
        Assert.Empty(allocation.TargetCoverageProjection.EmittedTargets);
        Assert.Null(allocation.TargetCoverageProjection.RawCombinedPercent);
        Assert.Equal("Targets: 1 effective / 1 total", display.TargetAllocationSummaryText);
        Assert.Equal("IC1/diff0  A 100 mm²  R 100%", Assert.Single(display.TargetAllocationLines));
        var item = Assert.Single(display.TargetAllocationItems);
        Assert.Equal("Target", item.RoleText);
        Assert.Equal("100%", item.RatioText);
        Assert.Equal("100.00 %", display.CombinedValueText);
    }

    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain, 0)]
    [InlineData(NotchCompensationModel.ConservativeNoGain, 0)]
    [InlineData(NotchCompensationModel.Disabled, 0)]
    public void Build_NormalCadAllocation_RoundedZeroTargetCharacterizesDisplayAndGeneratorEligibility(
        NotchCompensationModel model,
        int expectedDisplayTargetCount)
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10,
            diffIndexSelector: static (_, col) => col);
        var cad = TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 10.045, 10);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Id;
            regular.MatchScore = 1.0;
        }

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = model,
                EnableToFull = false,
                MultiOwnerStrictOverlapPercent = 0.01,
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0,
                EnabledVersions = [NotchAlgorithmVersion.V22],
            },
        };
        var batch = new NotchTableGenerator().ResolveCadAllocationBatch(
            new CadPadSet([cad]), grid, settings,
            selectedSparseResultRequest: new NotchTableGenerator.CadAllocationSparseResultRequest(
                cad.Id, AnchorIcIndex: 0, AnchorDiffIndex: 0));
        var resolved = Assert.IsType<NotchTableGenerator.CadAllocationSparseResult>(batch.SelectedSparseResult)
            .ResolvedResult;
        var allocation = resolved.TargetAllocation;
        var roundedZero = Assert.Single(allocation.Targets, static target => target.DiffIndex == 1);
        var display = NotchDisplayProjector.Build(resolved.Compensation, allocation);
        var table = NotchTableGenerator.ProjectCadAllocationResolvedBatch(batch, settings);
        var node = Assert.IsType<NotchV22Node>(Assert.Single(table.Rows).V22Node);

        Assert.Equal(0.45, roundedZero.EffectiveArea, 9);
        Assert.Equal(1, Assert.Single(NotchAllocationService.BuildAllocations(cad, grid),
            static item => item.Pad.DiffIndex == 1).Q7);
        Assert.True(roundedZero.PassesStrictThreshold);
        Assert.False(roundedZero.IsAnchorDiff);
        Assert.Equal(0, roundedZero.RatioPercentRounded);
        Assert.Empty(allocation.TargetCoverageProjection.EmittedTargets);
        Assert.Equal(new NotchV22Node(0, 100, settings.Notch.NullValue, 0, settings.Notch.NullValue, 0, 0), node);
        Assert.Equal($"Targets: {expectedDisplayTargetCount} effective / 2 total", display.TargetAllocationSummaryText);
        Assert.Equal(expectedDisplayTargetCount, display.TargetAllocationLines.Count);
        var displayedTarget = Assert.Single(display.TargetAllocationItems, static item => item.DiffText == "IC1/diff1");
        Assert.Equal("Below gate", displayedTarget.RoleText);
        Assert.Equal("0%", displayedTarget.RatioText);
        if (model == NotchCompensationModel.Disabled)
        {
            Assert.Null(allocation.TargetCoverageProjection.RawCombinedPercent);
        }
    }

    [AvaloniaFact]
    public void Build_LegacyRegularAnchor_DisabledRoundedZeroTargetPreservesPreviousDisplay()
    {
        var grid = TestGeometryFactory.CreateRegularGrid(1, 2, cellWidth: 10, cellHeight: 10,
            diffIndexSelector: static (_, col) => col);
        var cad = TestGeometryFactory.CreateCadPad(1, "PAD", 0, 0, 10.045, 10);
        foreach (var regular in grid.Pads)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Id;
            regular.MatchScore = 1.0;
        }

        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>([cad]),
            RegularPads = new ObservableCollection<RegularPad>(grid.Pads),
            EnableToRegular = true,
            EnableToFull = false,
            ToFullStrictOverlapPercent = 0.01m,
        };
        vm.SelectedNotchCompensationModelOption = Assert.Single(
            vm.NotchCompensationModelOptions,
            static option => option.Value == NotchCompensationModel.Disabled);
        var vmType = typeof(FreeformHelperViewModel);
        const BindingFlags fieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        vmType.GetField("_grid", fieldFlags)!.SetValue(vm, grid);
        vmType.GetField("_cad", fieldFlags)!.SetValue(vm, new CadPadSet([cad]));
        var project = Assert.IsType<ProjectFile>(vmType.GetField("_projectFile", fieldFlags)!.GetValue(vm));
        project.Settings.Notch.ComputationMode = NotchComputationMode.LegacyRegularAnchor;
        Assert.IsType<Dictionary<int, int>>(vmType.GetField("_cadOutputFwDiffIndexByCadId", fieldFlags)!.GetValue(vm))
            .Add(cad.Id, 0);
        Assert.IsType<Dictionary<int, int>>(vmType.GetField("_cadIcIndexByCadId", fieldFlags)!.GetValue(vm))
            .Add(cad.Id, 0);
        vmType.GetMethod("InvalidateWorkflowDataSnapshot", fieldFlags)!.Invoke(vm, null);

        var snapshot = Assert.IsType<PadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            cad.Id, includeNotchRowEligibilityDetails: false));
        var notch = Assert.IsType<PadInspectorNotchSnapshot>(snapshot.Cad?.Notch);
        var roundedZero = Assert.Single(notch.Targets, static target => target.DiffIndex == 1);
        var display = NotchDisplayProjector.Build(notch);

        Assert.Equal(0.45, roundedZero.EffectiveArea, 9);
        Assert.True(roundedZero.PassesStrictThreshold);
        Assert.False(roundedZero.IsAnchorDiff);
        Assert.Equal(0, roundedZero.RatioPercentRounded);
        Assert.True(Assert.Single(notch.Targets, static target => target.DiffIndex == 0).IsAnchorDiff);
        Assert.True(notch.TargetCoverageProjection!.HasEmittedTargetMembership);
        Assert.Empty(notch.TargetCoverageProjection!.EmittedTargets);
        Assert.Null(notch.TargetCoverageProjection.RawCombinedPercent);
        Assert.Equal("Targets: 1 effective / 2 total", display.TargetAllocationSummaryText);
        Assert.Equal("IC1/diff1  A 0.45 mm²  R 0%", Assert.Single(display.TargetAllocationLines));
        var displayedTarget = Assert.Single(display.TargetAllocationItems, static item => item.DiffText == "IC1/diff1");
        Assert.Equal("Target", displayedTarget.RoleText);
        Assert.Equal("0%", displayedTarget.RatioText);
    }

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
