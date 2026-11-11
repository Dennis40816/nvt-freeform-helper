// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Reflection;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Locks what the settings window draft copies from the project state when the window opens:
/// every setting that the draft and the owner share, and the option lists with their selected option.
/// </summary>
public sealed class SettingsWindowConstructorCopyBehaviorTests
{
    private const int MinimumSharedSettings = 55;

    [Fact]
    public void Constructor_CopiesEverySharedSettingFromTheOwner()
    {
        var owner = new FreeformHelperViewModel();
        var shared = SharedProperties().ToArray();
        Assert.True(shared.Length >= MinimumSharedSettings, $"Only {shared.Length} shared settings were found.");

        var draft = owner.CreateSettingsWindowViewModel();

        AssertDraftMatchesOwner(shared, owner, draft);
    }

    [Fact]
    public void Constructor_AfterTheOwnerChangedItsSettings_CopiesTheChangedValues()
    {
        var owner = new FreeformHelperViewModel();
        var shared = SharedProperties().ToArray();
        var changed = 0;
        foreach (var (draftProperty, ownerProperty) in shared)
        {
            if (!ownerProperty.CanWrite)
            {
                continue;
            }

            var current = ownerProperty.GetValue(owner);
            if (draftProperty.PropertyType == typeof(bool))
            {
                ownerProperty.SetValue(owner, !(bool)current!);
                changed++;
            }
            else if (draftProperty.PropertyType == typeof(decimal))
            {
                ownerProperty.SetValue(owner, (decimal)current! + 1m);
                changed++;
            }
        }

        Assert.True(changed >= MinimumSharedSettings / 2, $"Only {changed} settings were changed.");

        var draft = owner.CreateSettingsWindowViewModel();

        AssertDraftMatchesOwner(shared, owner, draft);
    }

    [Fact]
    public void Constructor_CopiesTheOptionListsAndKeepsEachSelectionInsideItsList()
    {
        var owner = new FreeformHelperViewModel();

        var draft = owner.CreateSettingsWindowViewModel();

        Assert.Equal(owner.ScanOrderOptions, draft.ScanOrderOptions);
        Assert.Equal(owner.GridAlignmentOptions, draft.GridAlignmentOptions);
        Assert.Equal(owner.RegularSourceModeOptions, draft.RegularSourceModeOptions);
        Assert.Equal(owner.RegularSourceLayerOptions, draft.RegularSourceLayerOptions);
        Assert.Equal(owner.LogLevelOptions, draft.LogLevelOptions);
        Assert.Equal(owner.CadOutputFwDiffAutoModeOptions, draft.CadOutputFwDiffAutoModeOptions);
        Assert.Equal(owner.NotchExportFileTypeOptions, draft.NotchExportFileTypeOptions);
        Assert.Equal(owner.NotchExportProfileOptions, draft.NotchExportProfileOptions);
        Assert.Equal(owner.NotchCompensationModelOptions, draft.NotchCompensationModelOptions);
        Assert.Contains(draft.SelectedScanOrderOption, draft.ScanOrderOptions);
        Assert.Contains(draft.SelectedGridAlignmentOption, draft.GridAlignmentOptions);
        Assert.Contains(draft.SelectedRegularSourceModeOption, draft.RegularSourceModeOptions);
        Assert.Contains(draft.SelectedLogLevelOption, draft.LogLevelOptions);
        Assert.Contains(draft.SelectedCadOutputFwDiffAutoModeOption, draft.CadOutputFwDiffAutoModeOptions);
        Assert.Contains(draft.SelectedNotchExportFileTypeOption, draft.NotchExportFileTypeOptions);
        Assert.Contains(draft.SelectedNotchExportProfileOption, draft.NotchExportProfileOptions);
        Assert.Contains(draft.SelectedNotchCompensationModelOption, draft.NotchCompensationModelOptions);
    }

    [Fact]
    public void Constructor_WithAnOwnerSelectionThatIsNotInTheList_FallsBackToTheFirstOption()
    {
        var owner = new FreeformHelperViewModel();
        var unknown = new FreeformHelper.Application.CadImport.RegularSourceLayerOption("layer-that-does-not-exist", "Missing");
        owner.SelectedRegularSourceLayerOption = unknown;

        var draft = owner.CreateSettingsWindowViewModel();

        Assert.NotNull(draft.SelectedRegularSourceLayerOption);
        Assert.NotEqual("layer-that-does-not-exist", draft.SelectedRegularSourceLayerOption.Name);
        if (draft.RegularSourceLayerOptions.Count > 0)
        {
            Assert.Equal(draft.RegularSourceLayerOptions[0], draft.SelectedRegularSourceLayerOption);
        }
        else
        {
            Assert.Equal("Select layer", draft.SelectedRegularSourceLayerOption.Display);
        }
    }

    private static void AssertDraftMatchesOwner(
        (PropertyInfo Draft, PropertyInfo Owner)[] shared,
        FreeformHelperViewModel owner,
        SettingsWindowViewModel draft)
    {
        foreach (var (draftProperty, ownerProperty) in shared)
        {
            Assert.True(
                Equals(ownerProperty.GetValue(owner), draftProperty.GetValue(draft)),
                $"{draftProperty.Name}: owner={ownerProperty.GetValue(owner)}, draft={draftProperty.GetValue(draft)}");
        }
    }

    private static IEnumerable<(PropertyInfo Draft, PropertyInfo Owner)> SharedProperties()
    {
        var ownerProperties = typeof(FreeformHelperViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(static property => property.Name, StringComparer.Ordinal);
        foreach (var draftProperty in typeof(SettingsWindowViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (draftProperty.CanWrite
                && draftProperty.GetIndexParameters().Length == 0
                && ownerProperties.TryGetValue(draftProperty.Name, out var ownerProperty)
                && ownerProperty.CanRead
                && ownerProperty.PropertyType == draftProperty.PropertyType)
            {
                yield return (draftProperty, ownerProperty);
            }
        }
    }
}
