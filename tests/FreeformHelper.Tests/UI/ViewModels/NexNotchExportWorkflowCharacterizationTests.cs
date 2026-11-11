// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.ComponentModel;
using FreeformHelper.Application.Export;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NexNotchExportWorkflowCharacterizationTests
{
    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportMissingGridReportsPrerequisiteWithoutEnteringBusyAsync()
    {
        using var files = new NexExportFiles();
        var vm = files.CreateEmpty();
        Assert.False(vm.GetWorkflowStateSnapshot().HasGrid);

        await AssertMissingInputAsync(vm, files.ExportPath);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportGridWithoutCadReportsPrerequisiteWithoutEnteringBusyAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateWithoutCadAsync();
        Assert.True(vm.GetWorkflowStateSnapshot().HasGrid);

        await AssertMissingInputAsync(vm, files.ExportPath);
    }

    private static async Task AssertMissingInputAsync(FreeformHelperViewModel vm, string exportPath)
    {
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("Export notch: import DXF and build grid first.", vm.StatusText);
        Assert.Equal("Idle.", vm.NotchExportProgressText);
        Assert.Equal(0.0, vm.NotchExportProgress);
        Assert.Empty(changes.Busy);
        Assert.False(vm.IsNotchExporting);
        Assert.False(File.Exists(exportPath));
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportMissingPickerReportsBlockedWithoutEnteringBusyAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("Export notch: dialog handler not wired.", vm.StatusText);
        Assert.Equal("C v2.2 export blocked: save dialog handler not wired.", vm.NotchExportSummary);
        Assert.Equal(vm.NotchExportSummary, vm.NotchExportProgressText);
        Assert.Empty(changes.Busy);
        Assert.False(vm.IsNotchExporting);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportWorkflowBlockedReleasesBusyBeforeReturningAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        vm.ClearStep1ResultCommand.Execute(null);
        vm.PickSaveNotchPathAsync = (_, _) => throw new InvalidOperationException("Picker must not open.");
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("Step 5: run Step 1 (Match) first. Step 4 diagnostics is optional.", vm.StatusText);
        Assert.Equal($"C v2.2 export blocked at stage 1: {vm.StatusText}", vm.NotchExportSummary);
        Assert.Equal(0.2, vm.NotchExportProgress);
        changes.AssertReleased();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportCancelSaveDialogReportsCancellationAndWritesNothingAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        vm.PickSaveNotchPathAsync = (_, _) =>
        {
            Assert.True(vm.IsNotchExporting);
            return Task.FromResult<string?>(null);
        };
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal("C v2.2 export canceled at stage 4 (save path not selected).", vm.StatusText);
        Assert.Equal(vm.StatusText, vm.NotchExportSummary);
        Assert.Equal(vm.StatusText, vm.NotchExportProgressText);
        Assert.Equal(0.8, vm.NotchExportProgress);
        Assert.False(File.Exists(files.ExportPath));
        changes.AssertReleased();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportAcceptCsvSelectionReportsStagesInOrderAndWritesSerializerOutputAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        NotchTable? selectedTable = null;
        vm.OpenNotchExportSelectionAsync = selection =>
        {
            selection.SelectedExportTypeOption = Assert.Single(selection.ExportTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Csv);
            var table = selection.BuildSelectedTable();
            Assert.NotEmpty(table.Rows);
            selectedTable = table;
            return Task.FromResult<NotchTable?>(table);
        };
        vm.PickSaveNotchPathAsync = (name, extension) =>
        {
            Assert.True(vm.IsNotchExporting);
            Assert.Equal("notch_table", name);
            Assert.Equal("csv", extension);
            return Task.FromResult<string?>(files.ExportPath);
        };
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        var table = Assert.IsType<NotchTable>(selectedTable);
        var payload = await TestFiles.ReadContentAsync(files.ExportPath, TestContext.Current.CancellationToken);
        Assert.Equal(NotchTableExporter.ExportAsCsv(table), payload);
        string[] stages =
        [
            "C v2.2 export [1/5]: Checking workflow prerequisites",
            "C v2.2 export [2/5]: Generating notch table",
            "C v2.2 export [3/5]: Waiting row selection dialog",
            "CSV review export [4/5]: Waiting save path dialog...",
            $"CSV review export [5/5]: Writing file to '{files.ExportPath}'"
        ];
        Assert.Equal(stages, changes.Status.Where(message => stages.Contains(message, StringComparer.Ordinal)));
        Assert.Equal($"CSV review notch exported. Rows={table.Rows.Count}.", vm.StatusText);
        Assert.Equal($"CSV review exported: rows={table.Rows.Count}.", vm.NotchExportSummary);
        Assert.Equal(vm.NotchExportSummary, vm.NotchExportProgressText);
        Assert.Equal(1.0, vm.NotchExportProgress);
        changes.AssertReleased();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportWriterRejectsMissingDirectoryReportsFailureAndReleasesBusyAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        var path = files.PathFor("missing", "notch.c");
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(path);
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.Equal($"C v2.2 export failed: Could not find a part of the path '{path}'.", vm.StatusText);
        Assert.Equal(vm.StatusText, vm.NotchExportSummary);
        Assert.Equal(vm.StatusText, vm.NotchExportProgressText);
        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.False(File.Exists(path));
        changes.AssertReleased();
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ExportSerializerRejectsSelectedTableReportsFailureAndReleasesBusyAsync()
    {
        using var files = new NexExportFiles();
        var vm = await files.CreateReadyAsync();
        vm.OpenNotchExportSelectionAsync = _ => Task.FromResult<NotchTable?>(new NotchTable(
            [new NotchTableRow(NotchAlgorithmVersion.V21, 0, 0, 0, null, [], "Invalid selected row")]));
        vm.PickSaveNotchPathAsync = (_, _) => Task.FromResult<string?>(files.ExportPath);
        using var changes = new NexExportChanges(vm);

        await vm.ExportNotchCommand.ExecuteAsync(null);

        Assert.StartsWith("C v2.2 export failed: V21 row must have at least 9 values", vm.StatusText, StringComparison.Ordinal);
        Assert.Equal(vm.StatusText, vm.NotchExportSummary);
        Assert.Equal(vm.StatusText, vm.NotchExportProgressText);
        Assert.Equal(1.0, vm.NotchExportProgress);
        Assert.False(File.Exists(files.ExportPath));
        changes.AssertReleased();
    }

    private sealed class NexExportFiles : IDisposable
    {
        private readonly DirectoryInfo _directory = TestFiles.CreateTempDirectory();

        public string ExportPath => PathFor("notch.csv");

        public string PathFor(params string[] parts) => Path.Combine([_directory.FullName, .. parts]);

        // The module's TestAppSettingsIsolation also protects any default store created by the UI.
        public FreeformHelperViewModel CreateEmpty() => new(
            new FreeformHelper.UI.Services.AppGeneralSettingsStore(PathFor("app-settings.json")));

        public async Task<FreeformHelperViewModel> CreateWithoutCadAsync()
        {
            var path = PathFor("project.json");
            await new JsonProjectStore().SaveAsync(path, new ProjectFile(), TestContext.Current.CancellationToken);
            var vm = CreateEmpty();
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(path);
            await vm.LoadProjectCommand.ExecuteAsync(null);
            Assert.NotEmpty(vm.RegularPads);
            Assert.Empty(vm.CadPads);
            return vm;
        }

        public async Task<FreeformHelperViewModel> CreateReadyAsync()
        {
            var vm = await CreateWithoutCadAsync();
            vm.ActiveAreaWidth = 20;
            vm.ActiveAreaHeight = 10;
            vm.GridPaddingPercent = 0;
            var cascade = Assert.Single(vm.CascadeIcSettings);
            cascade.XChannels = 2;
            cascade.YChannels = 1;
            await FreeformHelperViewModelTests.WaitForGridRebuildAsync(vm);
            var path = PathFor("signal.dxf");
            TestFiles.WriteText(path, string.Join(Environment.NewLine,
                "0", "SECTION", "2", "ENTITIES", "0", "LWPOLYLINE", "8", "signal", "70", "1", "90", "4",
                "10", "0", "20", "0", "10", "20", "20", "0", "10", "20", "20", "10", "10", "0", "20", "10",
                "0", "ENDSEC", "0", "EOF", string.Empty));
            vm.PickOpenDxfPathAsync = () => Task.FromResult<string?>(path);
            await vm.OpenDxfCommand.ExecuteAsync(null);
            Assert.Single(vm.CadPads);
            Assert.Equal(2, vm.RegularPads.Count);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
            vm.EnableV21 = false;
            vm.EnableV22 = true;
            vm.SelectedNotchExportFileTypeOption = Assert.Single(vm.NotchExportFileTypeOptions,
                option => option.Value == FreeformHelperViewModel.NotchExportFileType.Cv22);
            return vm;
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }

    private sealed class NexExportChanges : IDisposable
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly ConcurrentQueue<bool> _busy = new();
        private readonly ConcurrentQueue<string> _status = new();

        public NexExportChanges(FreeformHelperViewModel vm)
        {
            _vm = vm;
            _vm.PropertyChanged += OnChanged;
        }

        public bool[] Busy => _busy.ToArray();
        public string[] Status => _status.ToArray();

        public void AssertReleased()
        {
            Assert.Equal<bool>([true, false], Busy);
            Assert.False(_vm.IsNotchExporting);
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(FreeformHelperViewModel.IsNotchExporting))
            {
                _busy.Enqueue(_vm.IsNotchExporting);
            }

            if (args.PropertyName == nameof(FreeformHelperViewModel.StatusText))
            {
                _status.Enqueue(_vm.StatusText);
            }
        }

        public void Dispose() => _vm.PropertyChanged -= OnChanged;
    }
}
