using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed class SelectionSummaryResultBehaviorTests
{
    private static readonly string[] _textNames =
    [
        nameof(FreeformHelperViewModel.SelectionSummary),
        nameof(FreeformHelperViewModel.SelectedGridRangeSummary),
        nameof(FreeformHelperViewModel.CadSelectionText),
        nameof(FreeformHelperViewModel.RegularSelectionText),
        nameof(FreeformHelperViewModel.SelectionDetailText),
    ];

    [Fact]
    public void NewViewModel_ShowsTheInitialSelectionTexts()
    {
        var vm = new FreeformHelperViewModel();

        Assert.Equal("No selection.", vm.SelectionSummary);
        Assert.Equal("Select regular pads to edit size.", vm.SelectedGridRangeSummary);
        Assert.Equal("CAD: 0 pads.", vm.CadSelectionText);
        Assert.Equal("Regular: 0 pads.", vm.RegularSelectionText);
        Assert.Equal(string.Empty, vm.SelectionDetailText);
        Assert.Null(vm.SelectedRegularRange);
    }

    [Fact]
    public void ApplyCanvasSelection_CadPads_ShowsCadTexts()
    {
        var vm = new FreeformHelperViewModel();

        vm.ApplyCanvasSelection([1, 2], []);

        Assert.Equal("Selected: CAD=2.", vm.SelectionSummary);
        Assert.Equal("Selected: 2 pads.", vm.SelectedGridRangeSummary);
        Assert.Equal("CAD: 2 pads.", vm.CadSelectionText);
        Assert.Equal("Regular: 0 pads.", vm.RegularSelectionText);
    }

    [Fact]
    public void ApplyCanvasSelection_SelectionCleared_ShowsTheComputedEmptyTexts()
    {
        var vm = new FreeformHelperViewModel();
        vm.ApplyCanvasSelection([1, 2], []);

        vm.ApplyCanvasSelection([], []);

        Assert.Equal("No selection.", vm.SelectionSummary);
        Assert.Equal("No selection.", vm.SelectedGridRangeSummary);
        Assert.Equal("CAD: 0 pads.", vm.CadSelectionText);
    }

    [Fact]
    public void ApplyCanvasSelection_ChangedSelection_RaisesOnlyTheChangedTexts()
    {
        var vm = new FreeformHelperViewModel();
        var changes = RecordNotifications(vm, _textNames);

        vm.ApplyCanvasSelection([1, 2], []);

        Assert.Equal(
            [
                nameof(FreeformHelperViewModel.SelectionSummary),
                nameof(FreeformHelperViewModel.SelectedGridRangeSummary),
                nameof(FreeformHelperViewModel.CadSelectionText),
            ],
            changes);
    }

    [Fact]
    public void ApplyCanvasSelection_OtherPadsWithTheSameTexts_RaisesNoTextNotification()
    {
        var vm = new FreeformHelperViewModel();
        vm.ApplyCanvasSelection([1, 2], []);
        var changes = RecordNotifications(vm, _textNames);

        vm.ApplyCanvasSelection([3, 4], []);

        Assert.Empty(changes);
    }

    [Fact]
    public void ApplyCanvasSelection_RegularPadsWithoutGrid_KeepsNoRegularRange()
    {
        var vm = new FreeformHelperViewModel();

        vm.ApplyCanvasSelection([], [10, 11, 12]);

        Assert.Equal("Regular: 3 pads.", vm.RegularSelectionText);
        Assert.Null(vm.SelectedRegularRange);
    }
}
