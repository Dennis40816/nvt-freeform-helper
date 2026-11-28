// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchFwDiffEditExportBehaviorTests
{
    [AvaloniaFact]
    public async Task OutputFwDiffOverrideAfterExport_ClearsExportAndWritesNewSourceDiffLikeFreshProjectAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var cadId = NotchDataEditExportTestScope.FirstCadId(vm);
        var before = await files.ExportAsync(vm);
        var beforeState = vm.GetWorkflowStateSnapshot();
        var beforeRow = Assert.Single(NotchDataEditExportTestScope.Rows(before),
            row => row[7] == cadId.ToString(CultureInfo.InvariantCulture));
        Assert.NotEqual("3", beforeRow[5]);
        var info = CreatePadInfo(vm, cadId);
        info.CadOutputFwDiffOverride = 3m;

        info.ApplyCadOutputFwDiffOverrideCommand.Execute(null);

        Assert.Equal<int?>(3, vm.GetCadOutputFwDiffIndexOverride(cadId));
        Assert.Equal("3", info.DxfIndexText);
        Assert.Equal(beforeState, vm.GetWorkflowStateSnapshot());
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.True(vm.ExportNotchCommand.CanExecute(null));
        var immediate = await files.ExportAsync(vm);
        Assert.Equal("CSV review notch exported. Rows=2.", vm.StatusText);
        var afterRow = Assert.Single(NotchDataEditExportTestScope.Rows(immediate),
            row => row[7] == cadId.ToString(CultureInfo.InvariantCulture));
        Assert.Equal("3", afterRow[5]);
        Assert.NotEqual(before, immediate);

        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        var after = await files.ExportAsync(vm);
        Assert.Equal(immediate, after);
        using var freshFiles = new NotchDataEditExportTestScope();
        var fresh = await freshFiles.CreateAsync();
        var freshInfo = CreatePadInfo(fresh, NotchDataEditExportTestScope.FirstCadId(fresh));
        freshInfo.CadOutputFwDiffOverride = 3m;
        freshInfo.ApplyCadOutputFwDiffOverrideCommand.Execute(null);
        await fresh.AnalyzeIndexMappingCommand.ExecuteAsync(null);
        Assert.Equal(after, await freshFiles.ExportAsync(fresh));
    }

    private static CadPadInfoViewModel CreatePadInfo(FreeformHelperViewModel vm, int cadId)
    {
        return new CadPadInfoViewModel(
            pads: [vm.CadPads.Single(pad => pad.Id == cadId)],
            getDxfIndex: id => vm.TryGetCadOutputFwDiffIndex(id, out var index) ? index : null,
            getCadOutputFwDiffOverride: vm.GetCadOutputFwDiffIndexOverride,
            isDxfIndexAnchor: vm.IsCadOutputFwDiffIndexAnchorCadPad,
            getCustomValue: vm.GetCadPadCustomValue,
            applyCustomValue: vm.SetCadPadCustomValues,
            setCadOutputFwDiffOverride: vm.SetCadOutputFwDiffIndexOverride,
            clearCadOutputFwDiffOverride: vm.ClearCadOutputFwDiffIndexOverride,
            setDxfIndexAnchor: vm.SetCadOutputFwDiffIndexAnchorCadPad,
            clearDxfIndexAnchor: vm.ClearCadOutputFwDiffIndexAnchorCadPad);
    }
}
