// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinatePlannerBuildRefreshBehaviorTests
{
    [AvaloniaFact]
    public async Task OpenCoordinatePlannerCommand_WithoutAPlan_BuildsAndOpensTheCurrentSourceAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        using var shell = new ShellViewModel(vm) { IsDevPageAvailable = true };
        var host = shell.CoordinatePlanner;
        Assert.Null(host.CurrentWorkspace);
        Assert.True(host.HasNoWorkspace);
        Assert.False(host.HasWorkspace);

        await host.OpenCoordinatePlannerCommand.ExecuteAsync(null);

        var workspace = Assert.IsType<CoordinatePlannerWorkspaceViewModel>(host.CurrentWorkspace);
        Assert.True(host.HasWorkspace);
        Assert.False(host.HasNoWorkspace);
        Assert.False(host.IsWorkspaceStale);
        Assert.Equal(vm.WorkspaceDerivedSourceRevision, workspace.SourceRevision);
        Assert.Equal(vm.RegularPads, workspace.RegularPads);
        Assert.NotEmpty(workspace.ArtifactRows);
        Assert.True(shell.IsCoordinateActive);
        Assert.Same(host, shell.CurrentViewModel);
    }

    [AvaloniaFact]
    public async Task OpenCoordinatePlannerCommand_AfterGridChange_RefreshesRowsAndClearsCustomPointsAndPathsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var project = new SyntheticWorkflowProjectScope();
        var vm = await project.LoadAsync();
        using var shell = new ShellViewModel(vm) { IsDevPageAvailable = true };
        var work = new RecordingBackgroundWorkRunner();
        shell.BackgroundWork.Runner = work;
        var host = shell.CoordinatePlanner;
        await host.OpenCoordinatePlannerCommand.ExecuteAsync(null);
        var original = Assert.IsType<CoordinatePlannerWorkspaceViewModel>(host.CurrentWorkspace);
        original.CustomEditor.CustomPointLabelText = "Probe";
        original.AddCustomPointCommand.Execute(null);
        original.CustomEditor.CustomPathLabelText = "Sweep";
        original.AddCustomPathCommand.Execute(null);
        Assert.Single(original.Snapshot.Points, static point => point.Kind == CoordinatePlannerPointKind.CustomPoint);
        Assert.Single(original.Snapshot.Lines, static line => line.Kind == CoordinatePlannerLineKind.CustomPath);
        var originalRows = original.ArtifactRows.ToArray();
        var originalCorner = Assert.Single(originalRows, static row => row.Key == "aa-tr");

        vm.ActiveAreaWidth *= 2m;
        await vm.WaitForGridRebuildIdleAsync();
        await work.LastStartedAsync(ShellViewModel.WorkspaceSourceChangeDrainWorkName);

        Assert.Same(original, host.CurrentWorkspace);
        Assert.True(host.IsWorkspaceStale);
        Assert.Equal("Source changed", host.SourceStatusText);
        Assert.Equal(originalRows, original.ArtifactRows);
        await host.OpenCoordinatePlannerCommand.ExecuteAsync(null);

        var refreshed = Assert.IsType<CoordinatePlannerWorkspaceViewModel>(host.CurrentWorkspace);
        Assert.NotSame(original, refreshed);
        Assert.True(host.HasWorkspace);
        Assert.False(host.IsWorkspaceStale);
        Assert.Equal("Source up to date", host.SourceStatusText);
        Assert.Equal(vm.WorkspaceDerivedSourceRevision, refreshed.SourceRevision);
        Assert.Equal(vm.RegularPads, refreshed.RegularPads);
        Assert.NotEqual(original.Snapshot.ActiveAreaBounds, refreshed.Snapshot.ActiveAreaBounds);
        Assert.False(refreshed.HasCustomArtifacts);
        Assert.Empty(refreshed.CustomRecipeRows);
        Assert.DoesNotContain(refreshed.Snapshot.Points, static point => point.Kind == CoordinatePlannerPointKind.CustomPoint);
        Assert.DoesNotContain(refreshed.Snapshot.Points, static point => point.Kind == CoordinatePlannerPointKind.CustomPathStep);
        Assert.DoesNotContain(refreshed.Snapshot.Lines, static line => line.Kind == CoordinatePlannerLineKind.CustomPath);
        AssertRefreshedCorner(vm, refreshed, originalCorner);
    }

    private static void AssertRefreshedCorner(
        FreeformHelperViewModel vm,
        CoordinatePlannerWorkspaceViewModel workspace,
        CoordinateArtifactRow original)
    {
        var corner = Assert.Single(workspace.ArtifactRows, static row => row.Key == "aa-tr");
        Assert.NotEqual(original.MachineX, corner.MachineX);
        Assert.Equal((double)vm.ActiveAreaWidth, corner.MachineX);
        Assert.Equal(0d, corner.MachineY);
        Assert.Equal(vm.RegularPads.Max(static pad => pad.Bounds.MaxX), corner.WorldX);
        Assert.Equal(vm.RegularPads.Max(static pad => pad.Bounds.MaxY), corner.WorldY);
        Assert.Equal(workspace.PixelWidth, (decimal)corner.PixelX);
    }
}
