// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceHeaderBehaviorCoverageTests
{
    [AvaloniaTheory]
    [InlineData(true, "Applies to the chosen layer.")]
    [InlineData(false, "Layer scope · 2 target pads; selection is unchanged.")]
    public async Task RotationLayerScope_DescribesTargetsWithoutChangingSelectionAsync(bool selectLayer, string expectedHint)
    {
        using var culture = TestCulture.UseEnUs();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 4, 2), ("signal", 8, 0, 12, 2), ("aux", 20, 0, 24, 2));
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            NotchPreviewAutoPlayEnabled = false,
        };
        vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(
            static option => option.Value == FreeformHelper.UI.CadEditing.DxfEditRotationScope.TargetLayer);
        Assert.Equal("Applies to selected CAD pads.", vm.DxfRotationScopeHint);
        vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(
            static option => option.Value == FreeformHelper.UI.CadEditing.DxfEditRotationScope.SelectedPads);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        var selected = vm.CadPads.Where(pad => pad.Layer == (selectLayer ? "signal" : "aux"))
            .Select(static pad => pad.Id).Order().ToArray();
        vm.ApplyCanvasSelection(selected, []);
        Assert.Equal("Applies to selected CAD pads.", vm.DxfRotationScopeHint);

        vm.DxfEditRotationLayerName = "signal";
        vm.SelectedDxfEditRotationScopeOption = vm.DxfEditRotationScopeOptions.Single(
            static option => option.Value == FreeformHelper.UI.CadEditing.DxfEditRotationScope.TargetLayer);

        Assert.Equal(expectedHint, vm.DxfRotationScopeHint);
        Assert.Equal(selected, vm.SelectedCadPadIds.Order().ToArray());
    }

    [AvaloniaFact]
    public async Task DxfCheckHeader_RecheckingAfterFilteringOverlap_TracksBusyWarningAndClearStatesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 10, 20), ("ground", 5, 5, 15, 25));
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(path),
            NotchPreviewAutoPlayEnabled = false,
        };
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Equal("Not checked", vm.DxfCheckHeaderText);
        var headers = new ConcurrentQueue<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.DxfCheckHeaderText))
            {
                headers.Enqueue(vm.DxfCheckHeaderText);
            }
        };

        await vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Contains("Checking…", headers);
        Assert.Equal(1, vm.DxfOverlapIssueCount);
        Assert.Equal("1 overlaps", vm.DxfCheckHeaderText);
        vm.LayerToggles.Single(static layer => layer.Name == "ground").IsSelected = false;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        await vm.CheckDxfQualityCommand.ExecuteAsync(null);

        Assert.Empty(vm.DxfOverlapIssues);
        Assert.Equal("Checked", vm.DxfCheckHeaderText);
        Assert.Equal("Checked", headers.Last());
    }

    [AvaloniaFact]
    public void CompensationModelChange_UpdatesPreviewHintForDisabledAndEnabledModes()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false };
        var hints = new ConcurrentQueue<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.ToFullPreviewToggleHint))
            {
                hints.Enqueue(vm.ToFullPreviewToggleHint);
            }
        };

        vm.SelectedNotchCompensationModelOption = vm.NotchCompensationModelOptions.Single(
            static option => option.Value == NotchCompensationModel.Disabled);

        Assert.False(vm.CanShowToFullPreviewToggle);
        Assert.Equal("Enable To Full in Settings > Step 3 to show its preview.", vm.ToFullPreviewToggleHint);
        Assert.Contains(vm.ToFullPreviewToggleHint, hints);

        vm.SelectedNotchCompensationModelOption = vm.NotchCompensationModelOptions.Single(
            static option => option.Value == NotchCompensationModel.ConservativeNoGain);

        Assert.True(vm.CanShowToFullPreviewToggle);
        Assert.Equal("Display only; show or hide the To Full overlay.", vm.ToFullPreviewToggleHint);
        Assert.Equal(vm.ToFullPreviewToggleHint, hints.Last());
    }

    [AvaloniaFact]
    public async Task ValidationTraceHint_AfterAnalyzeAndClear_TracksAvailableRowsAndGuidanceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(
            ("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10), ("signal", 0, 0, 14, 10));
        var outputPath = TestFiles.NewTempFilePath("workspace-header-", ".c");
        var vm = await FreeformHelperViewModelTests.CreateNotchExportCacheViewModelAsync(path);
        vm.NotchPreviewAutoPlayEnabled = false;
        Assert.Equal("Validation: select REG, then run Analyze.", vm.ValidationTraceHint);
        await FreeformHelperViewModelTests.ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
        vm.ApplyCanvasSelection([], [vm.RegularPads[1].RegularPadId]);
        var hints = new ConcurrentQueue<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.ValidationTraceHint))
            {
                hints.Enqueue(vm.ValidationTraceHint);
            }
        };

        vm.UseSelectedRegularForNotchValidationCommand.Execute(null);

        Assert.NotEmpty(vm.NotchValidationItems);
        Assert.Equal("Trace ready; expand a category to inspect its rows.", vm.ValidationTraceHint);
        Assert.Contains(vm.ValidationTraceHint, hints);

        vm.ClearNotchValidationCommand.Execute(null);

        Assert.Empty(vm.NotchValidationItems);
        Assert.Equal("Validation: select REG, then run Analyze.", vm.ValidationTraceHint);
        Assert.Equal(vm.ValidationTraceHint, hints.Last());
    }
}
