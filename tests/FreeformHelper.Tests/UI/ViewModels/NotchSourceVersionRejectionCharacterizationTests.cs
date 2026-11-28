// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchSourceVersionRejectionCharacterizationTests
{
    [AvaloniaFact]
    public async Task Generation_SourceNotificationAloneBeforeAcceptance_RejectsRowsAndAllowsCurrentRequestAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new NotchDataEditExportTestScope();
        var vm = await files.CreateAsync();
        var sourceRevision = vm.SimulationWorkspaceSourceRevision;
        var computationRevision = vm.GetNotchStep3Revision();
        var clears = vm.GetNotchExportGenerationCacheMetrics().ClearCount;
        var mutations = 0;

        var stale = await vm.CreateSimulationWorkspaceSessionAsync(
            silent: false,
            progressTextReporter: message =>
            {
                if (message.Contains("Finalize canonical exports", StringComparison.Ordinal) &&
                    Interlocked.CompareExchange(ref mutations, 1, 0) == 0)
                {
                    // This existing seam changes only the source version, without clearing or changing settings.
                    vm.NotifySimulationWorkspaceSourceChanged();
                }
            });

        Assert.Equal(1, mutations);
        Assert.Equal(sourceRevision + 1, vm.SimulationWorkspaceSourceRevision);
        Assert.Equal(computationRevision, vm.GetNotchStep3Revision());
        Assert.Equal(clears, vm.GetNotchExportGenerationCacheMetrics().ClearCount);
        Assert.Null(stale);
        Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Empty(vm.RegularPads.SelectMany(pad => vm.GetLastNotchRowsForRegularPad(pad.RegularPadId)));
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.DoesNotContain("Simulation ready", vm.LastSimulationWorkspaceAttemptMessage, StringComparison.OrdinalIgnoreCase);

        var current = await vm.CreateSimulationWorkspaceSessionAsync(silent: false);

        Assert.NotNull(current);
        Assert.Equal(vm.SimulationWorkspaceSourceRevision, current.SourceRevision);
        Assert.Equal(2, current.Table.Rows.Count);
    }
}
