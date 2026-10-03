using System.Collections.ObjectModel;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void RegularSourceMode_TogglesLayerSourceFlag()
    {
        var vm = new FreeformHelperViewModel();

        vm.RegularSourceMode = RegularSourceMode.GeneratedGrid;
        Assert.False(vm.IsDxfLayerRegularSource);

        vm.RegularSourceMode = RegularSourceMode.FromDxfLayer;
        Assert.True(vm.IsDxfLayerRegularSource);
    }


    [Fact]
    public void HighlightStrokeWidthAdjust_DefaultIsMinusOne()
    {
        Assert.Equal(-1.0, new UiViewSnapshot().HighlightStrokeWidthAdjust);

        var appSettingsPath = Path.Combine(Path.GetTempPath(), $"freeform-app-general-{Guid.NewGuid():N}.json");
        try
        {
            var vm = new FreeformHelperViewModel(new AppGeneralSettingsStore(appSettingsPath));
            Assert.Equal(-1m, vm.HighlightStrokeWidthAdjust);
        }
        finally
        {
            if (File.Exists(appSettingsPath))
            {
                File.Delete(appSettingsPath);
            }
        }
    }


    [Fact]
    public void WorkspaceHeaderItems_ExposeCoreDisplayToggles()
    {
        var vm = new FreeformHelperViewModel();

        var toggleIds = vm.WorkspaceHeaderItems.Select(static item => item.Id).ToArray();
        Assert.Contains("cadLayer", toggleIds);
        Assert.Contains("regularGrid", toggleIds);
        Assert.Contains("highlightUnmatched", toggleIds);
        Assert.Contains("highlightFreeform", toggleIds);
        Assert.Contains("colorByArea", toggleIds);
    }


    [Fact]
    public void ToFullRuleEngine_DefaultsToEnabledAndTraceOff()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.EnableToFullRuleEngine);
        Assert.False(vm.EnableToFullRuleTrace);
        Assert.Equal("To Full rule engine: ON (trace OFF)", vm.NotchToFullRuleEngineSummary);
    }


    [Fact]
    public void NotchVersion_DefaultsToV21AndV22Enabled()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.EnableV21);
        Assert.True(vm.EnableV22);
    }


    [Fact]
    public void LenScale_RemainsHidden_WhenV21Enabled()
    {
        var vm = new FreeformHelperViewModel();

        vm.EnableV21 = true;

        Assert.False(vm.IsLenScaleVisible);
    }


    [Fact]
    public void RightPanelTab_DefaultsToSettings()
    {
        var vm = new FreeformHelperViewModel();

        Assert.True(vm.IsRightPanelSettingsTab);
        Assert.False(vm.IsRightPanelInspectorTab);
        Assert.Equal("Settings", vm.RightPanelActiveTabTitle);
    }


    [Fact]
    public void RightPanelTab_SwitchesWithMutualExclusion()
    {
        var vm = new FreeformHelperViewModel();

        vm.IsRightPanelInspectorTab = true;
        Assert.False(vm.IsRightPanelSettingsTab);
        Assert.True(vm.IsRightPanelInspectorTab);
        Assert.Equal("Inspector", vm.RightPanelActiveTabTitle);

        vm.IsRightPanelSettingsTab = true;
        Assert.True(vm.IsRightPanelSettingsTab);
        Assert.False(vm.IsRightPanelInspectorTab);
        Assert.Equal("Settings", vm.RightPanelActiveTabTitle);
    }


    [Fact]
    public void NotchValidationSectionFlags_ReflectPerGroupCollections()
    {
        var vm = new FreeformHelperViewModel();
        var directItem = CreateValidationDisplayItem("DIRECT", isDirect: true);
        var incomingItem = CreateValidationDisplayItem("IN", isIncoming: true);
        var outgoingItem = CreateValidationDisplayItem("OUT", isOutgoing: true);

        Assert.True(vm.IsNotchValidationItemsEmpty);
        Assert.True(vm.IsNotchValidationDirectEmpty);
        Assert.True(vm.IsNotchValidationIncomingEmpty);
        Assert.True(vm.IsNotchValidationOutgoingEmpty);

        vm.NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { directItem };
        vm.NotchValidationDirectItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { directItem };
        vm.NotchValidationIncomingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { incomingItem };
        vm.NotchValidationOutgoingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem> { outgoingItem };

        Assert.True(vm.HasNotchValidationItems);
        Assert.False(vm.IsNotchValidationItemsEmpty);
        Assert.True(vm.HasNotchValidationDirectItems);
        Assert.True(vm.HasNotchValidationIncomingItems);
        Assert.True(vm.HasNotchValidationOutgoingItems);

        vm.NotchValidationItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>();
        vm.NotchValidationDirectItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>();
        vm.NotchValidationIncomingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>();
        vm.NotchValidationOutgoingItems = new ObservableCollection<FreeformHelperViewModel.NotchValidationDisplayItem>();

        Assert.False(vm.HasNotchValidationItems);
        Assert.True(vm.IsNotchValidationItemsEmpty);
        Assert.False(vm.HasNotchValidationDirectItems);
        Assert.False(vm.HasNotchValidationIncomingItems);
        Assert.False(vm.HasNotchValidationOutgoingItems);
    }


    [Fact]
    public void SelectionSummary_UpdatesForCadAndRegularSelections()
    {
        var vm = new FreeformHelperViewModel();
        var selectedCadIds = new[] { 1, 2 };
        var selectedRegularIds = new[] { 10, 11, 12 };
        var mixedCadIds = new[] { 5 };
        var mixedRegularIds = new[] { 7 };

        vm.ApplyCanvasSelection(selectedCadIds, Array.Empty<int>());
        Assert.Equal("Selected: CAD=2.", vm.SelectionSummary);

        vm.ApplyCanvasSelection(Array.Empty<int>(), selectedRegularIds);
        Assert.Equal("Selected: Regular=3.", vm.SelectionSummary);

        vm.ApplyCanvasSelection(mixedCadIds, mixedRegularIds);
        Assert.Equal("Selected: CAD=1, Regular=1.", vm.SelectionSummary);
    }

    [Fact]
    public void SimulationSafetyGuidance_UsesCurrentOverviewCapAndKeepsDefaultSettingsText()
    {
        var vm = new FreeformHelperViewModel();
        var audit = SimulationSafetyAuditService.Analyze(
        [
            new NotchApplySimulationDiffCell(
                RegularPadId: 1,
                RegularRow: 0,
                RegularCol: 0,
                IcIndex: 0,
                DiffIndex: 10,
                BeforeValue: 400d,
                AfterValue: 450d),
        ], afterCap: 512d);

        vm.ApplySimulationSafetyOverview(audit, isStale: false, buildFailureText: null);

        Assert.Equal(
            (
                "512",
                "EMS cap 512",
                "Simulation safety: After > 512 is EMS risk. 400 is diagnostic input, not a correctness target.",
                "Export safety: run Simulation audit before FW handoff; any After > 512 needs explicit review."),
            (
                vm.SimulationSafetyOverviewEmsCapText,
                vm.NotchEmsSafetyShortText,
                vm.NotchEmsSafetyPolicySummary,
                vm.NotchExportSafetyPolicySummary));

        var settings = new SettingsWindowViewModel(vm);
        Assert.Equal(
            (
                "EMS cap 480",
                "Simulation safety: After > 480 is EMS risk. 400 is diagnostic input, not a correctness target.",
                "Export safety: run Simulation audit before FW handoff; any After > 480 needs explicit review."),
            (
                settings.NotchEmsSafetyShortText,
                settings.NotchEmsSafetyPolicySummary,
                settings.NotchExportSafetyPolicySummary));

        vm.ApplySimulationSafetyOverview(audit: null, isStale: false, buildFailureText: null);
        Assert.Equal(
            ("480", "EMS cap 480"),
            (vm.SimulationSafetyOverviewEmsCapText, vm.NotchEmsSafetyShortText));
    }

    [Fact]
    public void SimulationSafetyGuidance_RaisesDependentPropertiesWhenOverviewCapChanges()
    {
        var vm = new FreeformHelperViewModel();
        var changedProperties = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Enqueue(args.PropertyName);
        var audit = SimulationSafetyAuditResult.Empty(afterCap: 512d);

        vm.ApplySimulationSafetyOverview(audit, isStale: false, buildFailureText: null);

        Assert.Contains(nameof(vm.NotchEmsSafetyShortText), changedProperties);
        Assert.Contains(nameof(vm.NotchEmsSafetyPolicySummary), changedProperties);
        Assert.Contains(nameof(vm.NotchTargetCoverageCapHelpText), changedProperties);
        Assert.Contains(nameof(vm.NotchExportSafetyPolicySummary), changedProperties);
    }

    [Fact]
    public void TargetCoverageCapHelp_UsesWorkflowAndSettingsCapProvenance()
    {
        var vm = new FreeformHelperViewModel();
        var changedProperties = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, args) => changedProperties.Enqueue(args.PropertyName);

        vm.TargetCoverageCapPercent = 128m;
        Assert.Contains(nameof(vm.NotchTargetCoverageCapHelpText), changedProperties);
        changedProperties.Clear();

        vm.ApplySimulationSafetyOverview(
            SimulationSafetyAuditResult.Empty(afterCap: 512d),
            isStale: false,
            buildFailureText: null);
        var settings = new SettingsWindowViewModel(vm);

        Assert.Contains(nameof(vm.NotchTargetCoverageCapHelpText), changedProperties);
        Assert.Equal(
            "At uniform 400, target cap 128% maps to After 512; compare with EMS cap 512.",
            vm.NotchTargetCoverageCapHelpText);
        Assert.Equal(
            "At uniform 400, target cap 128% maps to After 512; compare with EMS cap 480.",
            settings.NotchTargetCoverageCapHelpText);

        var settingsChangedProperties = new System.Collections.Concurrent.ConcurrentQueue<string?>();
        settings.PropertyChanged += (_, args) => settingsChangedProperties.Enqueue(args.PropertyName);
        settings.TargetCoverageCapPercent = 127.5m;

        Assert.Contains(nameof(settings.NotchTargetCoverageCapHelpText), settingsChangedProperties);
        Assert.Equal(
            "At uniform 400, target cap 127.5% maps to After 510; compare with EMS cap 480.",
            settings.NotchTargetCoverageCapHelpText);

        vm.ApplySimulationSafetyOverview(audit: null, isStale: false, buildFailureText: null);
        Assert.Equal(
            "At uniform 400, target cap 128% maps to After 512; compare with EMS cap 480.",
            vm.NotchTargetCoverageCapHelpText);
    }

}
