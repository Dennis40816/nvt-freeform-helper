// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ViewModelOperationsBehaviorTests
{
    [AvaloniaFact]
    public void ClearSelectionCommand_RejectedPendingEdits_PreservesBothSelections()
    {
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        vm.CadPads = new ObservableCollection<CadPad> { TestGeometryFactory.CreateCadPad(1, "signal", 0, 0, 10, 10) };
        vm.RegularPads = new ObservableCollection<RegularPad> { TestGeometryFactory.CreateRegularPad(0, 0, 2, 0, 0, 10, 10) };
        vm.ApplyCanvasSelection([1], [2]);
        Assert.Equal(1, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(2, Assert.Single(vm.SelectedRegularPadIndices));
        var checks = 0;
        vm.EnsureNoPendingEdits = () =>
        {
            checks++;
            return false;
        };

        vm.ClearSelectionCommand.Execute(null);

        Assert.Equal(1, checks);
        Assert.Equal(1, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(2, Assert.Single(vm.SelectedRegularPadIndices));
        vm.EnsureNoPendingEdits = () => true;
        vm.ClearSelectionCommand.Execute(null);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.SelectedRegularPadIndices);
    }

    [AvaloniaFact]
    public async Task LoadProject_UnsetViewFields_RestoresVisibleLinesAndChannelPixelDimensionsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var project = new ProjectFile();
        project.Settings.Grid.XChannels = 2;
        project.Settings.Grid.YChannels = 3;
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        project.UiSnapshot.View = new UiViewSnapshot
        {
            CadLineOpacity = 0,
            RegularLineOpacity = -0.5,
            RegularSelectedFillOpacity = 0,
            NotchExportFileType = string.Empty,
            CoordinatePixelWidth = 0,
            CoordinatePixelHeight = -1,
        };
        await new JsonProjectStore().SaveAsync(files.PathFor("project.json"), project, TestContext.Current.CancellationToken);
        var vm = files.CreateViewModel();
        vm.CadLineOpacity = 0.25m;
        vm.RegularLineOpacity = 0.5m;
        vm.RegularSelectedFillOpacity = 0.42m;
        var exportOption = vm.NotchExportFileTypeOptions.Single(option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        vm.SelectedNotchExportFileTypeOption = exportOption;

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(6, vm.RegularPads.Count);
        Assert.Equal(1m, vm.CadLineOpacity);
        Assert.Equal(1m, vm.RegularLineOpacity);
        Assert.Equal(0.42m, vm.RegularSelectedFillOpacity);
        Assert.Equal(exportOption, vm.SelectedNotchExportFileTypeOption);
        Assert.Equal(2m, vm.CoordinatePixelWidth);
        Assert.Equal(3m, vm.CoordinatePixelHeight);
        Assert.False(vm.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public async Task RebuildGrid_DeletedBoundsLayerWithAutoHiddenDuplicates_FallsBackToActiveCadBoundsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 10, 10), ("signal", 0, 0, 10, 10), ("bounds", 30, 0, 40, 10));
        var vm = files.CreateViewModel();
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 1;
        cascade.YChannels = 1;
        vm.GridAlignmentMode = GridAlignmentMode.FromCadBounds;
        vm.GridPaddingPercent = 0;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal(1, vm.DuplicateCadPadCount);
        var boundPad = Assert.Single(vm.CadPads, pad => pad.Layer == "bounds");
        var signalPad = Assert.Single(vm.CadPads, pad => pad.Layer == "signal");
        vm.SelectedBoundLayerOption = vm.BoundLayerOptions.Single(option => option.Name == "bounds");
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal(boundPad.Bounds, Assert.Single(vm.RegularPads).Bounds);
        vm.ApplyCanvasSelection([boundPad.Id], []);

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal("bounds", vm.SelectedBoundLayerOption.Name);
        Assert.Equal(signalPad.Id, Assert.Single(vm.CadPads).Id);
        Assert.Equal(signalPad.Bounds, Assert.Single(vm.RegularPads).Bounds);
        Assert.Equal(1, vm.DeletedCadPadCount);
        vm.UndoCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal(boundPad.Bounds, Assert.Single(vm.RegularPads).Bounds);
        Assert.Equal(0, vm.DeletedCadPadCount);
    }
}
