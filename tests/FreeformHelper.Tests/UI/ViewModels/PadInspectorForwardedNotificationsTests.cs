// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class PadInspectorForwardedNotificationsTests
{
    [AvaloniaFact]
    public async Task Selection_CadThenRegularThenClear_ForwardsOrderedNamesAndCurrentValuesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var changes = RecordNotifications(vm);

        vm.ApplyCanvasSelection([11], []);
        var cadDisplay = vm.PadInspectorDisplay;
        var cadChanges = changes.ToArray();
        changes.Clear();
        vm.ApplyCanvasSelection([], [101]);
        var regularDisplay = vm.PadInspectorDisplay;
        var regularChanges = changes.ToArray();
        changes.Clear();
        vm.ApplyCanvasSelection([], []);
        var emptyDisplay = vm.PadInspectorDisplay;
        var emptyChanges = changes.ToArray();
        await Task.WhenAll(
            work.LastStartedAsync(FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName),
            work.LastStartedAsync(FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CadNotifications(cadDisplay), cadChanges);
        Assert.Equal(RegularNotifications(regularDisplay), regularChanges);
        Assert.Equal(EmptyNotifications(emptyDisplay), emptyChanges);
        Assert.Equal(["Notch 2.2: Unavailable", "Notch rows: Notch rows: computing..."],
            cadDisplay.CompensationLines.Select(static line => line.DisplayText));
        Assert.Equal("Freeform: None", Assert.Single(regularDisplay.CompensationLines).DisplayText);
        AssertQueuedTrace(cadDisplay);
        AssertQueuedTrace(regularDisplay);
        Assert.Same(emptyDisplay, vm.PadInspectorDisplay);
        Assert.False(vm.HasPadInspectorSummary);
        Assert.True(vm.HasNoPadInspectorSummary);
        Assert.False(vm.HasPadInspectorMultiSelection);
        Assert.Empty(vm.PadInspectorSelectionSummaryText);
        Assert.Empty(vm.PadInspectorCompactHeaderText);
        Assert.Empty(vm.PadInspectorPrimaryText);
        Assert.Empty(vm.PadInspectorSecondaryText);
        Assert.Empty(vm.PadInspectorMatchText);
        Assert.Empty(vm.PadInspectorCompensationLines);
        Assert.Empty(vm.PadInspectorRuleTraceLines);
        Assert.Empty(vm.PadInspectorRuleTraceSections);
        Assert.False(vm.HasPadInspectorRuleTrace);
        Assert.Null(vm.PadInspectorUi.Snapshot);
        Assert.False(vm.PadInspectorUi.IsDeferredRefreshPending);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Selection_AnotherPadOfSameKind_PreservesUiStateAndReplacesDisplayCollectionsAsync(bool cad)
    {
        using var culture = TestCulture.UseEnUs();
        var vm = CreateWorkspace();
        var work = new RecordingBackgroundWorkRunner();
        vm.BackgroundWork.Runner = work;
        var workName = cad
            ? FreeformHelperViewModel.DeferredCadInspectorRefreshWorkName
            : FreeformHelperViewModel.DeferredRegularInspectorRefreshWorkName;
        vm.ApplyCanvasSelection(cad ? [11] : [], cad ? [] : [101]);
        var superseded = work.LastStartedAsync(workName);
        var ui = vm.PadInspectorUi;
        var firstSnapshot = ui.Snapshot;
        var firstDisplay = vm.PadInspectorDisplay;

        vm.ApplyCanvasSelection(cad ? [12] : [], cad ? [] : [102]);
        var secondUi = vm.PadInspectorUi;
        var secondSnapshot = secondUi.Snapshot;
        var secondDisplay = vm.PadInspectorDisplay;
        vm.ApplyCanvasSelection([], []);
        await Task.WhenAll(superseded, work.LastStartedAsync(workName))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotNull(firstSnapshot);
        Assert.NotNull(secondSnapshot);
        Assert.Equal(cad ? 12 : 102, cad ? secondSnapshot.Cad?.CadPadId : secondSnapshot.Regular?.RegularPadId);
        Assert.Same(ui, secondUi);
        Assert.Same(ui, vm.PadInspectorUi);
        Assert.NotSame(firstSnapshot, secondSnapshot);
        // Current code keeps PadInspectorUi, but replaces the display and all three collections, even within one pad kind.
        Assert.NotSame(firstDisplay, secondDisplay);
        Assert.NotSame(firstDisplay.CompensationLines, secondDisplay.CompensationLines);
        Assert.NotSame(firstDisplay.RuleTraceLines, secondDisplay.RuleTraceLines);
        Assert.NotSame(firstDisplay.RuleTraceSections, secondDisplay.RuleTraceSections);
        AssertQueuedTrace(secondDisplay);
    }

    private static FreeformHelperViewModel CreateWorkspace()
    {
        var vm = new FreeformHelperViewModel { CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect };
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(11, "signal", 0, 0, 5, 10));
        vm.CadPads.Add(TestGeometryFactory.CreateCadPad(12, "signal", 10, 0, 15, 10));
        vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 0, 101, 0, 0, 10, 10, diffIndex: 7));
        vm.RegularPads.Add(TestGeometryFactory.CreateRegularPad(0, 1, 102, 10, 0, 20, 10, diffIndex: 8));
        return vm;
    }

    private static List<(string Name, object? Value)> RecordNotifications(FreeformHelperViewModel vm)
    {
        var changes = new List<(string Name, object? Value)>();
        vm.PropertyChanged += (_, args) =>
        {
            var name = args.PropertyName;
            if (name is not null &&
                (name.StartsWith("PadInspector", StringComparison.Ordinal) ||
                 name.StartsWith("HasPadInspector", StringComparison.Ordinal) ||
                 name.StartsWith("HasNoPadInspector", StringComparison.Ordinal)))
            {
                // Read the public root property at the event, including properties added by a future refactor.
                changes.Add((name, vm.GetType().GetProperty(name)!.GetValue(vm)));
            }
        };
        return changes;
    }

    private static void AssertQueuedTrace(PadInspectorDisplay display)
    {
        Assert.True(display.HasSummary);
        Assert.True(display.HasRuleTrace);
        Assert.Equal(["Computing: Queued", "Detailed trace is loading in background."], display.RuleTraceLines);
        var section = Assert.Single(display.RuleTraceSections);
        Assert.Equal("Other", section.Title);
        Assert.Equal(display.RuleTraceLines, section.Lines.Select(static line => line.DisplayText));
    }

    private static (string Name, object? Value)[] CadNotifications(PadInspectorDisplay display) =>
    [
        (nameof(FreeformHelperViewModel.HasPadInspectorMultiSelection), false),
        (nameof(FreeformHelperViewModel.PadInspectorSelectionSummaryText), string.Empty),
        (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), true),
        (nameof(FreeformHelperViewModel.HasPadInspectorSummary), true),
        (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), false),
        (nameof(FreeformHelperViewModel.PadInspectorCompactHeaderText), "CAD 11 · Diff -"),
        (nameof(FreeformHelperViewModel.PadInspectorPrimaryText), "CAD 11 · IC - · Diff idx -"),
        (nameof(FreeformHelperViewModel.PadInspectorSecondaryText), "Diff source: Auto (geometry seed)"),
        (nameof(FreeformHelperViewModel.PadInspectorMatchText), "Match: 0 regular pad(s), confidence -"),
        (nameof(FreeformHelperViewModel.PadInspectorCompensationLines), display.CompensationLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines), display.RuleTraceLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections), display.RuleTraceSections),
        (nameof(FreeformHelperViewModel.HasPadInspectorRuleTrace), true),
        (nameof(FreeformHelperViewModel.PadInspectorDisplay), display),
    ];

    private static (string Name, object? Value)[] RegularNotifications(PadInspectorDisplay display) =>
    [
        (nameof(FreeformHelperViewModel.HasPadInspectorMultiSelection), false),
        (nameof(FreeformHelperViewModel.PadInspectorSelectionSummaryText), string.Empty),
        (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), false),
        (nameof(FreeformHelperViewModel.PadInspectorCompactHeaderText), "REG 101 · Diff 7"),
        (nameof(FreeformHelperViewModel.PadInspectorPrimaryText), "REG 101 · IC 1 · Diff idx 7"),
        (nameof(FreeformHelperViewModel.PadInspectorSecondaryText), "Diff source: Unmatched"),
        (nameof(FreeformHelperViewModel.PadInspectorMatchText), "Match: 0 CAD pad(s), confidence -"),
        (nameof(FreeformHelperViewModel.PadInspectorCompensationLines), display.CompensationLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines), display.RuleTraceLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections), display.RuleTraceSections),
        (nameof(FreeformHelperViewModel.PadInspectorDisplay), display),
    ];

    private static (string Name, object? Value)[] EmptyNotifications(PadInspectorDisplay display) =>
    [
        (nameof(FreeformHelperViewModel.HasPadInspectorMultiSelection), false),
        (nameof(FreeformHelperViewModel.PadInspectorSelectionSummaryText), string.Empty),
        (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), false),
        (nameof(FreeformHelperViewModel.HasPadInspectorSummary), false),
        (nameof(FreeformHelperViewModel.HasNoPadInspectorSummary), true),
        (nameof(FreeformHelperViewModel.PadInspectorCompactHeaderText), string.Empty),
        (nameof(FreeformHelperViewModel.PadInspectorPrimaryText), string.Empty),
        (nameof(FreeformHelperViewModel.PadInspectorSecondaryText), string.Empty),
        (nameof(FreeformHelperViewModel.PadInspectorMatchText), string.Empty),
        (nameof(FreeformHelperViewModel.PadInspectorCompensationLines), display.CompensationLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceLines), display.RuleTraceLines),
        (nameof(FreeformHelperViewModel.PadInspectorRuleTraceSections), display.RuleTraceSections),
        (nameof(FreeformHelperViewModel.HasPadInspectorRuleTrace), false),
        (nameof(FreeformHelperViewModel.PadInspectorDisplay), display),
    ];
}
