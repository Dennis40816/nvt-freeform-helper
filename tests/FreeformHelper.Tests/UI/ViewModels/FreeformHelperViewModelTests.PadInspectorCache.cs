using System.Collections.ObjectModel;
using System.Reflection;
using FreeformHelper.Domain.Pads;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void BuildCadPadInspectorSnapshot_ReusesMatchedDetailsAcrossCalls_WhenRevisionUnchanged()
    {
        var cad = CreateRectCad(11, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 101, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 7;
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
        };
        SetPadMatchResult(vm, cad.Id, regular.RegularPadId);

        var first = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true,
            includeRuleTrace: true);
        var second = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true,
            includeRuleTrace: true);

        var firstCad = Assert.IsType<CadPadInspectorSnapshot>(first?.Cad);
        var secondCad = Assert.IsType<CadPadInspectorSnapshot>(second?.Cad);
        Assert.Same(firstCad.MatchedRegularDetails, secondCad.MatchedRegularDetails);
        Assert.Equal(firstCad.MatchText, secondCad.MatchText);
    }

    [Fact]
    public void BuildCadPadInspectorSnapshot_RebuildsMatchedDetails_WhenRevisionChanges()
    {
        var cad = CreateRectCad(12, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 102, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 8;
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
        };
        SetPadMatchResult(vm, cad.Id, regular.RegularPadId);

        var first = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true,
            includeRuleTrace: true);
        InvokePrivateNoArg(vm, "BumpNotchExportGridFingerprint");
        var second = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: false,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: true,
            includeRuleTrace: true);

        var firstCad = Assert.IsType<CadPadInspectorSnapshot>(first?.Cad);
        var secondCad = Assert.IsType<CadPadInspectorSnapshot>(second?.Cad);
        Assert.NotSame(firstCad.MatchedRegularDetails, secondCad.MatchedRegularDetails);
    }

    [Fact]
    public void BuildRegularPadInspectorSnapshot_ReusesRuleTraceAcrossCalls_WhenRevisionUnchanged()
    {
        var cad = CreateRectCad(13, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 103, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 9;
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
        };
        SetPadMatchResult(vm, cad.Id, regular.RegularPadId);

        var first = vm.BuildRegularPadInspectorSnapshot(
            regular.RegularPadId,
            includeMatchDetails: true,
            includeRuleTrace: true);
        var second = vm.BuildRegularPadInspectorSnapshot(
            regular.RegularPadId,
            includeMatchDetails: true,
            includeRuleTrace: true);

        var firstRegular = Assert.IsType<RegularPadInspectorSnapshot>(first?.Regular);
        var secondRegular = Assert.IsType<RegularPadInspectorSnapshot>(second?.Regular);
        Assert.Same(firstRegular.RuleTrace, secondRegular.RuleTrace);
    }

    [Fact]
    public void BuildRegularPadInspectorSnapshot_ReportsOverrideFreeformSource_WhenOverrideExists()
    {
        var cad = CreateRectCad(14, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 104, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 10;
        regular.Freeform = FreeformType.XWay;
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
        };
        SetPadMatchResult(vm, cad.Id, regular.RegularPadId);

        var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(projectField);
        var project = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(projectField!.GetValue(vm));
        project.FreeformOverrides[regular.Index] = FreeformType.XWay;

        var snapshot = vm.BuildRegularPadInspectorSnapshot(
            regular.RegularPadId,
            includeMatchDetails: true,
            includeRuleTrace: true);

        var regularSnapshot = Assert.IsType<RegularPadInspectorSnapshot>(snapshot?.Regular);
        Assert.Equal("Override", regularSnapshot.FreeformSource);
        Assert.Contains("override", regularSnapshot.FreeformSourceDetail, StringComparison.OrdinalIgnoreCase);
        var trace = Assert.Single(regularSnapshot.RuleTrace, static entry => entry.Rule == "Freeform");
        Assert.Contains("Source: Override", trace.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRegularPadInspectorSnapshot_ReportsStep2AutoDetectFreeformSource_WhenNoOverrideExists()
    {
        var cad = CreateRectCad(15, 0, 0, 10, 10);
        var regular = CreateRectRegular(0, 0, 105, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 11;
        regular.Freeform = FreeformType.YWay;
        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(new[] { cad }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
        };
        SetPadMatchResult(vm, cad.Id, regular.RegularPadId);

        var snapshot = vm.BuildRegularPadInspectorSnapshot(
            regular.RegularPadId,
            includeMatchDetails: true,
            includeRuleTrace: true);

        var regularSnapshot = Assert.IsType<RegularPadInspectorSnapshot>(snapshot?.Regular);
        Assert.Equal("Step 2 auto-detect", regularSnapshot.FreeformSource);
        Assert.Contains("auto-detect", regularSnapshot.FreeformSourceDetail, StringComparison.OrdinalIgnoreCase);
        var trace = Assert.Single(regularSnapshot.RuleTrace, static entry => entry.Rule == "Freeform");
        Assert.Contains("Source: Step 2 auto-detect", trace.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCadPadInspectorSnapshot_BlockedBoundaryCandidate_UsesResolvedToFullDisabledState()
    {
        var cad = CreateRectCad(21, 4, 4, 6, 6);
        var blockerLeft = CreateRectCad(22, 0, 0, 4, 10);
        var blockerRight = CreateRectCad(23, 6, 0, 10, 10);
        var blockerTop = CreateRectCad(24, 4, 0, 6, 4);
        var blockerBottom = CreateRectCad(25, 4, 6, 6, 10);
        var regular = CreateRectRegular(0, 0, 104, 0, 0, 10, 10);
        regular.IcIndex = 0;
        regular.DiffIndex = 10;

        var vm = new FreeformHelperViewModel
        {
            CadPads = new ObservableCollection<FreeformHelper.Domain.Pads.CadPad>(
                new[] { cad, blockerLeft, blockerRight, blockerTop, blockerBottom }),
            RegularPads = new ObservableCollection<FreeformHelper.Domain.Pads.RegularPad>(new[] { regular }),
            EnableToRegular = true,
            EnableToFull = true,
        };

        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var gridField = typeof(FreeformHelperViewModel).GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(gridField);
        cadField!.SetValue(
            vm,
            new FreeformHelper.Domain.Pads.CadPadSet(new[] { cad, blockerLeft, blockerRight, blockerTop, blockerBottom }));
        gridField!.SetValue(vm, new FreeformHelper.Domain.Pads.RegularGrid(
            rows: 1,
            cols: 1,
            xEdges: [0.0, 10.0],
            yEdges: [0.0, 10.0],
            pads: new[] { regular }));

        var snapshot = vm.BuildCadPadInspectorSnapshot(
            cad.Id,
            includeExpensiveNotchDetails: true,
            includeNotchRowEligibilityDetails: false,
            includeMatchDetails: false,
            includeRuleTrace: false);

        var notch = Assert.IsType<CadPadInspectorSnapshot>(snapshot?.Cad).Notch;
        Assert.NotNull(notch);
        Assert.False(notch!.IsToFullEnabled);
        Assert.Equal(cad.Area, notch.Stage3Area, 6);
        Assert.Equal(1.0, notch.ToFullRatio, 6);
    }

    private static void InvokePrivateNoArg(FreeformHelperViewModel vm, string methodName)
    {
        var method = typeof(FreeformHelperViewModel).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(vm, Array.Empty<object>());
    }
}
