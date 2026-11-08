// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CadPadInfoCoreBehaviorTests
{
    [Theory]
    [InlineData(true, false, "diagnostics + geometry")]
    [InlineData(false, true, "trace + geometry")]
    public void LowFrequencyDetails_ExpandsOnlyAvailableSections(bool hasDiagnostics, bool hasTrace, string expectedStatus)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel(hasDiagnostics, hasTrace);

        Assert.Equal(expectedStatus, vm.DebugStatusText);
        Assert.Equal(hasDiagnostics, vm.HasToFullDiagnosticsText);
        Assert.Equal(hasTrace, vm.HasRuleTrace);
        Assert.False(vm.ShowLowFrequencyDetailSection);
        Assert.Equal("Show details", vm.GeometryDetailsToggleText);

        vm.ShowNotchDiagnostics = hasDiagnostics;
        vm.ShowRuleTrace = hasTrace;

        Assert.Equal(hasDiagnostics || hasTrace, vm.ShowLowFrequencyDetailSection);
        Assert.False(vm.ShowGeometryDetails);

        vm.ShowLowFrequencyDetailSection = true;

        Assert.True(vm.ShowLowFrequencyDetailSection);
        Assert.True(vm.ShowGeometryDetails);
        Assert.Equal(hasDiagnostics, vm.ShowNotchDiagnostics);
        Assert.Equal(hasTrace, vm.ShowRuleTrace);
        Assert.Equal("Hide details", vm.GeometryDetailsToggleText);

        vm.ShowLowFrequencyDetailSection = false;

        Assert.False(vm.ShowLowFrequencyDetailSection);
        Assert.False(vm.ShowGeometryDetails);
        Assert.False(vm.ShowNotchDiagnostics);
        Assert.False(vm.ShowRuleTrace);
        Assert.Equal(expectedStatus, vm.DebugStatusText);
    }

    [Fact]
    public void CompactLabels_KeepResolvedAreaRowCountAndOwners()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel(hasDiagnostics: true, hasTrace: true);

        Assert.Equal("12.5 mm\u00b2", vm.Stage3AreaCompactText);
        Assert.Equal("0", vm.TargetAllocationCompactText);
        Assert.Equal("1 (v2.2)", vm.NotchRowCompactText);
        Assert.Equal("Owners 2: CAD 31, CAD 32", vm.OwnerSummaryCompactText);
        Assert.True(vm.HasOwnerSummary);
        Assert.Equal("diagnostics + trace + geometry", vm.DebugStatusText);

        var withoutOwners = CreateViewModel(hasDiagnostics: false, hasTrace: false);

        Assert.Equal("-", withoutOwners.OwnerSummaryCompactText);
        Assert.False(withoutOwners.HasOwnerSummary);
        Assert.Equal("geometry", withoutOwners.DebugStatusText);

        withoutOwners.ShowLowFrequencyDetailSection = true;

        Assert.True(withoutOwners.ShowGeometryDetails);
        Assert.False(withoutOwners.ShowNotchDiagnostics);
        Assert.False(withoutOwners.ShowRuleTrace);
    }

    [Fact]
    public void AssignmentMode_SavedAnchorIsInactiveAndManualOverrideTakesPrecedence()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateViewModel(hasDiagnostics: false, hasTrace: false,
            autoMode: CadOutputFwDiffAutoMode.StrictUnique);
        Assert.Equal("Auto (geometry seed, unique per IC)", vm.CadOutputFwDiffAssignmentModeText);

        vm.IsDxfIndexAnchor = true;

        Assert.True(vm.CanClearDxfIndexAnchor);
        Assert.Equal("Anchor saved (inactive in current mode)", vm.CadOutputFwDiffAssignmentModeText);

        vm.HasCadOutputFwDiffOverride = true;

        Assert.True(vm.CanClearCadOutputFwDiffOverride);
        Assert.Equal("Manual override", vm.CadOutputFwDiffAssignmentModeText);

        vm.HasCadOutputFwDiffOverride = false;

        Assert.False(vm.CanClearCadOutputFwDiffOverride);
        Assert.Equal("Anchor saved (inactive in current mode)", vm.CadOutputFwDiffAssignmentModeText);
    }

    private static CadPadInfoViewModel CreateViewModel(
        bool hasDiagnostics, bool hasTrace, CadOutputFwDiffAutoMode autoMode = CadOutputFwDiffAutoMode.BestMatchDirect)
    {
        const string diagnostics =
            "IC1/diff7 reg0: ov=1, src=1, blk=0, reach=1, blocker=0, owners=31|32, " +
            "ownerShare=50/50, boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)";
        var pad = TestGeometryFactory.CreateCadPad(7, "PAD", 0, 0, 1, 1);
        var cad = new CadPadInspectorSnapshot(
            CadPadId: pad.Id, Name: pad.Name, Layer: pad.Layer, Area: pad.Area,
            Bounds: pad.Bounds, Centroid: pad.Centroid, Vertices: pad.Polygon.Vertices.Length,
            DxfIndex: 7, CadOutputFwDiffOverride: null, IsDxfIndexAnchor: false,
            DxfIndexDisplayText: "7", CadOutputFwDiffAssignmentModeText: "Auto", IcIndex: 0,
            MatchText: "IC1/diff7", MatchDetailsText: "IC1/diff7 (best)",
            MatchedRegularPadIds: [0],
            MatchedRegularDetails: [new PadInspectorMatchedRegularSnapshot(0, 0, 7, 1, 1)],
            IsNotchRowEligible: true, NotchRowSummary: "Notch rows: 1 (v2.2)",
            Notch: new PadInspectorNotchSnapshot(
                ToRegularRatio: 1, ToFullRatio: 1, CombinedRatio: 1, IsToFullEnabled: false,
                Stage3Area: 12.5, StrictAreaThreshold: 0, Targets: [], Diagnostics: hasDiagnostics ? diagnostics : null),
            RuleTrace: hasTrace ? [new PadInspectorRuleTraceEntry("Freeform", "Applied", "Detected X overlap")] : [],
            MatchConfidence: 1);
        return new CadPadInfoViewModel(
            pads: [pad], getDxfIndex: static _ => 7, getCadOutputFwDiffOverride: null,
            isDxfIndexAnchor: null, getCustomValue: static _ => 0, applyCustomValue: null,
            setCadOutputFwDiffOverride: null, clearCadOutputFwDiffOverride: null,
            setDxfIndexAnchor: null, clearDxfIndexAnchor: null,
            cadOutputFwDiffAutoMode: autoMode,
            inspectorSnapshot: new PadInspectorSnapshot("cad", cad, Regular: null));
    }
}
