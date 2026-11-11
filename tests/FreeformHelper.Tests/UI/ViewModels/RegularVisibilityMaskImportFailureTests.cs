// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RegularVisibilityMaskImportFailureTests
{
    [AvaloniaFact]
    public async Task ImportUnreadableMaskKeepsDisabledEmbeddedMaskPadsAndSavedStateAsync()
    {
        using var files = new IoFailureScope();
        var saver = await CreateMaskedViewModelAsync(files);
        var projectPath = files.PathFor("project.json");
        saver.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        saver.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await saver.SaveProjectAsync());
        var vm = files.CreateViewModel();
        Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        vm.RegularMask.IsEnabled = false;
        var previousMask = vm.RegularMask.Workflow.Mask;
        var previousPath = vm.RegularMask.Workflow.SourcePath;
        var previousEmbedded = vm.RegularMask.Workflow.EmbeddedMask;
        var previousName = vm.RegularMask.Workflow.EmbeddedName;
        Assert.True(previousMask.HasLoadedMask);
        Assert.NotNull(previousEmbedded);
        var cad = vm.CadPads.ToArray();
        var regular = vm.RegularPads.ToArray();
        var grid = FreeformHelperViewModelPrivateAccess.Grid(vm);
        new FileInfo(previousPath!).Delete();
        var unreadablePath = files.PathFor("locked-mask.csv");
        TestFiles.WriteText(unreadablePath, "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,0,1\n");
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(unreadablePath);

        using (new FileStream(unreadablePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        }

        Assert.StartsWith("Regular visibility mask: failed to load locked-mask.csv. Ignored.", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(vm.StatusText, vm.RegularMask.Summary);
        Assert.False(vm.RegularMask.IsEnabled);
        Assert.Same(previousMask, vm.RegularMask.Workflow.Mask);
        Assert.Equal(previousPath, vm.RegularMask.Workflow.SourcePath);
        Assert.Same(previousEmbedded, vm.RegularMask.Workflow.EmbeddedMask);
        Assert.Equal(previousName, vm.RegularMask.Workflow.EmbeddedName);
        Assert.Equal(cad, vm.CadPads);
        Assert.Equal(regular, vm.RegularPads);
        Assert.Same(grid, FreeformHelperViewModelPrivateAccess.Grid(vm));
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        var reloaded = files.CreateViewModel();
        Assert.True((await reloaded.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        Assert.False(reloaded.RegularMask.IsEnabled);
        Assert.Equal(previousPath, reloaded.RegularMask.Workflow.SourcePath);
        Assert.Equal(previousEmbedded, reloaded.RegularMask.Workflow.EmbeddedMask);
        Assert.Equal(previousName, reloaded.RegularMask.Workflow.EmbeddedName);
        Assert.True(reloaded.RegularMask.Workflow.Mask.HasLoadedMask);
        Assert.Equal(0, Assert.Single(reloaded.RegularMask.Workflow.Mask.ActiveRegularPadIds));
        Assert.Equal(1, Assert.Single(reloaded.RegularMask.Workflow.Mask.InactiveRegularPadIds));
    }

    [AvaloniaFact]
    public async Task ImportMissingMaskReportsFailureAndKeepsPreviousEnabledMaskAsync()
    {
        using var files = new IoFailureScope();
        var vm = await CreateMaskedViewModelAsync(files);
        var previousMask = vm.RegularMask.Workflow.Mask;
        var previousPath = vm.RegularMask.Workflow.SourcePath;
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(files.PathFor("missing-mask.csv"));

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.StartsWith("Regular visibility mask: failed to load missing-mask.csv.", vm.StatusText, StringComparison.Ordinal);
        Assert.True(vm.RegularMask.IsEnabled);
        Assert.Same(previousMask, vm.RegularMask.Workflow.Mask);
        Assert.Equal(previousPath, vm.RegularMask.Workflow.SourcePath);
    }

    private static async Task<FreeformHelperViewModel> CreateMaskedViewModelAsync(IoFailureScope files)
    {
        var vm = files.CreateViewModel();
        vm.CascadeNum = 1;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await WaitForGridRebuildAsync(vm);
        var dxfPath = CreateTempDxfFile(("PREVIOUS", 0, 0, 10, 10), ("PREVIOUS", 20, 0, 30, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(2, vm.RegularPads.Count);
        var maskPath = files.PathFor("SeeRegular.csv");
        TestFiles.WriteText(maskPath, "Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,1,0\n");
        vm.PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath);
        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);
        Assert.True(vm.RegularMask.IsEnabled);
        Assert.Equal(0, Assert.Single(vm.RegularMask.Workflow.Mask.ActiveRegularPadIds));
        return vm;
    }
}
