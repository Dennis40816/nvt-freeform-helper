// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class SimulationWorkspaceDuplicateWarningTests
{
    [AvaloniaFact]
    public async Task DuplicateCadOutputDiffKeepsTheWorkspaceAndWarnsForVisibleAndSilentBuildsAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateWithoutDxfAsync();
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 0, 0, 14, 10));
        vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
        vm.ActiveAreaWidth = 20;
        vm.ActiveAreaHeight = 10;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        vm.GridPaddingPercent = 0;
        vm.CadOutputFwDiffAutoMode = CadOutputFwDiffAutoMode.BestMatchDirect;
        var cascade = Assert.Single(vm.CascadeIcSettings);
        cascade.XChannels = 2;
        cascade.YChannels = 1;
        await WaitForGridRebuildAsync(vm);
        await vm.OpenDxfCommand.ExecuteAsync(null);
        await WaitForGridRebuildAsync(vm);
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
        vm.EnableV21 = false;
        vm.EnableV22 = true;
        var duplicate = Assert.Single(vm.Step4DuplicateDiffGroups);
        Assert.Equal(2, duplicate.CadPadIds.Count);
        var warning = $"Simulation warning: duplicate CAD output FW diff at IC{duplicate.IcIndex + 1}/diff{duplicate.DiffIndex}; " +
            "results may include ambiguous mappings.";
        var messages = new List<string>();

        var session = await vm.CreateSimulationWorkspaceSessionAsync(silent: false, messages.Add);

        Assert.NotNull(session);
        Assert.NotEmpty(session.Table.Rows);
        var ready = $"Simulation ready. Rows={session.Table.Rows.Count}.";
        Assert.Equal($"{ready} {warning}", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(ready, vm.StatusText);
        Assert.Contains(warning, messages);
        Assert.Contains(ready, messages);

        vm.HighlightCadPadGroup(duplicate.CadPadIds, "Duplicate mapping");
        var status = vm.StatusText;
        messages.Clear();

        var prewarmed = await vm.CreateSimulationWorkspaceSessionAsync(silent: true, messages.Add);

        Assert.NotNull(prewarmed);
        Assert.Equal(session.Table.Rows.Count, prewarmed.Table.Rows.Count);
        Assert.Equal($"{ready} {warning}", vm.LastSimulationWorkspaceAttemptMessage);
        Assert.Equal(status, vm.StatusText);
        Assert.Contains(warning, messages);
        Assert.Contains(ready, messages);
    }
}
