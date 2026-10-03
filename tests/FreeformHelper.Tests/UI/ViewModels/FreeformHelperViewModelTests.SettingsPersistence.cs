using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{
    [Fact]
    public void SettingsWindowDraft_DoesNotApplyUntilSave()
    {
        var vm = new FreeformHelperViewModel();
        var originalWeightIou = vm.MappingWeightIou;
        var originalImportOnlyClosed = vm.ImportOnlyClosedPolylines;
        var originalUseLocalSizing = vm.UseLocalSizing;
        var originalGlobalFontSize = vm.GlobalFontSizePercent;
        var originalHighlightStrokeWidthAdjust = vm.HighlightStrokeWidthAdjust;
        var originalShowNotchToRegularLabels = vm.ShowNotchToRegularLabels;
        var originalEnableToFullRuleEngine = vm.EnableToFullRuleEngine;
        var originalEnableToFullRuleTrace = vm.EnableToFullRuleTrace;
        var originalLogLevel = vm.SelectedLogLevelOption;
        var targetLogLevel = vm.LogLevelOptions.FirstOrDefault(option => !option.Equals(originalLogLevel));
        if (string.IsNullOrWhiteSpace(targetLogLevel.Display))
        {
            targetLogLevel = originalLogLevel;
        }

        var settings = vm.CreateSettingsWindowViewModel();
        settings.MappingWeightIou = originalWeightIou + 1.25m;
        settings.ImportOnlyClosedPolylines = !originalImportOnlyClosed;
        settings.UseLocalSizing = !originalUseLocalSizing;
        settings.GlobalFontSizePercent = originalGlobalFontSize + 8m;
        settings.HighlightStrokeWidthAdjust = originalHighlightStrokeWidthAdjust + 0.5m;
        settings.ShowNotchToRegularLabels = !originalShowNotchToRegularLabels;
        settings.EnableToFullRuleEngine = !originalEnableToFullRuleEngine;
        settings.EnableToFullRuleTrace = !originalEnableToFullRuleTrace;
        settings.SelectedLogLevelOption = targetLogLevel;

        Assert.Equal(originalWeightIou, vm.MappingWeightIou);
        Assert.Equal(originalImportOnlyClosed, vm.ImportOnlyClosedPolylines);
        Assert.Equal(originalUseLocalSizing, vm.UseLocalSizing);
        Assert.Equal(originalGlobalFontSize, vm.GlobalFontSizePercent);
        Assert.Equal(originalHighlightStrokeWidthAdjust, vm.HighlightStrokeWidthAdjust);
        Assert.Equal(originalShowNotchToRegularLabels, vm.ShowNotchToRegularLabels);
        Assert.Equal(originalEnableToFullRuleEngine, vm.EnableToFullRuleEngine);
        Assert.Equal(originalEnableToFullRuleTrace, vm.EnableToFullRuleTrace);
        Assert.Equal(originalLogLevel, vm.SelectedLogLevelOption);

        settings.CancelCommand.Execute(null);

        Assert.Equal(originalWeightIou, vm.MappingWeightIou);
        Assert.Equal(originalImportOnlyClosed, vm.ImportOnlyClosedPolylines);
        Assert.Equal(originalUseLocalSizing, vm.UseLocalSizing);
        Assert.Equal(originalGlobalFontSize, vm.GlobalFontSizePercent);
        Assert.Equal(originalHighlightStrokeWidthAdjust, vm.HighlightStrokeWidthAdjust);
        Assert.Equal(originalShowNotchToRegularLabels, vm.ShowNotchToRegularLabels);
        Assert.Equal(originalEnableToFullRuleEngine, vm.EnableToFullRuleEngine);
        Assert.Equal(originalEnableToFullRuleTrace, vm.EnableToFullRuleTrace);
        Assert.Equal(originalLogLevel, vm.SelectedLogLevelOption);
    }

    [Fact]
    public void SettingsWindowDraft_SaveAppliesToViewModel()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        var targetWeightIou = vm.MappingWeightIou + 2m;
        var targetImportOnlyClosed = !vm.ImportOnlyClosedPolylines;
        var targetUseLocalSizing = !vm.UseLocalSizing;
        var targetGlobalFontSize = Math.Clamp(vm.GlobalFontSizePercent + 12m, 80m, 140m);
        var targetHighlightStrokeWidthAdjust = Math.Clamp(vm.HighlightStrokeWidthAdjust + 0.8m, -2m, 4m);
        var targetShowNotchToRegularLabels = !vm.ShowNotchToRegularLabels;
        var targetEnableToFullRuleEngine = !vm.EnableToFullRuleEngine;
        var targetEnableToFullRuleTrace = !vm.EnableToFullRuleTrace;
        var targetLogLevel = vm.LogLevelOptions.FirstOrDefault(option => !option.Equals(vm.SelectedLogLevelOption));
        if (string.IsNullOrWhiteSpace(targetLogLevel.Display))
        {
            targetLogLevel = vm.SelectedLogLevelOption;
        }

        settings.MappingWeightIou = targetWeightIou;
        settings.ImportOnlyClosedPolylines = targetImportOnlyClosed;
        settings.UseLocalSizing = targetUseLocalSizing;
        settings.GlobalFontSizePercent = targetGlobalFontSize;
        settings.HighlightStrokeWidthAdjust = targetHighlightStrokeWidthAdjust;
        settings.ShowNotchToRegularLabels = targetShowNotchToRegularLabels;
        settings.EnableToFullRuleEngine = targetEnableToFullRuleEngine;
        settings.EnableToFullRuleTrace = targetEnableToFullRuleTrace;
        settings.SelectedLogLevelOption = targetLogLevel;

        var closeRequested = false;
        settings.RequestClose += () => closeRequested = true;

        settings.SaveCommand.Execute(null);

        Assert.True(closeRequested);
        Assert.Equal(targetWeightIou, vm.MappingWeightIou);
        Assert.Equal(targetImportOnlyClosed, vm.ImportOnlyClosedPolylines);
        Assert.Equal(targetUseLocalSizing, vm.UseLocalSizing);
        Assert.Equal(targetGlobalFontSize, vm.GlobalFontSizePercent);
        Assert.Equal(targetHighlightStrokeWidthAdjust, vm.HighlightStrokeWidthAdjust);
        Assert.Equal(targetShowNotchToRegularLabels, vm.ShowNotchToRegularLabels);
        Assert.Equal(targetEnableToFullRuleEngine, vm.EnableToFullRuleEngine);
        Assert.Equal(targetEnableToFullRuleTrace, vm.EnableToFullRuleTrace);
        Assert.Equal(targetLogLevel, vm.SelectedLogLevelOption);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsWindowDraft_SaveAppliesGeneralSectionFields()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        // The view model also raises PropertyChanged from background work, so the handler and the
        // assertions below can run at the same time.
        var changedProperties = new System.Collections.Concurrent.ConcurrentQueue<string>();
        vm.PropertyChanged += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.PropertyName))
            {
                changedProperties.Enqueue(args.PropertyName!);
            }
        };

        var targetGridPadding = 12m;
        var targetActiveAreaWidth = vm.ActiveAreaWidth + 3m;
        var targetActiveAreaHeight = vm.ActiveAreaHeight + 2m;
        var targetPanelBiasX = vm.PanelBiasX + 0.5m;
        var targetPanelBiasY = vm.PanelBiasY - 0.5m;
        var targetImportOnlyClosed = !vm.ImportOnlyClosedPolylines;
        var targetImportBlockPolylines = !vm.ImportBlockPolylines;
        var targetRecalcBounds = !vm.RecalcBoundsOnLayerFilter;
        var targetScanOrder = vm.ScanOrderOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedScanOrderOption.Value);
        if (string.IsNullOrWhiteSpace(targetScanOrder.Display))
        {
            targetScanOrder = vm.SelectedScanOrderOption;
        }

        var targetAlignment = vm.GridAlignmentOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedGridAlignmentOption.Value);
        if (string.IsNullOrWhiteSpace(targetAlignment.Display))
        {
            targetAlignment = vm.SelectedGridAlignmentOption;
        }

        var targetRegularSourceMode = vm.RegularSourceModeOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedRegularSourceModeOption.Value);
        if (string.IsNullOrWhiteSpace(targetRegularSourceMode.Display))
        {
            targetRegularSourceMode = vm.SelectedRegularSourceModeOption;
        }

        settings.GridPaddingPercent = targetGridPadding;
        settings.ActiveAreaWidth = targetActiveAreaWidth;
        settings.ActiveAreaHeight = targetActiveAreaHeight;
        settings.PanelBiasX = targetPanelBiasX;
        settings.PanelBiasY = targetPanelBiasY;
        settings.ImportOnlyClosedPolylines = targetImportOnlyClosed;
        settings.ImportBlockPolylines = targetImportBlockPolylines;
        settings.RecalcBoundsOnLayerFilter = targetRecalcBounds;
        settings.SelectedScanOrderOption = targetScanOrder;
        settings.SelectedGridAlignmentOption = targetAlignment;
        settings.SelectedRegularSourceModeOption = targetRegularSourceMode;
        settings.SaveCommand.Execute(null);

        Assert.Equal(targetGridPadding, vm.GridPaddingPercent);
        Assert.Equal(targetActiveAreaWidth, vm.ActiveAreaWidth);
        Assert.Equal(targetActiveAreaHeight, vm.ActiveAreaHeight);
        Assert.Equal(targetPanelBiasX, vm.PanelBiasX);
        Assert.Equal(targetPanelBiasY, vm.PanelBiasY);
        Assert.Equal(targetImportOnlyClosed, vm.ImportOnlyClosedPolylines);
        Assert.Equal(targetImportBlockPolylines, vm.ImportBlockPolylines);
        Assert.Equal(targetRecalcBounds, vm.RecalcBoundsOnLayerFilter);
        Assert.Equal(targetScanOrder.Value, vm.SelectedScanOrder);
        Assert.Equal(targetScanOrder.Value, vm.SelectedScanOrderOption.Value);
        Assert.Equal(targetAlignment.Value, vm.GridAlignmentMode);
        Assert.Equal(targetAlignment.Value, vm.SelectedGridAlignmentOption.Value);
        Assert.Equal(targetRegularSourceMode.Value, vm.RegularSourceMode);
        Assert.Equal(targetRegularSourceMode.Value, vm.SelectedRegularSourceModeOption.Value);
        Assert.Contains(nameof(vm.IsPanelAlignment), changedProperties);
        Assert.Contains(nameof(vm.IsDxfLayerRegularSource), changedProperties);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsWindowDraft_CascadeDetails_DoNotApplyUntilSave()
    {
        var vm = new FreeformHelperViewModel();
        var originalCascade = vm.CascadeNum;
        var originalX = vm.XChannels;
        var originalY = vm.YChannels;

        var settings = vm.CreateSettingsWindowViewModel();
        settings.CascadeNum = 3m;
        settings.CascadeIcSettings[0].XChannels = 10m;
        settings.CascadeIcSettings[1].XChannels = 12m;
        settings.CascadeIcSettings[2].XChannels = 14m;
        settings.CascadeIcSettings[0].YChannels = 20m;
        settings.CascadeIcSettings[1].YChannels = 18m;
        settings.CascadeIcSettings[2].YChannels = 22m;

        Assert.Equal(36m, settings.XChannels);
        Assert.Equal(22m, settings.YChannels);
        Assert.Equal(originalCascade, vm.CascadeNum);
        Assert.Equal(originalX, vm.XChannels);
        Assert.Equal(originalY, vm.YChannels);
        Assert.Single(vm.CascadeIcSettings);

        settings.CancelCommand.Execute(null);

        Assert.Equal(originalCascade, vm.CascadeNum);
        Assert.Equal(originalX, vm.XChannels);
        Assert.Equal(originalY, vm.YChannels);
        Assert.Single(vm.CascadeIcSettings);
    }

    [Fact]
    public void SettingsWindowDraft_SaveAppliesCascadeDetails()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();

        settings.CascadeNum = 3m;
        settings.CascadeIcSettings[0].XChannels = 10m;
        settings.CascadeIcSettings[1].XChannels = 12m;
        settings.CascadeIcSettings[2].XChannels = 14m;
        settings.CascadeIcSettings[0].YChannels = 20m;
        settings.CascadeIcSettings[1].YChannels = 18m;
        settings.CascadeIcSettings[2].YChannels = 22m;
        settings.SaveCommand.Execute(null);

        Assert.Equal(3m, vm.CascadeNum);
        Assert.Equal(36m, vm.XChannels);
        Assert.Equal(22m, vm.YChannels);
        var appliedXChannels = vm.CascadeIcSettings.Select(static row => (int)row.XChannels).ToList();
        var appliedYChannels = vm.CascadeIcSettings.Select(static row => (int)row.YChannels).ToList();
        Assert.Equal(3, appliedXChannels.Count);
        Assert.Equal(3, appliedYChannels.Count);
        Assert.Equal(10, appliedXChannels[0]);
        Assert.Equal(12, appliedXChannels[1]);
        Assert.Equal(14, appliedXChannels[2]);
        Assert.Equal(20, appliedYChannels[0]);
        Assert.Equal(18, appliedYChannels[1]);
        Assert.Equal(22, appliedYChannels[2]);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsWindowDraft_SaveAppliesStep4SelectionState()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();
        var targetAutoMode = vm.CadOutputFwDiffAutoModeOptions.FirstOrDefault(option =>
            option.Value != vm.SelectedCadOutputFwDiffAutoModeOption.Value);
        if (string.IsNullOrWhiteSpace(targetAutoMode.Display))
        {
            targetAutoMode = vm.SelectedCadOutputFwDiffAutoModeOption;
        }

        var cadIcIndexField = typeof(FreeformHelperViewModel).GetField("_cadIcIndexByCadId", BindingFlags.Instance | BindingFlags.NonPublic);
        var projectField = typeof(FreeformHelperViewModel).GetField("_projectFile", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadIcIndexField);
        Assert.NotNull(projectField);

        var cadIcIndexByCadId = Assert.IsType<Dictionary<int, int>>(cadIcIndexField!.GetValue(vm));
        cadIcIndexByCadId[4201] = 1;

        settings.SelectedCadOutputFwDiffAutoModeOption = targetAutoMode;
        settings.CadOutputFwDiffIndexAnchorCadId = 4201;
        settings.SaveCommand.Execute(null);

        var project = Assert.IsType<FreeformHelper.Infrastructure.Project.ProjectFile>(projectField!.GetValue(vm));
        Assert.Equal(targetAutoMode.Value, vm.CadOutputFwDiffAutoMode);
        Assert.Equal(targetAutoMode.Value, vm.SelectedCadOutputFwDiffAutoModeOption.Value);
        Assert.Equal(4201m, vm.CadOutputFwDiffIndexAnchorCadId);
        Assert.Equal(4201, project.CadOutputFwDiffIndexAnchorCadPadByIc[1]);
        Assert.Null(project.CadOutputFwDiffIndexAnchorCadPadId);
        Assert.Equal("CAD Output FW Diff anchor saved: IC 2 -> CAD 4201.", vm.StatusText);
    }

    [Fact]
    public async Task MatchThresholdEdit_DirectAndSettingsDraft_PreserveGridWithoutCanvasNavigation()
    {
        var directHost = new TestCanvasHost();
        var directVm = new FreeformHelperViewModel
        {
            CanvasHost = directHost
        };
        await directVm.RebuildGridCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(directVm);
        var directPads = directVm.RegularPads;
        SetPadMatchResult(directVm, cadPadId: 101, regularPadId: directPads[0].RegularPadId);
        var directLinks = new[]
        {
            new PadMatchLink(101, directPads[0].RegularPadId, 1.0, 0.8, 0.8),
        };
        directVm.MatchLinksForCanvas = directLinks;
        directHost.SetSelection([101], [202]);
        directVm.HasUnsavedChanges = false;
        directHost.ResetCounters();

        Assert.False(directVm.HasUnsavedChanges);

        directVm.MatchThreshold += 0.03m;
        await WaitForGridRebuildAsync(directVm);

        Assert.Same(directPads, directVm.RegularPads);
        Assert.True(directVm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.Same(directLinks, directVm.MatchLinksForCanvas);
        Assert.Equal(101, Assert.Single(directHost.SelectedCadIds));
        Assert.Equal(202, Assert.Single(directHost.SelectedRegularIds));
        Assert.Equal(0, directHost.InvalidateCount);
        Assert.Equal(0, directHost.FitToContentCount);
        Assert.True(directVm.HasUnsavedChanges);

        var windowHost = new TestCanvasHost();
        var windowVm = new FreeformHelperViewModel
        {
            CanvasHost = windowHost
        };
        await windowVm.RebuildGridCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(windowVm);
        var windowPads = windowVm.RegularPads;
        SetPadMatchResult(windowVm, cadPadId: 303, regularPadId: windowPads[0].RegularPadId);
        var windowLinks = new[]
        {
            new PadMatchLink(303, windowPads[0].RegularPadId, 1.0, 0.8, 0.8),
        };
        windowVm.MatchLinksForCanvas = windowLinks;
        windowHost.SetSelection([303], [404]);
        windowVm.HasUnsavedChanges = false;
        windowHost.ResetCounters();

        Assert.False(windowVm.HasUnsavedChanges);

        var settings = windowVm.CreateSettingsWindowViewModel();
        settings.MatchThreshold += 0.03m;
        settings.SaveCommand.Execute(null);
        await WaitForGridRebuildAsync(windowVm);

        Assert.Same(windowPads, windowVm.RegularPads);
        Assert.True(windowVm.GetWorkflowStateSnapshot().HasStep1Result);
        Assert.Same(windowLinks, windowVm.MatchLinksForCanvas);
        Assert.Equal(303, Assert.Single(windowHost.SelectedCadIds));
        Assert.Equal(404, Assert.Single(windowHost.SelectedRegularIds));
        Assert.Equal(0, windowHost.InvalidateCount);
        Assert.Equal(0, windowHost.FitToContentCount);
        Assert.True(windowVm.HasUnsavedChanges);
    }

    [Fact]
    public void SettingsWindowDraft_DefaultSection_IsGeneral()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();

        Assert.Equal(SettingsWindowSection.General, settings.SelectedSection);
        Assert.True(settings.IsGeneralSectionSelected);
        Assert.False(settings.IsStep1SectionSelected);
        Assert.False(settings.IsStep2SectionSelected);
        Assert.False(settings.IsStep3SectionSelected);
        Assert.False(settings.IsStep4SectionSelected);
        Assert.False(settings.IsStep5SectionSelected);
    }

    [Fact]
    public void SettingsWindowDraft_SectionSelection_IsMutuallyExclusive()
    {
        var vm = new FreeformHelperViewModel();
        var settings = vm.CreateSettingsWindowViewModel();

        settings.IsStep3SectionSelected = true;
        Assert.Equal(SettingsWindowSection.Step3NotchCompensation, settings.SelectedSection);
        Assert.False(settings.IsGeneralSectionSelected);
        Assert.False(settings.IsStep1SectionSelected);
        Assert.False(settings.IsStep2SectionSelected);
        Assert.True(settings.IsStep3SectionSelected);
        Assert.False(settings.IsStep4SectionSelected);
        Assert.False(settings.IsStep5SectionSelected);

        settings.SelectedSection = SettingsWindowSection.Step5NotchExport;
        Assert.False(settings.IsGeneralSectionSelected);
        Assert.False(settings.IsStep1SectionSelected);
        Assert.False(settings.IsStep2SectionSelected);
        Assert.False(settings.IsStep3SectionSelected);
        Assert.False(settings.IsStep4SectionSelected);
        Assert.True(settings.IsStep5SectionSelected);
    }

}
