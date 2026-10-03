using System.Collections.ObjectModel;
using System.Reflection;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void Step3Preview_ToRegularLabelsRemainVisible_WhenToFullOverlayIsDisabled()
    {
        var cad = CreateRectCad(101, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 1001, 0, 0, 10, 10);
        var cadPads = new ObservableCollection<CadPad> { cad };
        var regularPadCollection = new ObservableCollection<RegularPad> { regular };
        var importedCadPads = new[] { cad };
        var previewGridXEdges = new[] { 0.0, 10.0 };
        var previewGridYEdges = new[] { 0.0, 10.0 };
        var previewRegularPads = new[] { regular };
        var selectedCadIds = new[] { 101 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPadCollection,
            EnableToFull = false,
            ShowNotchCanvasPreview = true,
            ShowNotchToRegularLabels = true,
            NotchPreviewVisualizationStep = 3m,
            NotchPreviewAutoPlayEnabled = false,
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);

        cadField!.SetValue(vm, new CadPadSet(importedCadPads));
        gridField!.SetValue(
            vm,
            new RegularGrid(
                rows: 1,
                cols: 1,
                xEdges: previewGridXEdges,
                yEdges: previewGridYEdges,
                pads: previewRegularPads));

        SetPadMatchResult(vm, cadPadId: 101, regularPadId: 1001);
        vm.ApplyCanvasSelection(selectedCadIds, Array.Empty<int>());

        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.IsNotchToRegularPreviewVisible);
        Assert.False(vm.IsNotchCanvasPreviewVisible);
        Assert.False(vm.CanShowToFullPreviewToggle);
    }


    [Fact]
    public void Step3Preview_EnableToFullChange_RecomputesPreviewImmediately()
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        Assert.NotEmpty(vm.NotchCanvasPreviewItems);
        Assert.True(vm.NotchCanvasPreviewItems[0].IsToFullEnabled);

        vm.EnableToFull = false;

        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.IsNotchToRegularPreviewVisible);
        Assert.False(vm.IsNotchCanvasPreviewVisible);
    }


    [Fact]
    public void Step3Preview_SettingsWindowSave_RecomputesPreviewImmediately()
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        Assert.NotEmpty(vm.NotchCanvasPreviewItems);
        Assert.True(vm.NotchCanvasPreviewItems[0].IsToFullEnabled);

        var settings = vm.CreateSettingsWindowViewModel();
        settings.EnableToFull = false;
        settings.SaveCommand.Execute(null);

        Assert.Empty(vm.NotchCanvasPreviewItems);
        Assert.False(vm.IsNotchToRegularPreviewVisible);
        Assert.False(vm.IsNotchCanvasPreviewVisible);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Step3Preview_SettingsWindowSave_TargetCoverageRequestInvalidatesFinalProjectionOnly(
        bool changeGuard)
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        var cad = Assert.Single(vm.CadPads);
        var revision = vm.GetNotchStep3Revision();
        var simulationRevision = vm.SimulationWorkspaceSourceRevision;
        var resolved = vm.GetCadV22ResolvedResult(cad.Id);
        Assert.NotNull(resolved);

        var settings = vm.CreateSettingsWindowViewModel();
        if (changeGuard)
        {
            settings.EnableTargetCoverageGuard = !settings.EnableTargetCoverageGuard;
        }
        else
        {
            settings.TargetCoverageCapPercent = 50m;
        }

        settings.SaveCommand.Execute(null);

        Assert.Equal(settings.EnableTargetCoverageGuard, vm.EnableTargetCoverageGuard);
        Assert.Equal(settings.TargetCoverageCapPercent, vm.TargetCoverageCapPercent);
        Assert.Equal(revision, vm.GetNotchStep3Revision());
        Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));
        Assert.Equal(simulationRevision + 1, vm.SimulationWorkspaceSourceRevision);
    }


    [Fact]
    public async Task Step1Match_WhenCadSelected_RefreshesStep3PreviewImmediately()
    {
        var cad = CreateRectCad(101, 0, 3, 3, 7);
        var regular = CreateRectRegular(0, 0, 1001, 0, 0, 10, 10);
        var cadPads = new ObservableCollection<CadPad> { cad };
        var regularPadCollection = new ObservableCollection<RegularPad> { regular };
        var importedCadPads = new[] { cad };
        var previewGridXEdges = new[] { 0.0, 10.0 };
        var previewGridYEdges = new[] { 0.0, 10.0 };
        var previewRegularPads = new[] { regular };
        var selectedCadIds = new[] { 101 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPadCollection,
            EnableToFull = true,
            ShowNotchCanvasPreview = true,
            ShowNotchToRegularLabels = true,
            NotchPreviewVisualizationStep = 3m,
            NotchPreviewAutoPlayEnabled = false,
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);

        cadField!.SetValue(vm, new CadPadSet(importedCadPads));
        gridField!.SetValue(
            vm,
            new RegularGrid(
                rows: 1,
                cols: 1,
                xEdges: previewGridXEdges,
                yEdges: previewGridYEdges,
                pads: previewRegularPads));

        vm.ApplyCanvasSelection(selectedCadIds, Array.Empty<int>());
        Assert.Empty(vm.NotchCanvasPreviewItems);

        var stepResult = await vm.RunWorkflowStepAsync(WorkflowStepId.Step1Match);

        Assert.True(stepResult.IsSupported);
        Assert.True(stepResult.IsReady);
        Assert.NotEmpty(vm.NotchCanvasPreviewItems);
        Assert.Equal(101, vm.NotchCanvasPreviewItems[0].CadPadId);
    }


    [Fact]
    public async Task Step3Preview_WhenMultipleCadSelected_ShowsAllSelectedCadToFullResults()
    {
        var cad1 = CreateRectCad(101, 0, 3, 3, 7);
        var cad2 = CreateRectCad(102, 10, 3, 13, 7);
        var regular1 = CreateRectRegular(0, 0, 1001, 0, 0, 10, 10);
        var regular2 = CreateRectRegular(0, 1, 1002, 10, 0, 20, 10);
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>(new[] { cad1, cad2 }),
            RegularPads = new ObservableCollection<RegularPad>(new[] { regular1, regular2 }),
            EnableToFull = true,
            ShowNotchCanvasPreview = true,
            ShowNotchToRegularLabels = true,
            NotchPreviewVisualizationStep = 3m,
            NotchPreviewAutoPlayEnabled = false,
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);

        cadField!.SetValue(vm, new CadPadSet(new[] { cad1, cad2 }));
        gridField!.SetValue(
            vm,
            new RegularGrid(
                rows: 1,
                cols: 2,
                xEdges: TwoPadGridXEdges,
                yEdges: SinglePadGridYEdges,
                pads: new[] { regular1, regular2 }));

        SetPadMatchResults(vm, (101, 1001), (102, 1002));
        var stepResult = await vm.RunWorkflowStepAsync(WorkflowStepId.Step1Match);
        Assert.True(stepResult.IsReady);
        vm.ApplyCanvasSelection([101, 102], Array.Empty<int>());

        vm.RefreshNotchCanvasPreviewCommand.Execute(null);

        Assert.Equal(SelectedCad101And102, vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId).ToArray());
        Assert.All(vm.NotchCanvasPreviewItems, static item => Assert.NotEmpty(item.ToFullFinalOutlinePolygons));

        Assert.Equal("Notch 2.2 preview updated: 2 selected CAD pads, To Full effective ON.", vm.StatusText);
    }


    [Fact]
    public void Step3Preview_ToRegularDisplayToggle_HidesLabelsOnly()
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        Assert.NotEmpty(vm.NotchCanvasPreviewItems);
        Assert.True(vm.IsNotchToRegularPreviewVisible);
        Assert.True(vm.IsNotchCanvasPreviewVisible);

        vm.ShowNotchToRegularLabels = false;

        Assert.False(vm.IsNotchToRegularPreviewVisible);
        Assert.True(vm.IsNotchCanvasPreviewVisible);
        Assert.True(vm.IsNotchToFullFinalPreviewVisible);
    }


    [Fact]
    public void Step3Preview_StageStateTextsFollowStageAndVisibility()
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        Assert.True(vm.CanControlNotchPreviewStage);
        Assert.Equal("Current layer: Stage 3 (Final)", vm.NotchPreviewStageSummary);
        Assert.Equal("Hidden", vm.NotchPreviewSeedLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewCandidateLayerStateText);
        Assert.Equal("Active", vm.NotchPreviewFinalLayerStateText);

        vm.NotchPreviewVisualizationStep = 2;
        Assert.Equal("Current layer: Stage 2 (Candidate)", vm.NotchPreviewStageSummary);
        Assert.Equal("Hidden", vm.NotchPreviewSeedLayerStateText);
        Assert.Equal("Active", vm.NotchPreviewCandidateLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewFinalLayerStateText);

        vm.NotchPreviewVisualizationStep = 1;
        Assert.Equal("Current layer: Stage 1 (Seed)", vm.NotchPreviewStageSummary);
        Assert.Equal("Active", vm.NotchPreviewSeedLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewCandidateLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewFinalLayerStateText);

        vm.ShowNotchCanvasPreview = false;
        Assert.False(vm.CanControlNotchPreviewStage);
        Assert.Equal("Hidden", vm.NotchPreviewSeedLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewCandidateLayerStateText);
        Assert.Equal("Hidden", vm.NotchPreviewFinalLayerStateText);
    }


    [Fact]
    public void Step3Preview_AutoPlaySummaryReflectsToggleAndInterval()
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        vm.NotchPreviewAutoPlayIntervalMs = 850m;

        vm.NotchPreviewAutoPlayEnabled = false;
        Assert.Equal("AutoPlay OFF (850 ms)", vm.NotchPreviewAutoPlaySummary);

        vm.NotchPreviewAutoPlayEnabled = true;
        Assert.Equal("AutoPlay ON (850 ms, loop 1->2->3)", vm.NotchPreviewAutoPlaySummary);
    }


    [Fact]
    public void OverlapCommands_ClearHighlightAndSelection()
    {
        var highlightedCadIds = new ObservableCollection<int> { 11, 12 };
        var vm = new FreeformHelperViewModel
        {
            DxfOverlapHighlightedCadIds = highlightedCadIds,
            HasDxfOverlapHighlights = true,
        };

        vm.SelectDxfOverlapPadsCommand.Execute(null);
        Assert.Equal("Selected: CAD=2.", vm.SelectionSummary);

        vm.ClearDxfOverlapHighlightsCommand.Execute(null);

        Assert.Empty(vm.DxfOverlapHighlightedCadIds);
        Assert.False(vm.HasDxfOverlapHighlights);
        Assert.Equal("No selection.", vm.SelectionSummary);
    }


    [Fact]
    public void SelectOverlapPads_RestoresHighlightFromLastResult()
    {
        var vm = new FreeformHelperViewModel();
        var field = typeof(FreeformHelperViewModel).GetField("_lastDxfOverlapCadIds", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(vm, new List<int> { 21, 22, 23 });

        vm.SelectDxfOverlapPadsCommand.Execute(null);

        Assert.True(vm.HasDxfOverlapHighlights);
        Assert.Equal(3, vm.DxfOverlapHighlightedCadIds.Count);
        Assert.Equal("Selected: CAD=3.", vm.SelectionSummary);
    }


    [Fact]
    public void SelectCadAreaBucketFromPad_SelectsOnlySameBucket()
    {
        var expectedCadIds = new[] { 101, 102 };
        var vm = new FreeformHelperViewModel
        {
            ColorCadByArea = true,
            AreaBucketTolerance = 0.001m,
            CadPads = new ObservableCollection<CadPad>
            {
                CreateSquareCad(101, 100.00),
                CreateSquareCad(102, 100.05),
                CreateSquareCad(103, 140.00),
            },
        };

        vm.SelectCadAreaBucketFromPad(101);

        Assert.Equal("Selected: CAD=2.", vm.SelectionSummary);
        Assert.Equal(expectedCadIds, vm.SelectedCadPadIds.OrderBy(id => id).ToArray());
    }


    [Fact]
    public void SelectCadAreaBucketFromPad_SkipsWhenColorByAreaDisabled()
    {
        var vm = new FreeformHelperViewModel
        {
            ColorCadByArea = false,
            AreaBucketTolerance = 0.001m,
            CadPads = new ObservableCollection<CadPad>
            {
                CreateSquareCad(201, 50.00),
                CreateSquareCad(202, 50.02),
            },
        };

        vm.SelectCadAreaBucketFromPad(201);

        Assert.Equal("No selection.", vm.SelectionSummary);
        Assert.Empty(vm.SelectedCadPadIds);
    }

}
