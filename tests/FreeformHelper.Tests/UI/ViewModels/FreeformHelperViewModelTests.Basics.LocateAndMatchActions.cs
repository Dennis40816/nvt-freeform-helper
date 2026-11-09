using System.Collections.ObjectModel;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void FitCommand_WithCanvasHost_RequestsFitOnce()
    {
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad> { CreateRectCad(4809, 0, 0, 5, 5) },
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        Assert.Equal(0, host.FitToContentCount);

        vm.FitCommand.Execute(null);

        Assert.Equal(1, host.FitToContentCount);
    }

    [Fact]
    public void ClearSelectionCommand_WithCadAndRegularSelected_ClearsCanvasAndViewModelSelection()
    {
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad> { CreateRectCad(4809, 0, 0, 5, 5) },
            RegularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 4616, 0, 0, 5, 5) },
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        host.SetSelection(_selectedCad4809, _selectedRegular4616);
        vm.ApplyCanvasSelection(_selectedCad4809, _selectedRegular4616);
        Assert.Equal(4809, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(4616, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(4809, Assert.Single(host.SelectedCadIds));
        Assert.Equal(4616, Assert.Single(host.SelectedRegularIds));

        vm.ClearSelectionCommand.Execute(null);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(host.SelectedCadIds);
        Assert.Empty(host.SelectedRegularIds);
    }

    [Fact]
    public void QuickFocusCommand_WithExistingSelection_ReplacesSelectionWithFocusedCadPad()
    {
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>
            {
                CreateRectCad(4809, 0, 0, 5, 5),
                CreateRectCad(4810, 5, 0, 10, 5),
            },
            RegularPads = new ObservableCollection<RegularPad> { CreateRectRegular(0, 0, 4616, 0, 0, 5, 5) },
            QuickFocusQueryText = "cad 4810",
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        host.SetSelection(_selectedCad4809, _selectedRegular4616);
        vm.ApplyCanvasSelection(_selectedCad4809, _selectedRegular4616);

        vm.QuickFocusCommand.Execute(null);

        Assert.Equal(4810, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(4810, Assert.Single(host.SelectedCadIds));
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom, 6);
        Assert.Equal("Quick focus: CAD 4810 focused (CAD output=1).", vm.StatusText);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void FocusNotchValidationItemCommand_ListItem_FocusesItsCadAndRegularPads(int itemIndex)
    {
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>
            {
                CreateRectCad(1, 0, 0, 10, 5),
                CreateRectCad(2, 0, 5, 10, 10),
            },
            RegularPads = new ObservableCollection<RegularPad>
            {
                CreateRectRegular(0, 0, 1, 0, 0, 5, 5),
                CreateRectRegular(0, 1, 2, 5, 0, 10, 5),
                CreateRectRegular(1, 0, 3, 0, 5, 5, 10),
                CreateRectRegular(1, 1, 4, 5, 5, 10, 10),
            },
            NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>
            {
                CreateValidationDisplayItem("DIRECT", isDirect: true),
                CreateValidationDisplayItem("OUT", isOutgoing: true) with
                {
                    CadPadId = 2,
                    SourceRegularPadId = 3,
                    TargetRegularPadId = 4,
                },
            },
        };
        var host = new TestCanvasHost();
        vm.CanvasHost = host;
        var item = vm.NotchValidationItems[itemIndex];
        var expectedRegularIds = new[] { item.SourceRegularPadId, item.TargetRegularPadId };

        vm.FocusNotchValidationItemCommand.Execute(item);

        Assert.Equal(item.CadPadId, Assert.Single(host.SelectedCadIds));
        Assert.Equal(expectedRegularIds, host.SelectedRegularIds.OrderBy(static id => id));
        Assert.Equal(1, host.FocusWithMinZoomCount);
    }

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

        FreeformHelperViewModelPrivateAccess.Cad(vm) = cad;
        FreeformHelperViewModelPrivateAccess.Grid(vm) = grid;
        vm.LayerToggles.Clear();
        vm.LayerToggles.Add(new FreeformHelperViewModel.LayerToggle("L1", isSelected: false));

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.Equal("Diagnostics: no CAD output pads.", vm.DxfRegularMappingSummary);
        Assert.Equal(vm.DxfRegularMappingSummary, vm.StatusText);
        Assert.False(vm.HasDxfRegularMappingIssues);
        Assert.Empty(vm.DxfRegularMappingIssues);
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

        Assert.Equal(_selectedCad4809, host.SelectedCadIds.OrderBy(id => id).ToArray());
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
        Assert.Equal(_selectedRegular4616, host.SelectedRegularIds.OrderBy(id => id).ToArray());
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

        Assert.Equal(_selectedCad4809, host.SelectedCadIds.OrderBy(id => id).ToArray());
        Assert.Equal(_selectedRegular4616, host.SelectedRegularIds.OrderBy(id => id).ToArray());
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
        Assert.Equal(_selectedRegular200, host.SelectedRegularIds.OrderBy(id => id).ToArray());
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

        Assert.Equal(_selectedCad72, host.SelectedCadIds.OrderBy(id => id).ToArray());
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

    [Fact]
    public void UseSelectedRegularForNotchValidationCommand_StaleCanvasIndex_KeepsThePreviousTraceAndRequestsSelection()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = new FreeformHelperViewModel { NotchValidationRegularPadId = 11m };
        vm.RegularPads.Add(CreateRectRegular(0, 0, 11, 0, 0, 10, 10));
        vm.RegularPads.Add(CreateRectRegular(0, 1, 12, 10, 0, 20, 10));
        var previousRow = CreateValidationDisplayItem("DIRECT", isDirect: true);
        vm.NotchValidationItems.Add(previousRow);
        vm.NotchValidationDirectItems.Add(previousRow);
        vm.ApplyCanvasSelection([], [13]);
        Assert.Equal(13, Assert.Single(vm.SelectedRegularPadIndices));

        vm.UseSelectedRegularForNotchValidationCommand.Execute(null);

        Assert.Equal(11m, vm.NotchValidationRegularPadId);
        Assert.Equal("Validation: select one REG first.", vm.NotchValidationSummaryText);
        Assert.Equal("Validation: no Regular selected.", vm.StatusText);
        Assert.Equal(previousRow, Assert.Single(vm.NotchValidationItems));
        Assert.Equal(previousRow, Assert.Single(vm.NotchValidationDirectItems));
        Assert.Equal(13, Assert.Single(vm.SelectedRegularPadIndices));
    }
}
