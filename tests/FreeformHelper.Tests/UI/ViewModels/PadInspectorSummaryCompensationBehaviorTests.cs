// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorSummaryCompensationBehaviorTests
{
    [Theory]
    [InlineData(FreeformType.XWay, PadInspectorFreeformDirection.XWay, true, false, true)]
    [InlineData(FreeformType.YWay, PadInspectorFreeformDirection.YWay, false, true, true)]
    [InlineData(FreeformType.XYWay, PadInspectorFreeformDirection.XYWay, true, true, false)]
    [InlineData(FreeformType.None, PadInspectorFreeformDirection.None, false, false, false)]
    public void Snapshot_AnchorSummary_ShowsDirectionAndResolvedCompensation(
        FreeformType freeform,
        PadInspectorFreeformDirection direction,
        bool showX,
        bool showY,
        bool toFullEnabled)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var cad = Assert.IsType<CadPadInspectorSnapshot>(snapshot.Cad);
        var rowSummary = $"Anchor REG4808, freeform={freeform}.";
        var tone = toFullEnabled ? PadInspectorLineTone.Key : PadInspectorLineTone.Warning;
        var diagnostics = toFullEnabled
            ? "IC3/diff112 reg4808: ov=80, src=80, blk=0, reach=120, blocker=0, " +
              "boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)"
            : "IC3/diff112 reg4808: ov=80, src=80, blk=0, reach=80, blocker=0, " +
              "boundary=N, candidate=N, tofull=N (GATE_NOT_BOUNDARY)";

        vm.PadInspectorUi.Snapshot = snapshot with
        {
            Cad = cad with
            {
                NotchRowSummary = rowSummary,
                Notch = new PadInspectorNotchSnapshot(
                    ToRegularRatio: 0.8, ToFullRatio: toFullEnabled ? 1.5 : 1,
                    CombinedRatio: toFullEnabled ? 1.2 : 0.8, IsToFullEnabled: toFullEnabled,
                    Stage3Area: toFullEnabled ? 120 : 80, StrictAreaThreshold: 0.1,
                    Targets: [], Diagnostics: diagnostics),
            },
        };

        string[] expectedLabels = ["To Full", "Reason", "Freeform", "To Regular", "Combined", "Notch rows"];
        Assert.Equal(expectedLabels, vm.PadInspectorCompensationLines.Select(line => line.Label));
        var toFull = vm.PadInspectorCompensationLines[0];
        Assert.Equal(toFullEnabled ? "150.0 % (Enabled)" : "100.0 % (Disabled)", toFull.Value);
        Assert.Equal(tone, toFull.Tone);
        Assert.True(toFull.IsEmphasized);
        var reason = vm.PadInspectorCompensationLines[1];
        Assert.Equal(toFullEnabled
            ? "Expansion applied on clear path. (EXPAND_CLEAR_PATH)"
            : "Regular pad is not boundary. (GATE_NOT_BOUNDARY)", reason.Value);
        Assert.Equal(tone, reason.Tone);
        Assert.True(reason.IsEmphasized);
        var freeformLine = vm.PadInspectorCompensationLines[2];
        Assert.Equal(freeform.ToString(), freeformLine.Value);
        Assert.Equal(direction, freeformLine.FreeformDirection);
        Assert.Equal(showX, freeformLine.ShowFreeformXSlash);
        Assert.Equal(showY, freeformLine.ShowFreeformYSlash);
        Assert.Equal(showX || showY, freeformLine.HasFreeformDirectionMarker);
        Assert.Equal(PadInspectorLineTone.Key, freeformLine.Tone);
        Assert.True(freeformLine.IsEmphasized);
        Assert.Equal("80.0 %", vm.PadInspectorCompensationLines[3].Value);
        Assert.Equal(toFullEnabled ? "120.00 %" : "80.00 %", vm.PadInspectorCompensationLines[4].Value);
        Assert.Equal(rowSummary, vm.PadInspectorCompensationLines[5].Value);
    }

    [Fact]
    public void Snapshot_ReplacedWithoutDiagnosticsOrFreeform_DropsOldRowsAndWarnsAboutOverflow()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var cad = Assert.IsType<CadPadInspectorSnapshot>(snapshot.Cad);
        var notch = new PadInspectorNotchSnapshot(
            ToRegularRatio: 0.8, ToFullRatio: 1.5, CombinedRatio: 1.2, IsToFullEnabled: true,
            Stage3Area: 120, StrictAreaThreshold: 0.1, Targets: [],
            Diagnostics: "IC3/diff112 reg4808: ov=80, src=80, blk=0, reach=120, blocker=0, " +
                         "boundary=Y, candidate=Y, tofull=Y (EXPAND_CLEAR_PATH)");
        vm.PadInspectorUi.Snapshot = snapshot with
        {
            Cad = cad with { Notch = notch, NotchRowSummary = "Anchor REG4808, freeform=XWay." },
        };
        Assert.Contains(vm.PadInspectorCompensationLines, line => line.Label == "Reason");
        Assert.Contains(vm.PadInspectorCompensationLines, line => line.HasFreeformDirectionMarker);

        vm.PadInspectorUi.Snapshot = snapshot with
        {
            Cad = cad with
            {
                Notch = notch with { ToRegularRatio = 2, CombinedRatio = 3, Diagnostics = null },
                NotchRowSummary = "Notch rows: 1 (v2.2)",
            },
        };

        string[] expectedLabels = ["To Full", "To Regular", "Combined", "Notch rows"];
        Assert.Equal(expectedLabels, vm.PadInspectorCompensationLines.Select(line => line.Label));
        Assert.DoesNotContain(vm.PadInspectorCompensationLines, line => line.HasFreeformDirectionMarker);
        Assert.Equal("200.0 %", vm.PadInspectorCompensationLines[1].Value);
        var combined = vm.PadInspectorCompensationLines[2];
        Assert.Equal("300.00 % (overflow risk >255%)", combined.Value);
        Assert.Equal(PadInspectorLineTone.Warning, combined.Tone);
        Assert.True(combined.IsEmphasized);
        Assert.Equal("Notch rows: 1 (v2.2)", vm.PadInspectorCompensationLines[3].Value);
    }
}
