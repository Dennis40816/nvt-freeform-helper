using System.Reflection;
using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Pads;
using FreeformHelper.Infrastructure.Project;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed partial class FreeformHelperViewModelTests
{


    [Fact]
    public async Task ApplyCadLoadOutcomeAsync_ClearsStaleCombinedMetadataBeforeApplyingHiddenRows()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 8, 0, 18, 10),
        };
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var resetBaselineMethod = typeof(FreeformHelperViewModel).GetMethod("ResetDxfEditBaseline", BindingFlags.Instance | BindingFlags.NonPublic);
        var applyCadLoadOutcomeMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyCadLoadOutcomeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var applyPersistedDxfEditProjectStateMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyPersistedDxfEditProjectState", BindingFlags.Instance | BindingFlags.NonPublic);
        var buildEntriesMethod = typeof(FreeformHelperViewModel).GetMethod("BuildDxfEditChangeEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(resetBaselineMethod);
        Assert.NotNull(applyCadLoadOutcomeMethod);
        Assert.NotNull(applyPersistedDxfEditProjectStateMethod);
        Assert.NotNull(buildEntriesMethod);

        cadField!.SetValue(vm, new CadPadSet(source));
        resetBaselineMethod!.Invoke(vm, new object?[] { new CadPadSet(source) });
        await SelectCadPadsAsync(vm, PairCadSelection);
        vm.CombineSelectedCadPadsCommand.Execute(null);
        Assert.True(vm.HasCombinedCadPads);

        var nextCad = new CadPadSet(new[]
        {
            CreateRectCad(1, 100, 0, 110, 10),
        });
        var applyTask = Assert.IsAssignableFrom<Task>(applyCadLoadOutcomeMethod!.Invoke(
            vm,
            new object?[]
            {
                new CadLoadOutcome(
                    nextCad,
                    CadLoadSource.Path,
                    @"C:\temp\next.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        nextCad,
                        0,
                        Array.Empty<int>(),
                        new Dictionary<int, int>())),
                null
            }));
        await applyTask;
        applyPersistedDxfEditProjectStateMethod!.Invoke(
            vm,
            new object?[]
            {
                new ProjectFile
                {
                    HiddenCadPadIds = new HashSet<int> { 1 },
                },
            });

        var entries = Assert.IsType<List<DxfEditChangeListEntry>>(buildEntriesMethod.Invoke(vm, null));
        var hiddenEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Hidden);
        Assert.Equal("Hidden", hiddenEntry.StatusText);
        Assert.Equal("Restore", hiddenEntry.ActionLabel);
    }


    [Fact]
    public async Task ApplyCadLoadOutcomeAsync_PreservesRawDuplicatePads_ButAutoHidesThemFromWorkingSet()
    {
        var vm = new FreeformHelperViewModel();
        var applyCadLoadOutcomeMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyCadLoadOutcomeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var buildActiveCadPadSetMethod = typeof(FreeformHelperViewModel).GetMethod("BuildActiveCadPadSet", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyCadLoadOutcomeMethod);
        Assert.NotNull(buildActiveCadPadSetMethod);
        var duplicateCadIds = new[] { 1 };
        var expectedVisibleCadIds = new[] { 0, 2 };

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 0, 0, 10, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(applyCadLoadOutcomeMethod!.Invoke(
            vm,
            new object?[]
            {
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        duplicateCadIds,
                        new Dictionary<int, int> { [1] = 0 })),
                null,
            }));
        await applyTask;

        var currentCad = GetCurrentCadPadSet(vm);
        Assert.Equal(3, currentCad.Pads.Count);
        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(expectedVisibleCadIds, vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        var activeCad = Assert.IsType<CadPadSet>(buildActiveCadPadSetMethod!.Invoke(vm, null));
        Assert.Equal(expectedVisibleCadIds, activeCad.Pads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());
    }


    [Fact]
    public async Task ResetAllDxfEditsCommand_PreservesAutoHiddenDuplicatePads()
    {
        var vm = new FreeformHelperViewModel();
        var applyCadLoadOutcomeMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyCadLoadOutcomeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyCadLoadOutcomeMethod);
        var duplicateCadIds = new[] { 1 };
        var expectedVisibleCadIds = new[] { 0, 2 };
        var selectedCadIds = new[] { 2 };

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 20, 0, 30, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(applyCadLoadOutcomeMethod!.Invoke(
            vm,
            new object?[]
            {
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        duplicateCadIds,
                        new Dictionary<int, int> { [1] = 0 })),
                null,
            }));
        await applyTask;

        await SelectCadPadsAsync(vm, selectedCadIds);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        Assert.Equal(1, vm.DeletedCadPadCount);

        vm.ResetAllDxfEditsCommand.Execute(null);
        await WaitForGridRebuildAsync(vm);

        Assert.Equal(0, vm.DeletedCadPadCount);
        Assert.Equal(expectedVisibleCadIds, vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        var currentCad = GetCurrentCadPadSet(vm);
        Assert.Equal(3, currentCad.Pads.Count);
    }


    [Fact]
    public async Task DxfEditChangeListApply_OnDuplicateRow_TogglesDuplicateVisibility()
    {
        var vm = new FreeformHelperViewModel();
        var applyCadLoadOutcomeMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyCadLoadOutcomeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        var buildEntriesMethod = typeof(FreeformHelperViewModel).GetMethod("BuildDxfEditChangeEntries", BindingFlags.Instance | BindingFlags.NonPublic);
        var applyEntryMethod = typeof(FreeformHelperViewModel).GetMethod("ApplyDxfEditChangeEntry", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(applyCadLoadOutcomeMethod);
        Assert.NotNull(buildEntriesMethod);
        Assert.NotNull(applyEntryMethod);

        var rawCad = new CadPadSet(new[]
        {
            CreateRectCad(0, 0, 0, 10, 10),
            CreateRectCad(1, 0, 0, 10, 10),
            CreateCad(2, "OTHER", 20, 0, 30, 10),
        });

        var applyTask = Assert.IsAssignableFrom<Task>(applyCadLoadOutcomeMethod!.Invoke(
            vm,
            new object?[]
            {
                new CadLoadOutcome(
                    rawCad,
                    CadLoadSource.Path,
                    @"C:\temp\duplicate.dxf",
                    new CadPadExactDuplicateSanitizationResult(
                        rawCad,
                        1,
                        [1],
                        new Dictionary<int, int> { [1] = 0 })),
                null,
            }));
        await applyTask;

        var entries = Assert.IsType<List<DxfEditChangeListEntry>>(buildEntriesMethod!.Invoke(vm, null));
        var duplicateEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Duplicate);
        Assert.Equal(0, duplicateEntry.FocusCadPadId);
        Assert.Equal("Auto-hidden duplicate", duplicateEntry.StatusText);
        Assert.Equal("Restore", duplicateEntry.ActionLabel);

        applyEntryMethod!.Invoke(vm, [duplicateEntry]);

        Assert.Equal([0, 1, 2], vm.CadPads.Select(static pad => pad.Id).OrderBy(static id => id).ToArray());

        entries = Assert.IsType<List<DxfEditChangeListEntry>>(buildEntriesMethod.Invoke(vm, null));
        duplicateEntry = Assert.Single(entries, entry => entry.Kind == DxfEditChangeKind.Duplicate);
        Assert.Equal(1, duplicateEntry.FocusCadPadId);
        Assert.Equal("Restored duplicate", duplicateEntry.StatusText);
        Assert.Equal("Hide again", duplicateEntry.ActionLabel);
    }


    [Fact]
    public async Task RestoreHiddenCadPad_PreservesRemainingUndoBatches()
    {
        var vm = new FreeformHelperViewModel();
        var source = new[]
        {
            CreateRectCad(1, 0, 0, 10, 10),
            CreateRectCad(2, 12, 0, 22, 10),
        };
        var cadField = typeof(FreeformHelperViewModel).GetField("_cad", BindingFlags.Instance | BindingFlags.NonPublic);
        var restoreHiddenCadPadMethod = typeof(FreeformHelperViewModel).GetMethod("RestoreHiddenCadPad", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(cadField);
        Assert.NotNull(restoreHiddenCadPadMethod);

        cadField!.SetValue(vm, new CadPadSet(source));

        await SelectCadPadsAsync(vm, [1]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        await SelectCadPadsAsync(vm, [2]);
        vm.DeleteSelectedCadPadsCommand.Execute(null);
        Assert.Equal(2, vm.DeletedCadPadCount);

        restoreHiddenCadPadMethod!.Invoke(vm, new object?[] { 2 });
        Assert.Equal(1, vm.DeletedCadPadCount);

        vm.RestoreLastDeletedCadPadsCommand.Execute(null);

        Assert.Equal(0, vm.DeletedCadPadCount);
    }
}
