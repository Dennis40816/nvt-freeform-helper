using System.Collections.ObjectModel;
using System.Reflection;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public async Task MatchCommand_WhenPrerequisitesMissing_SetsStep1Status()
    {
        var vm = new FreeformHelperViewModel();

        await vm.MatchCommand.ExecuteAsync(null);

        Assert.Equal("Step 1: import DXF and build grid first.", vm.StatusText);
    }


    [Fact]
    public async Task AutoDetectFreeformsCommand_WhenPrerequisitesMissing_SetsStep2Status()
    {
        var vm = new FreeformHelperViewModel();

        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);

        Assert.Equal("Step 2: import DXF and run Step 1 first.", vm.StatusText);
    }


    [Fact]
    public async Task AnalyzeIndexMappingCommand_WhenPrerequisitesMissing_SetsStep4Status()
    {
        var vm = new FreeformHelperViewModel();

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.Equal("Step 4: import DXF and build grid first.", vm.StatusText);
    }


    [Fact]
    public async Task AnalyzeIndexMappingCommand_WhenNoVisibleCadPads_KeepsDiagnosticsEmptyState()
    {
        var vm = new FreeformHelperViewModel();
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);

        var cadPads = new[] { CreateRectCad(1, 0, 0, 10, 10) };
        var cad = new CadPadSet(cadPads);
        var regularPads = new[]
        {
            CreateRectRegular(0, 0, 0, 0, 0, 10, 10),
        };
        var gridXEdges = new[] { 0.0, 10.0 };
        var gridYEdges = new[] { 0.0, 10.0 };
        var grid = new RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: gridXEdges,
            yEdges: gridYEdges,
            pads: regularPads);

        cadField!.SetValue(vm, cad);
        gridField!.SetValue(vm, grid);
        vm.LayerToggles.Clear();
        vm.LayerToggles.Add(new FreeformHelperViewModel.LayerToggle("L1", isSelected: false));

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.Equal("Diagnostics: no CAD output pads.", vm.DxfRegularMappingSummary);
        Assert.Equal(vm.DxfRegularMappingSummary, vm.StatusText);
        Assert.False(vm.HasDxfRegularMappingIssues);
        Assert.Empty(vm.DxfRegularMappingIssues);
    }


    [Fact]
    public void LocateCadByIdCommand_FocusesCadSelection()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(10, 0, 0, 5, 5) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 100, 0, 0, 5, 5) };
        var expectedCadIds = new[] { 10 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
            QuickLocateCadPadId = 10,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.LocateCadByIdCommand.Execute(null);

        Assert.Equal(expectedCadIds, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("CAD 10 focused (CAD output=1).", vm.StatusText);
    }


    [Fact]
    public void LocateRegularByIndexCommand_FocusesRegularSelection()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(33, 0, 0, 6, 6) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 201, 0, 0, 6, 6) };
        var expectedRegularIds = new[] { 201 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
            QuickLocateRegularPadIndex = 201,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.LocateRegularByIndexCommand.Execute(null);

        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("Regular index 201 focused (visible REG=1).", vm.StatusText);
    }


    [Fact]
    public void LocateRegularByIndexCommand_WhenTargetMissing_SetsStatus()
    {
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 201, 0, 0, 6, 6) };
        var vm = new FreeformHelperViewModel
        {
            RegularPads = regularPads,
            QuickLocateRegularPadIndex = 999,
        };

        vm.LocateRegularByIndexCommand.Execute(null);

        Assert.Equal("Regular index 999 is not visible.", vm.StatusText);
    }

    [Fact]
    public void QuickFocusCommand_WithCadQuery_FocusesCadSelection()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(4809, 0, 0, 5, 5) };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            QuickFocusQueryText = "cad 4809",
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(SelectedCad4809, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("Quick focus: CAD 4809 focused (CAD output=1).", vm.StatusText);
    }

    [Fact]
    public void QuickFocusCommand_WithRegularQuery_FocusesRegularSelection()
    {
        var regularPads = new ObservableCollection<RegularPad>
        {
            CreateRectRegular(0, 0, 4616, 0, 0, 5, 5),
        };
        var vm = new FreeformHelperViewModel
        {
            RegularPads = regularPads,
            QuickFocusQueryText = "reg 4616",
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.QuickFocusCommand.Execute(null);

        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(SelectedRegular4616, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("Quick focus: REG 4616 focused (visible REG=1).", vm.StatusText);
    }

    [Fact]
    public void QuickFocusCommand_WithDiffQuery_FocusesFwDiffAcrossCadAndRegular()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(4809, 0, 0, 5, 5) };
        var regular = CreateRectRegular(0, 0, 4616, 0, 0, 5, 5);
        regular.DiffIndex = 72;
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = new ObservableCollection<RegularPad> { regular },
            QuickFocusQueryText = "diff 72",
        };
        SetCadOutputFwDiff(vm, cadPadId: 4809, diffIndex: 72, icIndex: 0);
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(SelectedCad4809, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(SelectedRegular4616, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("Quick focus: FW diff 72 focused (CAD output=1, REG=1).", vm.StatusText);
    }

    [Fact]
    public void QuickFocusCommand_WithIcDiffQuery_FiltersByIc()
    {
        var regularIc1 = CreateRectRegular(0, 0, 100, 0, 0, 5, 5);
        regularIc1.DiffIndex = 72;
        regularIc1.IcIndex = 0;
        var regularIc2 = CreateRectRegular(0, 1, 200, 5, 0, 10, 5);
        regularIc2.DiffIndex = 72;
        regularIc2.IcIndex = 1;
        var vm = new FreeformHelperViewModel
        {
            RegularPads = new ObservableCollection<RegularPad> { regularIc1, regularIc2 },
            QuickFocusQueryText = "ic2 diff72",
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.QuickFocusCommand.Execute(null);

        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(SelectedRegular200, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal("Quick focus: IC 2 FW diff 72 focused (CAD output=0, REG=1).", vm.StatusText);
    }

    [Fact]
    public void QuickFocusCommand_WithPlainNumber_AutoResolvesCadBeforeRegularAndDiff()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(72, 0, 0, 5, 5) };
        var regular = CreateRectRegular(0, 0, 72, 0, 0, 5, 5);
        regular.DiffIndex = 72;
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = new ObservableCollection<RegularPad> { regular },
            QuickFocusQueryText = "72",
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;

        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(SelectedCad72, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal("Auto focus: CAD 72 focused (CAD output=1).", vm.StatusText);
    }


    [Fact]
    public void FocusCadMatches_FocusesSelectionWithMinZoom()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(10, 0, 0, 5, 5) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 100, 0, 0, 5, 5) };
        var expectedCadIds = new[] { 10 };
        var expectedRegularIds = new[] { 100 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        SetPadMatchResult(vm, cadPadId: 10, regularPadId: 100);

        vm.FocusCadMatches(10);

        Assert.Equal(expectedCadIds, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom, 6);
        Assert.Equal("Match focused: CAD 10 -> 1 regular pad(s).", vm.StatusText);
    }


    [Fact]
    public void HighlightCadMatches_DoesNotTriggerFocusZoom()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(20, 0, 0, 5, 5) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 1, 101, 5, 0, 10, 5) };
        var expectedCadIds = new[] { 20 };
        var expectedRegularIds = new[] { 101 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        SetPadMatchResult(vm, cadPadId: 20, regularPadId: 101);

        vm.HighlightCadMatches(20);

        Assert.Equal(expectedCadIds, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(0, host.FocusWithMinZoomCount);
        Assert.Equal("Match highlighted: CAD 20 -> 1 regular pad(s).", vm.StatusText);
    }


    [Fact]
    public void FocusRegularMatches_FocusesSelectionWithMinZoom()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(33, 0, 0, 6, 6) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 201, 0, 0, 6, 6) };
        var expectedCadIds = new[] { 33 };
        var expectedRegularIds = new[] { 201 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        SetPadMatchResult(vm, cadPadId: 33, regularPadId: 201);

        vm.FocusRegularMatches(201);

        Assert.Equal(expectedCadIds, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom, 6);
        Assert.Equal("Match focused: Regular 201 -> 1 CAD pad(s).", vm.StatusText);
    }


    [Fact]
    public void HighlightRegularMatches_DoesNotTriggerFocusZoom()
    {
        var cadPads = new ObservableCollection<CadPad> { CreateRectCad(34, 0, 0, 6, 6) };
        var regularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 1, 202, 6, 0, 12, 6) };
        var expectedCadIds = new[] { 34 };
        var expectedRegularIds = new[] { 202 };
        var vm = new FreeformHelperViewModel
        {
            CadPads = cadPads,
            RegularPads = regularPads,
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        SetPadMatchResult(vm, cadPadId: 34, regularPadId: 202);

        vm.HighlightRegularMatches(202);

        Assert.Equal(expectedCadIds, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(id => id).ToArray());
        Assert.Equal(0, host.FocusWithMinZoomCount);
        Assert.Equal("Match highlighted: Regular 202 -> 1 CAD pad(s).", vm.StatusText);
    }

}
