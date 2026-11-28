// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfEditBadgesAfterLoadWithoutDxfBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadProject_WithoutDxf_ClearsTheEditBadgesOfThePreviousDxfAsync(bool combine)
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = fixture.CreateViewModel();
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        var path = CreateTempDxfFile(
            ("signal", 0, 0, 10, 10), ("signal", 0, 0, 10, 10), ("signal", 20, 0, 30, 10));
        try
        {
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await SelectCadPadsAsync(vm, vm.CadPads.Select(static pad => pad.Id).ToArray());
            if (combine)
            {
                vm.CombineSelectedCadPadsCommand.Execute(null);
            }
            else
            {
                vm.DeleteSelectedCadPadsCommand.Execute(null);
            }

            await WaitForGridRebuildAsync(vm);
            Assert.Equal(1, vm.DuplicateCadPadCount);
            Assert.True(vm.DeletedCadPadCount > 0 || vm.CombinedCadPadCount > 0);

            await vm.LoadProjectCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);

            Assert.Empty(vm.CadPads);
            Assert.Equal(0, vm.DeletedCadPadCount);
            Assert.Equal(0, vm.CombinedCadPadCount);
            Assert.Equal(0, vm.DuplicateCadPadCount);
            Assert.False(vm.HasDeletedCadPads);
            Assert.False(vm.HasCombinedCadPads);
            Assert.False(vm.HasDuplicateCadPads);
        }
        finally
        {
            new FileInfo(path).Delete();
        }
    }
}
