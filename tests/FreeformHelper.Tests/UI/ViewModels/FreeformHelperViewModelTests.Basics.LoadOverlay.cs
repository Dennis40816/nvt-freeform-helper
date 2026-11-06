using Avalonia.Headless.XUnit;
using Avalonia.Threading;
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
}
