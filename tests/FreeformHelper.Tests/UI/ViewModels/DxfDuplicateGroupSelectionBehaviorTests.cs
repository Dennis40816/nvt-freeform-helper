// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class DxfDuplicateGroupSelectionBehaviorTests
{
    [AvaloniaFact]
    public async Task SelectDuplicateGroup_FocusesItsPadsAndPreservesSelectionWhenTheGroupIsHiddenAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var path = CreateTempDxfFile(
            ("signal", 0, 0, 15, 10), ("signal", 0, 0, 14, 10), ("other", 20, 0, 30, 10));
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            vm.ActiveAreaWidth = 30;
            vm.ActiveAreaHeight = 10;
            vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
            vm.GridPaddingPercent = 0;
            vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
            vm.NotchPreviewAutoPlayEnabled = false;
            var cascade = Assert.Single(vm.CascadeIcSettings);
            cascade.XChannels = 3;
            cascade.YChannels = 1;
            await WaitForGridRebuildAsync(vm);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            await vm.MatchCommand.ExecuteAsync(null);
            var group = Assert.Single(vm.Step4DuplicateDiffGroups);
            var expectedIds = vm.CadPads.Where(static pad => pad.Layer == "signal")
                .Select(static pad => pad.Id).Order().ToArray();
            Assert.Equal(expectedIds, group.CadPadIds.Order());
            var otherId = Assert.Single(vm.CadPads, static pad => pad.Layer == "other").Id;
            var regularIndex = vm.RegularPads[0].Index;
            vm.ApplyCanvasSelection([otherId], [regularIndex]);

            vm.SelectStep4DuplicateDiffGroupCommand.Execute(group);

            Assert.Equal(expectedIds, vm.SelectedCadPadIds.Order());
            Assert.Empty(vm.SelectedRegularPadIndices);
            Assert.Equal($"Duplicate CAD Output FW Diff focused: IC{group.IcIndex + 1}/diff{group.DiffIndex} (2 CAD pad(s)).",
                vm.StatusText);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            vm.ApplyCanvasSelection([otherId], [regularIndex]);

            vm.SelectStep4DuplicateDiffGroupCommand.Execute(group);

            Assert.Equal(otherId, Assert.Single(vm.SelectedCadPadIds));
            Assert.Equal(regularIndex, Assert.Single(vm.SelectedRegularPadIndices));
            Assert.Equal($"Duplicate CAD Output FW Diff IC{group.IcIndex + 1}/diff{group.DiffIndex} is not visible.",
                vm.StatusText);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
