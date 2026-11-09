// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RegularVisibilityMaskClearBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClearEmbeddedOnlyMask_RemovesSavedPayloadAndRestoresGeometryAsync(bool enabled)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new IoFailureScope();
        var vm = await LoadEmbeddedOnlyMaskAsync(files, enabled);
        var cad = Assert.Single(vm.CadPads);
        Assert.Equal(enabled, vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.True(vm.RegularVisibilityMaskForTests.Mask.HasLoadedMask);
        Assert.Null(vm.RegularVisibilityMaskForTests.SourcePath);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(cad.Id, out var beforeClear));
        Assert.Equal(enabled ? 1 : 0, beforeClear);
        vm.NotchExportSummary = "Previous export completed.";
        vm.NotchExportProgress = 1.0;
        vm.NotchExportProgressText = "Previous export completed.";

        vm.ClearRegularVisibilityMaskCommand.Execute(null);

        Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Contains("not loaded", vm.RegularVisibilityMaskUi.Summary, StringComparison.Ordinal);
        Assert.Equal("Regular visibility mask cleared. Step 4 uses pure geometry seed.", vm.StatusText);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(cad.Id, out var afterClear));
        Assert.Equal(0, afterClear);
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        var clearedPath = files.PathFor("cleared.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(clearedPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);

        Assert.True(await vm.SaveProjectAsync());

        var saved = JsonProjectStore.Load(clearedPath);
        Assert.NotNull(saved.EmbeddedDxf);
        Assert.Null(saved.EmbeddedRegularVisibilityMask);
        Assert.Null(saved.EmbeddedRegularVisibilityMaskName);
        Assert.Null(saved.UiSnapshot.Import.RegularVisibilityMaskSourcePath);
        Assert.False(saved.UiSnapshot.Import.UseRegularVisibilityMask);
        var reloaded = files.CreateViewModel();
        reloaded.PickLoadProjectPathAsync = () => Task.FromResult<string?>(clearedPath);

        await reloaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(reloaded);

        Assert.Single(reloaded.CadPads);
        Assert.False(reloaded.RegularVisibilityMaskUi.IsEnabled);
        Assert.Contains("not loaded", reloaded.RegularVisibilityMaskUi.Summary, StringComparison.Ordinal);
        Assert.True(reloaded.TryGetCadOutputFwDiffIndex(cad.Id, out var reloadedDiff));
        Assert.Equal(0, reloadedDiff);
    }

    [AvaloniaFact]
    public async Task ClearEnabledMaskWithoutCad_DisablesMaskAndNotifiesSimulationAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.CadPads);
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        var sourceRevision = vm.SimulationWorkspaceSourceRevision;

        vm.ClearRegularVisibilityMaskCommand.Execute(null);

        Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.Contains("not loaded", vm.RegularVisibilityMaskUi.Summary, StringComparison.Ordinal);
        Assert.Equal("Regular visibility mask cleared. Step 4 uses pure geometry seed.", vm.StatusText);
        Assert.True(vm.SimulationWorkspaceSourceRevision > sourceRevision);
        Assert.Empty(vm.CadPads);
    }

    [AvaloniaFact]
    public async Task ClearMaskTwice_SecondClearPreservesUnrelatedStatusAndSimulationSourceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        await WaitForGridRebuildAsync(vm);
        vm.RegularVisibilityMaskUi.IsEnabled = true;
        vm.ClearRegularVisibilityMaskCommand.Execute(null);
        vm.ClearStep5ResultCommand.Execute(null);
        Assert.Equal("Step 5 result cleared.", vm.StatusText);
        var sourceRevision = vm.SimulationWorkspaceSourceRevision;
        var summary = vm.RegularVisibilityMaskUi.Summary;

        vm.ClearRegularVisibilityMaskCommand.Execute(null);

        Assert.Equal("Step 5 result cleared.", vm.StatusText);
        Assert.Equal(sourceRevision, vm.SimulationWorkspaceSourceRevision);
        Assert.Equal(summary, vm.RegularVisibilityMaskUi.Summary);
        Assert.False(vm.RegularVisibilityMaskUi.IsEnabled);
    }

    private static async Task<FreeformHelperViewModel> LoadEmbeddedOnlyMaskAsync(IoFailureScope files, bool enabled)
    {
        var vm = files.CreateViewModel();
        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.PanelBiasX = 0;
        vm.PanelBiasY = 0;
        vm.GridPaddingPercent = 0;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await WaitForGridRebuildAsync(vm);
        var dxfPath = CreateTempDxfFile(("signal", 0, 0, 15, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        var maskPath = files.PathFor("SeeRegular.csv");
        TestFiles.WriteText(maskPath, "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,10\n");
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        var projectPath = files.PathFor("embedded.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        var project = JsonProjectStore.Load(projectPath);
        Assert.NotNull(project.EmbeddedRegularVisibilityMask);
        project.LastDxfPath = null;
        project.UiSnapshot.Import.RegularVisibilityMaskSourcePath = null;
        project.UiSnapshot.Import.UseRegularVisibilityMask = enabled;
        await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);
        var loaded = files.CreateViewModel();
        loaded.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        await loaded.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(loaded);
        Assert.Equal(2, loaded.RegularPads.Count);
        return loaded;
    }
}
