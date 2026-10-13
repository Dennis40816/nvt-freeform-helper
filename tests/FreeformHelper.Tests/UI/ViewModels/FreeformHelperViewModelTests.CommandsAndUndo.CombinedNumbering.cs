using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public async Task CombineSelectedCadPadsCommand_AfterClearingTheGroup_ContinuesOutputIdNumbering()
    {
        var vm = new FreeformHelperViewModel();
        InitializeDxfEditCadState(vm, [CreateRectCad(1, 0, 0, 10, 10), CreateRectCad(2, 8, 0, 18, 10)]);
        await SelectCadPadsAsync(vm, [1, 2]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        var firstId = Assert.Single(GetCombinedCadPadIds(vm));
        await SelectCadPadsAsync(vm, [firstId]);
        vm.ClearCombinedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [1, 2]);

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal(firstId + 1, Assert.Single(GetCombinedCadPadIds(vm)));
    }


    [Fact]
    public async Task CombineSelectedCadPadsCommand_AfterResetAllDxfEdits_RestartsOutputIdNumbering()
    {
        var vm = new FreeformHelperViewModel();
        InitializeDxfEditCadState(vm, [CreateRectCad(1, 0, 0, 10, 10), CreateRectCad(2, 8, 0, 18, 10)]);
        await SelectCadPadsAsync(vm, [1, 2]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        var firstId = Assert.Single(GetCombinedCadPadIds(vm));
        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        await SelectCadPadsAsync(vm, [1, 2]);

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal(firstId, Assert.Single(GetCombinedCadPadIds(vm)));
    }


    [Fact]
    public async Task UndoCommand_AfterSecondCombine_RestoresOutputIdNumbering()
    {
        var vm = new FreeformHelperViewModel();
        InitializeDxfEditCadState(vm,
        [
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
            CreateRectCad(3, 40, 0, 50, 10),
            CreateRectCad(4, 48, 0, 58, 10),
        ]);
        await SelectCadPadsAsync(vm, [1, 2]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [3, 4]);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        var bothIds = GetCombinedCadPadIds(vm).OrderBy(static id => id).ToArray();
        vm.UndoCommand.Execute(null);
        await SelectCadPadsAsync(vm, [3, 4]);

        vm.CombineSelectedCadPadsCommand.Execute(null);

        Assert.Equal(bothIds, GetCombinedCadPadIds(vm).OrderBy(static id => id).ToArray());
    }
}
