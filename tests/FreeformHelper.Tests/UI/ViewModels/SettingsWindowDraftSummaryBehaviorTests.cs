// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowDraftSummaryBehaviorTests
{
    [Theory]
    [InlineData(NotchCompensationModel.CurrentGain, true, "Current (Gain)",
        "To Regular x To Full (current model).",
        "Allocation model: Stage3 effective-area gain; ToFull-expanded area splits the combined source signal.",
        "Stage3 gain allocation")]
    [InlineData(NotchCompensationModel.ConservativeNoGain, true, "Conservative (No Gain)",
        "Apply ToRegular source combine; ToFull only gates boundary support.",
        "Allocation model: SourceArea-dominant; ToFull is support/cap, not direct full-area weight.",
        "Source-area dominant")]
    [InlineData(NotchCompensationModel.Disabled, false, "Disabled",
        "Turn off To Full and To Regular for baseline comparison.",
        "Allocation model: disabled baseline.", "Disabled baseline")]
    public void CompensationPreset_UpdatesDraftSwitchesAndTheirExplanations(
        NotchCompensationModel model, bool enabled, string display, string description,
        string allocationSummary, string allocationShortText)
    {
        var owner = new FreeformHelperViewModel();
        var draft = owner.CreateSettingsWindowViewModel();
        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(
            option => option.Value == (enabled ? NotchCompensationModel.Disabled : NotchCompensationModel.CurrentGain));
        var changes = new List<string?>();
        draft.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(option => option.Value == model);

        Assert.Equal(enabled, draft.EnableToRegular);
        Assert.Equal(enabled, draft.EnableToFull);
        Assert.Equal(description, draft.NotchCompensationModelSummary);
        Assert.Equal(allocationSummary, draft.NotchAllocationModelSummary);
        Assert.Equal(allocationShortText, draft.NotchAllocationModelShortText);
        var state = enabled ? "ON" : "OFF";
        Assert.Equal($"{display} · ToRegular {state} · ToFull {state}", draft.NotchEffectiveModelSummary);
        Assert.Equal(enabled ? "ON - restore area signal" : "OFF - baseline", draft.NotchToRegularShortText);
        Assert.Equal(enabled ? "ON - support/cap only" : "OFF - no boundary support", draft.NotchToFullShortText);
        Assert.Equal(enabled
            ? "ToRegular: undo NF area flattening and restore area-proportional signal before redistribution."
            : "ToRegular: disabled for baseline comparison.", draft.NotchToRegularSemanticSummary);
        Assert.Equal(enabled
            ? "ToFull: boundary support/cap/allowance only; target amount remains area-proportional."
            : "ToFull: disabled for baseline comparison.", draft.NotchToFullSemanticSummary);
        Assert.Contains(nameof(draft.NotchCompensationModelSummary), changes);
        Assert.Contains(nameof(draft.NotchAllocationModelSummary), changes);
        Assert.Contains(nameof(draft.NotchEffectiveModelSummary), changes);
        Assert.Equal(NotchCompensationModel.CurrentGain, owner.SelectedNotchCompensationModelOption.Value);
        Assert.True(owner.EnableToRegular);
        Assert.True(owner.EnableToFull);
    }

    [Fact]
    public void SafetyAndRuleSummaries_FollowDraftTogglesAndFractionalCaps()
    {
        using var culture = TestCulture.UseEnUs();
        var draft = new FreeformHelperViewModel().CreateSettingsWindowViewModel();
        draft.EnableBoundaryVirtualAreaCap = false;
        draft.EnableTargetCoverageGuard = false;
        draft.EnableToFullRuleEngine = false;
        draft.EnableToFullRuleTrace = true;
        draft.BoundaryVirtualAreaCapPercent = 137.5m;
        draft.TargetCoverageCapPercent = 122.5m;

        Assert.Equal("Boundary cap: OFF; Stage3 uses raw ToFull reachable area.", draft.NotchBoundaryVirtualAreaCapSummary);
        Assert.Equal("Boundary cap OFF", draft.NotchBoundaryVirtualAreaCapShortText);
        Assert.Equal("Target guard OFF", draft.NotchTargetCoverageGuardShortText);
        Assert.Equal("To Full rule engine: OFF (legacy gate path)", draft.ToFullRuleEngineSummary);

        draft.EnableBoundaryVirtualAreaCap = true;
        draft.EnableTargetCoverageGuard = true;
        draft.EnableToFullRuleEngine = true;

        Assert.Equal("Boundary cap: virtual ToFull area <= inside overlap x 137.5%.", draft.NotchBoundaryVirtualAreaCapSummary);
        Assert.Equal("Boundary cap 137.5%", draft.NotchBoundaryVirtualAreaCapShortText);
        Assert.Equal("Target guard 122.5%", draft.NotchTargetCoverageGuardShortText);
        Assert.Equal("To Full rule engine: ON (trace ON)", draft.ToFullRuleEngineSummary);

        draft.EnableToFullRuleTrace = false;

        Assert.Equal("To Full rule engine: ON (trace OFF)", draft.ToFullRuleEngineSummary);
    }

    [Fact]
    public void ThresholdLink_ReenabledAfterIndependentEdits_UsesQ7AndRoundsPercentEditsAwayFromZero()
    {
        var owner = new FreeformHelperViewModel { LinkNotchThresholds = false, NotchThresholdQ7 = 64m, NotchThresholdPercent = 20m };
        var draft = owner.CreateSettingsWindowViewModel();
        draft.NotchThresholdQ7 = 65m;
        Assert.Equal(20m, draft.NotchThresholdPercent);
        draft.NotchThresholdPercent = 12.5m;
        Assert.Equal(65m, draft.NotchThresholdQ7);

        draft.LinkNotchThresholds = true;

        Assert.Equal(50.78m, draft.NotchThresholdPercent);
        Assert.Equal(65m, draft.NotchThresholdQ7);
        draft.NotchThresholdPercent = 3.515625m; // Exactly 4.5 Q7: round away from zero without feeding back into percent.
        Assert.Equal(5m, draft.NotchThresholdQ7);
        Assert.Equal(3.515625m, draft.NotchThresholdPercent);
        Assert.Equal(64m, owner.NotchThresholdQ7);
        Assert.Equal(20m, owner.NotchThresholdPercent);

        draft.SaveCommand.Execute(null);

        var reopened = owner.CreateSettingsWindowViewModel();
        Assert.True(reopened.LinkNotchThresholds);
        Assert.Equal(5m, reopened.NotchThresholdQ7);
        Assert.Equal(3.515625m, reopened.NotchThresholdPercent);
    }
}
