// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Globalization;
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchCadEditExportBehaviorTests
{
    [AvaloniaFact]
    public async Task HideCadAfterExport_BlocksUntilRematchedAndExportsOnlyRemainingCadAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var cadId = NotchDataEditExportTestScope.FirstCadId(vm);
        var before = await files.ExportAsync(vm);
        Assert.Equal(2, NotchDataEditExportTestScope.Rows(before).Length);
        await FreeformHelperViewModelTests.SelectCadPadsAsync(vm, [cadId]);

        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.DoesNotContain(vm.CadPads, pad => pad.Id == cadId);
        Assert.Empty(vm.SelectedCadPadIds);
        NotchDataEditExportTestScope.AssertDownstreamCleared(vm, hasMatch: false, hasFreeform: false);
        const string matchRequired = "Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.";
        var pickerCalls = files.SavePickerCalls;
        await vm.ExportNotchCommand.ExecuteAsync(null);
        Assert.Equal(pickerCalls, files.SavePickerCalls);
        Assert.Equal(matchRequired, vm.StatusText);
        Assert.Equal("CSV review export blocked at stage 1: " + matchRequired, vm.NotchExportProgressText);
        Assert.Equal(before, await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken));

        await NotchDataEditExportTestScope.RunStepsAsync(vm);
        var after = await files.ExportAsync(vm);
        var row = Assert.Single(NotchDataEditExportTestScope.Rows(after));
        Assert.Equal(Assert.Single(vm.CadPads).Id.ToString(CultureInfo.InvariantCulture), row[7]);
        Assert.NotEqual(before, after);

        using var freshFiles = new NotchDataEditExportTestScope();
        var fresh = await freshFiles.CreateAsync();
        await FreeformHelperViewModelTests.SelectCadPadsAsync(fresh, [NotchDataEditExportTestScope.FirstCadId(fresh)]);
        fresh.DeleteSelectedCadPadsCommand.Execute(null);
        await NotchDataEditExportTestScope.RunStepsAsync(fresh);
        Assert.Equal(after, await freshFiles.ExportAsync(fresh));
    }

    [AvaloniaFact]
    public async Task RotateCadAfterExport_BlocksUntilRematchedAndMatchesFreshGeometryExportAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var cadId = NotchDataEditExportTestScope.FirstCadId(vm);
        var before = await files.ExportAsync(vm);
        var vertices = vm.CadPads.Single(pad => pad.Id == cadId).Polygon.Vertices.ToArray();
        await FreeformHelperViewModelTests.SelectCadPadsAsync(vm, [cadId]);
        vm.DxfEditRotationDegrees = 45m;

        vm.RotateSelectedCadPadsCommand.Execute(null);
        await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);

        Assert.Equal(1, vm.RotatedCadPadCount);
        Assert.NotEqual(vertices, vm.CadPads.Single(pad => pad.Id == cadId).Polygon.Vertices.ToArray());
        Assert.Empty(vm.SelectedCadPadIds);
        NotchDataEditExportTestScope.AssertDownstreamCleared(vm, hasMatch: false, hasFreeform: false);
        const string matchRequired = "Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.";
        var pickerCalls = files.SavePickerCalls;
        await vm.ExportNotchCommand.ExecuteAsync(null);
        Assert.Equal(pickerCalls, files.SavePickerCalls);
        Assert.Equal(matchRequired, vm.StatusText);
        Assert.Equal("CSV review export blocked at stage 1: " + matchRequired, vm.NotchExportProgressText);
        Assert.Equal(before, await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken));

        await NotchDataEditExportTestScope.RunStepsAsync(vm);
        var after = await files.ExportAsync(vm);
        Assert.NotEqual(before, after);
        Assert.Contains(NotchDataEditExportTestScope.Rows(after),
            row => row[7] == cadId.ToString(CultureInfo.InvariantCulture));

        using var freshFiles = new NotchDataEditExportTestScope();
        var fresh = await freshFiles.CreateAsync();
        await FreeformHelperViewModelTests.SelectCadPadsAsync(fresh, [NotchDataEditExportTestScope.FirstCadId(fresh)]);
        fresh.DxfEditRotationDegrees = 45m;
        fresh.RotateSelectedCadPadsCommand.Execute(null);
        await NotchDataEditExportTestScope.RunStepsAsync(fresh);
        Assert.Equal(after, await freshFiles.ExportAsync(fresh));
    }
}
