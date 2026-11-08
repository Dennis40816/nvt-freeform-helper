// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportPersistenceBehaviorTests
{
    [AvaloniaFact]
    public async Task ExportNotchCommand_AcceptedV22Scope_IsPersistedOnlyBySaveProject()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        var projectPath = Path.ChangeExtension(files.ExportPath, ".project.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
        vm.EnableV21 = true;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
        Assert.True(await vm.SaveProjectAsync());
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(null);
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
            var table = selection.BuildSelectedTable();
            Assert.NotEmpty(table.Rows);
            Assert.All(table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V22, row.Version));
            return Task.FromResult<NotchTable?>(table);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("C v2.2 export canceled at stage 4 (save path not selected).", vm.NotchExportSummary);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Cv22, vm.SelectedNotchExportFileTypeOption.Value);
        Assert.False(vm.EnableV21);
        Assert.True(vm.EnableV22);
        Assert.False(File.Exists(files.ExportPath));
        var beforeSave = JsonProjectStore.Load(projectPath);
        Assert.Equal("Csv", beforeSave.UiSnapshot.View.NotchExportFileType);
        Assert.Contains(NotchAlgorithmVersion.V21, beforeSave.Settings.Notch.EnabledVersions);
        Assert.Contains(NotchAlgorithmVersion.V22, beforeSave.Settings.Notch.EnabledVersions);

        Assert.True(await vm.SaveProjectAsync());

        var afterSave = JsonProjectStore.Load(projectPath);
        Assert.Equal("Cv22", afterSave.UiSnapshot.View.NotchExportFileType);
        Assert.Equal(NotchAlgorithmVersion.V22, Assert.Single(afterSave.Settings.Notch.EnabledVersions));
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_CsvAllVersions_PreservesSingleEnabledVersionOnSave()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        var projectPath = Path.ChangeExtension(files.ExportPath, ".project.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
            selection.SelectedVersionOption = Assert.Single(selection.VersionOptions, option => !option.Value.HasValue);
            return Task.FromResult<NotchTable?>(selection.BuildSelectedTable());
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.StartsWith("CSV review exported: rows=", vm.NotchExportSummary, StringComparison.Ordinal);
        Assert.False(vm.EnableV21);
        Assert.True(vm.EnableV22);
        Assert.True(await vm.SaveProjectAsync());
        var saved = JsonProjectStore.Load(projectPath);
        Assert.Equal("Csv", saved.UiSnapshot.View.NotchExportFileType);
        Assert.Equal(NotchAlgorithmVersion.V22, Assert.Single(saved.Settings.Notch.EnabledVersions));
        var payload = await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken);
        var rows = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal("2.2", row.Split(',')[1]));
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_CancelReview_DiscardsDraftTypeAndVersion()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.EnableV21 = true;
        var savePickerCalls = 0;
        vm.PickSaveNotchPathAsync = (_, _) =>
        {
            savePickerCalls++;
            return Task.FromResult<string?>(files.ExportPath);
        };
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
            selection.SelectedVersionOption = Assert.Single(selection.VersionOptions,
                option => option.Value == NotchAlgorithmVersion.V21);
            return Task.FromResult<NotchTable?>(null);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("C v2.2 export canceled at stage 3 (row selection).", vm.NotchExportSummary);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Cv22, vm.SelectedNotchExportFileTypeOption.Value);
        Assert.True(vm.EnableV21);
        Assert.True(vm.EnableV22);
        Assert.Equal(0, savePickerCalls);
        Assert.False(File.Exists(files.ExportPath));
    }
}
