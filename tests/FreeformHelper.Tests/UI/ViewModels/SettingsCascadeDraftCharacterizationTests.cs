// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsCascadeDraftCharacterizationTests
{
    [Fact]
    public void PerIcEditIsRecordedBeforeTotalsAndSummaryNotifications()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateTwoIcDraft(scope.Owner);
        var trace = new SettingsDraftTrace<(decimal, decimal, bool)>(draft,
            () => (draft.XChannels, draft.YChannels, draft.IsEdited(nameof(draft.CascadeIcSettings))));

        draft.CascadeIcSettings[0].XChannels = 4m;
        draft.CascadeIcSettings[0].YChannels = 7m;

        trace.Expect(
            ("changing:XChannels", (5m, 5m, true)),
            ("changed:CascadeLayoutSummary", (7m, 5m, true)),
            ("changed:XChannels", (7m, 5m, true)),
            ("changed:CascadeLayoutSummary", (7m, 5m, true)),
            ("changing:YChannels", (7m, 5m, true)),
            ("changed:CascadeLayoutSummary", (7m, 7m, true)),
            ("changed:YChannels", (7m, 7m, true)),
            ("changed:CascadeLayoutSummary", (7m, 7m, true)));
        Assert.Equal<int>([4, 3], draft.PerIcXChannels);
        Assert.Equal<int>([7, 5], draft.PerIcYChannels);
    }

    [Fact]
    public void CountChangePublishesReplacementThenXThenYThenFinalSummary()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateTwoIcDraft(scope.Owner);
        var rows = draft.CascadeIcSettings;
        var trace = new SettingsDraftTrace<(int, decimal, decimal)>(draft,
            () => (draft.CascadeIcSettings.Count, draft.XChannels, draft.YChannels));

        draft.CascadeNum = 1m;

        trace.Expect(
            ("changing:CascadeNum", (2, 5m, 5m)),
            ("changing:CascadeIcSettings", (2, 5m, 5m)),
            ("changed:CascadeIcSettings", (1, 5m, 5m)),
            ("changing:XChannels", (1, 5m, 5m)),
            ("changed:CascadeLayoutSummary", (1, 2m, 5m)),
            ("changed:XChannels", (1, 2m, 5m)),
            ("changing:YChannels", (1, 2m, 5m)),
            ("changed:CascadeLayoutSummary", (1, 2m, 3m)),
            ("changed:YChannels", (1, 2m, 3m)),
            ("changed:CascadeLayoutSummary", (1, 2m, 3m)),
            ("changed:CascadeNum", (1, 2m, 3m)));
        Assert.NotSame(rows, draft.CascadeIcSettings);
        Assert.NotSame(rows[0], draft.CascadeIcSettings[0]);
        Assert.Equal("IC 1 · X 2 · Y 3", draft.CascadeLayoutSummary);
    }

    [Fact]
    public void ReplacedRowsNoLongerEditOrNotifyTheDraft()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = CreateTwoIcDraft(scope.Owner);
        var oldRows = draft.CascadeIcSettings;
        draft.CascadeNum = 1m;
        draft.MarkOpened();
        var trace = new SettingsDraftTrace<(decimal, decimal)>(draft, () => (draft.XChannels, draft.YChannels));

        oldRows[0].XChannels = 19m;
        oldRows[1].YChannels = 23m;

        trace.Expect();
        Assert.Equal((2m, 3m), (draft.XChannels, draft.YChannels));
        Assert.False(draft.IsEdited(nameof(draft.CascadeIcSettings)));
        draft.CascadeIcSettings[0].YChannels = 7m;
        Assert.Equal(7m, draft.YChannels);
        Assert.True(draft.IsEdited(nameof(draft.CascadeIcSettings)));
    }

    private static SettingsWindowViewModel CreateTwoIcDraft(FreeformHelperViewModel owner)
    {
        var draft = owner.CreateSettingsWindowViewModel();
        draft.CascadeNum = 2m;
        draft.CascadeIcSettings[0].XChannels = 2m;
        draft.CascadeIcSettings[0].YChannels = 3m;
        draft.CascadeIcSettings[1].XChannels = 3m;
        draft.CascadeIcSettings[1].YChannels = 5m;
        draft.MarkOpened();
        return draft;
    }
}
