using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed class PadInspectorDisplayBehaviorTests
{
    private static readonly string[] _summaryNames =
    [
        nameof(FreeformHelperViewModel.HasPadInspectorSummary),
        nameof(FreeformHelperViewModel.HasNoPadInspectorSummary),
        nameof(FreeformHelperViewModel.PadInspectorCompactHeaderText),
        nameof(FreeformHelperViewModel.PadInspectorPrimaryText),
        nameof(FreeformHelperViewModel.PadInspectorSecondaryText),
        nameof(FreeformHelperViewModel.PadInspectorMatchText),
        nameof(FreeformHelperViewModel.PadInspectorCompensationLines),
        nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines),
        nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections),
        nameof(FreeformHelperViewModel.HasPadInspectorRuleTrace),
    ];

    [Fact]
    public void NewViewModel_HasNoPadInspectorSummary()
    {
        var vm = new FreeformHelperViewModel();

        Assert.False(vm.HasPadInspectorSummary);
        Assert.True(vm.HasNoPadInspectorSummary);
        Assert.Equal(string.Empty, vm.PadInspectorPrimaryText);
        Assert.Empty(vm.PadInspectorCompensationLines);
        Assert.False(vm.HasPadInspectorRuleTrace);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_CadSnapshot_ShowsCadTexts()
    {
        var vm = new FreeformHelperViewModel();

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);

        Assert.True(vm.HasPadInspectorSummary);
        Assert.False(vm.HasNoPadInspectorSummary);
        Assert.Equal("CAD 274 · Diff 112", vm.PadInspectorCompactHeaderText);
        Assert.Equal("CAD 274 · IC 3 · Diff idx 112", vm.PadInspectorPrimaryText);
        Assert.Equal("Diff source: Auto", vm.PadInspectorSecondaryText);
        Assert.Equal("Match: 1 regular pad(s), confidence 61 %", vm.PadInspectorMatchText);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_CadSnapshotWithoutNotch_ShowsUnavailableCompensationLines()
    {
        var vm = new FreeformHelperViewModel();

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);

        Assert.Equal(2, vm.PadInspectorCompensationLines.Count);
        Assert.Contains("Unavailable", vm.PadInspectorDisplay.CompensationText, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_SnapshotWithRuleTrace_ShowsTraceSections()
    {
        var vm = new FreeformHelperViewModel();

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);

        Assert.True(vm.HasPadInspectorRuleTrace);
        Assert.Single(vm.PadInspectorRuleTraceSections);
        Assert.Equal(
            vm.PadInspectorRuleTraceSections.Sum(static section => section.Lines.Count),
            vm.PadInspectorRuleTraceLines.Count);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_RegularSnapshot_ShowsRegularTexts()
    {
        var vm = new FreeformHelperViewModel();

        vm.PadInspectorUi.Snapshot = CreateRegularSnapshot(4616);

        Assert.True(vm.HasPadInspectorSummary);
        Assert.Equal("REG 4616 · Diff 72", vm.PadInspectorCompactHeaderText);
        Assert.Equal("REG 4616 · IC 1 · Diff idx 72", vm.PadInspectorPrimaryText);
        Assert.Equal("Diff source: Auto", vm.PadInspectorSecondaryText);
        Assert.Equal("Match: 0 CAD pad(s), confidence -", vm.PadInspectorMatchText);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_SnapshotWithoutRuleTrace_HasNoTrace()
    {
        var vm = new FreeformHelperViewModel();

        vm.PadInspectorUi.Snapshot = CreateRegularSnapshot(4616);

        Assert.False(vm.HasPadInspectorRuleTrace);
        Assert.Empty(vm.PadInspectorRuleTraceSections);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_Cleared_ResetsTextsAndCollapsesTrace()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        vm.IsPadInspectorRawTraceExpanded = true;

        vm.PadInspectorUi.Snapshot = null;

        Assert.False(vm.HasPadInspectorSummary);
        Assert.True(vm.HasNoPadInspectorSummary);
        Assert.Equal(string.Empty, vm.PadInspectorPrimaryText);
        Assert.Equal(string.Empty, vm.PadInspectorDisplay.CompensationText);
        Assert.Empty(vm.PadInspectorCompensationLines);
        Assert.False(vm.HasPadInspectorRuleTrace);
        Assert.False(vm.IsPadInspectorRawTraceExpanded);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_Replaced_CollapsesRawTrace()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        vm.IsPadInspectorRawTraceExpanded = true;

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(275);

        Assert.False(vm.IsPadInspectorRawTraceExpanded);
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_FirstSnapshot_RaisesEveryDisplayProperty()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _summaryNames);

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);

        Assert.Equal(_summaryNames.Order(StringComparer.Ordinal), changes.OfType<string>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_SecondSnapshotWithSameTexts_RaisesOnlyCollectionProperties()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        var changes = RecordNotifications(vm, _summaryNames);

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274) with { Kind = "cad-again" };

        string[] expected =
        [
            nameof(FreeformHelperViewModel.PadInspectorCompensationLines),
            nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines),
            nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections),
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), changes.OfType<string>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void CurrentPadInspectorSnapshot_SnapshotWithoutParts_KeepsEmptyTextsAndFlags()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _summaryNames);

        vm.PadInspectorUi.Snapshot = new PadInspectorSnapshot("none", null, null);

        string[] expected =
        [
            nameof(FreeformHelperViewModel.PadInspectorCompensationLines),
            nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines),
            nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections),
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), changes.OfType<string>().Order(StringComparer.Ordinal));
        Assert.False(vm.HasPadInspectorSummary);
    }

    [Fact]
    public void DebugDetails_NewViewModel_IsCollapsed()
    {
        var vm = new FreeformHelperViewModel();

        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);

        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);
    }

    [Fact]
    public void DebugDetails_SamePadRefreshed_StaysOpen()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        vm.PadInspectorUi.IsDebugDetailsExpanded = true;

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274) with { Kind = "cad-again" };

        Assert.True(vm.PadInspectorUi.IsDebugDetailsExpanded);
    }

    [Fact]
    public void DebugDetails_OtherPadSelected_Collapses()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        vm.PadInspectorUi.IsDebugDetailsExpanded = true;

        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(275);

        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);

        vm.PadInspectorUi.IsDebugDetailsExpanded = true;
        vm.PadInspectorUi.Snapshot = CreateRegularSnapshot(4616);

        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);
    }

    [Fact]
    public void DebugDetails_SnapshotCleared_Collapses()
    {
        var vm = new FreeformHelperViewModel();
        vm.PadInspectorUi.Snapshot = CreateCadSnapshot(274);
        vm.PadInspectorUi.IsDebugDetailsExpanded = true;

        vm.PadInspectorUi.Snapshot = null;

        Assert.False(vm.PadInspectorUi.IsDebugDetailsExpanded);
    }

    internal static PadInspectorSnapshot CreateCadSnapshot(int cadPadId)
    {
        return new PadInspectorSnapshot(
            "cad",
            new CadPadInspectorSnapshot(
                CadPadId: cadPadId,
                Name: $"CAD{cadPadId}",
                Layer: "AA",
                Area: 80,
                Bounds: new Rect2(0, 0, 10, 8),
                Centroid: new Point2(5, 4),
                Vertices: 4,
                DxfIndex: 112,
                CadOutputFwDiffOverride: null,
                IsDxfIndexAnchor: false,
                DxfIndexDisplayText: "112",
                CadOutputFwDiffAssignmentModeText: "Auto",
                IcIndex: 2,
                MatchText: "IC3/diff112",
                MatchDetailsText: "IC3/diff112 (best)",
                MatchedRegularPadIds: [4808],
                MatchedRegularDetails: [new PadInspectorMatchedRegularSnapshot(4808, 2, 112, 0.61, 0.54)],
                IsNotchRowEligible: true,
                NotchRowSummary: "1 row",
                Notch: null,
                RuleTrace: [new PadInspectorRuleTraceEntry("Sequence", "Adjusted", "IC-row sequence applied")],
                MatchConfidence: 0.61),
            Regular: null);
    }

    internal static PadInspectorSnapshot CreateRegularSnapshot(int regularPadId)
    {
        return new PadInspectorSnapshot(
            "regular",
            Cad: null,
            Regular: new RegularPadInspectorSnapshot(
                RegularPadId: regularPadId,
                Index: regularPadId,
                Row: 1,
                Col: 1,
                DisplayRow: 1,
                IcIndex: 0,
                DiffIndex: 72,
                Area: 80,
                Bounds: new Rect2(0, 0, 10, 8),
                Centroid: new Point2(5, 4),
                Freeform: FreeformType.None,
                MatchText: "-",
                MatchDetailsText: "-",
                MatchedCadPadIds: [],
                MatchedCadDetails: [],
                RuleTrace: [],
                MatchConfidence: 0,
                DiffSource: "Auto"));
    }
}
