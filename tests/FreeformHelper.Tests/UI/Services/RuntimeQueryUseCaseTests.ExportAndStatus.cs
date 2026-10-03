using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class RuntimeQueryUseCaseTests
{


    [Fact]
    public async Task ExecuteAsync_QueryStatus_IncludesCadLoadSpinnerDebugTelemetry()
    {
        CadLoadSpinnerDebugState.ResetForTest();
        CadLoadSpinnerDebugState.RecordVmOverlayVisibility(isVisible: true, source: "test");
        CadLoadSpinnerDebugState.RecordViewHostSync(action: "show", overlayVisible: true);
        CadLoadSpinnerDebugState.RecordHostShowRequest("test");
        CadLoadSpinnerDebugState.RecordHostShowIpcResult(success: true, source: "test");

        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "status",
            Args: null));

        Assert.True(response.Ok, $"{response.Error?.Code}:{response.Error?.Message}");
        Assert.Null(response.Error);

        var root = SerializeToRootElement(response.Data);
        Assert.Equal(Environment.ProcessId, root.GetProperty("processId").GetInt32());
        var exportState = root.GetProperty("notchExportState");
        Assert.Equal(shell.FreeformHelper.EnableV21, exportState.GetProperty("enableV21").GetBoolean());
        Assert.Equal(shell.FreeformHelper.EnableV22, exportState.GetProperty("enableV22").GetBoolean());
        Assert.Equal(
            shell.FreeformHelper.SelectedNotchExportFileTypeOption.Value.ToString(),
            exportState.GetProperty("fileType").GetString());
        Assert.Equal(
            shell.FreeformHelper.SelectedNotchExportProfileOption.Value.ToString(),
            exportState.GetProperty("profile").GetString());
        var spinner = root.GetProperty("cadLoadSpinner");
        Assert.True(spinner.GetProperty("vmOverlayShowCount").GetInt32() >= 1);
        Assert.True(spinner.GetProperty("viewHostShowCount").GetInt32() >= 1);
        Assert.True(spinner.GetProperty("hostShowRequestCount").GetInt32() >= 1);
        Assert.True(spinner.GetProperty("hostShowIpcSuccessCount").GetInt32() >= 1);
        Assert.True(spinner.GetProperty("events").GetArrayLength() >= 1);
        var cadOutputFwDiffAssignmentDecision = root.GetProperty("cadOutputFwDiffAssignmentDecision");
        Assert.True(cadOutputFwDiffAssignmentDecision.GetProperty("decisionCount").GetInt32() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(cadOutputFwDiffAssignmentDecision.GetProperty("mode").GetString()));

        CadLoadSpinnerDebugState.ResetForTest();
    }


    [Fact]
    public async Task ExecuteAsync_QueryExportNotch_RejectsUnsupportedFormat()
    {
        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "export-notch",
            Args: new Dictionary<string, string>
            {
                ["format"] = "binary",
                ["path"] = "build/perf/notch_table.bin"
            }));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("INVALID_ARGUMENTS", response.Error!.Code);
    }


    [Fact]
    public async Task ExecuteAsync_QueryExportNotch_ReturnsFailureWhenWorkflowNotReady()
    {
        using var shell = new ShellViewModel();
        var useCase = new RuntimeQueryUseCase(shell);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"freeformhelper-export-{Guid.NewGuid():N}.csv");

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "export-notch",
            Args: new Dictionary<string, string>
            {
                ["format"] = "csv",
                ["path"] = path
            }));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("STEP_EXECUTION_FAILED", response.Error!.Code);
        Assert.False(System.IO.File.Exists(path));
    }


    [Fact]
    public async Task ExecuteAsync_QueryExportNotch_AcceptsVersionedCFormat_WhenWorkflowNotReady()
    {
        using var shell = new ShellViewModel();
        shell.FreeformHelper.EnableV21 = true;
        shell.FreeformHelper.EnableV22 = false;
        var useCase = new RuntimeQueryUseCase(shell);
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"freeformhelper-export-{Guid.NewGuid():N}.c");

        var response = await useCase.ExecuteAsync(new RuntimeQueryRequest(
            Version: RuntimeQueryProtocol.Version,
            Command: "export-notch",
            Args: new Dictionary<string, string>
            {
                ["format"] = "c-v22",
                ["path"] = path
            }));

        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("STEP_EXECUTION_FAILED", response.Error!.Code);
        Assert.False(System.IO.File.Exists(path));
        Assert.True(shell.FreeformHelper.EnableV21);
        Assert.False(shell.FreeformHelper.EnableV22);
    }
}
