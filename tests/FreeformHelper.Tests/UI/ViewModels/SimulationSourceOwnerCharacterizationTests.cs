// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Domain.Notch;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class SimulationSourceOwnerCharacterizationTests
{
    [Fact]
    public async Task ImportCancellationAndFailureRetainAcceptedDataAndCollectionIdentityAsync()
    {
        var vm = CreateWorkspace();
        var path = WriteFrame("100,50");
        await ImportPathsAsync(vm, path);
        var accepted = vm.GetSimulationScenarioSnapshot();
        var files = vm.CsvFiles;
        var frames = vm.FrameOptions;
        var row = files[0];
        await ImportPathsAsync(vm);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        await ImportPathsAsync(vm, WriteFrame("7,abc"));
        Assert.StartsWith("CSV import failed:", vm.StatusText, StringComparison.Ordinal);
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        Assert.Same(files, vm.CsvFiles);
        Assert.Same(frames, vm.FrameOptions);
        Assert.Same(row, Assert.Single(vm.CsvFiles));
        Assert.Single(vm.FrameOptions);
        await ImportPathsAsync(vm, path.ToUpperInvariant(), path);
        Assert.Single(vm.CsvFiles);
        Assert.Equal(path, Assert.Single(vm.GetSimulationScenarioSnapshot()!.Dataset.Files).SourcePath);
    }

    [Fact]
    public async Task ImportAndClearInvalidateCachedSnapshotsWithoutReplacingCollectionsAsync()
    {
        var vm = CreateWorkspace();
        var first = WriteFrame("100,50");
        var last = WriteFrame("200,80");
        var incompatible = TestFiles.WriteTempCsv("Xch:\"3\",Ych:\"1\"\ntimestamp(0:0:0),DiffData:\n7,7,7\n");
        await ImportPathsAsync(vm, first, incompatible, last);
        var frames = vm.FrameOptions;
        var files = vm.CsvFiles;
        Assert.Equal<int>([0, 2], frames.Select(static frame => frame.Value));
        var accepted = vm.GetSimulationScenarioSnapshot();
        vm.SelectedFrameSliderIndex = 1;
        Assert.Equal(2, vm.GetSimulationScenarioSnapshot()!.Scenario.SelectedFrameIndex);
        Assert.Equal(200d, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells[0].BeforeValue);
        vm.SelectedFrameSliderIndex = 0;
        Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
        await ImportPathsAsync(vm, first);
        Assert.NotSame(accepted, vm.GetSimulationScenarioSnapshot());
        accepted = vm.GetSimulationScenarioSnapshot();
        vm.ClearCsvCommand.Execute(null);
        Assert.Empty(files);
        Assert.Empty(frames);
        Assert.True(vm.IsCsvSource);
        Assert.False(vm.HasCsvData);
        await ImportPathsAsync(vm, first);
        Assert.NotSame(accepted, vm.GetSimulationScenarioSnapshot());
        Assert.Same(files, vm.CsvFiles);
        Assert.Same(frames, vm.FrameOptions);
        Assert.Single(frames);
    }

    [Fact]
    public void PresetUnchangedBaselineWithOverridesStillReplacesSnapshot()
    {
        var vm = CreateWorkspace();
        vm.SelectRegularPad(0);
        vm.SelectedManualValue = 123;
        vm.ApplySelectedOverrideCommand.Execute(null);
        var accepted = vm.GetSimulationScenarioSnapshot();

        vm.ApplyManualPresetCommand.Execute("0");

        Assert.False(vm.HasManualOverrides);
        Assert.Equal(0d, vm.SelectedManualValue);
        Assert.NotSame(accepted, vm.GetSimulationScenarioSnapshot());
        Assert.Equal(0d, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells[0].BeforeValue);
        Assert.Equal("Manual preset applied: uniform 0.", vm.StatusText);
    }

    [Theory]
    [InlineData(SimulationInputSourceMode.Manual, false)]
    [InlineData(SimulationInputSourceMode.Csv, false)]
    [InlineData(SimulationInputSourceMode.Copper, false)]
    [InlineData(SimulationInputSourceMode.Manual, true)]
    [InlineData(SimulationInputSourceMode.Csv, true)]
    [InlineData(SimulationInputSourceMode.Copper, true)]
    public async Task OverrideChangesPreserveModeAndCsvAvailabilityRefreshPolicyAsync(
        SimulationInputSourceMode mode, bool hasCsv)
    {
        var vm = CreateWorkspace();
        if (hasCsv) await ImportPathsAsync(vm, WriteFrame("100,50"));
        vm.SelectedInputSourceOption = vm.SourceOptions.Single(option => option.Value == mode);
        vm.SelectRegularPad(0);
        vm.SelectedManualValue = 123;
        var accepted = vm.GetSimulationScenarioSnapshot();

        vm.ApplySelectedOverrideCommand.Execute(null);

        if (mode == SimulationInputSourceMode.Copper && !hasCsv)
        {
            // Observed on the old code: no override is accepted in Copper mode without a CSV.
            Assert.False(vm.HasSelectedOverride);
            return;
        }

        Assert.True(vm.HasSelectedOverride);
        Assert.Equal(mode, vm.SelectedInputSourceOption.Value);
        Assert.Equal(mode != SimulationInputSourceMode.Manual && hasCsv,
            ReferenceEquals(accepted, vm.GetSimulationScenarioSnapshot()));
        if (mode != SimulationInputSourceMode.Copper && !hasCsv)
        {
            Assert.Equal(123d, vm.GetSimulationScenarioSnapshot()!.SimulationResult.Cells[0].BeforeValue);
        }
        accepted = vm.GetSimulationScenarioSnapshot();
        vm.ClearSelectedOverrideCommand.Execute(null);
        Assert.False(vm.HasSelectedOverride);
        Assert.Equal(0d, vm.SelectedManualValue);
        Assert.Equal(mode != SimulationInputSourceMode.Manual && hasCsv,
            ReferenceEquals(accepted, vm.GetSimulationScenarioSnapshot()));
    }

    [Fact]
    public async Task SourceChangeStopsPlaybackBeforeAcceptingReplacementSnapshotAsync()
    {
        await Task.Run(async () =>
        {
            var vm = CreateWorkspace();
            await ImportPathsAsync(vm, WriteFrame("100,50"), WriteFrame("200,80"));
            vm.TogglePlaybackCommand.Execute(null);
            var accepted = vm.GetSimulationScenarioSnapshot();
            var events = new List<string>();
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(vm.IsPlaybackRunning))
                {
                    Assert.False(vm.IsPlaybackRunning);
                    Assert.Same(accepted, vm.GetSimulationScenarioSnapshot());
                    events.Add("stopped");
                }
                if (e.PropertyName == nameof(vm.SimulationOverlayItems))
                {
                    Assert.False(vm.IsPlaybackRunning);
                    Assert.NotSame(accepted, vm.GetSimulationScenarioSnapshot());
                    events.Add("accepted");
                }
            };

            vm.SelectedInputSourceOption = vm.SourceOptions[0];

            Assert.Equal<string>(["stopped", "accepted"], events);
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void SnapshotPublicationExposesAcceptedIndexesBeforeOverlayThenDiagnosticsSafetyStatusSelectionAndCommands()
    {
        var vm = CreateWorkspace();
        vm.SelectRegularPad(0);
        vm.StatusText = "before publication";
        var oldSnapshot = vm.GetSimulationScenarioSnapshot();
        var oldSafety = vm.GetSimulationSafetyAuditSnapshot();
        var events = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(vm.SimulationOverlayItems):
                    Assert.NotSame(oldSnapshot, vm.GetSimulationScenarioSnapshot());
                    Assert.True(vm.TryGetRegularQuerySnapshot(0, out var query));
                    Assert.Equal(1000d, query.BeforeValue);
                    Assert.NotEmpty(query.ImpactItems);
                    Assert.Same(oldSafety, vm.GetSimulationSafetyAuditSnapshot());
                    Assert.Equal(0d, vm.SelectedManualValue);
                    events.Add("overlay");
                    break;
                case nameof(vm.DiffViolationPadCount):
                    Assert.Same(oldSafety, vm.GetSimulationSafetyAuditSnapshot());
                    events.Add("mismatch");
                    break;
                case nameof(vm.SimulationEmsAfterCap):
                    Assert.Same(vm.GetSimulationScenarioSnapshot()!.SafetyAudit, vm.GetSimulationSafetyAuditSnapshot());
                    events.Add("safety");
                    break;
                case nameof(vm.StatusText): events.Add("status"); break;
                case nameof(vm.SelectedBeforeText):
                    Assert.Equal(1000d, vm.SelectedManualValue);
                    events.Add("selection");
                    break;
            }
        };
        vm.ImportCsvCommand.CanExecuteChanged += (_, _) => events.Add("commands");

        vm.GlobalValue = 1000;

        Assert.Equal<string>(["overlay", "mismatch", "safety", "status", "selection", "commands"], events);
    }

    private static Task ImportPathsAsync(SimulationWorkspaceViewModel vm, params string[] paths)
    {
        vm.PickOpenDiffCsvPathsAsync = () => Task.FromResult<IReadOnlyList<string>>(paths);
        return vm.ImportCsvAsync();
    }

    private static string WriteFrame(string values) =>
        TestFiles.WriteTempCsv($"Xch:\"2\",Ych:\"1\"\ntimestamp(0:0:0),DiffData:\n{values}\n");

    private static SimulationWorkspaceViewModel CreateWorkspace()
    {
        var grid = TestGeometryFactory.CreateLinearRegularGrid([10, 11]);
        var table = new NotchTable(
        [
            new NotchTableRow(0, 10, 0, 313, new NotchV22Node(10, 50, 65535, 0, 65535, 0, 0), "source order"),
        ]);
        var session = new FreeformHelper.UI.Services.SimulationWorkspaceSession(
            grid, table, 65535, SourceRevision: 1,
            ActiveRegularPadIds: grid.Pads.Select(static pad => pad.RegularPadId).ToHashSet());
        return new SimulationWorkspaceViewModel(
            new FreeformHelper.UI.Services.SimulationWorkspaceUseCase(
                new FreeformHelper.UI.Services.NotchApplySimulationReviewUseCase()), session);
    }
}
