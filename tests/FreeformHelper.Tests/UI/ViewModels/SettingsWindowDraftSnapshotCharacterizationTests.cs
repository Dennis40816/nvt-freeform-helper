// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Pins what Save in the settings window does with a value that changed in the project after the
/// window opened (issue #64, inventory finding SETTINGS-D01). The draft copies the whole project
/// state when it opens. Once any field differs from the live value, Save writes the whole draft
/// back, not only the fields the user edited in the window. A live edit that the draft never
/// touched is overwritten with the opening value. This test describes the current result, not
/// the wanted one.
/// A fix that tracks the edited fields changes the expected values here and must say so in its
/// pull request.
/// </summary>
public sealed class SettingsWindowDraftSnapshotCharacterizationTests
{
    private const decimal LiveCap = 150m;
    private const decimal FontStep = 8m;

    [Fact]
    public void Save_LiveEditMadeAfterTheWindowOpened_IsOverwrittenByTheOpeningValue()
    {
        var vm = new FreeformHelperViewModel();
        var openingCap = vm.TargetCoverageCapPercent;
        var openingFont = vm.GlobalFontSizePercent;
        Assert.NotEqual(LiveCap, openingCap);
        var draft = vm.CreateSettingsWindowViewModel();

        // The project changes while the window is open. The user edits only the font in the window.
        vm.TargetCoverageCapPercent = LiveCap;
        Assert.Equal(LiveCap, vm.TargetCoverageCapPercent);
        draft.GlobalFontSizePercent = openingFont + FontStep;
        draft.SaveCommand.Execute(null);

        Assert.Equal(openingFont + FontStep, vm.GlobalFontSizePercent);
        // The draft never touched the cap, but Save wrote the opening value back over the live edit.
        Assert.Equal(openingCap, vm.TargetCoverageCapPercent);
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
}
