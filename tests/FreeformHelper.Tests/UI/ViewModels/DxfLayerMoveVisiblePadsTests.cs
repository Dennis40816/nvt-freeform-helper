using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfLayerMoveVisiblePadsTests
{
    [Fact]
    public async Task MoveSelectedCadPadsToLayerCommand_UpdatesVisiblePadLayerAndVisibleExportAsync()
    {
        var dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 10, 10), ("signal", 20, 0, 30, 10));
        var outputPath = TestFiles.NewTempFilePath("freeformhelper-moved-layer-", ".dxf");
        try
        {
            var vm = new FreeformHelperViewModel
            {
                PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
                PickSaveExportPathAsync = (_, _) => Task.FromResult<string?>(outputPath),
            };
            await vm.OpenDxfCommand.ExecuteAsync(null);
            var ids = vm.CadPads.OrderBy(static pad => pad.Id).Select(static pad => pad.Id).ToArray();
            await FreeformHelperViewModelTests.SelectCadPadsAsync(vm, [ids[0]]);
            vm.DxfEditTargetLayerName = "moved";

            vm.MoveSelectedCadPadsToLayerCommand.Execute(null);

            Assert.Equal("moved", vm.CadPads.Single(pad => pad.Id == ids[0]).Layer);
            Assert.Equal("signal", vm.CadPads.Single(pad => pad.Id == ids[1]).Layer);
            await vm.ExportDxfVisibleCommand.ExecuteAsync(null);
            var exported = DxfPadImporter.Import(outputPath).Pads;
            Assert.Equal(0d, Assert.Single(exported, static pad => pad.Layer == "moved").Bounds.MinX, 6);
            Assert.Equal(20d, Assert.Single(exported, static pad => pad.Layer == "signal").Bounds.MinX, 6);
        }
        finally
        {
            File.Delete(dxfPath);
            File.Delete(outputPath);
        }
    }
}
