// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ManualSizingRejectedRequestBehaviorTests
{
    [AvaloniaTheory]
    [InlineData(null, null, null)]
    [InlineData(-1, 0, null)]
    [InlineData(6, 21, "Manual sizing: Manual sizes exceed available total.")]
    public async Task ApplySizingToCells_RejectedRequest_KeepsTheProjectSavedAndAddsNoUndoAsync(
        int? width, int? height, string? expectedStatus)
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGridAsync(fixture);
        var before = vm.RegularPads.Select(static pad => pad.Bounds).ToArray();
        var status = vm.StatusText;
        var canUndoBefore = vm.CanUndo;
        vm.HasUnsavedChanges = false;

        vm.ApplySizingToCells([0], [0], width, height);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(expectedStatus ?? status, vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);
        Assert.Equal(canUndoBefore, vm.CanUndo);
        Assert.Equal(before, vm.RegularPads.Select(static pad => pad.Bounds).ToArray());
    }

    [AvaloniaFact]
    public async Task ApplySizingToCells_ValidRequest_MarksTheProjectUnsavedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var fixture = new WorkspaceFixture();
        var vm = await OpenGridAsync(fixture);
        vm.HasUnsavedChanges = false;

        vm.ApplySizingToCells([0], [0], 6m, null);

        Assert.Equal("Manual sizing applied.", vm.StatusText);
        await WaitForGridRebuildAsync(vm);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
    }

    private static async Task<FreeformHelperViewModel> OpenGridAsync(WorkspaceFixture fixture)
    {
        var vm = fixture.CreateViewModel();
        vm.ActiveAreaWidth = 40m;
        vm.ActiveAreaHeight = 20m;
        vm.GridPaddingPercent = 0m;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.IsWidthRowLocal = false;
        vm.IsHeightColumnLocal = false;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 2;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        Assert.Equal(4, vm.RegularPads.Count);
        return vm;
    }
}
