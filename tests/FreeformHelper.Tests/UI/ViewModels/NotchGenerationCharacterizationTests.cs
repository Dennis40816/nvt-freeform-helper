// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Domain.Notch;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchGenerationCharacterizationTests
{
    private const string OperationLabel = "Characterization";
    private static readonly string[] _generationEffects =
    [
        "changing:busy", "changed:busy", "preparing:text", "preparing:callback",
        "dirty", "accepted:text", "changing:busy", "changed:busy",
    ];

    [AvaloniaFact]
    public async Task Generation_AcceptancePublishesTableBeforeCompletionTextAndBusyExitAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        var regularPads = vm.RegularPads;
        var selectedRegular = regularPads[0].Index;
        vm.ApplyCanvasSelection([], [selectedRegular]);
        await SaveAsync(vm, files);
        var status = vm.StatusText;
        var summary = vm.NotchExportSummary;
        var source = vm.SimulationWorkspaceSourceRevision;
        var effects = new List<(string Event, bool Busy, NotchTable? Table)>();
        ObserveGeneration(vm, effects);

        await GenerateAsync(vm, reportProgress: true, reporter: message =>
        {
            if (message == OperationLabel + ": preparing generation...")
                effects.Add(("preparing:callback", vm.IsNotchExporting, vm.NotchCacheForTests.LastGeneratedTable));
        });

        var table = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.NotEmpty(table.Rows);
        Assert.Equal(_generationEffects, effects.Select(item => item.Event));
        Assert.False(effects[0].Busy);
        Assert.All(effects.Skip(1).Take(6), item => Assert.True(item.Busy));
        Assert.False(effects[^1].Busy);
        Assert.All(effects.Take(5), item => Assert.Null(item.Table));
        Assert.All(effects.Skip(5), item => Assert.Same(table, item.Table));
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(summary, vm.NotchExportSummary);
        Assert.Equal(source, vm.SimulationWorkspaceSourceRevision);
        Assert.Same(regularPads, vm.RegularPads);
        Assert.Equal(selectedRegular, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.Empty(vm.NotchValidationItems);
        // KNOWN DEFECT: generating derived results marks an unchanged project dirty while capturing inputs.
        Assert.True(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("project")]
    [InlineData("grid")]
    [InlineData("cad")]
    public async Task Generation_OldCompletionCannotReplaceNewTableOrValidationAsync(string replacement)
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var vm = await CreateReadyAsync(files);
        var projectPath = await SaveAsync(vm, files);
        using var gate = new GenerationGate();
        var pending = GenerateAsync(vm, gate);
        try
        {
            await gate.WaitForEntryAsync(pending);
            await ReplaceSourceAsync(vm, dxf, projectPath, replacement);
            await GenerateAsync(vm);
            AnalyzeFirstGeneratedRegular(vm);
            var current = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
            var items = vm.NotchValidationItems;
            var status = vm.StatusText;
            var source = vm.SimulationWorkspaceSourceRevision;
            ResetEditState(vm);

            gate.Release();
            await pending;

            Assert.Same(current, vm.NotchCacheForTests.LastGeneratedTable);
            Assert.Same(items, vm.NotchValidationItems);
            Assert.NotEmpty(items);
            Assert.Equal(status, vm.StatusText);
            Assert.Equal(source, vm.SimulationWorkspaceSourceRevision);
            Assert.Equal(current.Rows.Count, vm.GetNotchExportGenerationCacheMetrics().EntryRowCount);
            Assert.False(vm.HasUnsavedChanges);
            Assert.False(vm.CanUndo);
        }
        finally
        {
            gate.Release();
            await pending;
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Generation_FailureOrCancellationRetainsAcceptedTableAndValidationAsync(bool cancel)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        AnalyzeFirstGeneratedRegular(vm);
        await SaveAsync(vm, files);
        ResetEditState(vm);
        var accepted = vm.NotchCacheForTests.LastGeneratedTable;
        var items = vm.NotchValidationItems;
        var status = vm.StatusText;
        var summary = vm.NotchValidationSummaryText;
        var progress = vm.NotchExportProgressText;
        Exception failure = cancel
            ? new OperationCanceledException("Synthetic generation cancellation.", TestContext.Current.CancellationToken)
            : new InvalidOperationException("Synthetic generation failure.");
        var reporter = new ThrowingProgress(failure);

        var thrown = await Record.ExceptionAsync(() => GenerateAsync(vm, reporter));

        Assert.Same(failure, thrown);
        Assert.Same(accepted, vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Same(items, vm.NotchValidationItems);
        Assert.Equal(summary, vm.NotchValidationSummaryText);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(progress, vm.NotchExportProgressText);
        Assert.False(vm.IsNotchExporting);
        // KNOWN DEFECT: input capture dirties the project even when generation fails or is canceled.
        Assert.True(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task Generation_PreparingReporterFailureLeavesBusySetAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        ResetEditState(vm);
        var accepted = vm.NotchCacheForTests.LastGeneratedTable;
        var status = vm.StatusText;
        var failure = new InvalidOperationException("Preparing reporter failed.");

        var thrown = await Record.ExceptionAsync(() => GenerateAsync(vm, reportProgress: true,
            reporter: _ => throw failure));

        Assert.Same(failure, thrown);
        // KNOWN DEFECT: preparation invokes the reporter before the try/finally, leaving the busy scope open.
        Assert.True(vm.IsNotchExporting);
        Assert.True(vm.ShouldShowNotchExportProgress);
        Assert.Equal(OperationLabel + ": preparing generation...", vm.NotchExportProgressText);
        Assert.Same(accepted, vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Equal(status, vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task Generation_SourceRevisionRejectsWarmRequestWithoutDiscardingAcceptedRowsAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        AnalyzeFirstGeneratedRegular(vm);
        var table = vm.NotchCacheForTests.LastGeneratedTable;
        var items = vm.NotchValidationItems;
        var status = vm.StatusText;
        using var gate = new GenerationGate();
        var pending = GenerateAsync(vm, gate);
        try
        {
            await gate.WaitForEntryAsync(pending);
            vm.NotifySimulationWorkspaceSourceChanged();
        }
        finally
        {
            gate.Release();
            await pending;
        }

        Assert.Same(table, vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Same(items, vm.NotchValidationItems);
        Assert.Equal(status, vm.StatusText);
        Assert.True(vm.TryGetNotchValidationReportForRegularPad(
            (int)vm.NotchValidationRegularPadId, out var report, out _, out _, out _));
        Assert.NotEmpty(report.DirectRows);
    }

    [AvaloniaFact]
    public async Task Generation_ResizedGridWithSamePadCountMissesThenReusesNewFingerprintAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await RunMatchAsync(vm);
        await GenerateAsync(vm);
        var before = vm.GetNotchExportGenerationCacheMetrics();
        var oldBounds = vm.RegularPads.Select(pad => pad.Bounds).ToArray();

        vm.ActiveAreaWidth = 25;
        await RunMatchAsync(vm);
        Assert.Equal(oldBounds.Length, vm.RegularPads.Count);
        Assert.False(oldBounds.SequenceEqual(vm.RegularPads.Select(pad => pad.Bounds)));
        Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
        await GenerateAsync(vm);

        var rebuilt = vm.GetNotchExportGenerationCacheMetrics();
        Assert.Equal(before.MissCount + 1, rebuilt.MissCount);
        Assert.Equal(before.StoreCount + 1, rebuilt.StoreCount);
        Assert.NotEmpty(Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable).Rows);
        await GenerateAsync(vm);
        var repeated = vm.GetNotchExportGenerationCacheMetrics();
        Assert.Equal(rebuilt.HitCount + 1, repeated.HitCount);
        Assert.Equal(rebuilt.MissCount, repeated.MissCount);
    }

    [AvaloniaFact]
    public async Task ProjectRoundTripPersistsInputsButRebuildsTableAndValidationOnDemandAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        vm.LinkNotchThresholds = false;
        vm.NotchThresholdPercent = 12m;
        vm.ToFullStrictOverlapPercent = 17m;
        await GenerateAsync(vm);
        AnalyzeFirstGeneratedRegular(vm);
        var expected = RowIdentities(vm);
        var projectPath = await SaveAsync(vm, files);
        var saved = JsonProjectStore.Load(projectPath);
        Assert.Equal(12.0, saved.Settings.Notch.ThresholdPercentV22);
        Assert.Equal(17.0, saved.Settings.Notch.MultiOwnerStrictOverlapPercent);

        Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(12m, vm.NotchThresholdPercent);
        Assert.Equal(17m, vm.ToFullStrictOverlapPercent);
        Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Empty(vm.NotchValidationItems);
        Assert.Equal("Notch export: not run.", vm.NotchExportSummary);
        Assert.False(vm.GetNotchExportGenerationCacheMetrics().HasEntry);
        Assert.False(vm.HasUnsavedChanges);
        await GenerateAsync(vm);
        Assert.Equal(expected, RowIdentities(vm));
        Assert.Empty(vm.NotchValidationItems);
        AnalyzeFirstGeneratedRegular(vm);
        Assert.NotEmpty(vm.NotchValidationItems);
        // KNOWN DEFECT: rebuilding derived results after load also sets the dirty flag.
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void RowPreviewCapsMatchesInTableOrderAndKeepsGlobalRowNumbers()
    {
        var vm = new FreeformHelperViewModel();
        var rows = Enumerable.Range(0, 40).Select(index => new NotchTableRow(
            NotchAlgorithmVersion.V22, 0, 100 + index, index % 2, 200 + index,
            Array.Empty<int>(), "synthetic row")).ToArray();
        vm.NotchCacheForTests.LastGeneratedTable = new NotchTable(rows);

        var preview = vm.GetLastNotchRowsForRegularPad(1);

        Assert.Equal(16, preview.Count);
        Assert.Equal(Enumerable.Range(1, 16).Select(index => index * 2), preview.Select(row => row.RowNumber));
        Assert.All(preview, row => Assert.Equal(1, row.RegularPadId));
        Assert.Equal(rows.Where(row => row.RegularPadIndex == 1).Take(16).Select(row => row.CadPadId),
            preview.Select(row => row.CadPadId));
        Assert.Empty(vm.GetLastNotchRowsForRegularPad(99));
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    internal static async Task<FreeformHelperViewModel> CreateReadyAsync(OperationProgressLifecycleTestScope files)
    {
        var vm = await files.CreateExportReadyAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        ResetEditState(vm);
        return vm;
    }

    internal static Task GenerateAsync(FreeformHelperViewModel vm, IProgress<NotchGenerationProgress>? progress = null,
        bool reportProgress = false, Action<string>? reporter = null)
    {
        // Reuse the existing typed seam; assertions observe public results, never the private result record.
        return (Task)FreeformHelperViewModelPrivateAccess.GenerateCurrentNotchTable(
            vm, Assert.IsType<CadPadSet>(FreeformHelperViewModelPrivateAccess.BuildFilteredCadPadSet(vm)),
            Assert.IsType<RegularGrid>(FreeformHelperViewModelPrivateAccess.Grid(vm)),
            OperationLabel, progress, null, reportProgress, true, reporter);
    }

    internal static void ResetEditState(FreeformHelperViewModel vm)
    {
        // Discard fixture-setup history without undoing the prepared geometry.
        while (FreeformHelperViewModelPrivateAccess.UndoService(vm).TryPop(out _))
        {
        }
        vm.HasUnsavedChanges = false;
        Assert.False(vm.CanUndo);
    }

    internal static void AnalyzeFirstGeneratedRegular(FreeformHelperViewModel vm)
    {
        var table = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.NotEmpty(table.Rows);
        vm.NotchValidationRegularPadId = table.Rows[0].RegularPadIndex;
        vm.AnalyzeNotchValidationCommand.Execute(null);
        Assert.NotEmpty(vm.NotchValidationItems);
    }

    internal static async Task<string> SaveAsync(FreeformHelperViewModel vm, OperationProgressLifecycleTestScope files)
    {
        var path = Path.ChangeExtension(files.ExportPath, ".json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(path);
        vm.ConfirmEmbedDxfAsync = () => Task.FromResult(true);
        Assert.True(await vm.SaveProjectAsync(), vm.StatusText);
        return path;
    }

    private static async Task ReplaceSourceAsync(FreeformHelperViewModel vm, DxfEditingBehaviorTestScope dxf,
        string path, string replacement)
    {
        var oldGrid = FreeformHelperViewModelPrivateAccess.Grid(vm);
        switch (replacement)
        {
            case "project":
                Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
                break;
            case "grid":
                vm.GridPaddingPercent = 5;
                break;
            case "cad":
                await dxf.OpenAsync(vm, ("replacement", 0, 0, 18, 10));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(replacement));
        }
        await RunMatchAsync(vm);
        Assert.NotSame(oldGrid, FreeformHelperViewModelPrivateAccess.Grid(vm));
    }

    private static async Task RunMatchAsync(FreeformHelperViewModel vm)
    {
        await vm.WaitForGridRebuildIdleAsync();
        await vm.MatchCommand.ExecuteAsync(null);
        await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
    }

    private static (NotchAlgorithmVersion Version, int Ic, int Diff, int Regular, int? Cad)[] RowIdentities(FreeformHelperViewModel vm)
    {
        var table = Assert.IsType<NotchTable>(vm.NotchCacheForTests.LastGeneratedTable);
        Assert.NotEmpty(table.Rows);
        return table.Rows.Select(row => (row.Version, row.IcIndex, row.DiffIndex, row.RegularPadIndex, row.CadPadId)).ToArray();
    }

    private static void ObserveGeneration(FreeformHelperViewModel vm,
        List<(string Event, bool Busy, NotchTable? Table)> effects)
    {
        vm.PropertyChanging += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.IsNotchExporting)) Record("changing:busy");
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.IsNotchExporting)) Record("changed:busy");
            if (args.PropertyName == nameof(vm.HasUnsavedChanges)) Record("dirty");
            if (args.PropertyName != nameof(vm.NotchExportProgressText)) return;
            if (vm.NotchExportProgressText == OperationLabel + ": preparing generation...") Record("preparing:text");
            if (vm.NotchExportProgressText.StartsWith(OperationLabel + ": generated ", StringComparison.Ordinal)) Record("accepted:text");
        };
        // Intermediate worker progress is separate; pin the exact acceptance effects and handler-visible table.
        void Record(string name) => effects.Add((name, vm.IsNotchExporting, vm.NotchCacheForTests.LastGeneratedTable));
    }

    private sealed class ThrowingProgress(Exception failure) : IProgress<NotchGenerationProgress>
    {
        public void Report(NotchGenerationProgress value) => throw failure;
    }

    private sealed class GenerationGate : IProgress<NotchGenerationProgress>, IDisposable
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

        public void Report(NotchGenerationProgress value)
        {
            if (value.Phase != "Finalize canonical exports") return;
            _entered.TrySetResult();
            // IProgress.Report is synchronous and runs on the generator worker, never on the waiting UI thread.
            TaskBlock.UntilComplete(_release.Task.WaitAsync(_cancellationToken));
        }

        public async Task WaitForEntryAsync(Task generation)
        {
            await Task.WhenAny(_entered.Task, generation).WaitAsync(_cancellationToken);
            if (generation.IsCompleted) await generation.WaitAsync(_cancellationToken);
            Assert.True(_entered.Task.IsCompletedSuccessfully, "Generation finished without reaching final projection.");
        }

        public void Release() => _release.TrySetResult();

        public void Dispose() => Release();
    }
}
