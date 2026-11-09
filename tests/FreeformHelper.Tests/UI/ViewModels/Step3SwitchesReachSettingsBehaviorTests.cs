// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class Step3SwitchesReachSettingsBehaviorTests
{
    [AvaloniaFact]
    public async Task SaveProject_WithToRegularAndToFullOff_StoresBothSwitchesAsync()
    {
        var projectPath = TestFiles.NewTempFilePath("step3-switches-", ".nfh.json");
        var vm = new FreeformHelperViewModel { PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
        vm.EnableToRegular = false;
        vm.EnableToFull = false;

        Assert.True(await vm.SaveProjectAsync());

        var saved = JsonProjectStore.Load(projectPath);
        Assert.False(saved.Settings.Notch.EnableToRegular);
        Assert.False(saved.Settings.Notch.EnableToFull);
    }

    [AvaloniaFact]
    public async Task ExportAfterAnalyzingSelectedCad_WithToFullOff_ExportsTheTableAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        Assert.False(vm.EnableToFull);
        vm.ApplyCanvasSelection([NotchDataEditExportTestScope.FirstCadId(vm)], []);
        await vm.AnalyzeIndexMappingCommand.ExecuteAsync(null);

        var text = await files.ExportAsync(vm);

        Assert.Equal("CSV review notch exported. Rows=2.", vm.StatusText);
        Assert.Equal(2, NotchDataEditExportTestScope.Rows(text).Length);
    }
}
