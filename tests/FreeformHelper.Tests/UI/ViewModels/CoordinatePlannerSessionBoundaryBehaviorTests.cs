// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;
using static FreeformHelper.Tests.SuppressedWorkspaceBehaviorTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class CoordinatePlannerSessionBoundaryBehaviorTests
{
    private static readonly string[] _expectedVisibleLayers = ["AA", "signal"];

    [AvaloniaFact]
    public async Task CreateSession_MissingDxf_SilentKeepsStatusAndInteractiveExplainsPrerequisitesAsync()
    {
        using var fixture = new WorkspaceFixture();
        var vm = fixture.CreateViewModel();
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        var originalStatus = vm.StatusText;

        var silent = await vm.CreateCoordinatePlannerWorkspaceSessionAsync(silent: true);

        Assert.Null(silent);
        Assert.Equal(originalStatus, vm.StatusText);

        var interactive = await vm.CreateCoordinatePlannerWorkspaceSessionAsync();

        Assert.Null(interactive);
        Assert.Equal("Coordinate: import DXF and build grid first.", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task CreateSession_ManualHideAndExactDuplicate_PreservesLayerChoicesAndProjectsPreferencesAsync()
    {
        using var fixture = new WorkspaceFixture();
        var path = CreateTempDxfFile(
            ("signal", 0, 0, 10, 10), ("signal", 0, 0, 10, 10), ("AA", 20, 0, 30, 10));
        try
        {
            var vm = fixture.CreateViewModel();
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            await WaitForGridRebuildAsync(vm);
            Assert.True(vm.HasDetectedDuplicateCadPads);
            var hidden = Assert.Single(vm.CadPads, static pad => pad.Layer == "signal");
            var retained = Assert.Single(vm.CadPads, static pad => pad.Layer == "AA");
            await SelectCadPadsAsync(vm, [hidden.Id]);
            vm.DeleteSelectedCadPadsCommand.Execute(null);
            await WaitForGridRebuildAsync(vm);
            vm.CoordinatePixelWidth = 100.5m;
            vm.CoordinatePixelHeight = 200.5m;
            vm.CoordinatePreferredAaOutlineLayerName = "signal";
            vm.ShowRegular = false;
            vm.HighlightUnmatched = false;
            vm.HighlightFreeform = false;

            var session = await vm.CreateCoordinatePlannerWorkspaceSessionAsync();

            Assert.NotNull(session);
            Assert.Equal(retained.Id, Assert.Single(session.CadPads).Id);
            Assert.Equal(_expectedVisibleLayers, session.VisibleLayerNames);
            Assert.Equal("signal", session.PreferredActiveAreaOutlineLayerName);
            Assert.Equal(101, session.DefaultPixelWidth);
            Assert.Equal(201, session.DefaultPixelHeight);
            Assert.Equal((double)vm.ActiveAreaWidth, session.DefaultMachineWidth);
            Assert.Equal((double)vm.ActiveAreaHeight, session.DefaultMachineHeight);
            Assert.Equal(vm.WorkspaceDerivedSourceRevision, session.SourceRevision);
            Assert.Equal(vm.RegularPads.Count, session.PadCount);
            Assert.False(session.DefaultShowRegular);
            Assert.False(session.DefaultHighlightUnmatched);
            Assert.False(session.DefaultHighlightFreeform);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
