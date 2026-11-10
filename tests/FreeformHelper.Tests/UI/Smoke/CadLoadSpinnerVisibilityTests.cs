// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CadLoadSpinnerVisibilityTests
{
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OverlappingCadAndModalScopesHideOnlyAfterBothEndAsync(bool cadExitsFirst)
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var host = new FakeCadLoadSpinnerHost();
        using var factory = host.Install();
        await using var fixture = new SpinnerViewFixture();
        await fixture.ShowAsync();
        var initialShowCount = host.ShowCallCount;
        var initialHideCount = host.HideCallCount;

        await using var cad = new HeldSpinnerScope(fixture.ViewModel.RunWithCadLoadCanvasOverlayAsync);
        await cad.WaitUntilEnteredAsync();
        await using var modal = new HeldSpinnerScope(fixture.ViewModel.RunWithModalLoadingSpinnerAsync);
        await modal.WaitUntilEnteredAsync();

        Assert.True(fixture.ViewModel.IsCadLoadCanvasOverlayVisible);
        Assert.True(fixture.ViewModel.IsModalLoadingSpinnerVisible);
        Assert.Equal(initialShowCount + 1, host.ShowCallCount);
        Assert.Equal(initialHideCount, host.HideCallCount);
        Assert.Same(fixture.Window, host.LastOwner);

        var first = cadExitsFirst ? cad : modal;
        var last = cadExitsFirst ? modal : cad;
        await first.DisposeAsync();

        Assert.Equal(!cadExitsFirst, fixture.ViewModel.IsCadLoadCanvasOverlayVisible);
        Assert.Equal(cadExitsFirst, fixture.ViewModel.IsModalLoadingSpinnerVisible);
        Assert.Equal(initialShowCount + 1, host.ShowCallCount);
        Assert.Equal(initialHideCount, host.HideCallCount);

        await last.DisposeAsync();

        Assert.False(fixture.ViewModel.IsCadLoadCanvasOverlayVisible);
        Assert.False(fixture.ViewModel.IsModalLoadingSpinnerVisible);
        Assert.Equal(initialShowCount + 1, host.ShowCallCount);
        Assert.Equal(initialHideCount + 1, host.HideCallCount);
    }

    [AvaloniaFact]
    public async Task RepeatedSyncWhileCadOverlayIsVisibleDoesNotShowAgainAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var host = new FakeCadLoadSpinnerHost();
        using var factory = host.Install();
        await using var fixture = new SpinnerViewFixture();
        await fixture.ShowAsync();
        var initialShowCount = host.ShowCallCount;
        var initialHideCount = host.HideCallCount;
        await using var cad = new HeldSpinnerScope(fixture.ViewModel.RunWithCadLoadCanvasOverlayAsync);
        await cad.WaitUntilEnteredAsync();

        fixture.ViewModel.IsModalLoadingSpinnerVisible = true;
        fixture.ViewModel.IsModalLoadingSpinnerVisible = false;
        fixture.ViewModel.IsModalLoadingSpinnerVisible = true;
        fixture.ViewModel.IsModalLoadingSpinnerVisible = false;
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(fixture.ViewModel.IsCadLoadCanvasOverlayVisible);
        Assert.Equal(initialShowCount + 1, host.ShowCallCount);
        Assert.Equal(initialHideCount, host.HideCallCount);

        await cad.DisposeAsync();

        Assert.Equal(initialShowCount + 1, host.ShowCallCount);
        Assert.Equal(initialHideCount + 1, host.HideCallCount);
    }

    [AvaloniaFact]
    public async Task DetachAndReattachWarmFreshHostAndRetainVisibleFlagAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var firstHost = new FakeCadLoadSpinnerHost();
        using var freshHost = new FakeCadLoadSpinnerHost();
        using var firstFactory = firstHost.Install();
        await using var fixture = new SpinnerViewFixture();
        await fixture.ShowAsync();
        var initialShowCount = firstHost.ShowCallCount;
        var initialHideCount = firstHost.HideCallCount;
        Assert.Equal(1, firstHost.WarmupCallCount);
        await using var cad = new HeldSpinnerScope(fixture.ViewModel.RunWithCadLoadCanvasOverlayAsync);
        await cad.WaitUntilEnteredAsync();
        Assert.Equal(initialShowCount + 1, firstHost.ShowCallCount);

        fixture.Window.Content = null;
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Null(fixture.View.CadLoadSpinnerHostForTest);
        Assert.Equal(initialHideCount + 1, firstHost.HideCallCount);
        Assert.Equal(1, firstHost.DisposeCallCount);

        using var freshFactory = freshHost.Install();
        fixture.Window.Content = fixture.View;
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Same(freshHost, fixture.View.CadLoadSpinnerHostForTest);
        Assert.Equal(1, freshHost.WarmupCallCount);
        Assert.True(fixture.ViewModel.IsCadLoadCanvasOverlayVisible);
        Assert.Equal(0, freshHost.ShowCallCount);
        Assert.Equal(0, freshHost.HideCallCount);

        await cad.DisposeAsync();

        Assert.Equal(0, freshHost.ShowCallCount);
        Assert.Equal(1, freshHost.HideCallCount);
        await using var modal = new HeldSpinnerScope(fixture.ViewModel.RunWithModalLoadingSpinnerAsync);
        await modal.WaitUntilEnteredAsync();
        Assert.Equal(1, freshHost.ShowCallCount);
        Assert.Same(fixture.Window, freshHost.LastOwner);

        await modal.DisposeAsync();

        Assert.Equal(2, freshHost.HideCallCount);
        Assert.Equal(initialHideCount + 1, firstHost.HideCallCount);
        Assert.Equal(1, firstHost.DisposeCallCount);
    }

    private static async Task WaitForSpinnerIdleAsync(FreeformHelperViewModel viewModel)
    {
        await HeadlessUiPump.FlushQueueAsync();
        await HeadlessUiPump.WaitUntilAsync(
            () => !viewModel.IsModalLoadingSpinnerVisible && !viewModel.IsCadLoadCanvasOverlayVisible,
            "The initial loading indicators did not become idle.");
        await HeadlessUiPump.FlushQueueAsync();
    }

    private sealed class SpinnerViewFixture : IAsyncDisposable
    {
        public FreeformHelperViewModel ViewModel { get; } = new();
        public FreeformHelperView View { get; }
        public Window Window { get; }

        public SpinnerViewFixture()
        {
            View = new FreeformHelperView { DataContext = ViewModel };
            Window = new Window { Width = 1200, Height = 800, Content = View };
        }

        public async Task ShowAsync()
        {
            Window.Show();
            await WaitForSpinnerIdleAsync(ViewModel);
        }

        public async ValueTask DisposeAsync()
        {
            Window.Close();
            await HeadlessUiPump.FlushQueueAsync();
        }
    }

    private sealed class HeldSpinnerScope : IAsyncDisposable
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _loading;

        public HeldSpinnerScope(Func<Func<Task>, Task> runAsync)
        {
            _loading = runAsync(() =>
            {
                _entered.SetResult();
                return _release.Task.WaitAsync(TestContext.Current.CancellationToken);
            });
        }

        public async Task WaitUntilEnteredAsync()
        {
            await _entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
        }

        public async ValueTask DisposeAsync()
        {
            _release.TrySetResult();
            await _loading.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await HeadlessUiPump.FlushQueueAsync();
        }
    }
}
