// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// The settings window is not modal. Its draft copies the project state when the window opens, and
/// the workflow panels can change the same values while the window stays open. Save must write back
/// only the fields the user edited in the window (issue #64, inventory finding SETTINGS-D01). The
/// old behavior wrote the whole opening snapshot back and overwrote those live edits.
/// </summary>
public sealed class SettingsWindowDraftSnapshotTests
{
    private const decimal LiveCap = 150m;
    private const decimal FontStep = 8m;

    [Fact]
    public void Save_LiveEditMadeAfterTheWindowOpened_IsKept()
    {
        var vm = new FreeformHelperViewModel();
        var openingCap = vm.TargetCoverageCapPercent;
        var openingFont = vm.GlobalFontSizePercent;
        Assert.NotEqual(LiveCap, openingCap);
        var draft = vm.CreateSettingsWindowViewModel();

        // The project changes while the window is open. The user edits only the font in the window.
        vm.TargetCoverageCapPercent = LiveCap;
        draft.GlobalFontSizePercent = openingFont + FontStep;
        draft.SaveCommand.Execute(null);

        Assert.Equal(openingFont + FontStep, vm.GlobalFontSizePercent);
        Assert.Equal(LiveCap, vm.TargetCoverageCapPercent);
    }

    [Fact]
    public void Save_WithoutALiveEdit_KeepsTheFieldsTheDraftDidNotTouch()
    {
        var vm = new FreeformHelperViewModel();
        var openingCap = vm.TargetCoverageCapPercent;
        var openingFont = vm.GlobalFontSizePercent;
        var draft = vm.CreateSettingsWindowViewModel();

        draft.GlobalFontSizePercent = openingFont + FontStep;
        draft.SaveCommand.Execute(null);

        Assert.Equal(openingFont + FontStep, vm.GlobalFontSizePercent);
        Assert.Equal(openingCap, vm.TargetCoverageCapPercent);
    }

    [Fact]
    public void Save_KeepsTheStep3GuardValuesChangedInTheWorkflowPanelWhileTheWindowWasOpen()
    {
        var vm = new FreeformHelperViewModel
        {
            EnableTargetCoverageGuard = false,
            TargetCoverageCapPercent = 128m,
            EnableBoundaryVirtualAreaCap = false,
            BoundaryVirtualAreaCapPercent = 100m,
        };
        var draft = vm.CreateSettingsWindowViewModel();

        // The user edits the Step 3 panel on the right while the window stays open.
        vm.EnableTargetCoverageGuard = true;
        vm.TargetCoverageCapPercent = 77m;
        vm.EnableBoundaryVirtualAreaCap = true;
        vm.BoundaryVirtualAreaCapPercent = 33m;

        draft.GlobalFontSizePercent += FontStep;
        draft.SaveCommand.Execute(null);

        Assert.Equal(
            (true, 77m, true, 33m),
            (vm.EnableTargetCoverageGuard,
             vm.TargetCoverageCapPercent,
             vm.EnableBoundaryVirtualAreaCap,
             vm.BoundaryVirtualAreaCapPercent));
    }

    [Fact]
    public void Save_AppliesAValueTheUserEditedInTheWindowEvenWhenThePanelChangedItToo()
    {
        var vm = new FreeformHelperViewModel { TargetCoverageCapPercent = 128m };
        var draft = vm.CreateSettingsWindowViewModel();

        vm.TargetCoverageCapPercent = 77m;
        draft.TargetCoverageCapPercent = 50m;
        draft.SaveCommand.Execute(null);

        Assert.Equal(50m, vm.TargetCoverageCapPercent);
    }

    [Fact]
    public void Save_KeepsOtherSharedValuesChangedWhileTheWindowWasOpen()
    {
        var vm = new FreeformHelperViewModel();
        var draft = vm.CreateSettingsWindowViewModel();

        vm.ShowNotchToRegularLabels = !vm.ShowNotchToRegularLabels;
        vm.ToFullStrictOverlapPercent += 5m;
        var panelValues = (vm.ShowNotchToRegularLabels, vm.ToFullStrictOverlapPercent);

        draft.GlobalFontSizePercent += FontStep;
        draft.SaveCommand.Execute(null);

        Assert.Equal(panelValues, (vm.ShowNotchToRegularLabels, vm.ToFullStrictOverlapPercent));
    }
}
