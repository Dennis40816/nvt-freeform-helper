// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsDraftExportPersistenceCharacterizationTests
{
    [Fact]
    public void FreshDraftKeepsDefaultThresholdsSwitchesAndHiddenExportValues()
    {
        using var scope = new SettingsDraftTestScope();

        var draft = scope.Owner.CreateSettingsWindowViewModel();

        Assert.Equal((10m, 65535m), (draft.LenScale, draft.NullValue));
        Assert.Equal(NotchCompensationModel.CurrentGain, draft.SelectedNotchCompensationModelOption.Value);
        Assert.Equal(NotchExportProfile.Release, draft.SelectedNotchExportProfileOption.Value);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Csv, draft.SelectedNotchExportFileTypeOption.Value);
        Assert.True(draft.EnableToRegular);
        Assert.True(draft.EnableToFull);
        Assert.True(draft.EnableV21);
        Assert.True(draft.EnableV22);
        Assert.True(draft.LinkNotchThresholds);
        var defaults = new ProjectSettings();
        Assert.Equal((decimal)defaults.Notch.ThresholdQ7, draft.NotchThresholdQ7);
        Assert.Equal((decimal)defaults.Notch.ThresholdPercentV22, draft.NotchThresholdPercent);
    }

    [Fact]
    public void UnknownOpeningExportSelectionsFallBackWithoutEditingTheLiveOwner()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var unknownType = new NotchExportFileTypeOption(
            (FreeformHelperViewModel.NotchExportFileType)99, "Unknown file", "unknown");
        var unknownProfile = new NotchExportProfileOption((NotchExportProfile)99, "Unknown profile");
        owner.SelectedNotchExportFileTypeOption = unknownType;
        owner.SelectedNotchExportProfileOption = unknownProfile;

        var draft = owner.CreateSettingsWindowViewModel();
        draft.SaveCommand.Execute(null);

        Assert.Equal(draft.NotchExportFileTypeOptions[0], draft.SelectedNotchExportFileTypeOption);
        Assert.Equal(draft.NotchExportProfileOptions[0], draft.SelectedNotchExportProfileOption);
        Assert.Equal(unknownType, owner.SelectedNotchExportFileTypeOption);
        Assert.Equal(unknownProfile, owner.SelectedNotchExportProfileOption);
        Assert.False(draft.IsEdited(nameof(draft.SelectedNotchExportFileTypeOption)));
        Assert.False(draft.IsEdited(nameof(draft.SelectedNotchExportProfileOption)));
    }

    [Fact]
    public void SaveCanonicalizesTheEditedFileTypeButAppliesTheEditedProfileVerbatim()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        draft.SelectedNotchExportFileTypeOption = new(FreeformHelperViewModel.NotchExportFileType.Cv22, "Draft label", "temporary");
        var profile = new NotchExportProfileOption(NotchExportProfile.Debug, "Draft profile");
        draft.SelectedNotchExportProfileOption = profile;
        owner.SelectedNotchExportFileTypeOption = owner.NotchExportFileTypeOptions.Single(
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv21);

        draft.SaveCommand.Execute(null);

        Assert.Equal(owner.NotchExportFileTypeOptions.Single(option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22),
            owner.SelectedNotchExportFileTypeOption);
        Assert.Equal(profile, owner.SelectedNotchExportProfileOption);
    }

    [Fact]
    public void SaveWithAnUnknownFileTypeKeepsTheCurrentLiveSelection()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        draft.SelectedNotchExportFileTypeOption = new((FreeformHelperViewModel.NotchExportFileType)99, "Unknown", "unknown");
        var live = owner.NotchExportFileTypeOptions.Single(option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv21);
        owner.SelectedNotchExportFileTypeOption = live;
        draft.NullValue = 42m;

        draft.SaveCommand.Execute(null);

        Assert.Equal(live, owner.SelectedNotchExportFileTypeOption);
        Assert.Equal(42m, owner.NullValue);
    }

    [Fact]
    public void FileTypeSafetyNotificationPrecedesTheSelectionChangedEvent()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var original = draft.SelectedNotchExportFileTypeOption;
        var next = draft.NotchExportFileTypeOptions.Single(option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        var trace = new SettingsDraftTrace<NotchExportFileTypeOption>(draft,
            () => draft.SelectedNotchExportFileTypeOption);

        draft.SelectedNotchExportFileTypeOption = next;

        trace.Expect(
            ("changing:SelectedNotchExportFileTypeOption", original),
            ("changed:NotchExportSafetyPolicySummary", next),
            ("changed:SelectedNotchExportFileTypeOption", next));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task HiddenCoordinateMappingAndExportValuesSurviveSaveAndProjectReloadAsync()
    {
        using var files = new LoadingModeTestScope();
        using var scope = new SettingsDraftTestScope(files.CreateViewModel());
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        draft.CoordinatePixelWidth = 1234m;
        draft.CoordinatePixelHeight = 567m;
        draft.LenScale = 23m;
        draft.MappingWeightIou = 2.5m;
        draft.MappingWeightCentroidDistance = 3.5m;
        draft.MappingWeightAreaRatio = 4.5m;
        draft.MappingLowConfidenceThreshold = 0.4m;
        draft.MappingAmbiguousMargin = 0.09m;
        draft.MappingCandidateNumber = 9m;
        draft.EnableV21 = false;
        draft.SelectedNotchExportFileTypeOption = draft.NotchExportFileTypeOptions.Single(
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        draft.SelectedNotchExportProfileOption = draft.NotchExportProfileOptions.Single(option => option.Value == NotchExportProfile.Debug);
        var closeRequests = 0;
        draft.RequestClose += () =>
        {
            closeRequests++;
            Assert.Equal((1234m, 567m, 23m), (owner.CoordinatePixelWidth, owner.CoordinatePixelHeight, owner.LenScale));
        };

        draft.SaveCommand.Execute(null);
        await owner.WaitForGridRebuildIdleAsync();
        Assert.True(await owner.SaveProjectAsync(), owner.StatusText);
        Assert.True((await owner.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        await owner.WaitForGridRebuildIdleAsync();
        var reopened = owner.CreateSettingsWindowViewModel();

        Assert.Equal(1, closeRequests);
        Assert.Equal((1234m, 567m, 23m), (reopened.CoordinatePixelWidth, reopened.CoordinatePixelHeight, reopened.LenScale));
        Assert.Equal((2.5m, 3.5m, 4.5m),
            (reopened.MappingWeightIou, reopened.MappingWeightCentroidDistance, reopened.MappingWeightAreaRatio));
        Assert.Equal((0.4m, 0.09m, 9m),
            (reopened.MappingLowConfidenceThreshold, reopened.MappingAmbiguousMargin, reopened.MappingCandidateNumber));
        Assert.False(reopened.EnableV21);
        Assert.True(reopened.EnableV22);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Cv22, reopened.SelectedNotchExportFileTypeOption.Value);
        Assert.Equal(NotchExportProfile.Debug, reopened.SelectedNotchExportProfileOption.Value);
    }
}
