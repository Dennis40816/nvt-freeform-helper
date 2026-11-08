// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportSelectionBehaviorTests
{
    private static readonly string[] _expectedVersions = ["2.1", "2.2"];

    [AvaloniaFact]
    public async Task ExportNotchCommand_SelectingCv21_ChangesFileSuggestionVersionScopeAndPayload()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.EnableV21 = true;
        var suggestedName = string.Empty;
        var suggestedExtension = string.Empty;
        vm.PickSaveNotchPathAsync = (name, extension) =>
        {
            suggestedName = name;
            suggestedExtension = extension;
            return Task.FromResult<string?>(files.ExportPath);
        };
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv21);
            var table = selection.BuildSelectedTable();
            Assert.NotEmpty(table.Rows);
            Assert.All(table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V21, row.Version));
            return Task.FromResult<NotchTable?>(table);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("notch_v2.1", suggestedName);
        Assert.Equal("c", suggestedExtension);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Cv21, vm.SelectedNotchExportFileTypeOption.Value);
        Assert.True(vm.EnableV21);
        Assert.False(vm.EnableV22);
        var payload = await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken);
        Assert.Contains("//   Sections: v2.1=present, v2.2=empty", payload, StringComparison.Ordinal);
        Assert.StartsWith("C v2.1 exported: rows=", vm.NotchExportSummary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task ExportNotchCommand_SelectingCsvAllVersions_KeepsBothVersionsAndExportsSelectedRows()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.EnableV21 = true;
        var suggestedName = string.Empty;
        var suggestedExtension = string.Empty;
        var selectedRowCount = 0;
        vm.PickSaveNotchPathAsync = (name, extension) =>
        {
            suggestedName = name;
            suggestedExtension = extension;
            return Task.FromResult<string?>(files.ExportPath);
        };
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
            selection.SelectedVersionOption = Assert.Single(selection.VersionOptions, option => !option.Value.HasValue);
            var table = selection.BuildSelectedTable();
            selectedRowCount = table.Rows.Count;
            Assert.Contains(table.Rows, row => row.Version == NotchAlgorithmVersion.V21);
            Assert.Contains(table.Rows, row => row.Version == NotchAlgorithmVersion.V22);
            return Task.FromResult<NotchTable?>(table);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("notch_table", suggestedName);
        Assert.Equal("csv", suggestedExtension);
        Assert.Equal(FreeformHelperViewModel.NotchExportFileType.Csv, vm.SelectedNotchExportFileTypeOption.Value);
        Assert.True(vm.EnableV21);
        Assert.True(vm.EnableV22);
        var payload = await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken);
        var lines = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("row_number,version,version_code,", lines[0], StringComparison.Ordinal);
        Assert.Equal(selectedRowCount + 1, lines.Length);
        Assert.Equal(_expectedVersions, lines.Skip(1).Select(line => line.Split(',')[1]).Distinct().Order(StringComparer.Ordinal));
        Assert.StartsWith("CSV review exported: rows=", vm.NotchExportSummary, StringComparison.Ordinal);
    }
}
