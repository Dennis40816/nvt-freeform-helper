// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchPreviewSummaryBehaviorTests
{
    [Theory]
    [InlineData(
        NotchCompensationModel.CurrentGain, "Current (Gain)", true,
        "Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal.",
        "Stage3 gain allocation")]
    [InlineData(
        NotchCompensationModel.ConservativeNoGain, "Conservative (No Gain)", true,
        "Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight.",
        "Source-area dominant")]
    [InlineData(
        NotchCompensationModel.Disabled, "Disabled", false,
        "Allocation model: disabled baseline.", "Disabled baseline")]
    public void CompensationModelSelection_DescribesAllocationAndEffectiveSwitches(
        NotchCompensationModel model, string display, bool enabled, string allocationSummary, string allocationShortText)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false };

        vm.SelectedNotchCompensationModelOption = vm.NotchCompensationModelOptions.Single(option => option.Value == model);

        Assert.Equal(enabled, vm.EnableToRegular);
        Assert.Equal(enabled, vm.EnableToFull);
        Assert.Equal(allocationSummary, vm.NotchAllocationModelSummary);
        Assert.Equal(allocationShortText, vm.NotchAllocationModelShortText);
        var state = enabled ? "ON" : "OFF";
        Assert.Equal($"{display} · ToRegular {state} · ToFull {state}", vm.NotchEffectiveModelSummary);
        Assert.Equal($"ToRegular {state}", vm.NotchToRegularStateText);
        Assert.Equal($"ToFull {state}", vm.NotchToFullStateText);
        Assert.Equal(enabled ? "ON - restore area signal" : "OFF - baseline", vm.NotchToRegularShortText);
        Assert.Equal(enabled ? "ON - support/cap only" : "OFF - no boundary support", vm.NotchToFullShortText);
        Assert.Equal(
            enabled
                ? "ToRegular: undo NF area flattening and restore area-proportional signal before redistribution."
                : "ToRegular: disabled for baseline comparison.",
            vm.NotchToRegularSemanticSummary);
        Assert.Equal(
            enabled
                ? "ToFull: boundary support/cap/allowance only; target amount remains area-proportional."
                : "ToFull: disabled for baseline comparison.",
            vm.NotchToFullSemanticSummary);
    }

    [Fact]
    public void ToFullSafetyDetails_TogglingCapsAndExpansion_ReportsCurrentSettings()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            BoundaryVirtualAreaCapPercent = 137.5m,
            TargetCoverageCapPercent = 122.5m,
            EnableBoundaryVirtualAreaCap = false,
            EnableTargetCoverageGuard = false,
            IsStep3ToFullDetailsExpanded = false,
        };

        Assert.Equal("Boundary cap: OFF; Stage3 uses raw ToFull reachable area.", vm.NotchBoundaryVirtualAreaCapSummary);
        Assert.Equal("Boundary cap OFF", vm.NotchBoundaryVirtualAreaCapShortText);
        Assert.Equal("Target guard OFF", vm.NotchTargetCoverageGuardShortText);
        Assert.Equal("Show ToFull details", vm.Step3ToFullDetailsToggleText);

        vm.EnableBoundaryVirtualAreaCap = true;
        vm.EnableTargetCoverageGuard = true;
        vm.IsStep3ToFullDetailsExpanded = true;

        Assert.Equal("Boundary cap: virtual ToFull area <= inside overlap x 137.5%.", vm.NotchBoundaryVirtualAreaCapSummary);
        Assert.Equal("Boundary cap 137.5%", vm.NotchBoundaryVirtualAreaCapShortText);
        Assert.Equal("Target guard 122.5%", vm.NotchTargetCoverageGuardShortText);
        Assert.Equal("Hide ToFull details", vm.Step3ToFullDetailsToggleText);
    }

    [Theory]
    [InlineData(false, false, "To Full rule engine: OFF (legacy gate path)")]
    [InlineData(false, true, "To Full rule engine: OFF (legacy gate path)")]
    [InlineData(true, true, "To Full rule engine: ON (trace ON)")]
    public void RuleEngineSummary_ReportsLegacyModeOrEnabledTrace(bool enabled, bool trace, string expected)
    {
        var vm = new FreeformHelperViewModel
        {
            NotchPreviewAutoPlayEnabled = false,
            EnableToFullRuleEngine = enabled,
            EnableToFullRuleTrace = trace,
        };

        Assert.Equal(expected, vm.NotchToFullRuleEngineSummary);
    }
}
