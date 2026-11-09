// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchExportHelperBehaviorTests
{
    [AvaloniaFact]
    public async Task ExportCsv_ExplicitV21Scope_ExportsOnlyV21AndPersistsScopeOnSaveAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await files.CreateExportReadyAsync();
        vm.EnableV21 = true;
        vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
            option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
        var csvPath = Path.ChangeExtension(files.ExportPath, ".csv");
        var projectPath = Path.ChangeExtension(files.ExportPath, ".project.json");
        var suggestedName = string.Empty;
        var suggestedExtension = string.Empty;
        vm.PickSaveNotchPathAsync = (name, extension) =>
        {
            suggestedName = name;
            suggestedExtension = extension;
            return Task.FromResult<string?>(csvPath);
        };
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedVersionOption = Assert.Single(selection.VersionOptions,
                option => option.Value == NotchAlgorithmVersion.V21);
            var table = selection.BuildSelectedTable();
            Assert.NotEmpty(table.Rows);
            Assert.All(table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V21, row.Version));
            return Task.FromResult<NotchTable?>(table);
        };

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("notch_table", suggestedName);
        Assert.Equal("csv", suggestedExtension);
        Assert.StartsWith("CSV review exported: rows=", vm.NotchExportSummary, StringComparison.Ordinal);
        Assert.True(vm.EnableV21);
        Assert.False(vm.EnableV22);
        var payload = await TestFiles.ReadContentAsync(csvPath, TestContext.Current.CancellationToken);
        var rows = payload.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal("2.1", row.Split(',')[1]));
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(projectPath);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(false);

        Assert.True(await vm.SaveProjectAsync());

        var saved = JsonProjectStore.Load(projectPath);
        Assert.Equal("Csv", saved.UiSnapshot.View.NotchExportFileType);
        Assert.Equal(NotchAlgorithmVersion.V21, Assert.Single(saved.Settings.Notch.EnabledVersions));
    }

    [AvaloniaFact]
    public async Task Simulation_LegacyIndependentV22Threshold_AdmitsRowsUntilThresholdsAreLinkedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new LegacyNotchGenerationTestScope();
        var vm = await files.CreateAsync();
        vm.LinkNotchThresholds = false;
        vm.NotchThresholdPercent = 0m;
        vm.NotchThresholdQ7 = 128m;

        var independent = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.NotNull(independent);
        Assert.Equal(2, independent.Table.Rows.Count);
        Assert.All(independent.Table.Rows, row => Assert.Equal(NotchAlgorithmVersion.V22, row.Version));
        var beforeLink = vm.GetNotchExportGenerationCacheMetrics();

        vm.LinkNotchThresholds = true;
        var linked = await vm.CreateSimulationWorkspaceSessionAsync(silent: true);

        Assert.Null(linked);
        Assert.Equal(100m, vm.NotchThresholdPercent);
        Assert.Equal("Simulation: no notch rows generated under current settings.", vm.LastSimulationWorkspaceAttemptMessage);
        var afterLink = vm.GetNotchExportGenerationCacheMetrics();
        Assert.Equal(beforeLink.MissCount + 1, afterLink.MissCount);
        Assert.Equal(beforeLink.HitCount, afterLink.HitCount);
    }
}
