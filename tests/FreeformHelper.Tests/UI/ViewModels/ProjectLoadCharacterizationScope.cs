// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Owns synthetic load fixtures and stops app persistence timers before deleting their isolated stores.</summary>
internal sealed class ProjectLoadCharacterizationScope : IDisposable
{
    private readonly LoadingModeTestScope _files = new();
    private readonly List<FreeformHelperViewModel> _viewModels = [];

    public AppGeneralSettingsStore AppStore => _files.AppStore;

    public string PathFor(string name) => _files.PathFor(name);

    public FreeformHelperViewModel CreateViewModel()
    {
        var vm = _files.CreateViewModel();
        vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        _viewModels.Add(vm);
        return vm;
    }

    public async Task<FreeformHelperViewModel> CreateSavedCadAsync(string name)
    {
        var vm = CreateViewModel();
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = 2;
            cascade.YChannels = 1;
        }

        vm.ActiveAreaWidth = 10;
        vm.ActiveAreaHeight = 20;
        vm.GridPaddingPercent = 0;
        vm.MatchThreshold = 0.27m;
        vm.AutoReplayStep2AfterProjectLoad = false;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(PathFor(name));
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Single(vm.CadPads);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        Assert.False(vm.HasUnsavedChanges);
        return vm;
    }

    public static async Task HideOnlyPadAsync(FreeformHelperViewModel vm)
    {
        var id = Assert.Single(vm.CadPads).Id;
        vm.ApplyCanvasSelection([id], []);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
    }

    public void Dispose()
    {
        foreach (var vm in _viewModels)
        {
            vm.AppGeneralPersistenceForTests.PersistTimer?.Stop();
        }

        _files.Dispose();
    }
}
