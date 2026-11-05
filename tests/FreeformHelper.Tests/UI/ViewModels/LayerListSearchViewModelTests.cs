using System.Collections.ObjectModel;
using FreeformHelper.UI.ViewModels;
using Xunit;
using LayerToggle = FreeformHelper.UI.ViewModels.FreeformHelperViewModel.LayerToggle;

namespace FreeformHelper.Tests;

public sealed class LayerListSearchViewModelTests
{
    [Fact]
    public void Attach_WithoutSearchText_ShowsAllLayersInSourceOrder()
    {
        var source = CreateLayers("PAD_TOP", "OUTLINE", "PAD_BOTTOM");
        var vm = new LayerListSearchViewModel();

        vm.Attach(source);

        Assert.Equal(["PAD_TOP", "OUTLINE", "PAD_BOTTOM"], Names(vm));
        Assert.False(vm.HasSearchText);
        Assert.Equal(string.Empty, vm.MatchSummary);
    }

    [Fact]
    public void SearchText_FiltersByNameAndReportsSummary()
    {
        var vm = new LayerListSearchViewModel();
        vm.Attach(CreateLayers("PAD_TOP", "OUTLINE", "PAD_BOTTOM"));

        vm.SearchText = "pad";

        Assert.Equal(["PAD_TOP", "PAD_BOTTOM"], Names(vm));
        Assert.True(vm.HasSearchText);
        Assert.Equal("2 of 3 layers", vm.MatchSummary);
    }

    [Fact]
    public void ClearSearchCommand_RestoresAllLayers()
    {
        var vm = new LayerListSearchViewModel();
        vm.Attach(CreateLayers("PAD_TOP", "OUTLINE"));
        vm.SearchText = "outline";

        vm.ClearSearchCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(["PAD_TOP", "OUTLINE"], Names(vm));
    }

    [Fact]
    public void VisibleLayers_ShareTogglesWithSource_SoSelectionReachesRealLayer()
    {
        var source = CreateLayers("PAD_TOP", "OUTLINE");
        var vm = new LayerListSearchViewModel();
        vm.Attach(source);
        vm.SearchText = "outline";

        Assert.Single(vm.VisibleLayers).IsSelected = false;

        Assert.False(source[1].IsSelected);
        Assert.True(source[0].IsSelected);
    }

    [Fact]
    public void Search_DoesNotChangeLayerSelection()
    {
        var source = CreateLayers("PAD_TOP", "OUTLINE");
        var vm = new LayerListSearchViewModel();
        vm.Attach(source);

        vm.SearchText = "no-such-layer";

        Assert.Empty(vm.VisibleLayers);
        Assert.All(source, static toggle => Assert.True(toggle.IsSelected));
        Assert.Equal("0 of 2 layers", vm.MatchSummary);
    }

    [Fact]
    public void SourceChanges_KeepSearchApplied()
    {
        var source = CreateLayers("PAD_TOP");
        var vm = new LayerListSearchViewModel();
        vm.Attach(source);
        vm.SearchText = "pad";

        source.Add(new LayerToggle("PAD_BOTTOM"));
        source.Add(new LayerToggle("OUTLINE"));
        Assert.Equal(["PAD_TOP", "PAD_BOTTOM"], Names(vm));

        source.Clear();
        Assert.Empty(vm.VisibleLayers);
    }

    [Fact]
    public void Attach_NewSource_StopsFollowingOldSource()
    {
        var oldSource = CreateLayers("OLD");
        var vm = new LayerListSearchViewModel();
        vm.Attach(oldSource);

        vm.Attach(CreateLayers("NEW"));
        oldSource.Add(new LayerToggle("OLD_2"));

        Assert.Equal(["NEW"], Names(vm));
    }

    [Fact]
    public void FreeformViewModel_ReplacingLayerToggles_UpdatesLayerSearch()
    {
        var vm = new FreeformHelperViewModel();
        vm.LayerSearch.SearchText = "top";

        vm.LayerToggles = CreateLayers("PAD_TOP", "OUTLINE");

        Assert.Equal(["PAD_TOP"], Names(vm.LayerSearch));
    }

    private static ObservableCollection<LayerToggle> CreateLayers(params string[] names) =>
        new(names.Select(static name => new LayerToggle(name)));

    private static string[] Names(LayerListSearchViewModel vm) =>
        vm.VisibleLayers.Select(static toggle => toggle.Name).ToArray();
}
