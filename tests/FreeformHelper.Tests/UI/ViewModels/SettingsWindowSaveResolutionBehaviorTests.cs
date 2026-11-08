// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SettingsWindowSaveResolutionBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_AfterDxfReplacement_ResolvesDraftLayerAgainstTheCurrentCatalogAsync(bool removeDraftLayer)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            var original = FreeformHelperViewModelTests.CreateTempDxfFile(("beta", 0, 0, 10, 20));
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(original);
            vm.EnsureNoPendingEdits = () => true;
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var draft = vm.CreateSettingsWindowViewModel();
            draft.SelectedRegularSourceLayerOption = draft.RegularSourceLayerOptions.Single(option => option.Name == "beta");
            draft.SelectedRegularSourceModeOption = draft.RegularSourceModeOptions.Single(
                option => option.Value == RegularSourceMode.FromDxfLayer);
            draft.MatchThreshold = 0.37m;

            var replacement = removeDraftLayer
                ? FreeformHelperViewModelTests.CreateTempDxfFile(("gamma", 20, 0, 30, 20))
                : FreeformHelperViewModelTests.CreateTempDxfFile(("beta", 0, 0, 10, 20), ("gamma", 20, 0, 30, 20));
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(replacement);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            vm.SelectedRegularSourceLayerOption = vm.RegularSourceLayerOptions.Single(option => option.Name == "gamma");
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            draft.SaveCommand.Execute(null);
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            var expectedLayer = removeDraftLayer ? "gamma" : "beta";
            Assert.Equal(expectedLayer, vm.SelectedRegularSourceLayerOption.Name);
            Assert.Equal(RegularSourceMode.FromDxfLayer, vm.RegularSourceMode);
            Assert.Equal(0.37m, vm.MatchThreshold);
            Assert.Equal(expectedLayer, vm.CreateSettingsWindowViewModel().SelectedRegularSourceLayerOption.Name);
            Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
            Assert.Equal(expectedLayer, JsonProjectStore.Load(files.PathFor("project.json")).Settings.Grid.RegularSourceLayerName);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task Save_InvisibleAnchorOnly_ReportsRejectionAndClosesTheDraftAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            await vm.OpenDxfCommand.ExecuteAsync(null);
            Assert.Single(vm.CadPads);
            var draft = vm.CreateSettingsWindowViewModel();
            draft.CadOutputFwDiffIndexAnchorCadId = 999m;
            var closeRequests = 0;
            draft.RequestClose += () => closeRequests++;

            draft.SaveCommand.Execute(null);

            Assert.Equal(-1m, vm.CadOutputFwDiffIndexAnchorCadId);
            Assert.Equal("CAD Output FW Diff anchor ignored: CAD 999 is not visible.", vm.StatusText);
            Assert.Equal(1, closeRequests);
            Assert.Single(vm.CadPads);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }

    [AvaloniaFact]
    public async Task ResetDefaults_ReplacesEditedSettingsAndClosesTheDraftAsync()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        try
        {
            vm.MatchThreshold = 0.37m;
            vm.TargetCoverageCapPercent = 180m;
            vm.NotchThresholdQ7 = 64m;
            vm.ActiveAreaPixelsX = 3240m;
            vm.SelectedNotchCompensationModelOption = vm.NotchCompensationModelOptions.Single(
                option => option.Value == NotchCompensationModel.Disabled);
            var draft = vm.CreateSettingsWindowViewModel();
            draft.MatchThreshold = 0.91m;
            var closeRequests = 0;
            draft.RequestClose += () => closeRequests++;

            draft.ResetAllSettingsToDefaults();
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

            var defaults = new ProjectSettings();
            Assert.Equal((decimal)defaults.Matching.MatchThreshold, vm.MatchThreshold);
            Assert.Equal((decimal)defaults.Notch.TargetCoverageCapPercent, vm.TargetCoverageCapPercent);
            Assert.Equal((decimal)defaults.Notch.ThresholdQ7, vm.NotchThresholdQ7);
            Assert.Equal(defaults.Notch.CompensationModel, vm.SelectedNotchCompensationModelOption.Value);
            Assert.True(vm.EnableToRegular);
            Assert.True(vm.EnableToFull);
            Assert.Equal(0m, vm.ActiveAreaPixelsX);
            Assert.True(vm.ApplyAppVisualPreferencesOnProjectLoad);
            Assert.True(vm.HasUnsavedChanges);
            Assert.Equal(1, closeRequests);
            Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
            var saved = JsonProjectStore.Load(files.PathFor("project.json"));
            Assert.Equal(defaults.Matching.MatchThreshold, saved.Settings.Matching.MatchThreshold);
            Assert.Equal(defaults.Notch.CompensationModel, saved.Settings.Notch.CompensationModel);
            Assert.Null(saved.Settings.ActiveAreaPixels.X);
        }
        finally
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }
    }
}
