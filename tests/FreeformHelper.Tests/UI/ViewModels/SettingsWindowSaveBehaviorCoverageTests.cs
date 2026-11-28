// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Logging;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowSaveBehaviorCoverageTests
{
    [AvaloniaTheory]
    [InlineData(NotchCompensationModel.Disabled, false)]
    [InlineData(NotchCompensationModel.ConservativeNoGain, true)]
    public async Task SaveCompensationPresetPersistsModelAndSwitchesAsync(NotchCompensationModel model, bool enabled)
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var draft = vm.CreateSettingsWindowViewModel();
            draft.SelectedNotchCompensationModelOption = draft.NotchCompensationModelOptions.Single(option => option.Value == model);

            draft.SaveCommand.Execute(null);

            Assert.Equal(model, vm.SelectedNotchCompensationModelOption.Value);
            Assert.Equal(enabled, vm.EnableToRegular);
            Assert.Equal(enabled, vm.EnableToFull);
            Assert.True(vm.HasUnsavedChanges);
            var reopened = vm.CreateSettingsWindowViewModel();
            Assert.Equal(model, reopened.SelectedNotchCompensationModelOption.Value);
            Assert.Equal(enabled, reopened.EnableToRegular);
            Assert.Equal(enabled, reopened.EnableToFull);
            await vm.WaitForGridRebuildIdleAsync();
            Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
            var saved = JsonProjectStore.Load(files.PathFor("project.json"));
            Assert.Equal(model, saved.Settings.Notch.CompensationModel);
            Assert.Equal(enabled, saved.Settings.Notch.EnableToRegular);
            Assert.Equal(enabled, saved.Settings.Notch.EnableToFull);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task SaveLogLevelIgnoresCasingAndAppliesCanonicalProcessLevelAsync()
    {
        using var logLevel = new ProcessLogLevelScope("Info");
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var expected = vm.LogLevelOptions.Single(option => option.Value == "Warn");
            var draft = vm.CreateSettingsWindowViewModel();
            draft.SelectedLogLevelOption = new LogLevelOption("wArN", "Warnings");
            Assert.Equal("Info", LoggingBootstrapper.GetCurrentMinimumLevelName());

            draft.SaveCommand.Execute(null);

            Assert.Equal(expected, vm.SelectedLogLevelOption);
            Assert.Equal("Warn", LoggingBootstrapper.GetCurrentMinimumLevelName());
            Assert.Equal(expected, vm.CreateSettingsWindowViewModel().SelectedLogLevelOption);
            await vm.WaitForGridRebuildIdleAsync();
            Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
            Assert.Equal("Warn", JsonProjectStore.Load(files.PathFor("project.json")).UiSnapshot.Import.LogLevel);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task SaveUntouchedDraftClosesWithoutChangingProjectStateAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            await vm.WaitForGridRebuildIdleAsync();
            var draft = vm.CreateSettingsWindowViewModel();
            var pads = vm.RegularPads;
            var canUndo = vm.CanUndo;
            var closeRequests = 0;
            draft.RequestClose += () => closeRequests++;
            vm.HasUnsavedChanges = false;
            vm.StatusText = "Settings inspected.";

            draft.SaveCommand.Execute(null);
            await vm.WaitForGridRebuildIdleAsync();

            Assert.Equal(1, closeRequests);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(canUndo, vm.CanUndo);
            Assert.Same(pads, vm.RegularPads);
            Assert.Equal("Settings inspected.", vm.StatusText);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task SaveDirectlyConstructedDraftAppliesCallerEditsAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var draft = new SettingsWindowViewModel(vm);
            draft.MatchThreshold = 0.37m;
            var closeRequests = 0;
            draft.RequestClose += () => closeRequests++;

            draft.SaveCommand.Execute(null);
            await vm.WaitForGridRebuildIdleAsync();

            Assert.Equal(0.37m, vm.MatchThreshold);
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal(1, closeRequests);
            Assert.Equal(0.37m, vm.CreateSettingsWindowViewModel().MatchThreshold);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }
}
