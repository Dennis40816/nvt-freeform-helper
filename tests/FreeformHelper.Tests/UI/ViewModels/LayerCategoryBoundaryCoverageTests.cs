// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerCategoryBoundaryCoverageTests
{
    [AvaloniaFact]
    public async Task CategoryEnable_MixedVisibilityAndRepeatedRequest_UndoRestoresOnlyTheActualChangesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        Assert.False(vm.CanUndo);
        vm.ApplyLayerSelection(vm.LayerToggles.Single(static toggle => toggle.Name == "beta"), soloSelect: false);
        await WaitForGridRebuildAsync(vm);
        var settings = vm.CreateSettingsWindowViewModel();
        var category = Assert.Single(settings.LayerCategoryRows);
        Assert.Equal(2, category.TotalCount);
        Assert.Equal(1, category.SelectedCount);
        vm.HasUnsavedChanges = false;
        vm.EnsureNoPendingEdits = () => false;

        category.EnableCommand.Execute(null);

        Assert.Equal("alpha", Assert.Single(vm.CadPads).Layer);
        Assert.Equal(1, Assert.Single(settings.LayerCategoryRows).SelectedCount);
        Assert.False(vm.HasUnsavedChanges);
        vm.EnsureNoPendingEdits = () => true;

        category.EnableCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(2, Assert.Single(settings.LayerCategoryRows).SelectedCount);
        Assert.True(vm.HasUnsavedChanges);
        vm.HasUnsavedChanges = false;
        category.EnableCommand.Execute(null);
        Assert.False(vm.HasUnsavedChanges);

        vm.Undo();
        await WaitForGridRebuildAsync(vm);

        Assert.Equal("alpha", Assert.Single(vm.CadPads).Layer);
        Assert.True(vm.LayerToggles.Single(static toggle => toggle.Name == "alpha").IsSelected);
        Assert.False(vm.LayerToggles.Single(static toggle => toggle.Name == "beta").IsSelected);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task CategoryCommands_NewEditLayerIsPolyline_StaleCategoryAfterReloadDoesNothingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        using var files = new LoadingModeTestScope();
        var vm = fixture.CreateViewModel();
        var originalPicker = vm.PickOpenDxfPathAsync;
        var path = CreateCategoryDxf(files);
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        var id = vm.CadPads.Single(static pad => pad.Layer == "alpha").Id;
        vm.ApplyCanvasSelection([id], []);
        vm.DxfEditNewLayerName = "edited";
        vm.CreateLayerAndMoveSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal("edited", vm.CadPads.Single(pad => pad.Id == id).Layer);
        var settings = vm.CreateSettingsWindowViewModel();
        var category = Assert.Single(settings.LayerCategoryRows,
            static row => row.Category == FreeformHelperViewModel.DxfLayerCategory.Polyline);
        var staleTextCategory = Assert.Single(settings.LayerCategoryRows,
            static row => row.Category == FreeformHelperViewModel.DxfLayerCategory.Text);
        Assert.Equal(3, category.TotalCount);

        category.DisableCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Empty(vm.CadPads);
        Assert.False(vm.LayerToggles.Single(static toggle => toggle.Name == "edited").IsSelected);
        vm.Undo();
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(2, vm.CadPads.Count);
        Assert.True(vm.LayerToggles.Single(static toggle => toggle.Name == "edited").IsSelected);

        vm.PickOpenDxfPathAsync = originalPicker;
        vm.HasUnsavedChanges = false;
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(FreeformHelperViewModel.DxfLayerCategory.Polyline,
            Assert.Single(vm.CreateSettingsWindowViewModel().LayerCategoryRows).Category);
        var visibility = vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)).ToArray();
        var canUndo = vm.CanUndo;
        vm.HasUnsavedChanges = false;

        staleTextCategory.DisableCommand.Execute(null);

        Assert.Equal(visibility, vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)));
        Assert.Equal(2, vm.CadPads.Count);
        Assert.Equal(FreeformHelperViewModel.DxfLayerCategory.Polyline, Assert.Single(settings.LayerCategoryRows).Category);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(canUndo, vm.CanUndo);
    }

    private static string CreateCategoryDxf(LoadingModeTestScope files)
    {
        var source = CreateTempDxfFile(("alpha", 0, 0, 10, 10), ("beta", 20, 0, 30, 10));
        var path = files.PathFor("categories.dxf");
        var content = TestFiles.ReadContent(source).ReplaceLineEndings("\n");
        TestFiles.WriteText(path, content.Replace("0\nENDSEC\n0\nEOF\n",
            "0\nTEXT\n8\nlabels\n1\nnote\n10\n0\n20\n0\n40\n1\n0\nENDSEC\n0\nEOF\n", StringComparison.Ordinal));
        return path;
    }
}
