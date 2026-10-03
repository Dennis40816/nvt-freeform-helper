using System.Collections.ObjectModel;
using System.Reflection;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [AvaloniaFact]
    public async Task SelectionPreviewAndDeferredInspector_SameRevision_ReuseResolvedSnapshot()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var revision = vm.GetNotchStep3Revision();
            var missesBeforeSelection = vm.GetNotchCompensationCacheMetrics().MissCount;

            vm.ApplyCanvasSelection([cad.Id], Array.Empty<int>());

            var immediateInspector = Assert.IsType<CadPadInspectorSnapshot>(vm.CurrentPadInspectorSnapshot?.Cad);
            Assert.Null(immediateInspector.Notch);
            Assert.Equal("Notch rows: computing...", immediateInspector.NotchRowSummary);
            var preview = Assert.Single(vm.NotchCanvasPreviewItems);
            var previewResolved = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(previewResolved);
            Assert.True(vm.IsPadInspectorDeferredRefreshPending);
            Assert.Equal(missesBeforeSelection + 1, vm.GetNotchCompensationCacheMetrics().MissCount);
            Assert.Equal(previewResolved.Compensation.ToRegularRatio, preview.ToRegularRatio);
            Assert.Equal(previewResolved.Compensation.ToFullRatio, preview.ToFullRatio);
            Assert.Equal(previewResolved.Compensation.IsToFullEnabled, preview.IsToFullEnabled);
            Assert.Same(previewResolved.Compensation.ToFullPolygons, preview.ToFullPolygons);
            Assert.Same(previewResolved.Stage1SeedPolygons, preview.ToFullSeedPolygons);
            Assert.Same(previewResolved.Stage2CandidatePolygons, preview.ToFullCandidatePolygons);
            Assert.Same(previewResolved.Stage3FinalOutlinePolygons, preview.ToFullFinalOutlinePolygons);

            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);

            var inspectorResolved = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(inspectorResolved);
            Assert.Same(previewResolved, inspectorResolved);
            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Equal(missesBeforeSelection + 1, vm.GetNotchCompensationCacheMetrics().MissCount);
            AssertInspectorProjectsResolved(vm, cad.Id, inspectorResolved);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task NotchDetail_SameSelectionRevision_ProjectsExistingResolvedSnapshot()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            vm.ApplyCanvasSelection([cad.Id], Array.Empty<int>());

            var revision = vm.GetNotchStep3Revision();
            var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cad.Id));
            Assert.NotEmpty(resolved.Stage1SeedPolygons);
            Assert.NotEmpty(resolved.Stage2CandidatePolygons);
            Assert.NotEmpty(resolved.Compensation.RegularDebugInfos);

            var detail = await OpenNotchDetailForTestAsync(vm);

            Assert.Equal(revision, vm.GetNotchStep3Revision());
            AssertNotchDetailProjectsResolved(vm, cad, resolved, detail);
            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending,
                timeoutMs: 3000);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task NotchDetail_WhenSelectionSnapshotIsCold_BuildsOneAuthoritativeResult()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var selectedCadIds = vm.CadPads
                .Where(static pad => pad.Layer.StartsWith("signal-", StringComparison.Ordinal))
                .Select(static pad => pad.Id)
                .ToArray();
            Assert.Equal(2, selectedCadIds.Length);
            vm.EnableToFull = false;
            vm.ApplyCanvasSelection(selectedCadIds, Array.Empty<int>());
            Assert.Empty(vm.NotchCanvasPreviewItems);
            Assert.False(vm.IsPadInspectorDeferredRefreshPending);

            var detail = await OpenNotchDetailForTestAsync(vm);
            var cad = detail.CadPad;
            var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cad.Id));

            Assert.NotEmpty(resolved.Compensation.RegularDebugInfos);
            AssertNotchDetailProjectsResolved(vm, cad, resolved, detail);
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));
            vm.ApplyCanvasSelection(Array.Empty<int>(), Array.Empty<int>());
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ResolvedSnapshot_CrossIcAllocationPool_MatchesGeneratorCompensation()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateTempDxfFile(
            ("signal-a", 0, 0, 5, 10),
            ("signal-b", 5, 0, 15, 10),
            ("frame", 19.9, 9.9, 20, 10));
        try
        {
            var vm = await CreateCrossIcAllocationPoolViewModelAsync(dxfPath);
            var cadA = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var cadB = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-b");
            Assert.True(vm.TryGetCadIcIndex(cadA.Id, out var cadAIc));
            Assert.True(vm.TryGetCadIcIndex(cadB.Id, out var cadBIc));
            Assert.Equal(0, cadAIc);
            Assert.Equal(1, cadBIc);

            var left = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 0);
            var right = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 1);
            var grid = new RegularGrid(
                rows: 1,
                cols: 2,
                xEdges: [left.Bounds.MinX, left.Bounds.MaxX, right.Bounds.MaxX],
                yEdges: [left.Bounds.MinY, left.Bounds.MaxY],
                pads: [left, right]);
            var settings = new ProjectSettings
            {
                Notch = new NotchSettings
                {
                    ComputationMode = NotchComputationMode.CadAllocation,
                    EnableToFullRuleEngine = true,
                    EnableToFullRuleTrace = true,
                    EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
                },
            };
            var table = new NotchTableGenerator().Generate(
                new CadPadSet([cadA, cadB]),
                grid,
                settings,
                activeRegularPadIds: new HashSet<int> { left.RegularPadId, right.RegularPadId },
                cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
                {
                    [cadA.Id] = left.DiffIndex,
                    [cadB.Id] = right.DiffIndex,
                });

            var generatorRow = Assert.Single(
                table.Rows,
                row => row.Version == NotchAlgorithmVersion.V22 && row.CadPadId == cadA.Id);
            var generatorNode = Assert.IsType<NotchV22Node>(generatorRow.V22Node);
            Assert.Equal(50, generatorNode.CombinePercent);
            Assert.Contains("R=50% F=100% C=50%", generatorRow.Comment, StringComparison.Ordinal);

            vm.ApplyCanvasSelection([cadA.Id], Array.Empty<int>());
            Assert.Empty(vm.NotchCanvasPreviewItems);
            var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cadA.Id));
            var debug = Assert.Single(resolved.Compensation.RegularDebugInfos);
            var expectedDebug = Assert.Single(NotchV22CompensationService.Compute(
                cadA,
                grid,
                enableToRegular: true,
                enableToFull: true,
                allCadPads: [cadA, cadB],
                activeRegularPadIds: new HashSet<int> { left.RegularPadId, right.RegularPadId },
                enableToFullRuleEngine: true,
                enableToFullRuleTrace: true).RegularDebugInfos);
            Assert.Equal(0.5, resolved.Compensation.ToRegularRatio, 6);
            Assert.Equal(1.0, resolved.Compensation.ToFullRatio, 6);
            Assert.Equal(0.5, resolved.Compensation.CombinedRatio, 6);
            Assert.Equal(new[] { cadA.Id, cadB.Id }.Order(), debug.OwnerCadPadIds);
            Assert.Equal(1, debug.BlockerCandidateCount);
            Assert.Equal(expectedDebug.ToFullRuleCode, debug.ToFullRuleCode);
            Assert.Equal(expectedDebug.ToFullRuleTrace, debug.ToFullRuleTrace);
            var target = Assert.Single(resolved.TargetAllocation.Targets);
            Assert.Equal((0, left.DiffIndex, 0.5, 50, true),
                (target.IcIndex, target.DiffIndex, target.Ratio, target.RatioPercentRounded, target.IsAnchorDiff));

            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cadA.Id));
            AssertInspectorProjectsResolved(vm, cadA.Id, resolved);
            AssertNotchDetailProjectsResolved(vm, cadA, resolved, await OpenNotchDetailForTestAsync(vm));

            var revision = vm.GetNotchStep3Revision();
            var misses = vm.GetNotchCompensationCacheMetrics().MissCount;
            var replacement = new CadPad(cadB.Id + 1000, "signal-c", "signal-c", cadB.Polygon);
            vm.CadPads = new ObservableCollection<CadPad>([replacement, cadA]);
            var replaced = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cadA.Id));
            var replacedDebug = Assert.Single(replaced.Compensation.RegularDebugInfos);
            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Equal(misses + 1, vm.GetNotchCompensationCacheMetrics().MissCount);
            Assert.NotSame(resolved, replaced);
            Assert.Equal(new[] { cadA.Id, replacement.Id }.Order(), replacedDebug.OwnerCadPadIds);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaTheory]
    [InlineData(20.1, 200, "200.00 %", false)]
    [InlineData(30.0, 300, "300.00 % (overflow risk >255%)", true)]
    public void InspectorCombinedRatio_MatchesGeneratorFinalTargetEligibility(
        double cadMaxX,
        int expectedCombinedPercent,
        string expectedDisplay,
        bool expectedOverflow)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var cad = CreateRectCad(101, 0, 0, cadMaxX, 10);
        var anchorRegular = CreateRectRegular(0, 0, 1001, 0, 0, 10, 10);
        var eligibleRegular = CreateRectRegular(0, 1, 1002, 10, 0, 20, 10);
        var finalRegular = CreateRectRegular(0, 2, 1003, 20, 0, cadMaxX, 10);
        var regulars = new[] { anchorRegular, eligibleRegular, finalRegular };
        for (var index = 0; index < regulars.Length; index++)
        {
            regulars[index].IcIndex = 0;
            regulars[index].DiffIndex = 10 + index;
            regulars[index].Freeform = FreeformType.XWay;
            regulars[index].MatchedCadPadId = cad.Id;
            regulars[index].MatchScore = 1.0;
        }

        var grid = new RegularGrid(
            rows: 1,
            cols: 3,
            xEdges: [0, 10, 20, cadMaxX],
            yEdges: [0, 10],
            pads: regulars);
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>([cad]),
            RegularPads = new ObservableCollection<RegularPad>(regulars),
            EnableToRegular = true,
            EnableToFull = false,
            ToFullStrictOverlapPercent = 1m,
        };
        vm.SelectedNotchCompensationModelOption = Assert.Single(
            vm.NotchCompensationModelOptions,
            static option => option.Value == NotchCompensationModel.ConservativeNoGain);

        var cadField = typeof(FreeformHelperViewModel).GetField(
            "_cad",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField(
            "_grid",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);
        cadField!.SetValue(vm, new CadPadSet([cad]));
        gridField!.SetValue(vm, grid);
        SetPadMatchResult(vm, cad.Id, anchorRegular.RegularPadId);
        SetCadOutputFwDiff(vm, cad.Id, anchorRegular.DiffIndex, anchorRegular.IcIndex);
        InvalidateWorkflowDataSnapshot(vm);

        var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cad.Id));
        Assert.Equal(
            [
                (anchorRegular.DiffIndex, 100, true, true),
                (eligibleRegular.DiffIndex, 100, true, false),
                (finalRegular.DiffIndex, 100, expectedOverflow, false),
            ],
            resolved.TargetAllocation.Targets
                .OrderBy(static target => target.DiffIndex)
                .Select(static target => (
                    target.DiffIndex,
                    target.RatioPercentRounded,
                    target.PassesStrictThreshold,
                    target.IsAnchorDiff)));
        Assert.Equal(expectedCombinedPercent, resolved.TargetAllocation.TargetCoverageProjection.RawCombinedPercent);
        Assert.Equal(expectedOverflow, resolved.TargetAllocation.TargetCoverageProjection.HasCombinedOverflowRisk);

        var settings = new ProjectSettings
        {
            Notch = new NotchSettings
            {
                ComputationMode = NotchComputationMode.CadAllocation,
                CompensationModel = NotchCompensationModel.ConservativeNoGain,
                EnableToRegular = true,
                EnableToFull = false,
                MultiOwnerStrictOverlapPercent = 1.0,
                LinkVersionThresholds = false,
                ThresholdPercentV22 = 0.0,
                EnabledVersions = new HashSet<NotchAlgorithmVersion> { NotchAlgorithmVersion.V22 },
            },
        };
        NotchTable Generate() => new NotchTableGenerator().Generate(
                new CadPadSet([cad]),
                grid,
                settings,
                activeRegularPadIds: regulars.Select(static regular => regular.RegularPadId).ToHashSet(),
                cadOutputFwDiffIndexByCadId: new Dictionary<int, int>
                {
                    [cad.Id] = anchorRegular.DiffIndex,
                });
        if (expectedOverflow)
        {
            var exception = Assert.Throws<InvalidOperationException>(Generate);
            Assert.Contains("Combine ratio exceeds 255%", exception.Message, StringComparison.Ordinal);
            Assert.Contains("raw=300%", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            var generatorRow = Assert.Single(Generate().Rows);
            var generatorNode = Assert.IsType<NotchV22Node>(generatorRow.V22Node);
            Assert.Equal(expectedCombinedPercent, generatorNode.CombinePercent);
            Assert.Contains($"C={expectedCombinedPercent}%", generatorRow.Comment, StringComparison.Ordinal);
        }

        vm.CurrentPadInspectorSnapshot = vm.BuildCadPadInspectorSnapshot(cad.Id);
        AssertInspectorProjectsResolved(vm, cad.Id, resolved);
        var combinedLine = Assert.Single(
            vm.PadInspectorCompensationLines,
            static line => line.Label == "Combined");
        Assert.Equal(expectedDisplay, combinedLine.Value);
        Assert.Equal(expectedOverflow, combinedLine.IsEmphasized);
        Assert.Equal(expectedOverflow ? PadInspectorLineTone.Warning : PadInspectorLineTone.Normal, combinedLine.Tone);
    }

    [AvaloniaFact]
    public async Task DeferredInspector_WhenResolvedSnapshotIsCold_PublishesOneStableResult()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var initial = vm.BuildCadPadInspectorSnapshot(
                cad.Id,
                includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false,
                includeRuleTrace: false);
            vm.CurrentPadInspectorSnapshot = initial;
            Assert.Null(initial?.Cad?.Notch);
            Assert.Equal("Notch rows: computing...", initial?.Cad?.NotchRowSummary);
            var missesBeforeRefresh = vm.GetNotchCompensationCacheMetrics().MissCount;

            vm.QueueDeferredCadInspectorSnapshotRefresh(cad.Id);
            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);

            var first = vm.GetCadV22ResolvedResult(cad.Id);
            var second = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Equal(cad.Id, vm.CurrentPadInspectorSnapshot?.Cad?.CadPadId);
            Assert.NotEqual("Notch rows: computing...", vm.CurrentPadInspectorSnapshot?.Cad?.NotchRowSummary);
            Assert.Equal(missesBeforeRefresh + 1, vm.GetNotchCompensationCacheMetrics().MissCount);
            AssertInspectorProjectsResolved(vm, cad.Id, first);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task DeferredInspector_WhenSelectionChanges_AppliesOnlyLatestCad()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var first = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var latest = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-b");

            vm.ApplyCanvasSelection([first.Id], Array.Empty<int>());
            vm.ApplyCanvasSelection([latest.Id], Array.Empty<int>());

            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);

            Assert.Equal(latest.Id, vm.CurrentPadInspectorSnapshot?.Cad?.CadPadId);
            Assert.Equal(latest.Id, Assert.Single(vm.NotchCanvasPreviewItems).CadPadId);

            vm.ApplyCanvasSelection([first.Id], Array.Empty<int>());
            Assert.True(vm.IsPadInspectorDeferredRefreshPending);
            vm.ApplyCanvasSelection(Array.Empty<int>(), Array.Empty<int>());

            Assert.False(vm.IsPadInspectorDeferredRefreshPending);
            Assert.Null(vm.CurrentPadInspectorSnapshot);
            Assert.Empty(vm.NotchCanvasPreviewItems);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task Inspector_WhenCacheWasBuiltWithStrictOverride_ProjectsCurrentResolvedResult()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var overridden = vm.GetCadV22ResolvedResult(cad.Id, strictOverlapRatioOverride: 1.0);
            Assert.NotNull(overridden);

            vm.CurrentPadInspectorSnapshot = vm.BuildCadPadInspectorSnapshot(cad.Id);
            var current = vm.GetCadV22ResolvedResult(cad.Id);

            Assert.NotNull(current);
            Assert.NotSame(overridden, current);
            Assert.NotEqual(
                overridden.TargetAllocation.StrictAreaThreshold,
                current.TargetAllocation.StrictAreaThreshold);
            AssertInspectorProjectsResolved(vm, cad.Id, current);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task LightweightInspector_WhenOnlyOverrideCompensationIsCached_DoesNotPoisonCurrentKey()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            var overridden = vm.GetCadV22CompensationResult(cad.Id, strictOverlapRatioOverride: 1.0);
            Assert.NotNull(overridden);

            var lightweight = vm.BuildCadPadInspectorSnapshot(
                cad.Id,
                includeExpensiveNotchDetails: false,
                includeNotchRowEligibilityDetails: false,
                includeMatchDetails: false,
                includeRuleTrace: false);

            Assert.Null(lightweight?.Cad?.Notch);
            var current = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(current);
            Assert.NotSame(overridden, current.Compensation);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    [AvaloniaFact]
    public async Task ResolvedSnapshot_OutputOnlyStateReusesIdentity_ComputationChangeInvalidatesIt()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        var dxfPath = CreateResolvedSnapshotDxfFile();
        try
        {
            var vm = await CreateSingleCadNotchPreviewViewModelAsync(dxfPath);
            var cad = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
            vm.ApplyCanvasSelection([cad.Id], Array.Empty<int>());
            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);
            var revision = vm.GetNotchStep3Revision();
            var resolved = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(resolved);

            vm.SelectedNotchExportProfileOption = Assert.Single(
                vm.NotchExportProfileOptions,
                static option => option.Value == NotchExportProfile.Debug);
            vm.SelectedNotchExportFileTypeOption = Assert.Single(
                vm.NotchExportFileTypeOptions,
                static option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv21);
            vm.EnableV21 = true;
            vm.EnableV22 = false;

            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));

            vm.EnableV22 = true;
            vm.EnableV21 = false;

            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));

            vm.EnableV21 = true;

            Assert.Equal(revision, vm.GetNotchStep3Revision());
            Assert.Same(resolved, vm.GetCadV22ResolvedResult(cad.Id));
            AssertNotchDetailProjectsResolved(vm, cad, resolved, await OpenNotchDetailForTestAsync(vm));

            vm.QueueDeferredCadInspectorSnapshotRefresh(cad.Id);
            Assert.True(vm.IsPadInspectorDeferredRefreshPending);

            vm.ToFullStrictOverlapPercent += 0.1m;

            Assert.True(vm.GetNotchStep3Revision() > revision);
            var invalidated = vm.GetCadV22ResolvedResult(cad.Id);
            Assert.NotNull(invalidated);
            Assert.NotSame(resolved, invalidated);
            AssertNotchDetailProjectsResolved(vm, cad, invalidated, await OpenNotchDetailForTestAsync(vm));
            await WaitForConditionAsync(
                () => !vm.IsPadInspectorDeferredRefreshPending &&
                    vm.CurrentPadInspectorSnapshot?.Cad?.Notch is not null,
                timeoutMs: 3000);
            Assert.Same(invalidated, vm.GetCadV22ResolvedResult(cad.Id));
            AssertInspectorProjectsResolved(vm, cad.Id, invalidated);
        }
        finally
        {
            File.Delete(dxfPath);
        }
    }

    private static async Task<NotchDetailViewModel> OpenNotchDetailForTestAsync(FreeformHelperViewModel vm)
    {
        var opened = new List<NotchDetailViewModel>();
        var statusBefore = vm.StatusText;
        vm.OpenNotchDetailAsync = detail =>
        {
            opened.Add(detail);
            return Task.CompletedTask;
        };

        await vm.ShowNotchDetailCommand.ExecuteAsync(null);

        Assert.Equal(statusBefore, vm.StatusText);
        return Assert.Single(opened);
    }

    private static void AssertNotchDetailProjectsResolved(
        FreeformHelperViewModel vm,
        CadPad cad,
        NotchV22ResolvedResult resolved,
        NotchDetailViewModel detail)
    {
        Assert.Same(cad, detail.CadPad);
        Assert.True(vm.TryGetCadOutputFwDiffIndex(cad.Id, out var expectedDiffIndex));
        Assert.Equal(expectedDiffIndex, detail.DxfIndex);
        Assert.Equal(FreeformType.XWay, detail.FreeformType);

        var allocation = Assert.Single(detail.Allocations);
        var regular = Assert.Single(detail.PreviewRegularPads);
        Assert.Equal(regular.Index, allocation.RegularIndex);
        Assert.Equal(regular.Row, allocation.Row);
        Assert.Equal(regular.Col, allocation.Col);
        Assert.Equal(regular.IcIndex, allocation.IcIndex);
        Assert.Equal(1.0, allocation.Ratio, 6);
        Assert.Equal(128, allocation.Q7);
        Assert.Equal((int)vm.NotchThresholdQ7, detail.ThresholdQ7);
        Assert.Equal(allocation.Q7, detail.MaxAllocationQ7);
        Assert.Equal("TH=0 (disabled)", detail.ThresholdSummary);

        Assert.Equal(resolved.Compensation.ToRegularRatio, detail.ToRegularRatio);
        Assert.Equal(resolved.Compensation.ToFullRatio, detail.ToFullRatio);
        Assert.Equal(resolved.Compensation.CombinedRatio, detail.CombinedRatio);
        Assert.Equal(resolved.Compensation.IsToFullEnabled, detail.IsToFullEnabled);
        var display = NotchDisplayProjector.Build(resolved.Compensation, resolved.TargetAllocation);
        Assert.Equal(display.ToRegularRatioText, detail.ToRegularRatioText);
        Assert.Equal(display.ToFullRatioText, detail.ToFullRatioText);
        Assert.Equal(display.CombinedRatioText, detail.CombinedRatioText);
        Assert.Same(resolved.Stage1SeedPolygons, detail.ToFullSeedPolygons);
        Assert.Same(resolved.Stage2CandidatePolygons, detail.ToFullPolygons);
        Assert.Same(resolved.Compensation.RegularDebugInfos, detail.RegularDebugInfos);
    }

    private static void AssertInspectorProjectsResolved(
        FreeformHelperViewModel vm,
        int cadPadId,
        NotchV22ResolvedResult resolved)
    {
        var inspector = Assert.IsType<PadInspectorNotchSnapshot>(vm.CurrentPadInspectorSnapshot?.Cad?.Notch);
        Assert.Equal(resolved.Compensation.ToRegularRatio, inspector.ToRegularRatio);
        Assert.Equal(resolved.Compensation.ToFullRatio, inspector.ToFullRatio);
        Assert.Equal(resolved.Compensation.CombinedRatio, inspector.CombinedRatio);
        Assert.Equal(resolved.Compensation.IsToFullEnabled, inspector.IsToFullEnabled);
        Assert.Equal(resolved.Compensation.Stage3Area, inspector.Stage3Area);
        Assert.Equal(resolved.TargetAllocation.StrictAreaThreshold, inspector.StrictAreaThreshold);
        Assert.Same(resolved.TargetAllocation.TargetCoverageProjection, inspector.TargetCoverageProjection);
        Assert.Equal(resolved.TargetAllocation.Targets.Count, inspector.Targets.Count);
        for (var index = 0; index < inspector.Targets.Count; index++)
        {
            var expected = resolved.TargetAllocation.Targets[index];
            var actual = inspector.Targets[index];
            Assert.Equal(expected.IcIndex, actual.IcIndex);
            Assert.Equal(expected.DiffIndex, actual.DiffIndex);
            Assert.Equal(expected.EffectiveArea, actual.EffectiveArea);
            Assert.Equal(expected.Ratio, actual.Ratio);
            Assert.Equal(expected.RatioPercentRounded, actual.RatioPercentRounded);
            Assert.Equal(expected.PassesStrictThreshold, actual.PassesStrictThreshold);
            Assert.Equal(expected.IsAnchorDiff, actual.IsAnchorDiff);
            Assert.Equal(expected.ToFullAppliedRegularCount, actual.ToFullAppliedRegularCount);
            Assert.Equal(expected.RegularCount, actual.RegularCount);
            Assert.Equal(expected.RegularPadIds, actual.RegularPadIds);
            Assert.Equal(
                expected.RegularAreas.Select(static area => (area.RegularPadId, area.EffectiveArea)),
                actual.RegularAreas.Select(static area => (area.RegularPadId, area.EffectiveArea)));
        }

        Assert.Equal(vm.GetCadV22CompensationDiagnostics(cadPadId), inspector.Diagnostics);
    }

    private static string CreateResolvedSnapshotDxfFile()
    {
        return CreateTempDxfFile(
            ("frame", 0, 0, 1, 1),
            ("signal-a", 12, 12, 18, 18),
            ("signal-b", 22, 12, 28, 18),
            ("frame", 29, 29, 30, 30));
    }

    private static async Task<FreeformHelperViewModel> CreateCrossIcAllocationPoolViewModelAsync(string dxfPath)
    {
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            ActiveAreaWidth = 20,
            ActiveAreaHeight = 10,
            GridPaddingPercent = 0,
            CascadeNum = 2,
            EnableToFullRuleEngine = true,
            EnableToFullRuleTrace = true,
        };
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(2, vm.CascadeIcSettings.Count);
        foreach (var cascade in vm.CascadeIcSettings)
        {
            cascade.XChannels = 1;
            cascade.YChannels = 1;
        }

        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

        var cadA = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-a");
        var cadB = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal-b");
        var left = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 0);
        var right = Assert.Single(vm.RegularPads, static pad => pad.IcIndex == 1);
        left.Freeform = FreeformType.XWay;
        left.MatchedCadPadId = cadA.Id;
        left.MatchScore = 1.0;
        right.Freeform = FreeformType.XWay;
        right.MatchedCadPadId = cadB.Id;
        right.MatchScore = 1.0;
        SetPadMatchResults(vm, (cadA.Id, left.RegularPadId), (cadB.Id, right.RegularPadId));
        SetCadOutputFwDiff(vm, cadA.Id, left.DiffIndex, icIndex: 0);
        SetCadOutputFwDiff(vm, cadB.Id, right.DiffIndex, icIndex: 1);
        InvalidateWorkflowDataSnapshot(vm);
        return vm;
    }

    private static async Task<FreeformHelperViewModel> CreateSingleCadNotchPreviewViewModelAsync(string dxfPath)
    {
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            ActiveAreaWidth = 30,
            ActiveAreaHeight = 30,
            GridPaddingPercent = 0,
        };

        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 3;
        cascade.YChannels = 3;
        await WaitForGridRebuildAsync(vm);

        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

        Assert.Equal(9, vm.RegularPads.Count);
        var signalCadIds = vm.CadPads
            .Where(static pad => pad.Layer.StartsWith("signal-", StringComparison.Ordinal))
            .Select(static pad => pad.Id)
            .ToHashSet();
        var matchedRegulars = vm.RegularPads
            .Where(regular => regular.MatchedCadPadId is int cadId && signalCadIds.Contains(cadId))
            .ToArray();
        Assert.Equal(2, matchedRegulars.Length);
        foreach (var regular in matchedRegulars)
        {
            regular.Freeform = FreeformType.XWay;
        }

        return vm;
    }
}
