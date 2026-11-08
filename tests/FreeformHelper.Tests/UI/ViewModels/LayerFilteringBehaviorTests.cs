// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerFilteringBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LoadProject_LegacyAnchorKeepsPerIcPriorityAndHiddenLayerCharacterizationConvertsItOnSaveAsync(
        bool hasPerIcAnchor, bool hideLegacyLayer)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(includeOccupiedPad: false);
        var fixedId = vm.CadPads.Single(pad => pad.Layer == "fixed").Id;
        var repairId = vm.CadPads.Single(pad => pad.Layer == "repair").Id;
        var projectPath = TestFiles.NewTempFilePath("layer-anchor-", ".json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
        Assert.True(await vm.SaveProjectAsync());
        var project = JsonProjectStore.Load(projectPath);
        project.CadOutputFwDiffIndexAnchorCadPadId = repairId;
        project.CadOutputFwDiffIndexAnchorCadPadByIc = hasPerIcAnchor
            ? new Dictionary<int, int> { [0] = fixedId }
            : new Dictionary<int, int>();
        project.UiSnapshot.View.LayerSelections.Single(selection => selection.Name == "repair").IsSelected = !hideLegacyLayer;
        await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();

        if (hideLegacyLayer)
        {
            Assert.Empty(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas);
            Assert.True(await vm.SaveProjectAsync());
            // Characterization: saving while the legacy anchor layer is hidden already converts the legacy anchor to a per-IC anchor.
            var afterHiddenSave = JsonProjectStore.Load(projectPath);
            Assert.Null(afterHiddenSave.CadOutputFwDiffIndexAnchorCadPadId);
            var converted = Assert.Single(afterHiddenSave.CadOutputFwDiffIndexAnchorCadPadByIc);
            Assert.Equal(0, converted.Key);
            Assert.Equal(repairId, converted.Value);
            return;
        }

        var expectedId = hasPerIcAnchor ? fixedId : repairId;
        Assert.True(vm.IsCadOutputFwDiffIndexAnchorCadPad(expectedId));
        Assert.False(vm.IsCadOutputFwDiffIndexAnchorCadPad(hasPerIcAnchor ? repairId : fixedId));
        Assert.Equal(expectedId, Assert.Single(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas));
        var layer = vm.LayerToggles.Single(toggle => toggle.Name == (hasPerIcAnchor ? "fixed" : "repair"));
        vm.ApplyLayerSelection(layer, soloSelect: false);
        Assert.Empty(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas);
        vm.ApplyLayerSelection(layer, soloSelect: false);
        Assert.Equal(expectedId, Assert.Single(vm.CadOutputFwDiffIndexAnchorCadIdsForCanvas));
        Assert.True(await vm.SaveProjectAsync());
        var saved = JsonProjectStore.Load(projectPath);
        Assert.Null(saved.CadOutputFwDiffIndexAnchorCadPadId);
        var anchor = Assert.Single(saved.CadOutputFwDiffIndexAnchorCadPadByIc);
        Assert.Equal(0, anchor.Key);
        Assert.Equal(expectedId, anchor.Value);
    }

    [AvaloniaFact]
    public async Task ImportMask_RepairsDuplicateSeedWithoutCreatingManualOverrideAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(includeOccupiedPad: false);
        var fixedId = vm.CadPads.Single(pad => pad.Layer == "fixed").Id;
        var repairId = vm.CadPads.Single(pad => pad.Layer == "repair").Id;
        Assert.True(vm.TryGetCadOutputFwDiffIndex(fixedId, out var originalFixed));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var originalRepair));
        Assert.Equal(originalFixed, originalRepair);

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(fixedId, out var unchangedFixed));
        Assert.Equal(originalFixed, unchangedFixed);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var repaired));
        Assert.Equal(vm.RegularPads.Single(pad => pad.Col == 1).DiffIndex, repaired);
        Assert.NotEqual(originalRepair, repaired);
        Assert.Null(vm.GetCadOutputFwDiffIndexOverride(repairId));
        var summary = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
        Assert.Equal("csv-constrained", summary.Mode);
        Assert.Equal(1, summary.DecisionSourceCounts["auto-repaired"]);
        Assert.Equal(1, summary.DecisionSourceCounts["seed"]);
    }

    [AvaloniaFact]
    public async Task ImportMask_RepairDoesNotTakeDiffAlreadyAssignedInSameIcAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(includeOccupiedPad: true);
        var repairId = vm.CadPads.Single(pad => pad.Layer == "repair").Id;
        var occupiedId = vm.CadPads.Single(pad => pad.Layer == "occupied").Id;
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var originalRepair));
        Assert.True(vm.TryGetCadOutputFwDiffIndex(occupiedId, out var originalOccupied));
        Assert.NotEqual(originalRepair, originalOccupied);

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var unchangedRepair));
        Assert.Equal(originalRepair, unchangedRepair);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(occupiedId, out var unchangedOccupied));
        Assert.Equal(originalOccupied, unchangedOccupied);
        var summary = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
        Assert.Equal("csv-constrained", summary.Mode);
        Assert.False(summary.DecisionSourceCounts.ContainsKey("auto-repaired"));
        Assert.Equal(3, summary.DecisionSourceCounts["seed"]);
    }

    [AvaloniaFact]
    public async Task ImportMask_ManualOverrideIsNotReplacedByAutomaticRepairAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await OpenWorkspaceAsync(includeOccupiedPad: false);
        var repairId = vm.CadPads.Single(pad => pad.Layer == "repair").Id;
        var manualDiff = vm.RegularPads.Single(pad => pad.Col == 0).DiffIndex;
        vm.SetCadOutputFwDiffIndexOverride(repairId, manualDiff);

        await vm.ImportRegularVisibilityMaskCommand.ExecuteAsync(null);

        Assert.True(vm.RegularVisibilityMaskUi.IsEnabled);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(repairId, out var actual));
        Assert.Equal(manualDiff, actual);
        Assert.Equal(manualDiff, vm.GetCadOutputFwDiffIndexOverride(repairId));
        var summary = vm.GetCadOutputFwDiffAssignmentDecisionSummarySnapshot();
        Assert.Equal("csv-constrained", summary.Mode);
        Assert.Equal(1, summary.DecisionSourceCounts["manual-override"]);
        Assert.False(summary.DecisionSourceCounts.ContainsKey("auto-repaired"));
    }

    private static async Task<FreeformHelperViewModel> OpenWorkspaceAsync(bool includeOccupiedPad)
    {
        var rectangles = new List<(string Layer, double MinX, double MinY, double MaxX, double MaxY)>
        {
            ("fixed", 0, 0, 10, 10),
            ("repair", 0, 0, 19, 10),
        };
        if (includeOccupiedPad)
        {
            rectangles.Add(("occupied", 10, 0, 20, 10));
        }

        var dxfPath = FreeformHelperViewModelTests.CreateTempDxfFile(rectangles.ToArray());
        var maskPath = TestFiles.WriteTempCsv("Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData,10,10\n");
        var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(TestFiles.NewTempFilePath("layer-app-", ".json")))
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            PickOpenRegularVisibilityMaskPathAsync = () => Task.FromResult<string?>(maskPath),
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 10,
            PanelBiasX = 0,
            PanelBiasY = 0,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            SelectedScanOrder = ScanOrder.LeftToRight_TopToBottom,
            RecalcBoundsOnLayerFilter = false,
            CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect,
            ApplyAppVisualPreferencesOnProjectLoad = false,
        };
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await vm.WaitForGridRebuildIdleAsync();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Equal(rectangles.Count, vm.CadPads.Count);
        Assert.Equal(2, vm.RegularPads.Count);
        Assert.NotEmpty(vm.MatchLinksForCanvas);
        return vm;
    }
}
