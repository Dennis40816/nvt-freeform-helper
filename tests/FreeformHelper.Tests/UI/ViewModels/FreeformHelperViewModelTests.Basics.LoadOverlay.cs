using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void CadLoadCanvasOverlayScope_TogglesVisibilityAndRestoresAfterExit()
    {
        var vm = new FreeformHelperViewModel();

        vm.BeginCadLoadCanvasOverlayScope();

        Assert.True(vm.IsCadLoadCanvasOverlayVisible);

        vm.EndCadLoadCanvasOverlayScope();

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

        Assert.True(yielded.Wait(TimeSpan.FromSeconds(5)));
    }

    [AvaloniaFact]
    public async Task YieldCadLoadCanvasOverlayFrame_OnTheUiThread_RunsQueuedRenderWorkFirst()
    {
        var rendered = false;
        Dispatcher.UIThread.Post(() => rendered = true, DispatcherPriority.Render);

        await FreeformHelperViewModel.YieldCadLoadCanvasOverlayFrameAsync();

        // On the UI thread the method must still give render-priority work a turn before continuing.
        Assert.True(rendered);
    }

    [Fact]
    public void CadLoadCanvasOverlayScope_NestedScopesKeepOverlayVisibleUntilOuterExit()
    {
        var vm = new FreeformHelperViewModel();

        vm.BeginCadLoadCanvasOverlayScope();
        vm.BeginCadLoadCanvasOverlayScope();

        vm.EndCadLoadCanvasOverlayScope();
        Assert.True(vm.IsCadLoadCanvasOverlayVisible);

        vm.EndCadLoadCanvasOverlayScope();

        Assert.False(vm.IsCadLoadCanvasOverlayVisible);
    }

    [Fact]
    public void ModalLoadingSpinnerScope_TogglesVisibilityAndRestoresAfterExit()
    {
        var vm = new FreeformHelperViewModel();

        vm.BeginModalLoadingSpinnerScope();

        Assert.True(vm.IsModalLoadingSpinnerVisible);

        vm.EndModalLoadingSpinnerScope();

        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }

    [Fact]
    public void ModalLoadingSpinnerScope_NestedScopesKeepSpinnerVisibleUntilOuterExit()
    {
        var vm = new FreeformHelperViewModel();

        vm.BeginModalLoadingSpinnerScope();
        vm.BeginModalLoadingSpinnerScope();

        vm.EndModalLoadingSpinnerScope();
        Assert.True(vm.IsModalLoadingSpinnerVisible);

        vm.EndModalLoadingSpinnerScope();

        Assert.False(vm.IsModalLoadingSpinnerVisible);
    }
}
