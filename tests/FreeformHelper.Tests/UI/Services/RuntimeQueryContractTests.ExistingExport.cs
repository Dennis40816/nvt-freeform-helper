// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryContractTests
{
    [AvaloniaFact]
    public async Task ExportWithoutReadyWorkflowPreservesExistingFileAndRestoresUiCallbacksAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = new ShellViewModel { IsDevPageAvailable = true };
        var helper = shell.FreeformHelper;
        var path = TestFiles.WriteTempCsv("previous reviewed export");
        var previousWrite = new FileInfo(path).LastWriteTimeUtc;
        helper.SelectedNotchExportFileTypeOption = Assert.Single(helper.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
        var previousOption = helper.SelectedNotchExportFileTypeOption;
        helper.PickSaveNotchPathAsync = static (_, _) => Task.FromResult<string?>(null);
        helper.OpenNotchExportSelectionAsync = static _ => Task.FromResult<NotchTable?>(null);
        var picker = helper.PickSaveNotchPathAsync;
        var selection = helper.OpenNotchExportSelectionAsync;

        var response = await new RuntimeQueryUseCase(shell).ExecuteAsync(new(RuntimeQueryProtocol.Version,
            "export-notch", ParseArguments("format=csv;path=" + path)));

        Assert.False(response.Ok);
        Assert.Null(response.Data);
        Assert.Equal("STEP_EXECUTION_FAILED", response.Error?.Code);
        Assert.Equal("Notch export did not update output file. summary=Notch export: not run.", response.Error?.Message);
        Assert.Equal("previous reviewed export", await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(previousWrite, new FileInfo(path).LastWriteTimeUtc);
        Assert.Equal(previousOption, helper.SelectedNotchExportFileTypeOption);
        Assert.Same(picker, helper.PickSaveNotchPathAsync);
        Assert.Same(selection, helper.OpenNotchExportSelectionAsync);
    }

    [AvaloniaFact]
    public async Task ExportReplacesExistingFileWithRequestedVersionAndRestoresUiCallbacksAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var shell = await CreateMatchedShellAsync();
        var helper = shell.FreeformHelper;
        helper.EnableV21 = true;
        helper.EnableV22 = true;
        var previousOption = helper.SelectedNotchExportFileTypeOption;
        var pickerCalls = 0;
        var selectionCalls = 0;
        helper.PickSaveNotchPathAsync = (_, _) =>
        {
            pickerCalls++;
            return Task.FromResult<string?>(null);
        };
        helper.OpenNotchExportSelectionAsync = _ =>
        {
            selectionCalls++;
            return Task.FromResult<NotchTable?>(null);
        };
        var picker = helper.PickSaveNotchPathAsync;
        var selection = helper.OpenNotchExportSelectionAsync;
        var path = TestFiles.NewTempFilePath("runtime-replace-export-", ".c");
        TestFiles.WriteText(path, "obsolete export");

        var exported = await QueryAsync(new RuntimeQueryUseCase(shell), "export-notch", "format= C-V21 ;path=" + path);

        var content = await TestFiles.ReadContentAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("c-v21", exported.GetProperty("format").GetString());
        Assert.Equal(path, exported.GetProperty("path").GetString());
        Assert.Equal(new FileInfo(path).Length, exported.GetProperty("fileSize").GetInt64());
        Assert.Contains("//   Sections: v2.1=present, v2.2=empty", content, StringComparison.Ordinal);
        AssertExportedRows(content, "c-v21", Assert.IsType<NotchTable>(helper.NotchCacheForTests.LastGeneratedTable));
        Assert.Equal(0, pickerCalls);
        Assert.Equal(0, selectionCalls);
        Assert.Equal(previousOption, helper.SelectedNotchExportFileTypeOption);
        Assert.Same(picker, helper.PickSaveNotchPathAsync);
        Assert.Same(selection, helper.OpenNotchExportSelectionAsync);
        Assert.True(helper.EnableV21);
        Assert.True(helper.EnableV22);
    }
}
