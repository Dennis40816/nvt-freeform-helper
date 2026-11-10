// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsNotchExportOwnerCharacterizationTests
{
    [Fact]
    public void UnlinkedThresholdEditsNotifyIndependently()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateThresholdDraft(scope.Owner, linked: false);
        var trace = new SettingsDraftTrace<(decimal, decimal)>(draft, () => (draft.NotchThresholdQ7, draft.NotchThresholdPercent));

        draft.NotchThresholdQ7 = 65m;
        draft.NotchThresholdPercent = 12.5m;

        trace.Expect(
            ("changing:NotchThresholdQ7", (64m, 50m)),
            ("changed:NotchThresholdQ7", (65m, 50m)),
            ("changing:NotchThresholdPercent", (65m, 50m)),
            ("changed:NotchThresholdPercent", (65m, 12.5m)));
    }

    [Fact]
    public void LinkedQ7EditPublishesRoundedPercentBeforeItsOwnChangedEvent()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateThresholdDraft(scope.Owner, linked: true);
        var trace = new SettingsDraftTrace<(decimal, decimal)>(draft, () => (draft.NotchThresholdQ7, draft.NotchThresholdPercent));

        draft.NotchThresholdQ7 = 65m;

        trace.Expect(
            ("changing:NotchThresholdQ7", (64m, 50m)),
            ("changing:NotchThresholdPercent", (65m, 50m)),
            ("changed:NotchThresholdPercent", (65m, 50.78m)),
            ("changed:NotchThresholdQ7", (65m, 50.78m)));
    }

    [Fact]
    public void LinkedPercentEditRoundsQ7AwayFromZeroWithoutFeedingBackAndReleasesTheGuard()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateThresholdDraft(scope.Owner, linked: true);
        var trace = new SettingsDraftTrace<(decimal, decimal)>(draft, () => (draft.NotchThresholdQ7, draft.NotchThresholdPercent));

        draft.NotchThresholdPercent = 3.515625m;

        trace.Expect(
            ("changing:NotchThresholdPercent", (64m, 50m)),
            ("changing:NotchThresholdQ7", (64m, 3.515625m)),
            ("changed:NotchThresholdQ7", (5m, 3.515625m)),
            ("changed:NotchThresholdPercent", (5m, 3.515625m)));
        draft.NotchThresholdQ7 = 32m;
        Assert.Equal(25m, draft.NotchThresholdPercent);
    }

    [Fact]
    public void EnablingTheLinkUsesQ7BeforeNotifyingTheLinkAndDisablingItKeepsBothValues()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateThresholdDraft(scope.Owner, linked: false);
        draft.NotchThresholdPercent = 20m;
        var trace = new SettingsDraftTrace<(bool, decimal, decimal)>(draft,
            () => (draft.LinkNotchThresholds, draft.NotchThresholdQ7, draft.NotchThresholdPercent));

        draft.LinkNotchThresholds = true;
        draft.LinkNotchThresholds = false;

        trace.Expect(
            ("changing:LinkNotchThresholds", (false, 64m, 20m)),
            ("changing:NotchThresholdPercent", (true, 64m, 20m)),
            ("changed:NotchThresholdPercent", (true, 64m, 50m)),
            ("changed:LinkNotchThresholds", (true, 64m, 50m)),
            ("changing:LinkNotchThresholds", (true, 64m, 50m)),
            ("changed:LinkNotchThresholds", (false, 64m, 50m)));
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdQ7), 64, 50, 129, 128, 100)]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdPercent), 50, 64, 101, 100, 128)]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdQ7), 64, 50, -1, 0, 0)]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdPercent), 50, 64, -1, 0, 0)]
    public void ThresholdClampNestsNormalizationAndLinkedUpdates(
        string name, int initial, int otherInitial, int input, int normalized, int otherNormalized)
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateThresholdDraft(scope.Owner, linked: true);
        var otherName = name == nameof(draft.NotchThresholdQ7)
            ? nameof(draft.NotchThresholdPercent) : nameof(draft.NotchThresholdQ7);
        var property = typeof(SettingsWindowViewModel).GetProperty(name)!;
        var other = typeof(SettingsWindowViewModel).GetProperty(otherName)!;
        var trace = new SettingsDraftTrace<(decimal, decimal)>(draft,
            () => ((decimal)property.GetValue(draft)!, (decimal)other.GetValue(draft)!));

        property.SetValue(draft, (decimal)input);

        trace.Expect(
            ($"changing:{name}", (initial, otherInitial)),
            ($"changing:{name}", (input, otherInitial)),
            ($"changing:{otherName}", (normalized, otherInitial)),
            ($"changed:{otherName}", (normalized, otherNormalized)),
            ($"changed:{name}", (normalized, otherNormalized)),
            ($"changed:{name}", (normalized, otherNormalized)));
    }

    [Theory]
    [InlineData(NotchCompensationModel.Disabled, NotchCompensationModel.CurrentGain, false, true)]
    [InlineData(NotchCompensationModel.CurrentGain, NotchCompensationModel.Disabled, true, false)]
    [InlineData(NotchCompensationModel.ConservativeNoGain, NotchCompensationModel.Disabled, true, false)]
    public void CompensationPresetWritesRegularThenFullAndRetainsAllSummaryNotifications(
        NotchCompensationModel target, NotchCompensationModel original, bool enabled, bool wasEnabled)
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(option => option.Value == original);
        var trace = new SettingsDraftTrace<(NotchCompensationModel, bool, bool)>(draft,
            () => (draft.SelectedNotchCompensationModelOption.Value, draft.EnableToRegular, draft.EnableToFull));

        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(option => option.Value == target);

        trace.Expect(
        [
            ("changing:SelectedNotchCompensationModelOption", (original, wasEnabled, wasEnabled)),
            ("changing:EnableToRegular", (target, wasEnabled, wasEnabled)),
            .. SettingsDraftSummaryExpectations.ModelEvents((target, enabled, wasEnabled)),
            ("changed:EnableToRegular", (target, enabled, wasEnabled)),
            ("changing:EnableToFull", (target, enabled, wasEnabled)),
            .. SettingsDraftSummaryExpectations.ModelEvents((target, enabled, enabled)),
            ("changed:EnableToFull", (target, enabled, enabled)),
            ("changed:NotchCompensationModelSummary", (target, enabled, enabled)),
            .. SettingsDraftSummaryExpectations.ModelEvents((target, enabled, enabled)),
            ("changed:SelectedNotchCompensationModelOption", (target, enabled, enabled)),
        ]);
        draft.EnableToRegular = !enabled;
        Assert.Equal(enabled, draft.EnableToFull);
        draft.EnableToFull = !enabled;
        Assert.Equal(target, draft.SelectedNotchCompensationModelOption.Value);
        Assert.Equal((!enabled, !enabled), (draft.EnableToRegular, draft.EnableToFull));
    }

    [Fact]
    public void ReentrantCompensationSelectionKeepsTheOuterPresetAndReleasesItsGuard()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var conservative = draft.NotchCompensationModelOptions.Single(option => option.Value == NotchCompensationModel.ConservativeNoGain);
        draft.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(draft.EnableToRegular) && !draft.EnableToRegular)
            {
                draft.SelectedNotchCompensationModelOption = conservative;
            }
        };

        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(
            option => option.Value == NotchCompensationModel.Disabled);

        Assert.Equal(conservative, draft.SelectedNotchCompensationModelOption);
        Assert.False(draft.EnableToRegular);
        Assert.False(draft.EnableToFull);
        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(
            option => option.Value == NotchCompensationModel.CurrentGain);
        Assert.True(draft.EnableToRegular);
        Assert.True(draft.EnableToFull);
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.BoundaryVirtualAreaCapPercent), 1001, 1000)]
    [InlineData(nameof(SettingsWindowViewModel.TargetCoverageCapPercent), 256, 255)]
    public void CapClampPublishesSummariesInsideTheNestedSetter(string name, int input, int normalized)
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var property = typeof(SettingsWindowViewModel).GetProperty(name)!;
        var initial = (decimal)property.GetValue(draft)!;
        var trace = new SettingsDraftTrace<decimal>(draft, () => (decimal)property.GetValue(draft)!);

        property.SetValue(draft, (decimal)input);

        trace.Expect(
        [
            ($"changing:{name}", initial),
            ($"changing:{name}", input),
            .. SettingsDraftSummaryExpectations.ModelEvents((decimal)normalized),
            ($"changed:{name}", normalized),
            ($"changed:{name}", normalized),
        ]);
    }

    [Fact]
    public void RuleSummariesPrecedeTheirOriginatingChangedEvents()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        draft.EnableToFullRuleEngine = true;
        draft.EnableToFullRuleTrace = false;
        var trace = new SettingsDraftTrace<string>(draft, () => draft.ToFullRuleEngineSummary);

        draft.EnableToFullRuleTrace = true;
        draft.EnableToFullRuleEngine = false;

        trace.Expect(
            ("changing:EnableToFullRuleTrace", "To Full rule engine: ON (trace OFF)"),
            ("changed:ToFullRuleEngineSummary", "To Full rule engine: ON (trace ON)"),
            ("changed:EnableToFullRuleTrace", "To Full rule engine: ON (trace ON)"),
            ("changing:EnableToFullRuleEngine", "To Full rule engine: ON (trace ON)"),
            ("changed:ToFullRuleEngineSummary", "To Full rule engine: OFF (legacy gate path)"),
            ("changed:EnableToFullRuleEngine", "To Full rule engine: OFF (legacy gate path)"));
    }

    private static SettingsWindowViewModel CreateThresholdDraft(FreeformHelperViewModel owner, bool linked)
    {
        var draft = owner.CreateSettingsWindowViewModel();
        draft.LinkNotchThresholds = false;
        draft.NotchThresholdQ7 = 64m;
        draft.NotchThresholdPercent = 50m;
        draft.LinkNotchThresholds = linked;
        draft.MarkOpened();
        return draft;
    }
}
