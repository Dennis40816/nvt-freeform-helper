// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what clearing the first group does after a combined pad was combined again (issue #64,
/// inventory finding EDIT-D02). Pads a and b become X, then X and c become Y. The change list
/// resolves X to its own group, so clearing that group removes X while group two still lists X as
/// a source, and a and b show again under Y, which still covers them. This test pins only what is
/// visible: the combined count and the visible pads. It describes the current result, not the
/// wanted one. A fix that blocks the clear or clears the dependent group changes the expected
/// values here and must say so in its pull request. A fix that only removes X from the source
/// list of group two keeps the visible result and needs its own test.
/// </summary>
[Collection("HeadlessUiSerial")]
public sealed class DxfNestedCombineClearCharacterizationTests
{
    [Fact]
    public async Task ClearInnerCombinedGroup_WhileOuterGroupUsesIt_ShowsTheSourcePadsUnderTheOuterPad()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20), ("signal", 8, 0, 18, 20), ("signal", 16, 0, 26, 20));
        try
        {
            var vm = new FreeformHelperViewModel { PickOpenDxfPathAsync = () => Task.FromResult<string?>(path) };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var firstId = vm.CadPads[0].Id;
            var secondId = vm.CadPads[1].Id;
            var thirdId = vm.CadPads[2].Id;
            await SelectCadPadsAsync(vm, [firstId, secondId]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var innerId = Assert.Single(vm.CadPads, pad => pad.Id != thirdId).Id;
            await SelectCadPadsAsync(vm, [innerId, thirdId]);
            vm.CombineSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            var outerId = Assert.Single(vm.CadPads).Id;
            Assert.Equal(2, vm.CombinedCadPadCount);
            DxfEditChangeListViewModel? list = null;
            vm.OpenDxfEditChangeListAsync = viewModel =>
            {
                list = viewModel;
                return Task.CompletedTask;
            };
            await vm.OpenCombinedDxfEditChangeListCommand.ExecuteAsync(null);
            var changeList = Assert.IsType<DxfEditChangeListViewModel>(list);
            var innerRow = Assert.Single(changeList.Rows, row => row.CadPadId == innerId);

            changeList.SelectedEntry = innerRow;
            changeList.ApplySelectedEntryCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);

            // The inner group is gone, but the outer pad still covers its sources, so the
            // sources are visible next to the outer pad. The outer group still lists the removed
            // inner pad as a source.
            Assert.Equal(1, vm.CombinedCadPadCount);
            var visibleIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
            Assert.Equal(new[] { firstId, secondId, outerId }.Order().ToArray(), visibleIds);
            Assert.DoesNotContain(innerId, visibleIds);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
