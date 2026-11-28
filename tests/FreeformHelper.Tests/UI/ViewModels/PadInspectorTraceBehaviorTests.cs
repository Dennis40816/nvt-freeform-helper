// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorTraceBehaviorTests
{
    [AvaloniaFact]
    public async Task CadTrace_PrecomputedOwnerDiagnostics_ExplainsTheTargetAreasOwnersAndGateAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1,
            ("signal", 0, 0, 5, 10), ("neighbor", 5, 0, 10, 10));
        var vm = workspace.ViewModel;
        vm.EnableToFull = false;
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal");
        var neighbor = Assert.Single(vm.CadPads, static pad => pad.Layer == "neighbor");
        var regular = Assert.Single(vm.RegularPads);
        var diagnostics = $"IC1/diff{regular.DiffIndex} reg{regular.RegularPadId} : " +
            "ov=50, src=50, blk=50, reach=50, blocker=1, " +
            $"owners={cad.Id};{neighbor.Id}, ownerShare={cad.Id}:50%|{neighbor.Id}:50%, " +
            "boundary=Y, candidate=N, tofull=N (GATE_TOFULL_DISABLED)";

        var snapshot = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            cad.Id, includeNotchRowEligibilityDetails: false, precomputedDiagnostics: diagnostics)?.Cad);

        var trace = Assert.Single(snapshot.RuleTrace, static entry => entry.Rule == "To Full");
        Assert.Equal("Disabled", trace.Outcome);
        Assert.Equal(string.Join(Environment.NewLine,
            "To Full ratio: 100.0 %",
            "Switch: OFF",
            $"Target: IC 1 / Diff idx {regular.DiffIndex} / REG {regular.RegularPadId}",
            "Area: ov=50, src=50, blk=50, reach=50",
            $"Owners: 2 ({cad.Id}, {neighbor.Id})",
            $"Owner share: {cad.Id}:50%|{neighbor.Id}:50%",
            "Boundary: Yes, Candidate: No, Blockers: 1",
            "Applied: No",
            "Rule: Global To Full switch is OFF. (GATE_TOFULL_DISABLED)"), trace.Detail);
    }

    [AvaloniaFact]
    public async Task CadTrace_LegacyDiagnosticsWithoutOptionalFields_UsesCandidateAndReasonDefaultsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        vm.EnableToFull = true;
        var cad = Assert.Single(vm.CadPads);
        var regular = Assert.Single(vm.RegularPads);
        var diagnostics = $"IC1/diff{regular.DiffIndex} reg{regular.RegularPadId} : " +
            "ov=50, src=50, blk=0, reach=100, blocker=0, boundary=Y, tofull=Y";

        var snapshot = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            cad.Id, includeNotchRowEligibilityDetails: false, precomputedDiagnostics: diagnostics)?.Cad);

        var trace = Assert.Single(snapshot.RuleTrace, static entry => entry.Rule == "To Full");
        Assert.Equal("Enabled", trace.Outcome);
        Assert.Equal(string.Join(Environment.NewLine,
            "To Full ratio: 200.0 %",
            "Switch: ON",
            $"Target: IC 1 / Diff idx {regular.DiffIndex} / REG {regular.RegularPadId}",
            "Area: ov=50, src=50, blk=0, reach=100",
            "Owners: 0 (-)",
            "Owner share: -",
            "Boundary: Yes, Candidate: Yes, Blockers: 0",
            "Applied: Yes",
            "Rule: Expansion applied."), trace.Detail);
    }

    [AvaloniaFact]
    public async Task CadTrace_WarmLightweightSnapshot_ReportsUnavailableDiagnosticsAndPendingEligibilityAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = await PadInspectorFormattingWorkspace.OpenAsync(1, ("signal", 0, 0, 5, 10));
        var vm = workspace.ViewModel;
        vm.EnableToFull = false;
        var cad = Assert.Single(vm.CadPads);
        Assert.NotNull(vm.GetCadV22ResolvedResult(cad.Id));

        var snapshot = Assert.IsType<CadPadInspectorSnapshot>(vm.BuildCadPadInspectorSnapshot(
            cad.Id, includeExpensiveNotchDetails: false, includeNotchRowEligibilityDetails: false)?.Cad);

        Assert.Null(Assert.IsType<PadInspectorNotchSnapshot>(snapshot.Notch).Diagnostics);
        var toFull = Assert.Single(snapshot.RuleTrace, static entry => entry.Rule == "To Full");
        Assert.Equal("Disabled", toFull.Outcome);
        Assert.Equal(string.Join(Environment.NewLine,
            "To Full ratio: 100.0 %", "Switch: OFF", "Diagnostics: unavailable."), toFull.Detail);
        var export = Assert.Single(snapshot.RuleTrace, static entry => entry.Rule == "Notch export");
        Assert.Equal("Computing...", export.Outcome);
        Assert.Equal("Deferred notch-row eligibility is running.", export.Detail);
    }
}
