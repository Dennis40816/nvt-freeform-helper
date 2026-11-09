// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowLayerPointerTests
{
    [AvaloniaFact]
    public async Task LeftClick_SecondLayer_TogglesOnlyThatLayerWithoutSoloingAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        Assert.Equal("beta", vm.LayerToggles[1].Name);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.NotEmpty(vm.MatchLinksForCanvas);

        await fixture.ClickLayerAsync("beta", RawInputModifiers.None);

        Assert.False(vm.LayerToggles.Single(static layer => layer.Name == "beta").IsSelected);
        Assert.Equal(2, vm.LayerToggles.Count(static layer => layer.IsSelected));
        Assert.All(vm.LayerToggles.Where(static layer => layer.Name != "beta"), static layer => Assert.True(layer.IsSelected));
        Assert.Equal(2, vm.CadPads.Count);
        Assert.DoesNotContain(vm.CadPads, static pad => pad.Layer == "beta");
        Assert.False(vm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.Empty(vm.MatchLinksForCanvas);
    }
}
