// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeListBulkApplyAvailabilityTests
{
    [Fact]
    public void CheckedCombinedRowThatCannotBeApplied_LeavesTheBulkActionDisabled()
    {
        var applied = new List<int>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Combined, 10, canApply: false),
        };
        var vm = CreateViewModel(DxfEditChangeKind.Combined, entries, applied);

        vm.AreAllVisibleRowsSelected = true;

        Assert.Equal(1, vm.SelectedVisibleCount);
        Assert.False(vm.CanApplySelectedEntries);
        Assert.False(vm.ApplySelectedEntriesCommand.CanExecute(null));
        Assert.Empty(applied);
    }

    [Fact]
    public void CheckedRowsWhereOnlyOneCanBeApplied_KeepsTheBulkActionEnabledAndAppliesThatRow()
    {
        var applied = new List<int>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Combined, 10, canApply: false),
            CreateEntry(DxfEditChangeKind.Combined, 11, canApply: true),
        };
        var vm = CreateViewModel(DxfEditChangeKind.Combined, entries, applied);

        vm.AreAllVisibleRowsSelected = true;

        Assert.True(vm.CanApplySelectedEntries);
        Assert.True(vm.ApplySelectedEntriesCommand.CanExecute(null));

        vm.ApplySelectedEntriesCommand.Execute(null);

        Assert.Equal(11, Assert.Single(applied));
    }

    private static DxfEditChangeListViewModel CreateViewModel(
        DxfEditChangeKind kind,
        List<DxfEditChangeListEntry> entries,
        List<int> applied)
    {
        return new DxfEditChangeListViewModel(
            kind,
            () => entries.Select(CloneEntry).ToList(),
            static _ => { },
            entry => applied.Add(entry.CadPadId));
    }

    private static DxfEditChangeListEntry CloneEntry(DxfEditChangeListEntry source)
    {
        return CreateEntry(source.Kind, source.CadPadId, source.CanApply);
    }

    private static DxfEditChangeListEntry CreateEntry(DxfEditChangeKind kind, int cadPadId, bool canApply)
    {
        return new DxfEditChangeListEntry(
            kind,
            cadPadId,
            cadPadId,
            $"PAD_{cadPadId}",
            kind.ToString(),
            "Layer 0",
            "detail",
            "detail",
            IconGlyphs.Undo,
            "Clear",
            "Clear",
            canFocus: true,
            canApply: canApply);
    }
}
