// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Threading.Channels;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LayerImagePreviewBehaviorTests
{
    [AvaloniaFact]
    public async Task QuickEditsProduceOneRenderAfterTheLastDebounceAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        int renders = 0;
        LayerImageRenderRequest? rendered = null;
        var context = LayerImageExportTestData.CreateContext() with
        {
            RenderPngAsync = (request, _, _) =>
            {
                renders++;
                rendered = request;
                return Task.FromResult<byte[]?>(null);
            },
        };
        using var vm = new LayerImageExportViewModel(context, clock);

        clock.Advance(TimeSpan.FromMilliseconds(100));
        vm.PreviewSettings.PixelsX = 600;
        clock.Advance(TimeSpan.FromMilliseconds(100));
        vm.PreviewSettings.PaddingX = 5;
        vm.PreviewSettings.LineWidthPixels = 2;
        vm.PreviewSettings.ToggleThemeCommand.Execute(null);
        Assert.Equal(0, renders);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(1, renders);
        Assert.NotNull(rendered);
        Assert.Equal(600, rendered.PixelsX);
        Assert.Equal(5, rendered.PaddingX);
        Assert.Equal(2m, rendered.LineWidthPixels);
        Assert.True(rendered.UseDarkTheme);
        Assert.False(vm.IsPreviewUpdating);
    }

    [AvaloniaFact]
    public async Task SlowOlderRenderCannotReplaceTheNewerPictureAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        var renderer = new ControlledPreview();
        byte[] olderImage = [1];
        byte[] newerImage = [2];
        var context = LayerImageExportTestData.CreateContext() with { RenderPngAsync = renderer.RenderAsync };
        using var vm = new LayerImageExportViewModel(context, clock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        var older = await renderer.NextAsync();
        Task olderTask = vm.WaitForPreviewAsync();
        Assert.True(vm.IsPreviewUpdating);

        vm.PreviewSettings.PaddingY = 3;
        clock.Advance(TimeSpan.FromMilliseconds(150));
        var newer = await renderer.NextAsync();
        Assert.True(older.Token.IsCancellationRequested);
        Assert.Equal(1200, newer.MaxSidePixels);
        Assert.True(vm.IsPreviewUpdating);
        newer.Completion.SetResult(newerImage);
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        older.Completion.SetResult(olderImage);
        await olderTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Same(newerImage, vm.PreviewPng);
        Assert.False(vm.IsPreviewUpdating);
        Assert.Null(vm.PreviewErrorText);
    }

    [AvaloniaFact]
    public async Task PendingRenderKeepsTheOldPictureAndErrorsClearItAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        var renderer = new ControlledPreview();
        byte[] image = [1];
        var context = LayerImageExportTestData.CreateContext() with { RenderPngAsync = renderer.RenderAsync };
        using var vm = new LayerImageExportViewModel(context, clock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        var first = await renderer.NextAsync();
        first.Completion.SetResult(image);
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        vm.PreviewSettings.PixelsY = 200;
        clock.Advance(TimeSpan.FromMilliseconds(150));
        var second = await renderer.NextAsync();
        Assert.True(vm.IsPreviewUpdating);
        Assert.Same(image, vm.PreviewPng);
        second.Completion.SetException(new InvalidOperationException("render failed"));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Null(vm.PreviewPng);
        Assert.Equal("Could not update preview.", vm.PreviewErrorText);
        Assert.False(vm.IsPreviewUpdating);
    }

    [AvaloniaFact]
    public async Task FileChoicesAndSearchDoNotInvalidatePreviewAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        int renders = 0;
        var context = LayerImageExportTestData.CreateContext() with
        {
            RenderPngAsync = (_, _, _) =>
            {
                renders++;
                return Task.FromResult<byte[]?>(null);
            },
        };
        using var vm = new LayerImageExportViewModel(context, clock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        vm.FileSettings.Format = DxfLayerImageFormat.Jpg;
        vm.FileSettings.OneFilePerLayer = true;
        vm.SearchText = "nc";
        clock.Advance(TimeSpan.FromSeconds(1));
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal(1, renders);
    }

    [AvaloniaFact]
    public async Task ClosingCancelsAndRejectsAnOutstandingRenderAsync()
    {
        var clock = LayerImageExportTestData.CreateClock();
        var renderer = new ControlledPreview();
        byte[] image = [1];
        var context = LayerImageExportTestData.CreateContext() with { RenderPngAsync = renderer.RenderAsync };
        using var vm = new LayerImageExportViewModel(context, clock);
        clock.Advance(TimeSpan.FromMilliseconds(150));
        var pending = await renderer.NextAsync();

        vm.Dispose();
        pending.Completion.SetResult(image);
        await vm.WaitForPreviewAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(pending.Token.IsCancellationRequested);
        Assert.Null(vm.PreviewPng);
        Assert.False(vm.IsPreviewUpdating);
    }

    private sealed class ControlledPreview
    {
        // Channel owns cross-await publication; tests complete each render explicitly.
        private readonly Channel<PendingPreview> _calls = Channel.CreateUnbounded<PendingPreview>();

        public Task<byte[]?> RenderAsync(LayerImageRenderRequest request, int maxSidePixels, CancellationToken token)
        {
            var call = new PendingPreview(request, maxSidePixels,
                new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously), token);
            Assert.True(_calls.Writer.TryWrite(call));
            return call.Completion.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        }

        public async Task<PendingPreview> NextAsync() => await _calls.Reader.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private sealed record PendingPreview(
        LayerImageRenderRequest Request,
        int MaxSidePixels,
        TaskCompletionSource<byte[]?> Completion,
        CancellationToken Token);
}
