// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using System.ComponentModel;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationSessionBridgeCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingGridPreservesTheAttemptAndOnlyVisibleBuildSetsStatusAsync(bool silent)
    {
        // TestAppSettingsIsolation initializes the assembly before this default-store constructor runs.
        var vm = new FreeformHelperViewModel();
        var status = vm.StatusText;
        var progress = new List<string>();

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent, progress.Add);

        Assert.Null(session);
        Assert.Empty(vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Empty(progress);
        Assert.Equal(silent ? status : "Simulation: import DXF and build grid first.", vm.StatusText);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingCadPreservesThePreviousAttemptWithoutReportingProgressAsync(bool silent)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        Assert.NotNull(await vm.CreateSimulationWorkspaceSessionAsync(true));
        var previousAttempt = vm.LastSimulationWorkspaceAttemptMessage;
        vm.CadDocument.Cad = null;
        vm.CadDocument.InvalidateFilteredCad();
        var status = vm.StatusText;
        var progress = new List<string>();

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent, progress.Add);

        Assert.Null(session);
        Assert.NotEmpty(vm.RegularPads);
        Assert.Equal(previousAttempt, vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Empty(progress);
        Assert.Equal(silent ? status : "Simulation: import DXF and build grid first.", vm.StatusText);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SuccessfulBuildPreservesBridgeProgressAndStatusOrderAsync(bool silent)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        var status = vm.StatusText;
        using var trace = new Sim7BuildTrace(vm);

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent, trace.Report);

        Assert.NotNull(session);
        Assert.Equal(vm.SimulationWorkspaceSourceRevision, session.SourceRevision);
        var ready = $"Simulation ready. Rows={session.Table.Rows.Count}.";
        Assert.Equal(ready, vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(silent ? status : ready, vm.StatusText);
        Assert.Equal<string>(silent
            ? ["P:Preparing simulation workspace...", "P:Generating simulation notch table...",
                "P:Resolving simulation diff grid...", "P:Finalizing simulation workspace...", $"P:{ready}"]
            : ["P:Preparing simulation workspace...", "S:Preparing simulation workspace...",
                "P:Generating simulation notch table...", "S:Generating simulation notch table...",
                "P:Resolving simulation diff grid...", "S:Resolving simulation diff grid...",
                "P:Finalizing simulation workspace...", "S:Finalizing simulation workspace...", $"S:{ready}", $"P:{ready}"],
            trace.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EmptyGenerationReportsNoRowsBeforeFinalizationAsync(bool silent)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        Assert.NotNull(await vm.CreateSimulationWorkspaceSessionAsync(false));
        vm.ClearStep2ResultCommand.Execute(null);
        var status = vm.StatusText;
        using var trace = new Sim7BuildTrace(vm);

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent, trace.Report);

        const string message = "Simulation: no notch rows generated under current settings.";
        Assert.Null(session);
        Assert.Equal(message, vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(silent ? status : message, vm.StatusText);
        Assert.Equal<string>(silent
            ? ["P:Preparing simulation workspace...", "P:Generating simulation notch table...", $"P:{message}"]
            : ["P:Preparing simulation workspace...", "S:Preparing simulation workspace...",
                "P:Generating simulation notch table...", "S:Generating simulation notch table...", $"S:{message}", $"P:{message}"],
            trace.Entries);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BuildFailureReportsErrorAndNextAdmittedAttemptResetsBeforeProgressAsync(bool silent)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.NullValue = 65536m;
        var status = vm.StatusText;
        using var trace = new Sim7BuildTrace(vm);

        var failed = await vm.CreateSimulationWorkspaceSessionAsync(silent, trace.Report);

        const string message = "Simulation build failed: NullValue must be in [0,65535].";
        Assert.Null(failed);
        Assert.Equal(message, vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(silent ? status : message, vm.StatusText);
        Assert.Equal<string>(silent
            ? ["P:Preparing simulation workspace...", "P:Generating simulation notch table...", $"P:{message}"]
            : ["P:Preparing simulation workspace...", "S:Preparing simulation workspace...",
                "P:Generating simulation notch table...", "S:Generating simulation notch table...",
                "S:Simulation workspace failed: NullValue must be in [0,65535].", $"S:{message}", $"P:{message}"],
            trace.Entries);
        vm.NullValue = 65535m;
        var attemptsAtPreparation = new List<string>();

        var recovered = await vm.CreateSimulationWorkspaceSessionAsync(silent, text =>
        {
            if (text == "Preparing simulation workspace...")
            {
                attemptsAtPreparation.Add(vm.LastSimulationWorkspaceAttemptMessage);
            }
        });

        Assert.Equal(string.Empty, Assert.Single(attemptsAtPreparation));
        Assert.NotNull(recovered);
        Assert.Equal($"Simulation ready. Rows={recovered.Table.Rows.Count}.", vm.LastSimulationWorkspaceAttemptMessage);
    }

    [Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DuplicateWarningPrecedesFinalizationAndIsRetainedInTheAttemptAsync(bool silent)
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        var path = FreeformHelperViewModelTests.CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 0, 0, 14, 10));
        try
        {
            await Sim7PrepareDuplicateCadAsync(vm, path);
            var duplicate = Assert.Single(vm.Step4DuplicateDiffGroups);
            var warning = $"Simulation warning: duplicate CAD output FW diff at IC{duplicate.IcIndex + 1}/diff{duplicate.DiffIndex}; " +
                "results may include ambiguous mappings.";
            var status = vm.StatusText;
            using var trace = new Sim7BuildTrace(vm);

            var session = await vm.CreateSimulationWorkspaceSessionAsync(silent, trace.Report);

            Assert.NotNull(session);
            var ready = $"Simulation ready. Rows={session.Table.Rows.Count}.";
            Assert.Equal($"{ready} {warning}", vm.LastSimulationWorkspaceAttemptMessage);
            Assert.Equal(silent ? status : ready, vm.StatusText);
            Assert.Equal<string>(silent
                ? [$"P:{warning}", "P:Finalizing simulation workspace...", $"P:{ready}"]
                : [$"S:{warning}", $"P:{warning}", "P:Finalizing simulation workspace...",
                    "S:Finalizing simulation workspace...", $"S:{ready}", $"P:{ready}"],
                trace.Entries.SkipWhile(entry => entry != $"P:{warning}" && entry != $"S:{warning}"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ProjectLoadSuspendsRepeatedChangesAndPublishesOnceBeforeLaterImmediateChangesAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.AutoReplayStep2AfterProjectLoad = true;
        var path = Path.ChangeExtension(files.ExportPath, ".json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync());
        vm.ActiveAreaWidth = 30m;
        await vm.WaitForGridRebuildIdleAsync();
        var initial = vm.SimulationWorkspaceSourceRevision;
        var changes = new List<(object? Sender, EventArgs Args, int Revision, decimal Width)>();
        var aliases = new List<(object? Sender, int Revision)>();
        var duringBatch = new List<int>();
        var propertyNames = new List<string?>();
        vm.SimulationWorkspaceSourceChanged += (sender, args) =>
            changes.Add((sender, args, vm.SimulationWorkspaceSourceRevision, vm.ActiveAreaWidth));
        vm.WorkspaceDerivedSourceChanged += (sender, _) => aliases.Add((sender, vm.WorkspaceDerivedSourceRevision));
        vm.PropertyChanged += (_, change) =>
        {
            propertyNames.Add(change.PropertyName);
            if (change.PropertyName == nameof(vm.ActiveAreaWidth) && vm.ActiveAreaWidth == 20m)
            {
                vm.NotifySimulationWorkspaceSourceChanged();
                vm.NotifySimulationWorkspaceSourceChanged();
                duringBatch.Add(vm.SimulationWorkspaceSourceRevision);
            }
        };

        var loaded = await vm.LoadProjectFromPathAsync(path);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.True(loaded.IsLoaded);
        Assert.Equal(initial, Assert.Single(duringBatch));
        Assert.Equal((vm, EventArgs.Empty, initial + 1, 20m), Assert.Single(changes));
        Assert.Equal((vm, initial + 1), Assert.Single(aliases));
        Assert.DoesNotContain(nameof(vm.SimulationWorkspaceSourceRevision), propertyNames);
        Assert.DoesNotContain(nameof(vm.LastSimulationWorkspaceAttemptMessage), propertyNames);

        vm.NotifySimulationWorkspaceSourceChanged();

        Assert.Equal(initial + 2, vm.SimulationWorkspaceSourceRevision);
        Assert.Equal((vm, EventArgs.Empty, initial + 2, 20m), changes[1]);
        Assert.Equal((vm, initial + 2), aliases[1]);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RejectedGenerationLeavesTheAdmittedAttemptEmptyAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        vm.NullValue = 65536m;
        Assert.Null(await vm.CreateSimulationWorkspaceSessionAsync(true));
        Assert.NotEmpty(vm.LastSimulationWorkspaceAttemptMessage);
        vm.NullValue = 65535m;
        var mutations = 0;

        var stale = await vm.CreateSimulationWorkspaceSessionAsync(false, message =>
        {
            if (message.Contains("Finalize canonical exports", StringComparison.Ordinal) &&
                Interlocked.CompareExchange(ref mutations, 1, 0) == 0)
            {
                vm.NotifySimulationWorkspaceSourceChanged();
            }
        });

        Assert.Equal(1, mutations);
        Assert.Null(stale);
        Assert.Empty(vm.LastSimulationWorkspaceAttemptMessage);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ImmediateSourceChangesIncrementBeforeEachReentrantEvent()
    {
        var vm = new FreeformHelperViewModel();
        var initial = vm.SimulationWorkspaceSourceRevision;
        var changes = new List<(object? Sender, EventArgs Args, int Revision)>();
        vm.SimulationWorkspaceSourceChanged += (sender, args) =>
        {
            changes.Add((sender, args, vm.SimulationWorkspaceSourceRevision));
            if (changes.Count == 1)
            {
                vm.NotifySimulationWorkspaceSourceChanged();
            }
        };

        vm.NotifySimulationWorkspaceSourceChanged();

        Assert.Equal<(object?, EventArgs, int)>(
            [(vm, EventArgs.Empty, initial + 1), (vm, EventArgs.Empty, initial + 2)], changes);
        Assert.Equal(initial + 2, vm.WorkspaceDerivedSourceRevision);
    }

    private static async Task Sim7PrepareDuplicateCadAsync(FreeformHelperViewModel vm, string path)
    {
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.GridPaddingPercent = 0;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await vm.WaitForGridRebuildIdleAsync();
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        vm.EnableV21 = false;
        vm.EnableV22 = true;
    }

    private sealed class Sim7BuildTrace : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        // The queue synchronizes progress callbacks; the view model remains on the headless UI thread.
        private readonly ConcurrentQueue<string> _entries = new();

        public Sim7BuildTrace(FreeformHelperViewModel vm)
        {
            _vm = vm;
            vm.PropertyChanged += OnPropertyChanged;
        }

        public string[] Entries => _entries.ToArray();

        public void Report(string message) => Record("P:", message);

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs change)
        {
            if (change.PropertyName == nameof(_vm.StatusText))
            {
                Record("S:", _vm.StatusText);
            }
        }

        private void Record(string prefix, string message)
        {
            if (message is "Preparing simulation workspace..." or "Generating simulation notch table..." or
                "Resolving simulation diff grid..." or "Finalizing simulation workspace..." ||
                message.StartsWith("Simulation ready.", StringComparison.Ordinal) ||
                message.StartsWith("Simulation warning:", StringComparison.Ordinal) ||
                message.StartsWith("Simulation:", StringComparison.Ordinal) ||
                message.StartsWith("Simulation build failed:", StringComparison.Ordinal) ||
                message.StartsWith("Simulation workspace failed:", StringComparison.Ordinal))
            {
                _entries.Enqueue(prefix + message);
            }
        }

        public void Dispose() => _vm.PropertyChanged -= OnPropertyChanged;
    }
}
