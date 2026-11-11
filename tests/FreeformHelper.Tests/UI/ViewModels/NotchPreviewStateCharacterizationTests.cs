// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Services;
using FreeformHelper.Application.Settings;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using Nvt.Core.TestSupport;
using Xunit;
using static FreeformHelper.Tests.NotchGenerationCharacterizationTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchPreviewStateCharacterizationTests
{
    private static readonly string[] _validationEffects =
    [
        "changing:merged", "changed:merged", "changing:direct", "changed:direct", "summary", "status",
    ];

    [AvaloniaFact]
    public async Task Validation_HandlersSeeOldGroupsUntilEachReplacementThenFinalCountsAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        ResetEditState(vm);
        var oldItems = vm.NotchValidationItems;
        var observations = new List<(string Event, int All, int Direct)>();
        ObserveValidation(vm, observations);

        AnalyzeFirstGeneratedRegular(vm);

        Assert.Equal(_validationEffects, observations.Select(item => item.Event));
        Assert.Equal(0, observations[0].All);
        Assert.Equal(0, observations[1].Direct);
        Assert.Equal(0, observations[2].Direct);
        Assert.True(observations[1].All > 0);
        Assert.True(observations[3].Direct > 0);
        Assert.All(observations.Skip(3), item => Assert.Equal(vm.NotchValidationDirectCount, item.Direct));
        Assert.NotSame(oldItems, vm.NotchValidationItems);
        Assert.Empty(oldItems);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("analyze")]
    [InlineData("reject")]
    [InlineData("clear")]
    public async Task Validation_ActionsKeepAcceptedTableSelectionAndEditHistoryAsync(string action)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        AnalyzeFirstGeneratedRegular(vm);
        vm.ApplyCanvasSelection([], [vm.RegularPads[0].Index]);
        ResetEditState(vm);
        var table = vm.NotchCacheForTests.LastGeneratedTable;
        var oldItems = vm.NotchValidationItems;
        var status = vm.StatusText;

        PerformValidationAction(vm, action);

        Assert.Same(table, vm.NotchCacheForTests.LastGeneratedTable);
        Assert.NotSame(oldItems, vm.NotchValidationItems);
        Assert.Equal(vm.RegularPads[0].Index, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        if (action == "analyze") Assert.Equal(oldItems, vm.NotchValidationItems);
        else Assert.Empty(vm.NotchValidationItems);
        if (action == "clear") Assert.Equal(status, vm.StatusText);
        if (action == "reject") Assert.Equal("Validation: REG 999 not found.", vm.StatusText);
        Assert.NotEmpty(vm.GetLastNotchRowsForRegularPad(oldItems[0].SourceRegularPadId));
    }

    [AvaloniaTheory]
    [InlineData(0.5, 0)]
    [InlineData(1.5, 2)]
    [InlineData(-0.6, 0)]
    public async Task Validation_TargetRoundsToEvenAndClampsNegativeIdsAsync(double input, int target)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        ResetEditState(vm);
        vm.NotchValidationRegularPadId = (decimal)input;

        vm.AnalyzeNotchValidationCommand.Execute(null);

        Assert.Equal((decimal)input, vm.NotchValidationRegularPadId);
        if (vm.RegularPads.Any(pad => pad.RegularPadId == target))
        {
            Assert.StartsWith($"REG {target} |", vm.NotchValidationSummaryText, StringComparison.Ordinal);
            Assert.All(vm.NotchValidationDirectItems, item => Assert.Equal(target, item.SourceRegularPadId));
        }
        else
        {
            Assert.Equal($"Validation: REG {target} not found.", vm.NotchValidationSummaryText);
            Assert.Empty(vm.NotchValidationItems);
        }
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public async Task Detail_OldDialogCompletionAfterProjectLoadLeavesNewWorkspaceUntouchedAsync()
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        var projectPath = await SaveAsync(vm, files);
        vm.ApplyCanvasSelection([vm.CadPads[0].Id], []);
        NotchDetailViewModel? captured = null;
        var close = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.OpenNotchDetailAsync = detail =>
        {
            captured = detail;
            return close.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var pending = vm.ShowNotchDetailCommand.ExecuteAsync(null);
        try
        {
            var detail = Assert.IsType<NotchDetailViewModel>(captured);
            Assert.False(pending.IsCompleted);
            Assert.True((await vm.LoadProjectFromPathAsync(projectPath)).IsLoaded);
            await vm.WaitForGridRebuildIdleAsync();
            var status = vm.StatusText;
            var source = vm.SimulationWorkspaceSourceRevision;
            close.SetResult();
            await pending;

            Assert.NotSame(detail.CadPad, Assert.Single(vm.CadPads));
            Assert.Equal(status, vm.StatusText);
            Assert.Equal(source, vm.SimulationWorkspaceSourceRevision);
            Assert.Empty(vm.SelectedCadPadIds);
            Assert.Empty(vm.NotchCanvasPreviewItems);
            Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
            Assert.False(vm.HasUnsavedChanges);
        }
        finally
        {
            close.TrySetResult();
            await pending;
        }
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Detail_DialogFailureOrCancellationKeepsAcceptedResultsAsync(bool cancel)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = await CreateReadyAsync(files);
        await GenerateAsync(vm);
        AnalyzeFirstGeneratedRegular(vm);
        vm.ApplyCanvasSelection([vm.CadPads[0].Id], []);
        ResetEditState(vm);
        var table = vm.NotchCacheForTests.LastGeneratedTable;
        var preview = vm.NotchCanvasPreviewItems;
        var validation = vm.NotchValidationItems;
        var status = vm.StatusText;
        Exception failure = cancel
            ? new OperationCanceledException("Detail canceled.", TestContext.Current.CancellationToken)
            : new InvalidOperationException("Detail failed.");
        vm.OpenNotchDetailAsync = _ => Task.FromException(failure);

        var thrown = await Record.ExceptionAsync(() => vm.ShowNotchDetailCommand.ExecuteAsync(null));

        Assert.Same(failure, thrown);
        Assert.Same(table, vm.NotchCacheForTests.LastGeneratedTable);
        Assert.Same(preview, vm.NotchCanvasPreviewItems);
        Assert.Same(validation, vm.NotchValidationItems);
        Assert.Equal(status, vm.StatusText);
        Assert.False(vm.IsNotchExporting);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaTheory]
    [InlineData("handler", "Notch detail: dialog handler not wired.")]
    [InlineData("source", "Notch detail: import DXF and build grid first.")]
    [InlineData("selection", "Notch detail: select a CAD pad first.")]
    public async Task Detail_RejectedRequestLeavesHistoryAndSelectionUnchangedAsync(string missing, string expectedStatus)
    {
        using var files = new OperationProgressLifecycleTestScope();
        var vm = missing == "source" ? await files.CreateWithoutDxfAsync() : await CreateReadyAsync(files);
        var calls = 0;
        if (missing != "handler") vm.OpenNotchDetailAsync = _ =>
        {
            calls++;
            return Task.CompletedTask;
        };
        ResetEditState(vm);

        await vm.ShowNotchDetailCommand.ExecuteAsync(null);

        Assert.Equal(expectedStatus, vm.StatusText);
        Assert.Equal(0, calls);
        Assert.Empty(vm.SelectedCadPadIds);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        Assert.Null(vm.NotchCacheForTests.LastGeneratedTable);
    }

    [AvaloniaFact]
    public void PreviewCollection_ChangingSeesOldDataAndVisibilityPrecedesChangedWithNewData()
    {
        var vm = new FreeformHelperViewModel { NotchPreviewAutoPlayEnabled = false, EnableToFull = true };
        var polygon = TestGeometryFactory.CreateRect(0, 0, 10, 10);
        var replacement = new ObservableCollection<NotchCanvasPreviewItem>
        {
            new(101, 1, 1, true, [polygon], [polygon], [polygon], [polygon]),
        };
        ResetEditState(vm);
        var old = vm.NotchCanvasPreviewItems;
        var observations = new List<(string Event, ObservableCollection<NotchCanvasPreviewItem> Items, bool Visible)>();
        vm.PropertyChanging += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.NotchCanvasPreviewItems)) Record("changing");
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.IsNotchCanvasPreviewVisible)) Record("visibility");
            if (args.PropertyName == nameof(vm.NotchCanvasPreviewItems)) Record("changed");
        };

        vm.NotchCanvasPreviewItems = replacement;

        Assert.Equal("changing", observations[0].Event);
        Assert.Same(old, observations[0].Items);
        Assert.False(observations[0].Visible);
        Assert.Equal("changed", observations[^1].Event);
        Assert.Contains(observations.Skip(1).SkipLast(1), item => item.Event == "visibility");
        Assert.All(observations.Skip(1), item => { Assert.Same(replacement, item.Items); Assert.True(item.Visible); });
        observations.Clear();
        vm.NotchCanvasPreviewItems = replacement;
        Assert.Empty(observations);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        // Playback can also publish visibility. Only its order relative to collection replacement matters.
        void Record(string name) => observations.Add((name, vm.NotchCanvasPreviewItems, vm.IsNotchCanvasPreviewVisible));
    }

    [AvaloniaTheory]
    [InlineData("project")]
    [InlineData("grid")]
    [InlineData("cad")]
    public async Task Preview_DeferredOldSelectionCannotReviveAfterSourceReplacementAsync(string replacement)
    {
        using var files = new OperationProgressLifecycleTestScope();
        using var dxf = new DxfEditingBehaviorTestScope();
        var clock = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var vm = await CreatePreviewAsync(files, dxf, clock);
        var path = await SaveAsync(vm, files);
        var ids = vm.CadPads.Select(pad => pad.Id).ToArray();
        vm.ApplyCanvasSelection([ids[0]], []);
        Assert.NotEmpty(vm.NotchCanvasPreviewItems);
        vm.ApplyCanvasSelection(ids, []);
        await HeadlessUiPump.FlushQueueAsync();

        await ReplacePreviewSourceAsync(vm, dxf, path, replacement);
        var preview = vm.NotchCanvasPreviewItems;
        var status = vm.StatusText;
        var revision = vm.GetNotchStep3Revision();
        clock.Advance(TimeSpan.FromSeconds(1));
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Empty(vm.SelectedCadPadIds);
        Assert.Empty(preview);
        Assert.Same(preview, vm.NotchCanvasPreviewItems);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(revision, vm.GetNotchStep3Revision());
        Assert.False(vm.IsNotchCanvasPreviewVisible);
    }

    [AvaloniaFact]
    public async Task RealProject_SelectedCadDetailAgreesWithResolvedCompensationWithoutDirtyingProjectAsync()
    {
        Assert.SkipWhen(!ExampleData.IsAvailable && !ExampleData.IsRequired, ExampleData.MissingMessage);
        using var example = ExampleProjectFixture.Create(Path.Combine(ExampleData.Root, "BOE36.35", "project_3635.json"));
        using var files = new LoadingModeTestScope();
        var vm = files.CreateViewModel(example.ProjectPath);
        Assert.True((await vm.LoadProjectFromPathAsync(example.ProjectPath)).IsLoaded);
        await vm.WaitForGridRebuildIdleAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        var cad = vm.CadPads.First(pad => vm.GetMatchedRegularPadIds(pad.Id).Count > 0);
        vm.ApplyCanvasSelection([cad.Id], []);
        ResetEditState(vm);
        var resolved = Assert.IsType<NotchV22ResolvedResult>(vm.GetCadV22ResolvedResult(cad.Id));
        NotchDetailViewModel? captured = null;
        vm.OpenNotchDetailAsync = detail => { captured = detail; return Task.CompletedTask; };
        var status = vm.StatusText;

        await vm.ShowNotchDetailCommand.ExecuteAsync(null);

        var detail = Assert.IsType<NotchDetailViewModel>(captured);
        Assert.Equal(cad.Id, detail.CadPad.Id);
        Assert.NotEmpty(detail.Allocations);
        Assert.Equal(resolved.Compensation.CombinedRatio, detail.CombinedRatio);
        Assert.Equal(resolved.Stage2CandidatePolygons, detail.ToFullPolygons);
        Assert.Equal(status, vm.StatusText);
        Assert.False(vm.HasUnsavedChanges);
        Assert.False(vm.CanUndo);
        vm.ApplyCanvasSelection([], []);
    }

    private static void PerformValidationAction(FreeformHelperViewModel vm, string action)
    {
        if (action == "clear") vm.ClearNotchValidationCommand.Execute(null);
        else
        {
            if (action == "reject") vm.NotchValidationRegularPadId = 999;
            vm.AnalyzeNotchValidationCommand.Execute(null);
        }
    }

    private static void ObserveValidation(FreeformHelperViewModel vm, List<(string Event, int All, int Direct)> observations)
    {
        vm.PropertyChanging += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.NotchValidationItems)) Record("changing:merged");
            if (args.PropertyName == nameof(vm.NotchValidationDirectItems)) Record("changing:direct");
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.NotchValidationItems)) Record("changed:merged");
            if (args.PropertyName == nameof(vm.NotchValidationDirectItems)) Record("changed:direct");
            if (args.PropertyName == nameof(vm.NotchValidationSummaryText)) Record("summary");
            if (args.PropertyName == nameof(vm.StatusText)) Record("status");
        };
        void Record(string name) => observations.Add((name, vm.NotchValidationItems.Count, vm.NotchValidationDirectCount));
    }

    private static async Task<FreeformHelperViewModel> CreatePreviewAsync(OperationProgressLifecycleTestScope files,
        DxfEditingBehaviorTestScope dxf, ManualTimeProvider clock)
    {
        var vm = await files.CreateLoadedAsync();
        vm.NotchPreviewAutoPlayEnabled = false;
        vm.NotchPreviewTimingForTests.TimeProvider = clock;
        vm.EnableToFull = true;
        vm.ShowNotchCanvasPreview = true;
        vm.RecalcBoundsOnLayerFilter = false;
        vm.GridAlignmentMode = GridAlignmentMode.FromPanelAa;
        await dxf.OpenAsync(vm, ("signal", 0, 0, 8, 10), ("signal", 12, 0, 20, 10));
        await vm.MatchCommand.ExecuteAsync(null);
        Assert.True(vm.GetWorkflowStateSnapshot().HasStep1Result, vm.StatusText);
        return vm;
    }

    private static async Task ReplacePreviewSourceAsync(FreeformHelperViewModel vm, DxfEditingBehaviorTestScope dxf,
        string path, string replacement)
    {
        switch (replacement)
        {
            case "project":
                Assert.True((await vm.LoadProjectFromPathAsync(path)).IsLoaded);
                break;
            case "grid":
                Assert.Single(vm.CascadeIcSettings).XChannels = 3;
                break;
            case "cad":
                await dxf.OpenAsync(vm, ("replacement", 0, 0, 20, 10));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(replacement));
        }
        await vm.WaitForGridRebuildIdleAsync();
    }
}
