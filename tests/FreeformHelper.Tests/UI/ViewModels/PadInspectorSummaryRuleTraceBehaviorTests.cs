// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class PadInspectorSummaryRuleTraceBehaviorTests
{
    [Fact]
    public void Snapshot_UnorderedTrace_GroupsRulesAndKeepsWarningsAndRawDetails()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel();
        var snapshot = PadInspectorDisplayBehaviorTests.CreateCadSnapshot(274);
        var cad = Assert.IsType<CadPadInspectorSnapshot>(snapshot.Cad);
        PadInspectorRuleTraceEntry[] entries =
        [
            new("Review", "Available", string.Empty),
            new("Raw diagnostics", "Captured", "Unparsed overlap diagnostics"),
            new("Diff idx", "Auto", "Diff idx value: 112"),
            new("Match", "No matched regular pad", "Best overlap target: none."),
            new("Notch export", "No row", "No Step2 freeform anchor."),
            new("To Full", "Disabled", string.Join(Environment.NewLine,
                "Rule: Regular pad is not boundary. (GATE_NOT_BOUNDARY)",
                "Rule: Global To Full switch is OFF.",
                "Raw diagnostics retained without a label")),
            new("To Regular", "80.0 %", string.Empty),
            new("Switch To Full", "OFF", string.Empty),
        ];

        vm.PadInspectorUi.Snapshot = snapshot with { Cad = cad with { RuleTrace = entries } };

        string[] expectedTitles = ["Notch compute", "Notch eligibility", "Match", "Identity", "Debug raw", "Other"];
        Assert.Equal(expectedTitles, vm.PadInspectorRuleTraceSections.Select(section => section.Title));
        var compute = vm.PadInspectorRuleTraceSections[0];
        string[] expectedCompute =
        [
            "To Full: Disabled",
            "Rule: Regular pad is not boundary. (GATE_NOT_BOUNDARY)",
            "Rule: Global To Full switch is OFF.",
            "Raw diagnostics retained without a label",
            "To Regular: 80.0 %",
            "Switch To Full: OFF",
        ];
        Assert.Equal(expectedCompute, compute.Lines.Select(line => line.DisplayText));
        Assert.Equal(PadInspectorLineTone.Warning, compute.Lines[0].Tone);
        Assert.True(compute.Lines[0].IsEmphasized);
        Assert.Equal(PadInspectorLineTone.Warning, compute.Lines[1].Tone);
        Assert.Equal(PadInspectorLineTone.Normal, compute.Lines[2].Tone);
        Assert.False(compute.Lines[3].HasLabel);
        Assert.Equal(PadInspectorLineTone.Warning, vm.PadInspectorRuleTraceSections[1].Lines[0].Tone);
        Assert.Equal(PadInspectorLineTone.Warning, vm.PadInspectorRuleTraceSections[2].Lines[0].Tone);
        var raw = vm.PadInspectorRuleTraceSections[4];
        Assert.True(raw.IsRawSection);
        Assert.Equal("Raw diagnostics: Captured", raw.Lines[0].DisplayText);
        Assert.Equal("Unparsed overlap diagnostics", raw.Lines[1].DisplayText);
        Assert.False(raw.Lines[1].HasLabel);
        Assert.Equal(PadInspectorLineTone.Normal, raw.Lines[0].Tone);
        Assert.True(vm.HasPadInspectorRuleTrace);
        Assert.Equal(vm.PadInspectorRuleTraceSections.SelectMany(section => section.Lines)
            .Select(line => line.DisplayText), vm.PadInspectorRuleTraceLines);
    }
}
