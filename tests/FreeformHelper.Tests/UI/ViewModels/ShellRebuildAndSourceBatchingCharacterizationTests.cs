// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class ShellRebuildAndSourceBatchingCharacterizationTests
{
    [AvaloniaFact]
    public async Task ManualSizingRebuild_PreservesSelectionOnceAndNextRebuildClearsNewSelectionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        var first = Assert.Single(vm.CadPads, pad => pad.Layer == "signal-a");
        var second = Assert.Single(vm.CadPads, pad => pad.Layer == "signal-b");
        var originalScanOrder = vm.SelectedScanOrder;
        vm.ApplyCanvasSelection([first.Id], []);
        Assert.Equal(first.Id, Assert.Single(vm.SelectedCadPadIds));

        vm.ApplySizingToCells([0], [0], 12m, null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(12d, Assert.Single(vm.RegularPads, pad => pad.Row == 0 && pad.Col == 0).Bounds.Width, 5);
        Assert.Equal(first.Id, Assert.Single(vm.SelectedCadPadIds));
        Assert.Equal(first.Id, Assert.Single(vm.InteractionState.SelectedCadIds));
        vm.ApplyCanvasSelection([second.Id], []);
        Assert.Equal(second.Id, Assert.Single(vm.SelectedCadPadIds));

        vm.SelectedScanOrder = vm.ScanOrderOptions.First(option => option.Value != originalScanOrder).Value;
        await vm.WaitForGridRebuildIdleAsync();

        Assert.NotEqual(originalScanOrder, vm.SelectedScanOrder);
        Assert.Equal(9, vm.RegularPads.Count);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(vm.InteractionState.SelectedCadIds);
    }

    [AvaloniaFact]
    public async Task LoadProject_BatchesRestoredSourcesIntoOneNotificationWithFinalSessionRevisionAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.AutoReplayStep2AfterProjectLoad = true;
        var savedEnableToRegular = vm.EnableToRegular;
        var path = Path.ChangeExtension(files.ExportPath, ".json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        vm.ActiveAreaWidth = 30m;
        await vm.WaitForGridRebuildIdleAsync();
        vm.EnableToRegular = !savedEnableToRegular;
        Assert.Empty(vm.MatchLinksForCanvas);
        var revision = vm.SimulationWorkspaceSourceRevision;
        var notifications = new List<(
            int Revision, decimal Width, bool ToRegular, int CadCount, int RegularCount, bool HasMatches)>();
        vm.SimulationWorkspaceSourceChanged += (_, _) => notifications.Add((
            vm.SimulationWorkspaceSourceRevision,
            vm.ActiveAreaWidth,
            vm.EnableToRegular,
            vm.CadPads.Count,
            vm.RegularPads.Count,
            vm.MatchLinksForCanvas.Count > 0));
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);

        await vm.LoadProjectCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal((revision + 1, 20m, savedEnableToRegular, 1, 2, true), Assert.Single(notifications));
        Assert.Equal(revision + 1, vm.SimulationWorkspaceSourceRevision);
        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);
        Assert.NotNull(session);
        Assert.Equal(revision + 1, session.SourceRevision);
        Assert.NotEmpty(session.Table.Rows);
        Assert.Single(notifications);
    }
}
