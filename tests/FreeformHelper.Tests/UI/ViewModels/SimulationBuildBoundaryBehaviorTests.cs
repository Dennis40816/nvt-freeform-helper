// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationBuildBoundaryBehaviorTests
{
    [AvaloniaFact]
    public async Task SilentEmptyBuild_AfterSuccessfulBuild_ReportsAttemptWithoutChangingVisibleFeedbackAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        Assert.NotNull(await vm.CreateSimulationWorkspaceSessionAsync(silent: false));
        Assert.Equal("Simulation ready. Rows=2.", vm.LastSimulationWorkspaceAttemptMessage);
        vm.ClearStep2ResultCommand.Execute(null);
        var status = vm.StatusText;
        var progress = vm.NotchExportProgress;
        var progressText = vm.NotchExportProgressText;
        var messages = new List<string>();

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent: true, messages.Add);

        Assert.Null(session);
        Assert.Equal("Simulation: no notch rows generated under current settings.", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Contains(vm.LastSimulationWorkspaceAttemptMessage, messages);
        Assert.DoesNotContain("Simulation ready. Rows=2.", messages);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(progress, vm.NotchExportProgress);
        Assert.Equal(progressText, vm.NotchExportProgressText);
        Assert.False(vm.IsNotchExporting);
    }

    [AvaloniaFact]
    public async Task LoadProject_NotifiesSimulationOnceAfterGridAndMatchingHaveBeenRestoredAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.AutoReplayStep2AfterProjectLoad = true;
        var projectPath = Path.ChangeExtension(files.ExportPath, ".json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        vm.ActiveAreaWidth = 30;
        await vm.WaitForGridRebuildIdleAsync();
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = new List<(int Revision, int CadCount, int RegularCount, bool HasMatches, bool RestoredWidth)>();
        vm.SimulationWorkspaceSourceChanged += (_, _) => notifications.Add((
            vm.SimulationWorkspaceSourceRevision,
            vm.CadPads.Count,
            vm.RegularPads.Count,
            vm.MatchLinksForCanvas.Count > 0,
            vm.ActiveAreaWidth == 20));
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal((revision + 1, 1, 2, true, true), Assert.Single(notifications));
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);
        Assert.NotNull(session);
        Assert.Equal(revision + 1, session.SourceRevision);
        Assert.NotEmpty(session.Table.Rows);
    }
}
