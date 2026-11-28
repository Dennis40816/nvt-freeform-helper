// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.ComponentModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsOwnerGuardFailureCharacterizationTests
{
    [Theory]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdQ7), nameof(SettingsWindowViewModel.NotchThresholdPercent))]
    [InlineData(nameof(SettingsWindowViewModel.NotchThresholdPercent), nameof(SettingsWindowViewModel.NotchThresholdQ7))]
    [InlineData(nameof(SettingsWindowViewModel.LinkNotchThresholds), nameof(SettingsWindowViewModel.NotchThresholdPercent))]
    public void FailedLinkedNotificationKeepsThresholdSynchronizationInactive(string editedProperty, string nestedProperty)
    {
        // SettingsDraftTestScope uses the module-level TestAppSettingsIsolation path.
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        draft.LinkNotchThresholds = false;
        draft.NotchThresholdQ7 = 64m;
        draft.NotchThresholdPercent = 20m;
        draft.LinkNotchThresholds = editedProperty != nameof(draft.LinkNotchThresholds);
        PropertyChangedEventHandler observer = (_, args) =>
        {
            if (args.PropertyName == nestedProperty)
            {
                throw new InvalidOperationException("Nested threshold observer failed.");
            }
        };
        draft.PropertyChanged += observer;

        Assert.Throws<InvalidOperationException>(() => EditThreshold(draft, editedProperty));
        draft.PropertyChanged -= observer;
        var percentAfterFailure = draft.NotchThresholdPercent;
        draft.NotchThresholdQ7 = 96m;

        Assert.True(draft.LinkNotchThresholds);
        Assert.Equal(96m, draft.NotchThresholdQ7);
        Assert.Equal(percentAfterFailure, draft.NotchThresholdPercent);
        draft.NotchThresholdPercent = 10m;
        Assert.Equal(96m, draft.NotchThresholdQ7);
    }

    [Fact]
    public void FailedPresetNotificationReleasesCompensationSynchronization()
    {
        using var scope = new SettingsDraftTestScope();
        var draft = scope.Owner.CreateSettingsWindowViewModel();
        var disabled = draft.NotchCompensationModelOptions.FirstOrDefault(option => option.Value == NotchCompensationModel.Disabled);
        var enabled = draft.NotchCompensationModelOptions.FirstOrDefault(option => option.Value == NotchCompensationModel.CurrentGain);
        draft.SelectedNotchCompensationModelOption = enabled;
        draft.EnableToRegular = true;
        draft.EnableToFull = true;
        PropertyChangedEventHandler observer = (_, args) =>
        {
            if (args.PropertyName == nameof(draft.EnableToRegular))
            {
                throw new InvalidOperationException("Preset observer failed.");
            }
        };
        draft.PropertyChanged += observer;

        Assert.Throws<InvalidOperationException>(() => draft.SelectedNotchCompensationModelOption = disabled);
        draft.PropertyChanged -= observer;

        Assert.False(draft.EnableToRegular);
        Assert.True(draft.EnableToFull);
        draft.SelectedNotchCompensationModelOption = enabled;
        Assert.True(draft.EnableToRegular);
        Assert.True(draft.EnableToFull);
        draft.SelectedNotchCompensationModelOption = disabled;
        Assert.False(draft.EnableToRegular);
        Assert.False(draft.EnableToFull);
    }

    private static void EditThreshold(SettingsWindowViewModel draft, string propertyName)
    {
        switch (propertyName)
        {
            case nameof(SettingsWindowViewModel.NotchThresholdQ7):
                draft.NotchThresholdQ7 = 32m;
                break;
            case nameof(SettingsWindowViewModel.NotchThresholdPercent):
                draft.NotchThresholdPercent = 25m;
                break;
            case nameof(SettingsWindowViewModel.LinkNotchThresholds):
                draft.LinkNotchThresholds = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(propertyName));
        }
    }
}
