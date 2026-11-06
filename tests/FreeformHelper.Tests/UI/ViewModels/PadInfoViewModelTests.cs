using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInfoViewModelTests
{
    private static readonly int[] _matchedRegularPadIds = [4808];

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RegularPadInfo_ApplyChangesCommand_AppliesOnlyChangedDimensions(bool editWidth, bool editHeight)
    {
        var appliedSizes = new List<(decimal? Width, decimal? Height)>();
        var closeCount = 0;
        var vm = CreateSinglePadViewModel(
            applyPadSize: (rows, cols, width, height) =>
            {
                Assert.Equal(1, Assert.Single(rows));
                Assert.Equal(2, Assert.Single(cols));
                appliedSizes.Add((width, height));
            },
            closePadInfo: () => closeCount++);
        if (editWidth)
        {
            vm.PadWidth = 12m;
        }
        if (editHeight)
        {
            vm.PadHeight = 9m;
        }
        Assert.True(vm.HasPendingChanges);

        vm.ApplyChangesCommand.Execute(null);

        var applied = Assert.Single(appliedSizes);
        Assert.Equal(editWidth ? 12m : (decimal?)null, applied.Width);
        Assert.Equal(editHeight ? 9m : (decimal?)null, applied.Height);
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(1, closeCount);

        vm.ApplyChangesCommand.Execute(null);

        Assert.Single(appliedSizes);
        Assert.False(vm.HasPendingChanges);
    }

    [Fact]
    public void RegularPadInfo_DiscardChangesCommand_RestoresDimensionsWithoutApplying()
    {
        var applyCount = 0;
        var vm = CreateSinglePadViewModel(applyPadSize: (_, _, _, _) => applyCount++);
        var initialWidth = vm.PadWidth;
        var initialHeight = vm.PadHeight;
        vm.PadWidth = 12m;
        vm.PadHeight = 9m;
        Assert.True(vm.HasPendingChanges);

        vm.DiscardChangesCommand.Execute(null);

        Assert.Equal(initialWidth, vm.PadWidth);
        Assert.Equal(initialHeight, vm.PadHeight);
        Assert.False(vm.HasPendingChanges);
        Assert.Equal(0, applyCount);

        vm.ApplyChangesCommand.Execute(null);

        Assert.Equal(0, applyCount);
    }

    [Fact]
    public void RegularPadInfo_ResetPadSizeCommand_ResetsSelectedRowAndColumnAndCloses()
    {
        var resetCount = 0;
        var closeCount = 0;
        var vm = CreateSinglePadViewModel(
            resetPadSize: (rows, cols) =>
            {
                Assert.Equal(1, Assert.Single(rows));
                Assert.Equal(2, Assert.Single(cols));
                resetCount++;
            },
            closePadInfo: () => closeCount++);

        vm.ResetPadSizeCommand.Execute(null);

        Assert.Equal(1, resetCount);
        Assert.Equal(1, closeCount);
    }

    [Theory]
    [InlineData(7.6, 8)]
    [InlineData(2_000_000.0, 1_000_000)]
    [InlineData(-3.2, 0)]
    public void CadPadInfo_OutputFwDiffCommands_ApplyRoundedClampedOverrideAndClear(double input, int expected)
    {
        var appliedOverrides = new List<(int CadId, int DiffIndex)>();
        var clearedCadIds = new List<int>();
        var vm = CreateSingleCadPadViewModel(
            setCadOutputFwDiffOverride: (id, diff) => appliedOverrides.Add((id, diff)),
            clearCadOutputFwDiffOverride: clearedCadIds.Add);
        vm.CadOutputFwDiffOverride = (decimal)input;

        vm.ApplyCadOutputFwDiffOverrideCommand.Execute(null);

        Assert.Equal((274, expected), Assert.Single(appliedOverrides));
        Assert.Equal((decimal)expected, vm.CadOutputFwDiffOverride);
        Assert.True(vm.HasCadOutputFwDiffOverride);
        Assert.True(vm.CanClearCadOutputFwDiffOverride);

        vm.ClearCadOutputFwDiffOverrideCommand.Execute(null);

        Assert.Equal(274, Assert.Single(clearedCadIds));
        Assert.False(vm.HasCadOutputFwDiffOverride);
        Assert.False(vm.CanClearCadOutputFwDiffOverride);
    }

    [Fact]
    public void CadPadInfo_DxfIndexAnchorCommands_SetAndClearSelectedCadAnchor()
    {
        var isAnchor = false;
        var anchoredCadIds = new List<int>();
        var clearedCadIds = new List<int>();
        var vm = CreateSingleCadPadViewModel(
            isDxfIndexAnchor: _ => isAnchor,
            setDxfIndexAnchor: id =>
            {
                anchoredCadIds.Add(id);
                isAnchor = true;
            },
            clearDxfIndexAnchor: id =>
            {
                clearedCadIds.Add(id);
                isAnchor = false;
            });

        vm.SetDxfIndexAnchorCommand.Execute(null);

        Assert.Equal(274, Assert.Single(anchoredCadIds));
        Assert.True(vm.IsDxfIndexAnchor);
        Assert.True(vm.CanClearDxfIndexAnchor);
        Assert.Equal("112", vm.DxfIndexText);

        vm.ClearDxfIndexAnchorCommand.Execute(null);

        Assert.Equal(274, Assert.Single(clearedCadIds));
        Assert.False(vm.IsDxfIndexAnchor);
        Assert.False(vm.CanClearDxfIndexAnchor);
        Assert.Equal("112", vm.DxfIndexText);
    }

    [Fact]
    public void CadPadInfo_SelectAreaBucketCommand_SelectsMatchingPads()
    {
        var workspace = new FreeformHelperViewModel
        {
            ColorCadByArea = true,
            AreaBucketTolerance = 0.001m,
            CadPads = new ObservableCollection<CadPad>
            {
                TestGeometryFactory.CreateCadPad(274, "AA", 0, 0, 10, 8),
                TestGeometryFactory.CreateCadPad(275, "AA", 20, 0, 30, 8),
                TestGeometryFactory.CreateCadPad(276, "AA", 40, 0, 55, 8),
            },
        };
        var vm = CreateSingleCadPadViewModel(selectAreaBucket: () => workspace.SelectCadAreaBucketFromPad(274));

        Assert.NotNull(vm.SelectAreaBucketCommand);
        vm.SelectAreaBucketCommand.Execute(null);

        Assert.Equal([274, 275], workspace.SelectedCadPadIds.Order());
    }

    [Fact]
    public void CadPadInfo_HighlightAllNotchOwnersCommand_HighlightsDistinctOwnersExceptSelf()
    {
        IReadOnlyCollection<int>? highlightedCadIds = null;
        var vm = CreateSingleCadPadViewModel(
            highlightCadOwnerPads: ids => highlightedCadIds = ids,
            notchDiagnostics:
                "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=32|274|31, " +
                "ownerShare=50/50, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)\n" +
                "IC3/diff113 reg4809: ov=10, src=10, blk=0, reach=10, blocker=0, owners=32, " +
                "ownerShare=100, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)");

        vm.HighlightAllNotchOwnersCommand.Execute(null);

        Assert.NotNull(highlightedCadIds);
        Assert.Equal([31, 32], highlightedCadIds);
    }

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
    public void CadPadInfo_LocateMatchCommand_TriggersFocusAction()
    {
        int? focusedCadId = null;
        var vm = CreateSingleCadPadViewModel(focusCadMatches: id => focusedCadId = id);

        Assert.True(vm.HasFocusMatchAction);

        vm.LocateMatchCommand.Execute(null);

        Assert.Equal(274, focusedCadId);
    }

    [Fact]
    public void CadPadInfo_UsesSharedNotchDisplayProjectionTexts()
    {
        // ToRegularRatioText and ToFullRatioText format percent values with the current culture.
        using var culture = TestCulture.UseEnUs();
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
        var resolved = NotchV22ResolvedResultService.Build(
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
        var highlightedRegularPadIds = new List<int>();
        var vm = CreateSingleCadPadViewModel(targetAllocation: resolved.TargetAllocation,
            highlightRegularPads: ids => highlightedRegularPadIds.Add(Assert.Single(ids)));
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
        HighlightTargetAllocations(vm.TargetAllocationItems);
        Assert.Equal([4808, 4809, 4810], highlightedRegularPadIds);
        Assert.Equal("Self before To Full", diff83.RoleText);
        Assert.Equal("54%", diff83.RatioText);
        Assert.Equal("Target", diff84.RoleText);
        Assert.Equal("Below gate", diff85.RoleText);
    }

    private static void HighlightTargetAllocations(IEnumerable<PadInfoTargetAllocationViewModel> items)
    {
        foreach (var item in items)
        {
            Assert.NotNull(item.HighlightCommand);
            item.HighlightCommand.Execute(null);
        }
    }

    private static RegularPadInfoViewModel CreateSinglePadViewModel(
        Action<IReadOnlyList<int>, IReadOnlyList<int>, decimal?, decimal?>? applyPadSize = null,
        Action<IReadOnlyList<int>, IReadOnlyList<int>>? resetPadSize = null,
        Action? closePadInfo = null)
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
            applyPadSize: applyPadSize,
            resetPadSize: resetPadSize,
            setFreeform: null,
            getCrossIcOwnerShares: null,
            getNotchRowsByRegularPad: null,
            highlightCadOwnerPads: null,
            focusRegularMatches: null,
            highlightRegularMatches: null,
            closePadInfo: closePadInfo);
    }

    private static CadPadInfoViewModel CreateSingleCadPadViewModel(
        Action<int>? focusCadMatches = null,
        Action<int>? highlightCadMatches = null,
        NotchV22TargetAllocationSummary? targetAllocation = null,
        Action<IReadOnlyCollection<int>>? highlightRegularPads = null,
        Action<int, int>? setCadOutputFwDiffOverride = null,
        Action<int>? clearCadOutputFwDiffOverride = null,
        Func<int, bool>? isDxfIndexAnchor = null,
        Action<int>? setDxfIndexAnchor = null,
        Action<int>? clearDxfIndexAnchor = null,
        Action? selectAreaBucket = null,
        Action<IReadOnlyCollection<int>>? highlightCadOwnerPads = null,
        string? notchDiagnostics = null)
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

        var snapshot = CreateCadPadInspectorSnapshot(cadPad, notchDiagnostics);

        return new CadPadInfoViewModel(
            pads: new List<CadPad> { cadPad },
            getDxfIndex: _ => 112,
            getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: isDxfIndexAnchor,
            getCustomValue: _ => 0,
            applyCustomValue: null,
            setCadOutputFwDiffOverride: setCadOutputFwDiffOverride,
            clearCadOutputFwDiffOverride: clearCadOutputFwDiffOverride,
            setDxfIndexAnchor: setDxfIndexAnchor,
            clearDxfIndexAnchor: clearDxfIndexAnchor,
            closePadInfo: null,
            notchDetailCommand: null,
            selectAreaBucket: selectAreaBucket,
            getMatchedRegularPadIds: null,
            getMatchedRegularLinks: null,
            getRegularPadIcDiff: null,
            focusCadMatches: focusCadMatches,
            highlightCadMatches: highlightCadMatches,
            highlightCadOwnerPads: highlightCadOwnerPads,
            highlightRegularPads: highlightRegularPads,
            getCadV22CompensationPreview: targetAllocation is null
                ? null
                : _ => (0.941, 1.556, targetAllocation.TargetCoverageProjection.DisplayCombinedRatio, true),
            getCadV22CompensationDiagnostics: null,
            getCadV22TargetAllocationSummary: targetAllocation is null ? null : _ => targetAllocation,
            inspectorSnapshot: targetAllocation is null ? snapshot : null);
    }

    private static PadInspectorSnapshot CreateCadPadInspectorSnapshot(CadPad cadPad, string? notchDiagnostics = null)
    {
        return new PadInspectorSnapshot(
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
                MatchedRegularPadIds: _matchedRegularPadIds,
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
                    Diagnostics: notchDiagnostics ??
                        "IC3/diff112 reg4808: ov=10, src=10, blk=0, reach=10, blocker=0, owners=-, " +
                        "ownerShare=-, boundary=N, candidate=Y, tofull=Y (GATE_NOT_BOUNDARY)"),
                RuleTrace: new[]
                {
                    new PadInspectorRuleTraceEntry("Sequence", "Adjusted", "IC-row sequence applied"),
                },
                MatchConfidence: 0.61),
            Regular: null);
    }
}
