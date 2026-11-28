// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsDraftLifecycleCharacterizationTests
{
    [Fact]
    public void CancelDiscardsNotchMappingAndExportEditsWithoutResettingTheDraft()
    {
        using var scope = new SettingsDraftTestScope();
        var owner = scope.Owner;
        var draft = owner.CreateSettingsWindowViewModel();
        var original = (owner.EnableToRegular, owner.EnableToFull, owner.MappingWeightIou, owner.LenScale, owner.NullValue);
        var originalThresholds = (owner.LinkNotchThresholds, owner.NotchThresholdQ7, owner.NotchThresholdPercent);
        var originalFileType = owner.SelectedNotchExportFileTypeOption;
        var closeRequests = 0;
        draft.RequestClose += () => closeRequests++;

        draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(
            option => option.Value == NotchCompensationModel.Disabled);
        draft.LinkNotchThresholds = false;
        draft.NotchThresholdQ7 = 65m;
        draft.NotchThresholdPercent = 12.5m;
        draft.MappingWeightIou = 2.5m;
        draft.LenScale = 23m;
        draft.NullValue = 42m;
        draft.SelectedNotchExportFileTypeOption = draft.NotchExportFileTypeOptions.Single(
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);

        Assert.Equal(original, (owner.EnableToRegular, owner.EnableToFull, owner.MappingWeightIou, owner.LenScale, owner.NullValue));
        Assert.Equal(originalThresholds, (owner.LinkNotchThresholds, owner.NotchThresholdQ7, owner.NotchThresholdPercent));
        Assert.Equal(originalFileType, owner.SelectedNotchExportFileTypeOption);
        draft.CancelCommand.Execute(null);

        Assert.Equal(1, closeRequests);
        Assert.Equal(original, (owner.EnableToRegular, owner.EnableToFull, owner.MappingWeightIou, owner.LenScale, owner.NullValue));
        Assert.Equal(originalThresholds, (owner.LinkNotchThresholds, owner.NotchThresholdQ7, owner.NotchThresholdPercent));
        Assert.Equal(originalFileType, owner.SelectedNotchExportFileTypeOption);
        Assert.Equal((65m, 12.5m, 23m), (draft.NotchThresholdQ7, draft.NotchThresholdPercent, draft.LenScale));
        Assert.False(draft.EnableToFull);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task StaleIdentityRejectsGeneralGridNotchMappingExportAndPixelsBeforeClosingAsync()
    {
        using var files = new LoadingModeTestScope();
        using var scope = new SettingsDraftTestScope(files.CreateViewModel());
        var owner = scope.Owner;
        await owner.WaitForGridRebuildIdleAsync();
        Assert.True(await owner.SaveProjectAsync());
        var draft = owner.CreateSettingsWindowViewModel();
        var identity = draft.ActiveAreaPixels.ProjectIdentity;
        var original = (owner.MatchThreshold, owner.PanelBiasX, owner.EnableToFull, owner.MappingWeightIou, owner.NullValue);
        var channels = owner.CascadeIcSettings[0].XChannels;
        draft.MatchThreshold = 0.37m;
        draft.PanelBiasX = 17m;
        draft.CascadeIcSettings[0].XChannels = channels + 1m;
        draft.EnableToFull = !owner.EnableToFull;
        draft.MappingWeightIou = 2.5m;
        draft.NullValue = 42m;
        draft.ActiveAreaPixels.PixelsX = 100m;
        draft.ActiveAreaPixels.PixelsY = 200m;
        Assert.True((await owner.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        await owner.WaitForGridRebuildIdleAsync();
        var currentIdentity = owner.CreateSettingsWindowViewModel().ActiveAreaPixels.ProjectIdentity;
        var closeRequests = 0;
        draft.RequestClose += () =>
        {
            closeRequests++;
            Assert.Equal(original, (owner.MatchThreshold, owner.PanelBiasX, owner.EnableToFull, owner.MappingWeightIou, owner.NullValue));
            Assert.Contains("Settings not saved", owner.StatusText, StringComparison.Ordinal);
        };

        draft.SaveCommand.Execute(null);

        Assert.Equal(1, closeRequests);
        Assert.NotNull(identity);
        Assert.NotSame(identity, currentIdentity);
        Assert.Same(identity, draft.ActiveAreaPixels.ProjectIdentity);
        Assert.Equal(channels, owner.CascadeIcSettings[0].XChannels);
        Assert.Equal((0m, 0m), (owner.ActiveAreaPixelsX, owner.ActiveAreaPixelsY));
        Assert.False(owner.HasUnsavedChanges);
        Assert.Equal((0.37m, 17m, 2.5m, 42m), (draft.MatchThreshold, draft.PanelBiasX, draft.MappingWeightIou, draft.NullValue));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task CategoryCommandsReplaceRowsAfterChangingLiveSelectionAndSurviveCancelAsync()
    {
        using var files = new LoadingModeTestScope();
        using var scope = new SettingsDraftTestScope(files.CreateViewModel());
        var owner = scope.Owner;
        await owner.OpenDxfCommand.ExecuteAsync(null);
        await owner.WaitForGridRebuildIdleAsync();
        var draft = owner.CreateSettingsWindowViewModel();
        var rows = draft.LayerCategoryRows;
        var category = rows.Single(row => row.Category == FreeformHelperViewModel.DxfLayerCategory.Polyline);
        var layer = Assert.Single(owner.LayerToggles, toggle => toggle.Name == "signal");
        Assert.True(layer.IsSelected);
        Assert.Equal(1, category.SelectedCount);
        var trace = new SettingsDraftTrace<(bool, bool, int)>(draft,
            () => (layer.IsSelected, ReferenceEquals(rows, draft.LayerCategoryRows),
                draft.LayerCategoryRows.Single(row => row.Category == category.Category).SelectedCount));

        category.DisableCommand.Execute(null);

        trace.Expect(
            ("changing:LayerCategoryRows", (false, true, 1)),
            ("changed:LayerCategoryRows", (false, false, 0)),
            ("changed:HasLayerCategoryRows", (false, false, 0)));
        draft.CancelCommand.Execute(null);
        await owner.WaitForGridRebuildIdleAsync();
        Assert.False(layer.IsSelected);
        Assert.True(owner.HasUnsavedChanges);
        Assert.Equal(0, owner.CreateSettingsWindowViewModel().LayerCategoryRows.Single(row => row.Category == category.Category).SelectedCount);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ResetOnAStaleDraftResetsTheCurrentProjectThenClosesWithoutReloadingTheDraftAsync()
    {
        using var files = new LoadingModeTestScope();
        using var scope = new SettingsDraftTestScope(files.CreateViewModel());
        var owner = scope.Owner;
        owner.MatchThreshold = 0.37m;
        owner.TargetCoverageCapPercent = 180m;
        owner.LenScale = 23m;
        owner.MappingWeightIou = 2.5m;
        await owner.WaitForGridRebuildIdleAsync();
        Assert.True(await owner.SaveProjectAsync());
        var draft = owner.CreateSettingsWindowViewModel();
        var identity = draft.ActiveAreaPixels.ProjectIdentity;
        draft.NullValue = 42m;
        Assert.True((await owner.LoadProjectFromPathAsync(files.PathFor("project.json"))).IsLoaded);
        await owner.WaitForGridRebuildIdleAsync();
        Assert.NotSame(identity, owner.CreateSettingsWindowViewModel().ActiveAreaPixels.ProjectIdentity);
        var defaults = new ProjectSettings();
        var closeRequests = 0;
        draft.RequestClose += () =>
        {
            closeRequests++;
            Assert.Equal((decimal)defaults.Matching.MatchThreshold, owner.MatchThreshold);
            Assert.Equal((decimal)defaults.Notch.TargetCoverageCapPercent, owner.TargetCoverageCapPercent);
            Assert.Equal((decimal)defaults.Notch.LenScale, owner.LenScale);
        };

        draft.ResetAllSettingsToDefaults();
        await owner.WaitForGridRebuildIdleAsync();

        Assert.Equal(1, closeRequests);
        Assert.True(owner.HasUnsavedChanges);
        Assert.True(owner.ApplyAppVisualPreferencesOnProjectLoad);
        Assert.Equal(0.37m, draft.MatchThreshold);
        Assert.Equal(180m, draft.TargetCoverageCapPercent);
        Assert.Equal(42m, draft.NullValue);
        Assert.Same(identity, draft.ActiveAreaPixels.ProjectIdentity);
    }
}
