// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SelectionLocateEffectsBehaviorTests
{
    [Fact]
    public void CadMatchActions_FocusUsesMinimumZoomAndHighlightReplacesSelectionWithoutMovingTheView()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;

        vm.FocusCadMatches(10);

        AssertSelection(vm, host, 10, 100);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);
        Assert.Equal("Match focused: CAD 10 -> 1 regular pad(s).", vm.StatusText);

        vm.HighlightCadMatches(20);

        AssertSelection(vm, host, 20, 200);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);
        Assert.Equal(0, host.FocusSelectionCount);
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.Equal("Match highlighted: CAD 20 -> 1 regular pad(s).", vm.StatusText);
    }

    [Fact]
    public void RegularMatchActions_HighlightKeepsTheViewAndFocusReplacesSelectionWithMinimumZoom()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;

        vm.HighlightRegularMatches(100);

        AssertSelection(vm, host, 10, 100);
        Assert.Equal(0, host.FocusWithMinZoomCount);
        Assert.Equal(0.0, host.LastMinZoom);
        Assert.Equal("Match highlighted: Regular 100 -> 1 CAD pad(s).", vm.StatusText);

        vm.FocusRegularMatches(200);

        AssertSelection(vm, host, 20, 200);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);
        Assert.Equal(0, host.FocusSelectionCount);
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.Equal("Match focused: Regular 200 -> 1 CAD pad(s).", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task LocateSavedMatchTarget_AfterLayerToggle_SelectsOnlyVisiblePadsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        var hiddenCad = vm.CadPads.Single(static pad => pad.Layer == "signal-a");
        var visibleCad = vm.CadPads.Single(static pad => pad.Layer == "signal-b");
        var link = Assert.Single(vm.GetMatchedRegularLinks(hiddenCad.Id));
        var regular = vm.RegularPads.Single(pad => pad.RegularPadId == link.RegularPadId);
        var item = new NotchValidationDisplayItem(
            KindText: "DIRECT",
            RowText: "#1",
            VersionText: "2.2",
            IcText: "IC 1",
            SourceText: "Source",
            TargetText: "Target",
            RatioText: string.Empty,
            AreaText: string.Empty,
            ValuesText: string.Empty,
            CommentText: string.Empty,
            NoteText: string.Empty,
            SourceRegularPadId: regular.RegularPadId,
            TargetRegularPadId: regular.RegularPadId,
            CadPadId: hiddenCad.Id,
            IcIndex: regular.IcIndex,
            SourceDiffIndex: regular.DiffIndex,
            TargetDiffIndex: regular.DiffIndex,
            IsDirect: true,
            IsIncoming: false,
            IsOutgoing: false);
        vm.ApplyLayerSelection(vm.LayerToggles.Single(static toggle => toggle.Name == "signal-a"), soloSelect: false);
        await vm.WaitForGridRebuildIdleAsync();
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == hiddenCad.Id);
        Assert.Empty(vm.MatchLinksForCanvas);
        var host = new SelectionLayerCanvasProbe();
        vm.CanvasHost = host;

        vm.FocusNotchValidationItemCommand.Execute(item);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(host.SelectedCadIds);
        Assert.Equal(regular.RegularPadId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(regular.RegularPadId, Assert.Single(host.SelectedRegularIds));
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(2.0, host.LastMinZoom);
        Assert.Equal("Validation focus: DIRECT #1 (CAD=0, REG=1).", vm.StatusText);

        vm.HighlightCadPadGroup([hiddenCad.Id, visibleCad.Id, hiddenCad.Id]);

        Assert.Equal(visibleCad.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(visibleCad.Id, Assert.Single(host.SelectedCadIds));
        Assert.Empty(vm.SelectedRegularPadIndices);
        Assert.Empty(host.SelectedRegularIds);
        Assert.Equal(1, host.FocusWithMinZoomCount);
        Assert.Equal(0, host.FocusSelectionCount);
        Assert.Equal(0, host.FitToContentCount);
        Assert.Equal(0, host.ResetViewCount);
        Assert.Equal("Owner CAD highlighted: 1 pad(s).", vm.StatusText);
    }

    private static void AssertSelection(
        FreeformHelperViewModel vm, SelectionLayerCanvasProbe host, int cadId, int regularId)
    {
        Assert.Equal(cadId, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(cadId, Assert.Single(host.SelectedCadIds));
        Assert.Equal(regularId, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal(regularId, Assert.Single(host.SelectedRegularIds));
    }

    private static FreeformHelperViewModel CreateWorkspace()
    {
        var first = TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 10, 10);
        var second = TestGeometryFactory.CreateRegularPad(0, 1, 200, 10, 0, 20, 10);
        first.MatchedCadPadId = 10;
        second.MatchedCadPadId = 20;
        return new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<CadPad>
            {
                TestGeometryFactory.CreateCadPad(10, "signal", 0, 0, 10, 10),
                TestGeometryFactory.CreateCadPad(20, "signal", 10, 0, 20, 10),
            },
            RegularPads = new ObservableCollection<RegularPad> { first, second },
        };
    }
}
