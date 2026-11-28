// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RegularVisibilityMaskRefreshBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(null, null, "SeeRegular.csv", true)]
    [InlineData("  missing-mask.csv  ", "   ", "missing-mask.csv", false)]
    [InlineData("ignored.csv", "  embedded-mask.csv  ", "embedded-mask.csv", true)]
    public async Task LoadProject_EmbeddedMask_ResolvesSourceLabelAndSavedEnabledStateAsync(
        string? sourcePath, string? embeddedName, string expectedLabel, bool enabled)
    {
        using var culture = TestCulture.UseEnUs();
        var (saved, _) = await OpenMaskedWorkspaceAsync();
        var projectPath = TestFiles.NewTempFilePath("cov33-mask-label-", ".json");
        saved.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        saved.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await saved.SaveProjectAsync());
        var project = JsonProjectStore.Load(projectPath);
        Assert.NotNull(project.EmbeddedRegularVisibilityMask);
        project.LastDxfPath = null;
        project.EmbeddedRegularVisibilityMaskName = embeddedName;
        project.UiSnapshot.Import.RegularVisibilityMaskSourcePath = sourcePath;
        project.UiSnapshot.Import.UseRegularVisibilityMask = enabled;
        await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);
        var vm = CreateViewModel();

        var result = await vm.LoadProjectFromPathAsync(projectPath);
        await WaitForGridRebuildAsync(vm);

        Assert.True(result.IsLoaded);
        Assert.Equal(enabled, vm.RegularMask.IsEnabled);
        var expectedSummary = enabled
            ? $"Regular visibility mask enabled: {expectedLabel} loaded, active=1, inactive=1, frames=1. " +
              "Step 4 geometry seed only uses active regulars."
            : $"Regular visibility mask loaded but disabled: {expectedLabel}. Step 4 uses pure geometry seed.";
        Assert.Equal(expectedSummary, vm.RegularMask.Summary);
    }

    [AvaloniaFact]
    public async Task RebuildGrid_ChangedChannelCount_IgnoresIncompatibleMaskAndRecoversAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (vm, maskPath) = await OpenMaskedWorkspaceAsync();
        var cascade = Assert.Single(vm.CascadeIcSettings);

        cascade.XChannels = 3;
        await WaitForGridRebuildAsync(vm);

        Assert.True(vm.RegularMask.IsEnabled);
        Assert.Equal(3, vm.RegularPads.Count);
        Assert.StartsWith(
            $"Regular visibility mask: {Path.GetFileName(maskPath)} incompatible with current grid 1x3. Ignored.",
            vm.RegularMask.Summary, StringComparison.Ordinal);

        cascade.XChannels = 2;
        await WaitForGridRebuildAsync(vm);

        Assert.Contains("active=1, inactive=1, frames=1", vm.RegularMask.Summary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task RebuildGrid_EmptiedMaskFile_ReportsFailureAndRecoversAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (vm, maskPath) = await OpenMaskedWorkspaceAsync();
        TestFiles.WriteText(maskPath, string.Empty);

        vm.ActiveAreaWidth = 22;
        await WaitForGridRebuildAsync(vm);

        Assert.True(vm.RegularMask.IsEnabled);
        Assert.StartsWith(
            $"Regular visibility mask: failed to load {Path.GetFileName(maskPath)}. Ignored.",
            vm.RegularMask.Summary, StringComparison.Ordinal);

        WriteMask(maskPath);
        vm.ActiveAreaWidth = 20;
        await WaitForGridRebuildAsync(vm);

        Assert.Contains("active=1, inactive=1, frames=1", vm.RegularMask.Summary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task DisableMask_AllCadLayersOff_KeepsOutputEmptyAndInvalidatesExportAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var (vm, _) = await OpenMaskedWorkspaceAsync();
        var cadIds = vm.CadPads.Select(static pad => pad.Id).ToArray();
        Assert.Equal(2, cadIds.Length);
        vm.DeselectAllLayers();
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        vm.NotchExportSummary = "Previous export completed.";
        vm.NotchExportProgress = 1.0;
        vm.NotchExportProgressText = "Previous export completed.";
        var revision = vm.SimulationWorkspaceSourceRevision;

        vm.RegularMask.IsEnabled = false;

        Assert.Equal("Regular visibility mask disabled. Step 4 uses pure geometry seed.", vm.StatusText);
        Assert.Contains("loaded but disabled", vm.RegularMask.Summary, StringComparison.Ordinal);
        Assert.Empty(vm.CadPads);
        Assert.All(cadIds, id => Assert.False(vm.TryGetCadOutputFwDiffIndex(id, out _)));
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.True(vm.SimulationWorkspaceSourceRevision > revision);
    }

    private static FreeformHelperViewModel CreateViewModel()
    {
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("cov33-mask-app-", ".json")))
        {
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            RecalcBoundsOnLayerFilter = false,
            ApplyAppVisualPreferencesOnProjectLoad = false,
        };
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        return vm;
    }

    private static async Task<(FreeformHelperViewModel ViewModel, string MaskPath)> OpenMaskedWorkspaceAsync()
    {
        var vm = CreateViewModel();
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 10, 10), ("signal", 10, 0, 20, 10));
        var maskPath = TestFiles.NewTempFilePath("cov33-mask-", ".csv");
        WriteMask(maskPath);
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath);
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        Assert.True(vm.RegularMask.IsEnabled);
        Assert.Equal(2, vm.RegularPads.Count);
        return (vm, maskPath);
    }

    private static void WriteMask(string path) =>
        TestFiles.WriteText(path, "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,10,0\n");
}
