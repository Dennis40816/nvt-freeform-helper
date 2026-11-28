// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchCompensationInputBehaviorCoverageTests
{
    [AvaloniaFact]
    public async Task CompensationQueries_GridWithoutLoadedCad_ReturnNoDetailsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = await CreateGridViewModelAsync();
        Assert.Single(vm.RegularPads);
        Assert.Empty(vm.CadPads);

        Assert.Null(vm.GetCadV22CompensationPreview(1));
        Assert.Null(vm.GetCadV22ResolvedResult(1));
        Assert.Null(vm.GetCadV22TargetAllocationSummary(1));
        Assert.Null(vm.GetCadV22StageOverlays(1));
    }

    private static async Task<FreeformHelperViewModel> CreateGridViewModelAsync()
    {
        var vm = new FreeformHelperViewModel
        {
            ActiveAreaWidth = 10,
            ActiveAreaHeight = 10,
            GridAlignmentMode = GridAlignmentMode.FromPanelAa,
            GridPaddingPercent = 0,
            RecalcBoundsOnLayerFilter = false,
            EnableToRegular = true,
            EnableToFull = false,
            NotchPreviewAutoPlayEnabled = false,
        };
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 1;
        cascade.YChannels = 1;
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
        Assert.Single(vm.RegularPads);
        return vm;
    }
}
