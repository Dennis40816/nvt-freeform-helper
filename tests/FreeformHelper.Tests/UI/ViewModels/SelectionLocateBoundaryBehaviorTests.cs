// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SelectionLocateBoundaryBehaviorTests
{
    [Fact]
    public void QuickFocus_UnresolvedQueries_ReportTheTargetAndPreserveCurrentSelection()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.ApplyCanvasSelection([20], [200]);
        var queries = new (string Query, string Status)[]
        {
            ("cad", "Quick focus: missing numeric id. Use CAD 4809, REG 4616, or DIFF 72."),
            ("cad 999", "Quick focus: CAD 999 is not visible."),
            ("reg 999", "Quick focus: REG 999 is not visible."),
            ("diff 999", "Quick focus: FW diff 999 is not visible."),
            ("999", "Quick focus: 999 did not match visible CAD, REG, or FW diff."),
            ("cad 10 ic2", "Quick focus: IC 2 CAD 10 is not visible."),
            ("reg 100 ic2", "Quick focus: IC 2 REG 100 is not visible."),
        };

        foreach (var (query, status) in queries)
        {
            vm.QuickFocusQueryText = query;
            vm.QuickFocusCommand.Execute(null);

            Assert.Equal(status, vm.StatusText);
            Assert.Equal(20, Assert.Single(vm.SelectedCadPadIds));
            Assert.Equal(200, Assert.Single(vm.SelectedRegularPadIndices));
        }
    }

    [Fact]
    public void QuickFocus_AutoLookup_PrefersRegularIdentityThenFallsBackToDiffWithinTheRequestedIc()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.RegularPads = new ObservableCollection<RegularPad>
        {
            TestGeometryFactory.CreateRegularPad(0, 0, 72, 0, 0, 10, 10, diffIndex: 73),
            TestGeometryFactory.CreateRegularPad(0, 1, 100, 10, 0, 20, 10, diffIndex: 72, icIndex: 1),
            TestGeometryFactory.CreateRegularPad(0, 2, 101, 20, 0, 30, 10, diffIndex: 73, icIndex: 1),
        };
        vm.ApplyCanvasSelection([20], [101]);
        vm.QuickFocusQueryText = "72";

        vm.QuickFocusCommand.Execute(null);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(72, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Auto focus: REG 72 focused (visible REG=1).", vm.StatusText);

        vm.QuickFocusQueryText = "72 ic2";
        vm.QuickFocusCommand.Execute(null);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Auto focus: IC 2 FW diff 72 focused (CAD output=0, REG=1).", vm.StatusText);

        vm.QuickFocusQueryText = "73";
        vm.QuickFocusCommand.Execute(null);

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Equal([72, 101], vm.SelectedRegularPadIndices.Order());
        Assert.Equal("Auto focus: FW diff 73 focused (CAD output=0, REG=2).", vm.StatusText);
    }

    [Fact]
    public void MatchLocators_UseLegacyPadMatchesAndKeepSelectionWhenTheNextTargetHasNoVisibleMatch()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        vm.RegularPads[0].MatchedCadPadId = 10;
        Assert.Empty(vm.GetMatchedRegularLinks(10));
        Assert.Empty(vm.GetMatchedCadLinks(100));
        Assert.Equal(100, Assert.Single(vm.GetMatchedRegularPadIds(10)));
        Assert.Equal(10, Assert.Single(vm.GetMatchedCadPadIds(100)));

        vm.FocusCadMatches(10);

        Assert.Equal(10, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Match focused: CAD 10 -> 1 regular pad(s).", vm.StatusText);

        vm.HighlightRegularMatches(100);

        Assert.Equal(10, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Equal("Match highlighted: Regular 100 -> 1 CAD pad(s).", vm.StatusText);

        vm.FocusCadMatches(20);
        Assert.Equal("CAD 20 has no overlap match.", vm.StatusText);
        vm.FocusRegularMatches(200);
        Assert.Equal("Regular 200 has no overlap match.", vm.StatusText);
        vm.FocusCadMatches(999);
        Assert.Equal("CAD 999 is not visible.", vm.StatusText);
        vm.FocusRegularMatches(999);
        Assert.Equal("Regular 999 is not visible.", vm.StatusText);
        Assert.Empty(vm.GetMatchedCadPadIds(999));
        Assert.Null(vm.GetRegularPadIcDiff(999));
        Assert.Equal(10, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(100, Assert.Single(vm.SelectedRegularPadIndices));
    }

    private static FreeformHelperViewModel CreateWorkspace() => new()
    {
        CadPads = new ObservableCollection<CadPad>
        {
            TestGeometryFactory.CreateCadPad(10, "signal", 0, 0, 10, 10),
            TestGeometryFactory.CreateCadPad(20, "signal", 10, 0, 20, 10),
        },
        RegularPads = new ObservableCollection<RegularPad>
        {
            TestGeometryFactory.CreateRegularPad(0, 0, 100, 0, 0, 10, 10, diffIndex: 72),
            TestGeometryFactory.CreateRegularPad(0, 1, 200, 10, 0, 20, 10, diffIndex: 73, icIndex: 1),
        },
    };
}
