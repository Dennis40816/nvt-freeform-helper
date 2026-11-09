using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Infrastructure.Dxf;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public async Task CadLoadCanvasOverlayScope_TogglesVisibilityAndRestoresAfterExit()
    {
        var vm = new FreeformHelperViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loading = vm.RunWithCadLoadCanvasOverlayAsync(() =>
        {
            entered.SetResult();
            return release.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(vm.IsCadLoadCanvasOverlayVisible);
        }
        finally
        {
            release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(vm.IsCadLoadCanvasOverlayVisible);
    }

    [AvaloniaFact]
    public void YieldCadLoadCanvasOverlayFrame_OffTheUiThread_DoesNotWaitForTheDispatcher()
    {
        // The application exists but the UI thread is blocked below, as it is for a plain test that runs
        // right after an Avalonia test. Waiting for the dispatcher from another thread would never end.
        var yielded = Task.Run(static async () =>
        {
            // Without a visible application the method would return early for a different reason.
            Assert.NotNull(global::Avalonia.Application.Current);
            await FreeformHelperViewModel.YieldCadLoadCanvasOverlayFrameAsync();
        });

        TaskBlock.UntilComplete(yielded.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [AvaloniaFact]
    public async Task YieldCadLoadCanvasOverlayFrame_OnTheUiThread_RunsQueuedRenderWorkFirst()
    {
        var rendered = false;
        HeadlessUiPump.RunningDispatcher.Post(() => rendered = true, DispatcherPriority.Render);

        await FreeformHelperViewModel.YieldCadLoadCanvasOverlayFrameAsync();

        // On the UI thread the method must still give render-priority work a turn before continuing.
        Assert.True(rendered);
    }

    [Fact]
    public async Task CadLoadCanvasOverlayScope_NestedScopesKeepOverlayVisibleUntilOuterExit()
    {
        var vm = new FreeformHelperViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loading = vm.RunWithCadLoadCanvasOverlayAsync(async () =>
        {
            await vm.RunWithCadLoadCanvasOverlayAsync(() => Task.CompletedTask);
            entered.SetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(vm.IsCadLoadCanvasOverlayVisible);
        }
        finally
        {
            release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(vm.IsCadLoadCanvasOverlayVisible);
    }

    [Fact]
    public async Task ModalLoadingSpinnerScope_TogglesVisibilityAndRestoresAfterExit()
    {
        var vm = new FreeformHelperViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loading = vm.RunWithModalLoadingSpinnerAsync(() =>
        {
            entered.SetResult();
            return release.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(vm.IsModalLoadingSpinnerVisible);
        }
        finally
        {
            release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task ModalLoadingSpinnerScope_NestedScopesKeepSpinnerVisibleUntilOuterExit()
    {
        var vm = new FreeformHelperViewModel();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var loading = vm.RunWithModalLoadingSpinnerAsync(async () =>
        {
            await vm.RunWithModalLoadingSpinnerAsync(() => Task.CompletedTask);
            entered.SetResult();
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.True(vm.IsModalLoadingSpinnerVisible);
        }
        finally
        {
            release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public async Task LoadProject_SkipsACombinedGroupWhoseOutputPadHasTooFewVertices()
    {
        var combinedCount = await LoadProjectWithCombinedGroupsAsync(vertexCounts: [2]);

        Assert.Equal(0, combinedCount);
    }

    [Fact]
    public async Task LoadProject_ReplaysTheValidGroupsAroundAnInvalidOne()
    {
        var combinedCount = await LoadProjectWithCombinedGroupsAsync(vertexCounts: [2, 4]);

        Assert.Equal(1, combinedCount);
    }

    private static async Task<int> LoadProjectWithCombinedGroupsAsync(int[] vertexCounts)
    {
        var dxfPath = CreateTempDxfFile(("L1", 0, 0, 10, 10), ("L1", 8, 0, 18, 10));
        var directory = TestFiles.CreateTempDirectory();
        try
        {
            var cad = new DxfImportService().ImportFromPath(dxfPath, new DxfImportOptions());
            var sourceCadIds = cad.Pads.Select(static pad => pad.Id).OrderBy(static id => id).ToList();
            var maxCadId = sourceCadIds.Max();
            var project = new ProjectFile { LastDxfPath = dxfPath };
            for (var index = 0; index < vertexCounts.Length; index++)
            {
                project.DxfCombinedCadGroups.Add(CreateCombinedGroup(index + 1, sourceCadIds, maxCadId + 1 + index, vertexCounts[index]));
            }

            var projectPath = Path.Combine(directory.FullName, "combined-groups.json");
            await new JsonProjectStore().SaveAsync(projectPath, project, TestContext.Current.CancellationToken);

            var vm = new FreeformHelperViewModel
            {
                PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath),
            };
            await vm.LoadProjectCommand.ExecuteAsync(null);

            return vm.CombinedCadPadCount;
        }
        finally
        {
            File.Delete(dxfPath);
            directory.Delete(recursive: true);
        }
    }

    private static ProjectDxfCombinedCadGroup CreateCombinedGroup(int groupId, List<int> sourceCadIds, int outputPadId, int vertexCount)
    {
        var vertices = new[]
        {
            new Point2(0, 0),
            new Point2(10, 0),
            new Point2(10, 10),
            new Point2(0, 10),
        };

        return new ProjectDxfCombinedCadGroup
        {
            GroupId = groupId,
            SourceCadIds = sourceCadIds,
            OutputPads =
            [
                new ProjectCadPadSnapshot
                {
                    Id = outputPadId,
                    Name = $"COMB_{outputPadId}",
                    Layer = "L1",
                    Vertices = [.. vertices.Take(vertexCount)],
                },
            ],
        };
    }

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
