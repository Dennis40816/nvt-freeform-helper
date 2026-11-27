// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchDetailGenerationEntryTests
{
    [AvaloniaFact]
    public async Task DetailDoesNotReadOrWriteTheExportCacheAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ApplyCanvasSelection([vm.CadPads[0].Id], []);
        var opened = new List<NotchDetailViewModel>();
        vm.OpenNotchDetailAsync = detail => { opened.Add(detail); return Task.CompletedTask; };
        var before = vm.GetNotchExportGenerationCacheMetrics();

        await vm.ShowNotchDetailCommand.ExecuteAsync(null);

        Assert.Single(opened);
        Assert.Equal(before, vm.GetNotchExportGenerationCacheMetrics());
        Assert.False(vm.IsNotchExporting);
    }

    [AvaloniaFact]
    public async Task DetailStaysOpenWhileItsRowsAreWorkedOutAsync()
    {
        HeadlessAppBootstrap.EnsureInitialized();
        using var fixture = new SuppressedWorkspaceBehaviorTests.WorkspaceFixture();
        var vm = await fixture.OpenAsync();
        vm.ApplyCanvasSelection([vm.CadPads[0].Id], []);
        using var closed = new SemaphoreSlim(0);
        NotchDetailViewModel? opened = null;
        // The detail "stays open" until the test releases it.
        async Task HoldDetailAsync(NotchDetailViewModel detail)
        {
            opened = detail;
            await closed.WaitAsync(TestContext.Current.CancellationToken);
        }

        vm.OpenNotchDetailAsync = HoldDetailAsync;

        var command = vm.ShowNotchDetailCommand.ExecuteAsync(null);
        await HeadlessUiPump.WaitUntilAsync(() => opened is { ExplanationMessage: not "Working out rows..." },
            "The open detail window never received its rows.");

        Assert.False(command.IsCompleted);
        closed.Release();
        await command;
    }
}
