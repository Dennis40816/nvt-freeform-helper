// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.ViewModels;

namespace FreeformHelper.Tests;

internal sealed class DxfEditingBehaviorTestScope : IDisposable
{
    private readonly LoadingModeTestScope _settings = new();
    private readonly List<FileInfo> _inputs = [];

    public FreeformHelperViewModel CreateViewModel()
    {
        var vm = _settings.CreateViewModel();
        vm.RecalcBoundsOnLayerFilter = false;
        return vm;
    }

    public async Task OpenAsync(
        FreeformHelperViewModel vm,
        params (string Layer, double MinX, double MinY, double MaxX, double MaxY)[] pads)
    {
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(pads);
        _inputs.Add(new FileInfo(path));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
    }

    public void Dispose()
    {
        foreach (var input in _inputs)
        {
            input.Delete();
        }

        _settings.Dispose();
    }
}
