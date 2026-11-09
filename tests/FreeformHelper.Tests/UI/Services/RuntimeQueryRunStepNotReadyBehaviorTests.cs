// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class RuntimeQueryRunStepNotReadyBehaviorTests
{
    [AvaloniaFact]
    public async Task RunStep3_WithAGridButNoSelectedCad_ReturnsStepNotReadyWithTheSelectionHintAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var path = CreateTempDxfFile(("signal", 0, 0, 20, 10));
        try
        {
            var vm = shell.FreeformHelper;
            vm.NotchPreviewAutoPlayEnabled = false;
            vm.ShowNotchCanvasPreview = true;
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
            vm.ClearSelectionCommand.Execute(null);
            Assert.NotEmpty(vm.CadPads);
            Assert.NotEmpty(vm.RegularPads);
            var useCase = new RuntimeQueryUseCase(shell);

            var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
                RuntimeQueryProtocol.Version, "run-step", new Dictionary<string, string> { ["step"] = "3" }));

            Assert.False(response.Ok);
            Assert.Equal("STEP_NOT_READY", response.Error?.Code);
            Assert.Equal("Notch 2.2 preview: select at least one CAD pad.", response.Error?.Message);
            Assert.False(vm.GetWorkflowStateSnapshot().HasStep3Result);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
