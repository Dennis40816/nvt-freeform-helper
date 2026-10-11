// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using Avalonia.Headless.XUnit;
using FreeformHelper.Application.Export;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.ViewModels;
using Xunit;
using static FreeformHelper.Tests.FreeformHelperViewModelTests;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class NotchValidationCountProjectionTests
{
    [AvaloniaFact]
    public async Task AnalyzeNotchValidationCommand_RegWithAllGroups_RaisesCountsAfterCollectionsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            var changes = ObserveNotchValidationChanges(vm);

            vm.AnalyzeNotchValidationCommand.Execute(null);

            AssertCounts(vm, 1, 1, 1);
            AssertGroupChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationIncomingCount),
                nameof(vm.NotchValidationOutgoingCount));
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [AvaloniaFact]
    public async Task AnalyzeNotchValidationCommand_SameCounts_RaisesNoCountChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            var changes = ObserveNotchValidationChanges(vm);

            vm.AnalyzeNotchValidationCommand.Execute(null);

            AssertCounts(vm, 1, 1, 1);
            AssertGroupChanges(changes);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [AvaloniaFact]
    public async Task AnalyzeNotchValidationCommand_DifferentCounts_RaisesChangedCountsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            vm.ActiveAreaWidth = 30;
            Assert.Single(vm.CascadeIcSettings).XChannels = 3;
            await WaitForGridRebuildAsync(vm);
            await vm.MatchCommand.ExecuteAsync(null);
            await vm.AutoDetectFreeformsCommand.ExecuteAsync(null);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            AssertCounts(vm, 1, 1, 2);
            vm.NotchValidationRegularPadId = vm.RegularPads[1].RegularPadId;
            var changes = ObserveNotchValidationChanges(vm);

            vm.AnalyzeNotchValidationCommand.Execute(null);

            AssertCounts(vm, 0, 1, 0);
            AssertGroupChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationOutgoingCount));
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [AvaloniaFact]
    public async Task ClearNotchValidationCommand_PopulatedGroups_RaisesZeroCountsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            var changes = ObserveNotchValidationChanges(vm);

            vm.ClearNotchValidationCommand.Execute(null);

            AssertCounts(vm, 0, 0, 0);
            AssertGroupChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationIncomingCount),
                nameof(vm.NotchValidationOutgoingCount));
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [AvaloniaFact]
    public async Task AnalyzeNotchValidationCommand_MissingReg_ClearsCountsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            vm.NotchValidationRegularPadId = int.MaxValue;
            var changes = ObserveNotchValidationChanges(vm);

            vm.AnalyzeNotchValidationCommand.Execute(null);

            AssertCounts(vm, 0, 0, 0);
            AssertGroupChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationIncomingCount),
                nameof(vm.NotchValidationOutgoingCount));
            Assert.Contains("not found", vm.NotchValidationSummaryText, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [AvaloniaFact]
    public async Task ClearStep5ResultCommand_PopulatedGroups_ClearsCountsInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            var changes = ObserveNotchValidationChanges(vm);

            vm.ClearStep5ResultCommand.Execute(null);

            AssertCounts(vm, 0, 0, 0);
            AssertGroupChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationIncomingCount),
                nameof(vm.NotchValidationOutgoingCount));
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task ClearNotchValidationCommand_EmptyGroups_RaisesNoCountChanges()
    {
        var vm = new FreeformHelperViewModel();
        await WaitForGridRebuildAsync(vm);
        var changes = ObserveNotchValidationChanges(vm);

        vm.ClearNotchValidationCommand.Execute(null);

        AssertCounts(vm, 0, 0, 0);
        AssertGroupChanges(changes);
    }

    [Fact]
    public async Task AnalyzeNotchValidationCommand_NoGeneratedTable_RaisesNoCountChanges()
    {
        var vm = new FreeformHelperViewModel();
        await WaitForGridRebuildAsync(vm);
        var changes = ObserveNotchValidationChanges(vm);

        vm.AnalyzeNotchValidationCommand.Execute(null);

        AssertCounts(vm, 0, 0, 0);
        AssertGroupChanges(changes);
    }

    [AvaloniaFact]
    public async Task LoadProjectCommand_PopulatedGroups_RaisesZeroCountsOnceInOrder()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 15, 10), ("signal", 5, 0, 20, 10),
            ("signal", 0, 0, 14, 10));
        var outputPath = Path.ChangeExtension(path, ".c");
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            var vm = await CreateNotchExportCacheViewModelAsync(path);
            await ExportNotchProfileAsync(vm, NotchExportProfile.Release, outputPath);
            vm.NotchValidationRegularPadId = vm.RegularPads[0].RegularPadId;
            vm.AnalyzeNotchValidationCommand.Execute(null);
            await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile(), CancellationToken.None);
            vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath);
            var changes = ObserveNotchValidationChanges(vm);

            await vm.LoadProjectCommand.ExecuteAsync(null);

            AssertCounts(vm, 0, 0, 0);
            AssertProjectClearChanges(changes, nameof(vm.NotchValidationDirectCount), nameof(vm.NotchValidationIncomingCount),
                nameof(vm.NotchValidationOutgoingCount));
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
            File.Delete(outputPath);
            File.Delete(projectPath);
        }
    }

    [Fact]
    public async Task LoadProjectCommand_EmptyGroups_RaisesNoCountChanges()
    {
        var path = CreateTempDxfFile(("signal", 0, 0, 10, 20));
        var projectPath = Path.ChangeExtension(path, ".json");
        try
        {
            await new JsonProjectStore().SaveAsync(projectPath, new ProjectFile(), CancellationToken.None);
            var vm = new FreeformHelperViewModel { PickLoadProjectPathAsync = () => Task.FromResult<string?>(projectPath) };
            await WaitForGridRebuildAsync(vm);
            var changes = ObserveNotchValidationChanges(vm);

            await vm.LoadProjectCommand.ExecuteAsync(null);

            AssertCounts(vm, 0, 0, 0);
            AssertProjectClearChanges(changes);
            await WaitForGridRebuildAsync(vm);
        }
        finally
        {
            File.Delete(path);
            File.Delete(projectPath);
        }
    }

    private static ConcurrentQueue<string?> ObserveNotchValidationChanges(FreeformHelperViewModel vm)
    {
        var changes = new ConcurrentQueue<string?>();
        vm.PropertyChanged += (_, change) => changes.Enqueue(change.PropertyName);
        return changes;
    }

    private static void AssertCounts(FreeformHelperViewModel vm, int direct, int incoming, int outgoing)
    {
        Assert.Equal((direct, incoming, outgoing),
            (vm.NotchValidationDirectCount, vm.NotchValidationIncomingCount, vm.NotchValidationOutgoingCount));
        Assert.Equal(direct, vm.NotchValidationDirectItems.Count);
        Assert.Equal(incoming, vm.NotchValidationIncomingItems.Count);
        Assert.Equal(outgoing, vm.NotchValidationOutgoingItems.Count);
    }

    private static void AssertGroupChanges(ConcurrentQueue<string?> changes, params string[] counts)
    {
        var collectionChanges = GetCollectionChangeNames();
        Assert.Equal(collectionChanges.Concat(counts), changes.Where(name => IsCountChange(name) ||
            collectionChanges.Contains(name, StringComparer.Ordinal)));
    }

    private static void AssertProjectClearChanges(ConcurrentQueue<string?> changes, params string[] counts)
    {
        var collectionChanges = GetCollectionChangeNames();
        var actual = changes.Where(name => IsCountChange(name) ||
            collectionChanges.Contains(name, StringComparer.Ordinal)).ToArray();
        var firstClearLength = collectionChanges.Length + counts.Length;
        Assert.Equal(collectionChanges.Concat(counts), actual.Take(firstClearLength));
        Assert.All(actual.Skip(firstClearLength).Chunk(collectionChanges.Length),
            names => Assert.Equal(collectionChanges, names));
    }

    private static string[] GetCollectionChangeNames() =>
        [
            nameof(FreeformHelperViewModel.HasNotchValidationItems),
            nameof(FreeformHelperViewModel.IsNotchValidationItemsEmpty),
            nameof(FreeformHelperViewModel.NotchValidationItems),
            nameof(FreeformHelperViewModel.HasNotchValidationDirectItems),
            nameof(FreeformHelperViewModel.IsNotchValidationDirectEmpty),
            nameof(FreeformHelperViewModel.NotchValidationDirectItems),
            nameof(FreeformHelperViewModel.HasNotchValidationIncomingItems),
            nameof(FreeformHelperViewModel.IsNotchValidationIncomingEmpty),
            nameof(FreeformHelperViewModel.NotchValidationIncomingItems),
            nameof(FreeformHelperViewModel.HasNotchValidationOutgoingItems),
            nameof(FreeformHelperViewModel.IsNotchValidationOutgoingEmpty),
            nameof(FreeformHelperViewModel.NotchValidationOutgoingItems),
        ];

    private static bool IsCountChange(string? name) => name is
        nameof(FreeformHelperViewModel.NotchValidationDirectCount) or
        nameof(FreeformHelperViewModel.NotchValidationIncomingCount) or
        nameof(FreeformHelperViewModel.NotchValidationOutgoingCount);
}
