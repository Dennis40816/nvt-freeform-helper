// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Specialized;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationPresentationOwnerCharacterizationTests
{
    [Theory]
    [InlineData(NotchApplySimulationCanvasColorMode.Auto, "-15", "+15", "40", "40")]
    [InlineData(NotchApplySimulationCanvasColorMode.Threshold, "-7.5", "+7.5", "0", "TH 7.5")]
    public void DisplayChoicesRetainAcceptedResultAndPublishSignedAndSequentialLegends(
        NotchApplySimulationCanvasColorMode color, string negativeStart, string negativeEnd,
        string positiveStart, string positiveEnd)
    {
        var vm = CreateWorkspace(withTransfer: false);
        vm.GlobalValue = -15;
        vm.SelectRegularPad(2);
        vm.SelectedManualValue = 40;
        vm.ApplySelectedOverrideCommand.Execute(null);
        var accepted = vm.GetSimulationScenarioSnapshot();
        vm.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.After);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        vm.SelectedCanvasColorOption = vm.CanvasColorOptions.Single(option => option.Value == color);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        vm.ColorThresholdValue = 7.5;
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 0);
        Assert.Equal((negativeStart, "0", negativeEnd),
            (vm.ColorLegendStartText, vm.ColorLegendCenterText, vm.ColorLegendEndText));
        Assert.True(vm.ShowLegendCenterLabel);
        Assert.False(vm.ShowSequentialLegend);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 1);
        Assert.Equal((positiveStart, string.Empty, positiveEnd),
            (vm.ColorLegendStartText, vm.ColorLegendCenterText, vm.ColorLegendEndText));
        Assert.False(vm.ShowLegendCenterLabel);
        Assert.True(vm.ShowSequentialLegend);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        Assert.Equal(40d, Assert.Single(vm.SimulationOverlayItems).DisplayValue);
    }

    [Fact]
    public void AreaChangeClearsSelectionBeforeOverlayWhileQueryRetainsTheFullCellIndex()
    {
        var vm = CreateWorkspace();
        vm.GlobalValue = 100;
        vm.SelectRegularPad(2);
        var accepted = vm.GetSimulationScenarioSnapshot();
        var events = new List<string>();
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            if (args.PropertyName is nameof(vm.SelectedRegularPadId) or nameof(vm.SimulationOverlayItems))
            {
                Assert.Equal(-1, vm.SelectedRegularPadId);
                Assert.False(vm.ShowSelectedOverrideEditor);
                Assert.Null(vm.BuildHoverTipText(2));
                Assert.True(vm.TryGetRegularQuerySnapshot(2, out var query));
                Assert.Equal(100d, query.BeforeValue);
                Assert.False(query.IsWithinSelectedArea);
                Assert.True(query.IsActiveSurface);
                events.Add(args.PropertyName);
            }
        };

        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 0);

        Assert.Equal<string>([nameof(vm.SelectedRegularPadId), nameof(vm.SimulationOverlayItems)], events);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
    }

    [Fact]
    public void SelectionPublishesEmptyThenPopulatedImpactsAndKeepsDuplicateSummaries()
    {
        var vm = CreateWorkspace();
        vm.GlobalValue = 100;
        var impacts = vm.SelectedImpactItems;
        var command = vm.ApplySelectedOverrideCommand;
        var events = new List<string>();
        ((INotifyCollectionChanged)impacts).CollectionChanged += (_, args) =>
            events.Add($"{args.Action}:{impacts.Count}");
        vm.PropertyChanging += (sender, args) =>
        {
            Assert.Same(vm, sender);
            if (args.PropertyName == nameof(vm.SelectedRegularPadId))
            {
                Assert.Equal(-1, vm.SelectedRegularPadId);
                events.Add("changing");
            }
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            if (args.PropertyName is nameof(vm.SelectedImpactSummaryText) or nameof(vm.HasSelectedImpactItems)
                or nameof(vm.SelectedImpactCountText))
                events.Add($"{args.PropertyName}:{impacts.Count}");
            if (args.PropertyName == nameof(vm.SelectedRegularPadId)) events.Add("changed");
        };

        vm.SelectRegularPad(1);

        Assert.Equal<string>(
        [
            "changing", "Reset:0", "SelectedImpactSummaryText:0", "HasSelectedImpactItems:0",
            "SelectedImpactCountText:0", "Reset:0", "Add:1", "SelectedImpactSummaryText:1",
            "HasSelectedImpactItems:1", "SelectedImpactCountText:1", "SelectedImpactSummaryText:1",
            "HasSelectedImpactItems:1", "SelectedImpactCountText:1", "changed",
        ], events);
        Assert.Same(impacts, vm.SelectedImpactItems);
        Assert.Same(command, vm.ApplySelectedOverrideCommand);
        Assert.Equal("Target receive", Assert.Single(impacts).RoleText);
        Assert.Equal("+20", impacts[0].DeltaText);
        Assert.Equal("120", vm.SelectedAfterText);
    }

    [Fact]
    public void SafetyPublicationAcceptsAuditAfterOverlayAndMismatchBeforeRiskRowsAndNotifications()
    {
        var vm = CreateWorkspace();
        vm.SelectRegularPad(0);
        var oldSafety = vm.GetSimulationSafetyAuditSnapshot();
        var risks = vm.SimulationHighRiskDiffs;
        var command = vm.SelectSimulationRiskDiffCommand;
        var events = new List<string>();
        ((INotifyCollectionChanged)risks).CollectionChanged += (_, args) =>
        {
            Assert.Same(vm.GetSimulationScenarioSnapshot()!.SafetyAudit, vm.GetSimulationSafetyAuditSnapshot());
            if (args.Action == NotifyCollectionChangedAction.Reset) events.Add("risk reset");
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            switch (args.PropertyName)
            {
                case nameof(vm.SimulationOverlayItems):
                    Assert.Same(oldSafety, vm.GetSimulationSafetyAuditSnapshot());
                    Assert.True(vm.TryGetRegularQuerySnapshot(0, out var query));
                    Assert.Equal(1000d, query.BeforeValue);
                    Assert.Equal(0d, vm.SelectedManualValue);
                    events.Add("overlay");
                    break;
                case nameof(vm.DiffViolationPadCount):
                    Assert.Same(oldSafety, vm.GetSimulationSafetyAuditSnapshot());
                    events.Add("mismatch");
                    break;
                case nameof(vm.SimulationEmsAfterCap):
                    Assert.Same(vm.GetSimulationScenarioSnapshot()!.SafetyAudit, vm.GetSimulationSafetyAuditSnapshot());
                    Assert.Equal(vm.GetSimulationSafetyAuditSnapshot().HighRiskDiffs.Count, risks.Count);
                    Assert.True(vm.HasSimulationSafetyViolations);
                    events.Add("safety");
                    break;
                case nameof(vm.SelectedBeforeText):
                    Assert.Equal(1000d, vm.SelectedManualValue);
                    events.Add("selection");
                    break;
            }
        };

        vm.GlobalValue = 1000;

        Assert.Equal<string>(["overlay", "mismatch", "risk reset", "safety", "selection"], events);
        Assert.Same(risks, vm.SimulationHighRiskDiffs);
        Assert.Same(command, vm.SelectSimulationRiskDiffCommand);
    }

    [Fact]
    public void PresentationExtensionsFollowTheirTriggerBeforeTheOriginatingSetterCompletes()
    {
        var vm = CreateWorkspace();
        var events = new List<string>();
        string[] observed =
        [
            nameof(vm.SelectedCanvasViewMode), nameof(vm.ColorScaleCaptionText), nameof(vm.ViewModeSummaryText),
            nameof(vm.SelectedCanvasViewOption), nameof(vm.AreaSummaryText), nameof(vm.RandomScopeText),
            nameof(vm.SelectedAreaOption),
        ];
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is { } name && observed.Contains(name)) events.Add(name);
        };

        vm.SetCanvasViewMode(NotchApplySimulationCanvasViewMode.Delta);

        Assert.Equal<string>(["SelectedCanvasViewMode", "ColorScaleCaptionText", "ViewModeSummaryText",
            "SelectedCanvasViewOption"], events);
        Assert.Equal("Delta (After - Before)", vm.ColorScaleCaptionText);
        events.Clear();
        vm.SelectedAreaOption = vm.AreaOptions.Single(static option => option.IcIndex == 1);
        Assert.Equal<string>(["AreaSummaryText", "RandomScopeText", "ViewModeSummaryText", "SelectedAreaOption"], events);
        Assert.Equal("Random fills IC 2 only.", vm.RandomScopeText);
    }

    [Fact]
    public void SelectionAndSourceChangesKeepInlineEditorNotificationMultiplicity()
    {
        var vm = CreateWorkspace();
        var visibility = new List<bool>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.ShowSelectedOverrideEditor)) visibility.Add(vm.ShowSelectedOverrideEditor);
        };

        vm.SelectRegularPad(0);
        Assert.Equal<bool>([true, true], visibility);
        visibility.Clear();
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(static option => option.Value == SimulationInputSourceMode.Csv);
        Assert.Equal<bool>([false, false], visibility);
        visibility.Clear();
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(static option => option.Value == SimulationInputSourceMode.Manual);
        Assert.Equal<bool>([true, true], visibility);
        visibility.Clear();
        vm.SelectRegularPad(-1);
        Assert.Equal<bool>([false, false], visibility);
    }

    [Fact]
    public void NegativeThresholdRetainsNestedChangingAndChangedValues()
    {
        var vm = CreateWorkspace();
        var events = new List<(string Event, double Value)>();
        vm.PropertyChanging += (sender, args) =>
        {
            Assert.Same(vm, sender);
            if (args.PropertyName == nameof(vm.ColorThresholdValue)) events.Add(("changing", vm.ColorThresholdValue));
        };
        vm.PropertyChanged += (sender, args) =>
        {
            Assert.Same(vm, sender);
            if (args.PropertyName is nameof(vm.ColorThresholdValue) or nameof(vm.ColorModeSummaryText)
                or nameof(vm.ColorLegendEndText) or nameof(vm.ColorLegendStartText))
                events.Add((args.PropertyName, vm.ColorThresholdValue));
        };

        vm.ColorThresholdValue = -2;

        Assert.Equal<(string, double)>(
        [
            ("changing", 5), ("changing", -2), ("ColorModeSummaryText", 0),
            ("ColorLegendEndText", 0), ("ColorLegendStartText", 0),
            ("ColorThresholdValue", 0), ("ColorThresholdValue", 0),
        ], events);
    }

    private static SimulationWorkspaceViewModel CreateWorkspace(bool withTransfer = true)
    {
        // TestAppSettingsIsolation initializes the settings sandbox before this assembly runs.
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11, 12],
            icIndexSelector: static col => col == 2 ? 1 : 0);
        var table = new NotchTable(withTransfer
            ? [new NotchTableRow(0, 10, 0, 313, new NotchV22Node(10, 100, 11, 20, 65535, 0, 0), "S3 publication")]
            : []);
        var session = new FreeformHelper.UI.Services.SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(
                new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()), session);
    }
}
