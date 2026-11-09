// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [AvaloniaFact]
    public async Task ScanOrderRebuild_FirstGridFitsOnceAndMissingDxfSourceClearsCanvasAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        Assert.Empty(vm.RegularPads);
        var originalScanOrder = vm.SelectedScanOrder;

        vm.SelectedScanOrder = ScanOrder.RightToLeft_TopToBottom;
        await WaitForGridRebuildAsync(vm);

        Assert.NotEmpty(vm.RegularPads);
        Assert.Equal(1, host.FitToContentCount);
        Assert.True(host.InvalidateCount > 0);
        var regularId = vm.RegularPads[0].Index;
        vm.HighlightRegularPadGroup([regularId]);
        Assert.Equal(regularId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(regularId, Assert.Single(host.SelectedRegularIds));
        host.ResetCounters();

        vm.SelectedScanOrder = originalScanOrder;
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(0, host.FitToContentCount);
        Assert.True(host.InvalidateCount > 0);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(host.SelectedRegularIds);
        vm.HighlightRegularPadGroup([vm.RegularPads[0].Index]);
        host.ResetCounters();

        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(vm.RegularPads);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal("DXF not loaded; unable to use DXF layer regular source.", vm.StatusText);
        Assert.Equal("Load DXF first.\nThen select a regular source layer.", vm.CanvasHintText);
        Assert.True(host.InvalidateCount > 0);
        Assert.Equal(0, host.FitToContentCount);
    }

    [AvaloniaFact]
    public async Task LoadProject_UnknownNumericExportType_KeepsChosenFormatAndStillAcceptsKnownTypeAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel();
        var selected = vm.NotchExportFileTypeOptions.Single(
            static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        vm.SelectedNotchExportFileTypeOption = selected;
        var project = new ProjectFile();
        project.Settings.View.AutoReplayStep2AfterProjectLoad = false;
        project.UiSnapshot.View.NotchExportFileType = "999";
        var path = files.PathFor("project.json");
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(selected, vm.SelectedNotchExportFileTypeOption);
        Assert.False(vm.HasUnsavedChanges);
        project.UiSnapshot.View.NotchExportFileType = "Csv";
        await new JsonProjectStore().SaveAsync(path, project, TestContext.Current.CancellationToken);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Csv, vm.SelectedNotchExportFileTypeOption.Value);
        Assert.False(vm.HasUnsavedChanges);
    }
}
