// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

public sealed partial class IndexMappingRepairEffectTests
{
    [AvaloniaFact]
    public async Task MappingOverrideApplyAndClearAreReflectedInReopenedReportAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        var regularId = selected.ApplyRegularPadIndex!.Value;
        Assert.Null(selected.OverrideRegularPadIndex);

        report.ApplyOverrideCommand.Execute(null);

        Assert.Equal("Mapping override applied.", report.ActionStatus);
        Assert.Equal(regularId, selected.OverrideRegularPadIndex);
        Assert.Equal($"Mapping override saved: CAD {cadId} -> regular id {regularId}.", vm.StatusText);
        var reopened = await RepairWorkspace.AnalyzeAsync(vm);
        var saved = GetRepair(reopened, cadId);
        Assert.Equal(regularId, saved.OverrideRegularPadIndex);
        reopened.SelectDecisionCommand.Execute(saved);

        reopened.ClearOverrideCommand.Execute(null);

        Assert.Equal("Mapping override cleared.", reopened.ActionStatus);
        Assert.Null(saved.OverrideRegularPadIndex);
        Assert.Equal($"Mapping override cleared: CAD {cadId}.", vm.StatusText);
        var cleared = await RepairWorkspace.AnalyzeAsync(vm);
        Assert.Null(GetRepair(cleared, cadId).OverrideRegularPadIndex);
    }

    [AvaloniaFact]
    public async Task MappingOverrideFromOpenReportRejectsRemovedRegularSourceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        Assert.True(selected.CanApplyOverride);
        await RemoveRegularSourceAsync(vm);

        report.ApplyOverrideCommand.Execute(null);

        Assert.Equal("Failed to apply mapping override.", report.ActionStatus);
        Assert.Equal("Mapping override: target regular pad not in current grid.", vm.StatusText);
        Assert.Null(selected.OverrideRegularPadIndex);
    }

    [AvaloniaFact]
    public async Task StaleReportCannotLocateOrRepairHiddenCadAfterRegularSourceRemovedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var workspace = new RepairWorkspace();
        var vm = await workspace.OpenAsync();
        var report = await RepairWorkspace.AnalyzeAsync(vm);
        var selected = SelectRepair(report);
        var cadId = selected.CadPadId!.Value;
        vm.ApplyCanvasSelection([cadId], []);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);
        await RemoveRegularSourceAsync(vm);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == cadId);
        var previousDiff = vm.GetCadOutputFwDiffIndexOverride(cadId);
        Assert.NotEqual(selected.ApplyDiffIndex, previousDiff);

        report.LocateSelectedIssueCommand.Execute(null);

        Assert.Equal("Diagnostic target is not visible.", report.ActionStatus);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.SelectedRegularPadIndices);

        report.ApplyDiffOverrideCommand.Execute(null);

        Assert.Equal("Failed to apply diff repair override.", report.ActionStatus);
        Assert.Equal($"Diff repair override ignored: CAD {cadId} is not visible.", vm.StatusText);
        Assert.Equal(previousDiff, vm.GetCadOutputFwDiffIndexOverride(cadId));
    }

    [AvaloniaFact]
    public async Task AnalyzeExactOnePadMappingShowsSuccessfulDiagnosticsInHeaderAsync()
    {
        using var culture = TestCulture.UseEnUs();
        var dxfPath = CreateTempDxfFile(("clean", 0, 0, 10, 10));
        var vm = new FreeformHelperViewModel
        {
            PickOpenDxfPathAsync = () => Task.FromResult<string?>(dxfPath),
            ActiveAreaWidth = 10,
            ActiveAreaHeight = 10,
            GridPaddingPercent = 0,
        };
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 1;
        cascade.YChannels = 1;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.Single(vm.CadPads);
        Assert.Single(vm.RegularPads);

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        Assert.StartsWith("Diagnostics OK:", vm.DxfRegularMappingSummary, StringComparison.Ordinal);
        Assert.Equal("Diagnostics OK: map=1, low=0, amb=0, dup=0.", vm.StatusText);
        Assert.False(vm.HasDxfRegularMappingIssues);
        Assert.Empty(vm.DxfRegularMappingIssues);
        Assert.False(vm.IsAnalyzingIndexMapping);
        Assert.Equal(1d, vm.IndexMappingProgress);
    }

    private static async Task RemoveRegularSourceAsync(FreeformHelperViewModel vm)
    {
        vm.SelectedRegularSourceModeOption = Assert.Single(vm.RegularSourceModeOptions,
            option => option.Value == RegularSourceMode.FromDxfLayer);
        vm.SelectedRegularSourceLayerOption = Assert.Single(vm.RegularSourceLayerOptions, option => option.Name is null);
        await WaitForGridRebuildAsync(vm);
        Assert.Empty(vm.RegularPads);
    }
}
