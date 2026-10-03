using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInfoViewModelTests
{
    private static readonly int[] MatchedRegularPadIds = [4808];

    [Fact]
    public void RegularPadInfo_DefaultsToCollapsedGeometryDetails()
    {
        var vm = CreateSinglePadViewModel();

        Assert.False(vm.ShowGeometryDetails);
        Assert.Equal("Show details", vm.GeometryDetailsToggleText);
    }

    [Fact]
    public void RegularPadInfo_ToggleGeometryDetailsCommand_TogglesVisibilityAndText()
    {
        var vm = CreateSinglePadViewModel();

        vm.ToggleGeometryDetailsCommand.Execute(null);
        Assert.True(vm.ShowGeometryDetails);
        Assert.Equal("Hide details", vm.GeometryDetailsToggleText);

        vm.ToggleGeometryDetailsCommand.Execute(null);
        Assert.False(vm.ShowGeometryDetails);
        Assert.Equal("Show details", vm.GeometryDetailsToggleText);
    }

    [Fact]
    public void CadPadInfo_ToggleLowFrequencyDetailSectionCommand_TogglesAllDetailSections()
    {
        var vm = CreateSingleCadPadViewModel();

        Assert.False(vm.ShowLowFrequencyDetailSection);
        Assert.False(vm.ShowNotchDiagnostics);
        Assert.False(vm.ShowRuleTrace);
        Assert.False(vm.ShowGeometryDetails);

        vm.ToggleLowFrequencyDetailSectionCommand.Execute(null);

        Assert.True(vm.ShowLowFrequencyDetailSection);
        Assert.True(vm.ShowNotchDiagnostics);
        Assert.True(vm.ShowRuleTrace);
        Assert.True(vm.ShowGeometryDetails);

        vm.ToggleLowFrequencyDetailSectionCommand.Execute(null);

        Assert.False(vm.ShowLowFrequencyDetailSection);
        Assert.False(vm.ShowNotchDiagnostics);
        Assert.False(vm.ShowRuleTrace);
        Assert.False(vm.ShowGeometryDetails);
    }

    [Fact]
    public void CadPadInfo_OpenOwnerDetailCommand_TriggersFocusAction()
    {
        int? focusedCadId = null;
        var vm = CreateSingleCadPadViewModel(focusCadMatches: id => focusedCadId = id);

        Assert.True(vm.HasFocusMatchAction);

        vm.OpenOwnerDetailCommand.Execute(null);

        Assert.Equal(274, focusedCadId);
    }

    [Fact]
    public void CadPadInfo_UsesSharedNotchDisplayProjectionTexts()
    {
        var vm = CreateSingleCadPadViewModel();

        Assert.Equal("Undo NF (To Regular): 94.10%", vm.ToRegularRatioText);
        Assert.Equal("To Full: 155.60% (enabled)", vm.ToFullRatioText);
        Assert.Equal("146.40 %", vm.CombinedValueText);
        Assert.Equal("Regular pad is not boundary.", vm.ToFullReasonShortText);
    }

    [Fact]
    public void CadPadInfo_TargetAllocationDisplay_UsesFinalEmittedTargetEligibility()
    {
        const double cadArea = 4570.0;
        const double cadHeight = 5.4;
        const double emittedArea = 18.09;
        const double roundedZeroArea = 18.0; // Q7-positive CAD allocation with rounded-zero regular coverage.
        const double strictOverlapRatio = 0.00398;
        var anchorMaxX = (cadArea - emittedArea - roundedZeroArea) / cadHeight;
        var emittedMaxX = anchorMaxX + (emittedArea / cadHeight);
        var cadMaxX = emittedMaxX + (roundedZeroArea / cadHeight);
        var gridMaxX = emittedMaxX + 400.0;
        var cad = TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, cadMaxX, cadHeight);
        var anchor = TestGeometryFactory.CreateRegularPad(
            0, 0, 4808, 0, 0, anchorMaxX, 10, diffIndex: 83);
        var emitted = TestGeometryFactory.CreateRegularPad(
            0, 1, 4809, anchorMaxX, 0, emittedMaxX, 10, diffIndex: 84);
        var roundedZero = TestGeometryFactory.CreateRegularPad(
            0, 2, 4810, emittedMaxX, 0, gridMaxX, 10, diffIndex: 85);
        var regulars = new[] { anchor, emitted, roundedZero };
        foreach (var regular in regulars)
        {
            regular.Freeform = FreeformType.XWay;
            regular.MatchedCadPadId = cad.Id;
            regular.MatchScore = 1.0;
        }

        var grid = new RegularGrid(
            rows: 1,
            cols: 3,
            xEdges: [0, anchorMaxX, emittedMaxX, gridMaxX],
            yEdges: [0, 10],
            pads: regulars);
        var activeRegularPadIds = regulars.Select(static regular => regular.RegularPadId).ToHashSet();
        Assert.Equal(1, Assert.Single(NotchAllocationService.BuildAllocations(cad, grid),
            allocation => allocation.Pad.DiffIndex == roundedZero.DiffIndex).Q7);
        var compensation = NotchV22CompensationService.Compute(
            cad,
            grid,
            enableToRegular: true,
            enableToFull: true,
            allCadPads: [cad],
            activeRegularPadIds,
            strictOverlapRatioOverride: strictOverlapRatio,
            enableToFullRuleEngine: true,
            enableToFullRuleTrace: false,
            enableBoundaryVirtualAreaCap: true,
            boundaryVirtualAreaCapRatio: 1.0);
        var resolved = new NotchV22ResolvedResultService().Build(
            cad,
            compensation,
            strictOverlapRatio,
            anchorIcIndex: 0,
            anchorDiffIndex: anchor.DiffIndex,
            allocationAreaMode: NotchV22TargetAllocationAreaMode.TargetRegularSourceCoverage);
        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                EnableToRegular = true,
                EnableToFull = true,
                EnableToFullRuleEngine = true,
                EnableToFullRuleTrace = false,
                EnableBoundaryVirtualAreaCap = true,
                BoundaryVirtualAreaCapRatio = 1.0,
                MultiOwnerStrictOverlapPercent = strictOverlapRatio * 100.0,
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0.0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        var table = new NotchTableGenerator().Generate(
            new CadPadSet([cad]),
            grid,
            settings,
            activeRegularPadIds: activeRegularPadIds,
            cadOutputFwDiffIndexByCadId: new Dictionary<int, int> { [cad.Id] = anchor.DiffIndex });
        var node = Assert.IsType<NotchV22Node>(Assert.Single(table.Rows).V22Node);
        var emittedTarget = Assert.Single(resolved.TargetAllocation.TargetCoverageProjection.EmittedTargets);
        var roundedZeroTarget = Assert.Single(
            resolved.TargetAllocation.Targets,
            target => target.DiffIndex == roundedZero.DiffIndex);
        var vm = CreateSingleCadPadViewModel(targetAllocation: resolved.TargetAllocation);

        Assert.Equal(emitted.DiffIndex, emittedTarget.DiffIndex);
        Assert.False(emittedTarget.PassesStrictThreshold);
        Assert.Equal(1, emittedTarget.ToFullAppliedRegularCount);
        Assert.Equal(54, emittedTarget.RatioPercentRounded);
        Assert.False(roundedZeroTarget.PassesStrictThreshold);
        Assert.Equal(1, roundedZeroTarget.ToFullAppliedRegularCount);
        Assert.Equal(0, roundedZeroTarget.RatioPercentRounded);
        Assert.Equal(roundedZeroArea, roundedZeroTarget.EffectiveArea, 12);
        Assert.Equal(18.1886, resolved.TargetAllocation.StrictAreaThreshold, 4);
        Assert.Equal(3, resolved.TargetAllocation.Targets.Count);
        Assert.Equal(108, resolved.TargetAllocation.TargetCoverageProjection.RawCombinedPercent);
        Assert.Equal(108, node.CombinePercent);
        Assert.Equal(emitted.DiffIndex, node.TargetDiffIndex1);
        Assert.Equal(54, node.TargetRatioPercent1);
        Assert.Equal(settings.Notch.NullValue, node.TargetDiffIndex2);
        Assert.Equal(0, node.TargetRatioPercent2);
        Assert.Equal("Targets: 1 effective / 3 total", vm.TargetAllocationSummaryText);
        var line = Assert.Single(vm.TargetAllocationLines);
        Assert.Equal("IC1/diff84  A 18.09 mm²  R 54%", line);
        var diff83 = Assert.Single(vm.TargetAllocationItems, item => item.DiffText == "IC1/diff83");
        var diff84 = Assert.Single(vm.TargetAllocationItems, item => item.DiffText == "IC1/diff84");
        var diff85 = Assert.Single(vm.TargetAllocationItems, item => item.DiffText == "IC1/diff85");
        Assert.Equal(
            ["IC1/diff83", "IC1/diff84", "IC1/diff85"],
            vm.TargetAllocationItems.Select(static item => item.DiffText));
        Assert.Equal(
            ["4533.91 mm²", "18.09 mm²", "18 mm²"],
            vm.TargetAllocationItems.Select(static item => item.AreaText));
        Assert.Equal(
            [4808, 4809, 4810],
            vm.TargetAllocationItems.Select(static item => Assert.Single(item.RegularPadIds)));
        Assert.Equal("Self before To Full", diff83.RoleText);
        Assert.Equal("54%", diff83.RatioText);
        Assert.Equal("Target", diff84.RoleText);
        Assert.Equal("Below gate", diff85.RoleText);
    }

    private static RegularPadInfoViewModel CreateSinglePadViewModel()
    {
        var pad = new RegularPad(
            row: 1,
            col: 2,
            index: 42,
            polygon: new Polygon2(new[]
            {
                new Point2(0, 0),
                new Point2(10, 0),
                new Point2(10, 8),
                new Point2(0, 8),
            }))
        {
            IcIndex = 0,
            DiffIndex = 42,
            Freeform = FreeformType.None,
            MatchedCadPadId = null,
            MatchScore = 0,
        };

        return new RegularPadInfoViewModel(
            pads: new List<RegularPad> { pad },
            toDisplayRow: static row => row,
            getDxfIndex: null,
            getMatchedCadPadIds: null,
            getMatchedCadLinks: null,
            applyPadSize: null,
            resetPadSize: null,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null,
            closePadInfo: null);
    }

    private static CadPadInfoViewModel CreateSingleCadPadViewModel(
        Action<int>? focusCadMatches = null,
        Action<int>? highlightCadMatches = null,
        NotchV22TargetAllocationSummary? targetAllocation = null)
    {
        var cadPad = new CadPad(
            274,
            "CAD274",
            "AA",
            new Polygon2(new[]
            {
                new Point2(0, 0),
                new Point2(10, 0),
                new Point2(10, 8),
                new Point2(0, 8),
            }));

        var snapshot = new PadInspectorSnapshot(
            "cad",
            new CadPadInspectorSnapshot(
                CadPadId: cadPad.Id,
                Name: cadPad.Name,
                Layer: cadPad.Layer,
                Area: cadPad.Area,
                Bounds: cadPad.Bounds,
                Centroid: cadPad.Centroid,
                Vertices: cadPad.Polygon.Vertices.Length,
                DxfIndex: 112,
                CadOutputFwDiffOverride: null,
                IsDxfIndexAnchor: false,
                DxfIndexDisplayText: "112",
                CadOutputFwDiffAssignmentModeText: "Auto",
                IcIndex: 2,
                MatchText: "IC3/diff112",
                MatchDetailsText: "IC3/diff112 (best)",
                MatchedRegularPadIds: MatchedRegularPadIds,
                MatchedRegularDetails: new[]
                {
                    new PadInspectorMatchedRegularSnapshot(4808, 2, 112, 0.61, 0.54),
                },
                IsNotchRowEligible: true,
                NotchRowSummary: "1 row",
                Notch: new PadInspectorNotchSnapshot(
                    ToRegularRatio: 0.941,
                    ToFullRatio: 1.556,
                    CombinedRatio: 1.464,
                    IsToFullEnabled: true,
                    Stage3Area: 12.3,
                    StrictAreaThreshold: 0.1,
                    Targets: Array.Empty<PadInspectorNotchTargetSnapshot>(),
                    Diagnostics: "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=-, ownerShare=-, boundary=N, candidate=Y, tofull=Y (GATE_NOT_BOUNDARY)"),
                RuleTrace: new[]
                {
                    new PadInspectorRuleTraceEntry("Sequence", "Adjusted", "IC-row sequence applied"),
                },
                MatchConfidence: 0.61),
            Regular: null);

        return new CadPadInfoViewModel(
            pads: new List<CadPad> { cadPad },
            getDxfIndex: _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null,
            getCustomValue: _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: null,
            clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null,
            clearDxfIndexAnchor: null,
            closePadInfo: null,
            notchDetailCommand: null,
            selectAreaBucket: null,
            getMatchedRegularPadIds: null,
            getMatchedRegularLinks: null,
            getRegularPadIcDiff: null,
            focusCadMatches: focusCadMatches,
            highlightCadMatches: highlightCadMatches,
            highlightCadOwnerPads: null,
            getCadV22CompensationPreview: targetAllocation is null
                ? null
                : _ => (0.941, 1.556, targetAllocation.TargetCoverageProjection.DisplayCombinedRatio, true),
            getCadV22CompensationDiagnostics: null,
            getCadV22TargetAllocationSummary: targetAllocation is null ? null : _ => targetAllocation,
            inspectorSnapshot: targetAllocation is null ? snapshot : null);
    }
}

