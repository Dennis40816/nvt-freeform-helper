// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsGeneralGridOwnerCharacterizationTests
{
    [Fact]
    public void IdentityCanBeInstalledAfterConstructionAndStaysWithThePixelDraft()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = new SettingsWindowViewModel(owner);
        var pixels = draft.ActiveAreaPixels;
        Assert.Null(pixels.ProjectIdentity);
        var opened = owner.CreateSettingsWindowViewModel();
        var identity = opened.ActiveAreaPixels.ProjectIdentity;
        Assert.NotNull(identity);

        pixels.Load(owner.ActiveAreaWidth, owner.ActiveAreaHeight, 100m, 200m, identity);
        draft.MarkOpened();
        draft.MatchThreshold = 0.37m;
        draft.ActiveAreaWidth = 20m;
        draft.SaveCommand.Execute(null);

        Assert.Same(pixels, draft.ActiveAreaPixels);
        Assert.Same(identity, pixels.ProjectIdentity);
        Assert.Equal(0.37m, owner.MatchThreshold);
        Assert.Equal(20m, owner.ActiveAreaWidth);
        Assert.Same(identity, owner.CreateSettingsWindowViewModel().ActiveAreaPixels.ProjectIdentity);
    }

    [Fact]
    public void DirectDraftWithNoProjectIdentityStillAppliesEditedSettings()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = new SettingsWindowViewModel(scope.Owner);
        draft.MarkOpened();
        draft.MatchThreshold = 0.37m;

        draft.SaveCommand.Execute(null);

        Assert.Null(draft.ActiveAreaPixels.ProjectIdentity);
        Assert.Equal(0.37m, scope.Owner.MatchThreshold);
    }

    [Fact]
    public void CancelKeepsGeneralGridAndPixelEditsOutOfLiveState()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        var original = (owner.MatchThreshold, owner.ImportBlockPolylines, owner.PanelBiasX, owner.ActiveAreaPixelsX);
        var originalChannels = owner.CascadeIcSettings[0].XChannels;
        var closeRequests = 0;
        draft.RequestClose += () => closeRequests++;

        draft.MatchThreshold = 0.37m;
        draft.ImportBlockPolylines = !owner.ImportBlockPolylines;
        draft.PanelBiasX = 12m;
        draft.CascadeIcSettings[0].XChannels = originalChannels + 1m;
        draft.ActiveAreaPixels.PixelsX = 123m;

        Assert.Equal(original, (owner.MatchThreshold, owner.ImportBlockPolylines, owner.PanelBiasX, owner.ActiveAreaPixelsX));
        Assert.Equal(originalChannels, owner.CascadeIcSettings[0].XChannels);
        draft.CancelCommand.Execute(null);

        Assert.Equal(1, closeRequests);
        Assert.Equal(original, (owner.MatchThreshold, owner.ImportBlockPolylines, owner.PanelBiasX, owner.ActiveAreaPixelsX));
        Assert.Equal(originalChannels, owner.CascadeIcSettings[0].XChannels);
        Assert.Equal(0.37m, draft.MatchThreshold);
        Assert.Equal(123m, draft.ActiveAreaPixels.PixelsX);
    }

    [Fact]
    public void SaveRemembersAnEditRevertedToItsOpeningValueButPreservesUntouchedLiveSettings()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        var opening = draft.MatchThreshold;
        draft.MatchThreshold = 0.37m;
        draft.MatchThreshold = opening;
        var openingBias = draft.PanelBiasX;
        draft.PanelBiasX = openingBias;
        owner.MatchThreshold = 0.61m;
        owner.PanelBiasX = 17m;
        owner.MappingWeightIou = 2.5m;
        owner.TargetCoverageCapPercent = 150m;

        draft.SaveCommand.Execute(null);

        Assert.Equal(opening, owner.MatchThreshold);
        Assert.Equal(17m, owner.PanelBiasX);
        Assert.Equal(2.5m, owner.MappingWeightIou);
        Assert.Equal(150m, owner.TargetCoverageCapPercent);
        Assert.True(draft.IsEdited(nameof(owner.MatchThreshold)));
        Assert.False(draft.IsEdited(nameof(owner.PanelBiasX)));
    }

    [Fact]
    public void ChangedSubscribersSeeTheNewValueBeforeTheEditIsRecorded()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var oldValue = draft.MatchThreshold;
        var trace = new SettingsDraftTrace<(decimal, bool)>(draft,
            () => (draft.MatchThreshold, draft.IsEdited(nameof(draft.MatchThreshold))));

        draft.MatchThreshold = 0.37m;

        trace.Expect(
            ("changing:MatchThreshold", (oldValue, false)),
            ("changed:MatchThreshold", (0.37m, false)));
        Assert.True(draft.IsEdited(nameof(draft.MatchThreshold)));
        draft.MarkOpened();
        Assert.False(draft.IsEdited(nameof(draft.MatchThreshold)));
    }

    [Fact]
    public void ScanOrderEditIsMappedAfterSubscriberDelivery()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var oldOption = draft.SelectedScanOrderOption;
        var next = draft.ScanOrderOptions.FirstOrDefault(option => option.Value != oldOption.Value);
        Assert.NotEqual(oldOption.Value, next.Value);
        var trace = new SettingsDraftTrace<(ScanOrderOption, bool)>(draft,
            () => (draft.SelectedScanOrderOption, draft.IsEdited(nameof(FreeformHelperViewModel.SelectedScanOrder))));

        draft.SelectedScanOrderOption = next;

        trace.Expect(
            ("changing:SelectedScanOrderOption", (oldOption, false)),
            ("changed:SelectedScanOrderOption", (next, false)));
        Assert.True(draft.IsEdited(nameof(FreeformHelperViewModel.SelectedScanOrder)));
    }

    [Fact]
    public void ActiveAreaDimensionsUpdateTheExistingPixelDraftBeforeTheHostChangedEvent()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var pixels = draft.ActiveAreaPixels;
        var identity = pixels.ProjectIdentity;
        var width = draft.ActiveAreaWidth;
        var height = draft.ActiveAreaHeight;
        var trace = new SettingsDraftTrace<(decimal, decimal, decimal, decimal)>(draft,
            () => (draft.ActiveAreaWidth, draft.ActiveAreaHeight, pixels.WidthMm, pixels.HeightMm));

        draft.ActiveAreaWidth = width + 10m;
        draft.ActiveAreaHeight = height + 20m;

        trace.Expect(
            ("changing:ActiveAreaWidth", (width, height, width, height)),
            ("changed:ActiveAreaWidth", (width + 10m, height, width + 10m, height)),
            ("changing:ActiveAreaHeight", (width + 10m, height, width + 10m, height)),
            ("changed:ActiveAreaHeight", (width + 10m, height + 20m, width + 10m, height + 20m)));
        Assert.Same(pixels, draft.ActiveAreaPixels);
        Assert.Same(identity, pixels.ProjectIdentity);
        Assert.Equal((width, height), (scope.Owner.ActiveAreaWidth, scope.Owner.ActiveAreaHeight));
    }

    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.CoordinatePixelWidth), 0, 1)]
    [InlineData(nameof(SettingsWindowViewModel.CoordinatePixelHeight), 0, 1)]
    [InlineData(nameof(SettingsWindowViewModel.HighlightStrokeWidthAdjust), 5, 4)]
    [InlineData(nameof(SettingsWindowViewModel.HighlightStrokeWidthAdjust), -3, -2)]
    public void NumericNormalizationRetainsNestedChangingAndDuplicateChangedEvents(string propertyName, int input, int normalized)
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var property = typeof(SettingsWindowViewModel).GetProperty(propertyName)!;
        var initial = (decimal)property.GetValue(draft)!;
        var trace = new SettingsDraftTrace<decimal>(draft, () => (decimal)property.GetValue(draft)!);

        property.SetValue(draft, (decimal)input);

        trace.Expect(
            ($"changing:{propertyName}", initial),
            ($"changing:{propertyName}", input),
            ($"changed:{propertyName}", normalized),
            ($"changed:{propertyName}", normalized));
    }
}
