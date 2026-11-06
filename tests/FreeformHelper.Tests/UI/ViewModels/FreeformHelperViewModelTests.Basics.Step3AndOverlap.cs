using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(importedCadPads);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: previewGridXEdges,
            yEdges: previewGridYEdges,
            pads: previewRegularPads);

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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(importedCadPads);
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: previewGridXEdges,
            yEdges: previewGridYEdges,
            pads: previewRegularPads);

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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = new CadPadSet(new[] { cad1, cad2 });
        FreeformHelperViewModelPrivateAccess.Grid(vm) = new RegularGrid(
            rows: 1,
            cols: 2,
            xEdges: _twoPadGridXEdges,
            yEdges: _singlePadGridYEdges,
            pads: new[] { regular1, regular2 });

        SetPadMatchResults(vm, (101, 1001), (102, 1002));
        var stepResult = await vm.RunWorkflowStepAsync(WorkflowStepId.Step1Match);
        Assert.True(stepResult.IsReady);
        vm.ApplyCanvasSelection([101, 102], Array.Empty<int>());

        await vm.RunWorkflowStepAsync(WorkflowStepId.Step3NotchPreview);

        Assert.Equal(_selectedCad101And102, vm.NotchCanvasPreviewItems.Select(static item => item.CadPadId).ToArray());
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

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    public void PreviousNotchPreviewStageCommand_MovesToPreviousStageAndWrapsAtFirst(int initialStage, int expectedStage)
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        vm.NotchPreviewVisualizationStep = initialStage;
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        Assert.True(vm.CanControlNotchPreviewStage);

        vm.PreviousNotchPreviewStageCommand.Execute(null);

        Assert.Equal((decimal)expectedStage, vm.NotchPreviewVisualizationStep);
        Assert.Equal(expectedStage, vm.NotchPreviewStageIndex);
        Assert.Equal(expectedStage == 1, vm.IsNotchToFullSeedPreviewVisible);
        Assert.Equal(expectedStage == 2, vm.IsNotchToFullCandidatePreviewVisible);
        Assert.Equal(expectedStage == 3, vm.IsNotchToFullFinalPreviewVisible);
        Assert.Equal(1, host.InvalidateCount);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 1)]
    public void NextNotchPreviewStageCommand_MovesToNextStageAndWrapsAtLast(int initialStage, int expectedStage)
    {
        var vm = CreateViewModelWithStep3PreviewReady();
        vm.NotchPreviewVisualizationStep = initialStage;
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        Assert.True(vm.CanControlNotchPreviewStage);

        vm.NextNotchPreviewStageCommand.Execute(null);

        Assert.Equal((decimal)expectedStage, vm.NotchPreviewVisualizationStep);
        Assert.Equal(expectedStage, vm.NotchPreviewStageIndex);
        Assert.Equal(expectedStage == 1, vm.IsNotchToFullSeedPreviewVisible);
        Assert.Equal(expectedStage == 2, vm.IsNotchToFullCandidatePreviewVisible);
        Assert.Equal(expectedStage == 3, vm.IsNotchToFullFinalPreviewVisible);
        Assert.Equal(1, host.InvalidateCount);
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

        Assert.Equal(("CAD: 2 pads.", "Regular: 0 pads."),
            (vm.CadSelectionText, vm.RegularSelectionText));
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

        Assert.Equal(("CAD: 0 pads.", "Regular: 0 pads."),
            (vm.CadSelectionText, vm.RegularSelectionText));
        Assert.Empty(vm.SelectedCadPadIds);
    }

    [AvaloniaFact]
    public async Task NotchPreviewAutoPlay_WhenAdvancingTheStageFails_EndsTheLoopWithoutFailure()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var work = EnableAutoPlayWithRecorder(vm);
            await ShowCadPreviewAsync(vm);
            var loop = work.LastStartedAsync(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName);
            using var trap = FailOnStageChange(vm, new InvalidOperationException("stage rejected"));

            await loop.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(trap.Fired);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task NotchPreviewAutoPlay_WhenAdvancingTheStageRunsOutOfMemory_Propagates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var work = EnableAutoPlayWithRecorder(vm);
            await ShowCadPreviewAsync(vm);
            var loop = work.LastStartedAsync(FreeformHelperViewModel.NotchPreviewAutoPlayWorkName);
            using var trap = FailOnStageChange(vm, CreateBoundaryOutOfMemory());

            await Assert.ThrowsAsync<OutOfMemoryException>(() => loop.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(trap.Fired);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task DeferredCadInspectorRefresh_WhenApplyingTheSnapshotFails_EndsWithoutFailureAndClearsPending()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            vm.PadInspectorUi.Snapshot = vm.BuildCadPadInspectorSnapshot(
                cad.Id,
                includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false,
                includeRuleTrace: false);
            using var trap = FailOnSnapshotChange(vm, new InvalidOperationException("snapshot rejected"));
            var work = new RecordingBackgroundWorkRunner();
            vm.BackgroundWork.Runner = work;

            vm.QueueDeferredCadInspectorSnapshotRefresh(cad.Id);
            Assert.True(vm.PadInspectorUi.IsDeferredRefreshPending);
            await work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName);

            Assert.True(trap.Fired);
            Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task DeferredCadInspectorRefresh_WhenApplyingTheSnapshotRunsOutOfMemory_Propagates()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            vm.PadInspectorUi.Snapshot = vm.BuildCadPadInspectorSnapshot(
                cad.Id,
                includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false,
                includeRuleTrace: false);
            using var trap = FailOnSnapshotChange(vm, CreateBoundaryOutOfMemory());
            var work = new RecordingBackgroundWorkRunner();
            vm.BackgroundWork.Runner = work;

            vm.QueueDeferredCadInspectorSnapshotRefresh(cad.Id);
            await Assert.ThrowsAsync<OutOfMemoryException>(() =>
                work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName));

            Assert.True(trap.Fired);
            Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    // Installs the recorder first, so the loop that showing the preview starts can be observed.
    private static RecordingBackgroundWorkRunner EnableAutoPlayWithRecorder(FreeformHelperViewModel vm)
    {
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        vm.NotchPreviewAutoPlayIntervalMs = 200m;
        vm.NotchPreviewAutoPlayEnabled = true;
        return work;
    }

    private static async Task ShowCadPreviewAsync(FreeformHelperViewModel vm)
    {
        var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        vm.ApplyCanvasSelection([cad.Id], Array.Empty<int>());
        await WaitForConditionAsync(
            () => !vm.PadInspectorUi.IsDeferredRefreshPending && vm.IsNotchCanvasPreviewVisible,
            timeoutMs: 3000);
    }

    private static OutOfMemoryException CreateBoundaryOutOfMemory() =>
        (OutOfMemoryException)RuntimeHelpers.GetUninitializedObject(typeof(OutOfMemoryException));

    private static OnceThrowingPropertyChange FailOnStageChange(FreeformHelperViewModel vm, Exception failure) =>
        new(vm, nameof(vm.NotchPreviewVisualizationStep), failure);

    private static OnceThrowingPropertyChange FailOnSnapshotChange(FreeformHelperViewModel vm, Exception failure) =>
        new(vm.PadInspectorUi, nameof(PadInspectorUiState.Snapshot), failure);

    // Throws the failure from the first change notification of one property, then lets later changes pass.
    private sealed class OnceThrowingPropertyChange : IDisposable
    {
        private readonly INotifyPropertyChanged _vm;
        private readonly string _propertyName;
        private readonly Exception _failure;
        private int _fired;

        public OnceThrowingPropertyChange(INotifyPropertyChanged vm, string propertyName, Exception failure)
        {
            _vm = vm;
            _propertyName = propertyName;
            _failure = failure;
            _vm.PropertyChanged += OnPropertyChanged;
        }

        public bool Fired => Volatile.Read(ref _fired) == 1;

        public void Dispose() => _vm.PropertyChanged -= OnPropertyChanged;

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == _propertyName && Interlocked.Exchange(ref _fired, 1) == 0)
            {
                throw _failure;
            }
        }
    }
}
