// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceBuildResultTests
{
    [AvaloniaFact]
    public async Task BuildWithInvalidNullValueReportsFailureAndCanRecoverAfterCorrectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        Assert.NotNull(await vm.CreateSimulationWorkspaceSessionAsync(silent: false));
        vm.NullValue = 65536m;
        var messages = new List<string>();

        var failed = await vm.CreateSimulationWorkspaceSessionAsync(silent: false, messages.Add);

        Assert.Null(failed);
        Assert.Equal("Simulation build failed: NullValue must be in [0,65535].", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(vm.LastSimulationWorkspaceAttemptMessage, vm.StatusText);
        Assert.Contains(vm.LastSimulationWorkspaceAttemptMessage, messages);
        Assert.False(vm.IsNotchExporting);

        vm.NullValue = 65535m;
        var recovered = await vm.CreateSimulationWorkspaceSessionAsync(silent: false);

        Assert.NotNull(recovered);
        Assert.NotEmpty(recovered.Table.Rows);
        Assert.Equal(65535, recovered.NullDiffValue);
        Assert.Equal(vm.SimulationWorkspaceSourceRevision, recovered.SourceRevision);
        Assert.Equal($"Simulation ready. Rows={recovered.Table.Rows.Count}.", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(vm.LastSimulationWorkspaceAttemptMessage, vm.StatusText);
        Assert.False(vm.IsNotchExporting);
    }

    [AvaloniaFact]
    public async Task SilentBuildWithInvalidNullValueReportsFailureAndRecoveryWithoutChangingVisibleStatusAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        Assert.NotNull(await vm.CreateSimulationWorkspaceSessionAsync(silent: false));
        vm.NullValue = 65536m;
        var status = vm.StatusText;
        var progressText = vm.NotchExportProgressText;
        var messages = new List<string>();

        var failed = await vm.CreateSimulationWorkspaceSessionAsync(silent: true, messages.Add);

        Assert.Null(failed);
        Assert.Equal("Simulation build failed: NullValue must be in [0,65535].", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Contains(vm.LastSimulationWorkspaceAttemptMessage, messages);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(progressText, vm.NotchExportProgressText);
        Assert.False(vm.IsNotchExporting);

        vm.NullValue = 65535m;
        var recovered = await vm.CreateSimulationWorkspaceSessionAsync(silent: true, messages.Add);

        Assert.NotNull(recovered);
        Assert.NotEmpty(recovered.Table.Rows);
        Assert.Equal($"Simulation ready. Rows={recovered.Table.Rows.Count}.", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Contains(vm.LastSimulationWorkspaceAttemptMessage, messages);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(progressText, vm.NotchExportProgressText);
        Assert.False(vm.IsNotchExporting);
    }
}
