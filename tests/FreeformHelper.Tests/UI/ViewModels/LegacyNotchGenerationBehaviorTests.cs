// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class LegacyNotchGenerationBehaviorTests
{
    [AvaloniaFact]
    public async Task Simulation_LegacyRepeatedRequest_ReusesGeneratedRowsAndKeepsPayload()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();

        var first = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);
        Assert.NotNull(first);
        Assert.Equal(2, first.Table.Rows.Count);
        var afterFirst = vm.GetNotchExportGenerationCacheMetrics();

        var second = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.NotNull(second);
        Assert.Equal(first.SourceRevision, second.SourceRevision);
        Assert.Equal(first.Table.Rows.Count, second.Table.Rows.Count);
        foreach (var (expected, actual) in first.Table.Rows.Zip(second.Table.Rows))
        {
            Assert.Equal(NotchAlgorithmVersion.V22, actual.Version);
            Assert.Equal(expected.IcIndex, actual.IcIndex);
            Assert.Equal(expected.DiffIndex, actual.DiffIndex);
            Assert.Equal(expected.RegularPadIndex, actual.RegularPadIndex);
            Assert.Equal(expected.CadPadId, actual.CadPadId);
            Assert.Equal(expected.Values, actual.Values);
        }

        var afterSecond = vm.GetNotchExportGenerationCacheMetrics();
        Assert.True(afterSecond.HasEntry);
        Assert.Equal(afterFirst.HitCount + 1, afterSecond.HitCount);
        Assert.Equal(afterFirst.MissCount, afterSecond.MissCount);
        Assert.Equal(afterFirst.StoreCount, afterSecond.StoreCount);
        Assert.Equal("Simulation ready. Rows=2.", vm.LastSimulationWorkspaceAttemptMessage);
    }

    [AvaloniaFact]
    public async Task Simulation_LegacyWithoutFreeforms_ReportsEmptyScanAndDoesNotCacheEmptyTable()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        vm.ClearStep2ResultCommand.Execute(null);
        Assert.All(vm.RegularPads, pad => Assert.Equal(FreeformType.None, pad.Freeform));
        var before = vm.GetNotchExportGenerationCacheMetrics();
        var emptyScan = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = await vm.CreateSimulationWorkspaceSessionAsync(silent: false, progressTextReporter: message =>
        {
            if (message.Contains(": Scan CAD ", StringComparison.Ordinal))
            {
                emptyScan.TrySetResult(message);
            }
        });
        var progressText = await emptyScan.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var second = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal("Simulation workspace: Scan CAD 0/0, rows=0", progressText);
        Assert.Equal("Simulation: no notch rows generated under current settings.", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.False(vm.IsNotchExporting);
        var after = vm.GetNotchExportGenerationCacheMetrics();
        Assert.False(after.HasEntry);
        Assert.Equal(before.MissCount + 2, after.MissCount);
        Assert.Equal(before.HitCount, after.HitCount);
        Assert.Equal(before.StoreCount, after.StoreCount);
    }

    [AvaloniaFact]
    public async Task Simulation_LegacyThresholdLoweredAfterEmptyResult_GeneratesBothRegularRows()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        vm.NotchThresholdPercent = 100m;

        var rejected = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.Null(rejected);
        Assert.Equal("Simulation: no notch rows generated under current settings.", vm.LastSimulationWorkspaceAttemptMessage);
        var afterRejected = vm.GetNotchExportGenerationCacheMetrics();
        Assert.False(afterRejected.HasEntry);
        vm.NotchThresholdPercent = 0m;

        var admitted = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.NotNull(admitted);
        Assert.Equal(vm.RegularPads.Select(pad => pad.RegularPadId).Order(),
            admitted.Table.Rows.Select(row => row.RegularPadIndex).Order());
        var cad = Assert.Single(vm.CadPads);
        Assert.All(admitted.Table.Rows, row =>
        {
            Assert.Equal(NotchAlgorithmVersion.V22, row.Version);
            Assert.Equal(cad.Id, row.CadPadId);
            Assert.Equal(9, row.Values.Length);
            Assert.Equal(200, row.Values[1]);
        });
        var afterAdmitted = vm.GetNotchExportGenerationCacheMetrics();
        Assert.True(afterAdmitted.HasEntry);
        Assert.Equal(afterRejected.MissCount + 1, afterAdmitted.MissCount);
        Assert.Equal(afterRejected.StoreCount + 1, afterAdmitted.StoreCount);
        Assert.Equal(afterRejected.HitCount, afterAdmitted.HitCount);
        Assert.Equal("Simulation ready. Rows=2.", vm.LastSimulationWorkspaceAttemptMessage);
    }

    [AvaloniaFact]
    public async Task Export_LegacyWithoutFreeforms_ReportsZeroCandidatesAndStopsBeforeDialogs()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        vm.ClearStep2ResultCommand.Execute(null);
        var summaries = new ConcurrentQueue<string>();
        var emptyScan = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.NotchExportSummary))
            {
                var message = vm.NotchExportSummary;
                summaries.Enqueue(message);
                if (message.Contains(": Scan CAD ", StringComparison.Ordinal))
                {
                    emptyScan.TrySetResult(message);
                }
            }
        };
        var reviewCalls = 0;
        var saveCalls = 0;
        vm.OpenNotchExportSelectionAsync = _ =>
        {
            reviewCalls++;
            return Task.FromResult<NotchTable?>(null);
        };
        vm.PickSaveNotchPathAsync = (_, _) =>
        {
            saveCalls++;
            return Task.FromResult<string?>(null);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);
        var progressText = await emptyScan.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.Equal("C v2.2 export [2/5]: Scan CAD 0/0, built rows=0", progressText);
        Assert.Contains("C v2.2 export stopped at stage 2: no rows generated.", summaries);
        Assert.Equal(0, reviewCalls);
        Assert.Equal(0, saveCalls);
        Assert.False(vm.IsNotchExporting);
        Assert.False(vm.GetNotchExportGenerationCacheMetrics().HasEntry);
    }
}
