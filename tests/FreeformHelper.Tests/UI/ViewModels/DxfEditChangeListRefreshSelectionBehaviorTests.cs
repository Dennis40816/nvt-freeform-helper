// Copyright (c) 2026 Dennis Liu. All rights reserved.

using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeListRefreshSelectionBehaviorTests
{
    [Fact]
    public void RefreshEntries_PreservesChecksByKindAndCadIdAndKeepsInspectorIndependent()
    {
        using var culture = TestCulture.UseEnUs();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Hidden, 42),
            CreateEntry(DxfEditChangeKind.Hidden, 43),
            CreateEntry(DxfEditChangeKind.Moved, 42),
        };
        var applied = new List<int>();
        var vm = new DxfEditChangeListViewModel(
            DxfEditChangeKind.Hidden, () => entries.ToList(), _ => { }, entry =>
            {
                applied.Add(entry.CadPadId);
                entries.RemoveAll(row => row.Kind == entry.Kind && row.CadPadId == entry.CadPadId);
            });
        vm.Rows[0].IsSelected = true;
        vm.SelectEntryCommand.Execute(vm.Rows[1]);
        entries =
        [
            CreateEntry(DxfEditChangeKind.Hidden, 44),
            CreateEntry(DxfEditChangeKind.Moved, 42),
            CreateEntry(DxfEditChangeKind.Hidden, 43),
            CreateEntry(DxfEditChangeKind.Hidden, 42),
        ];

        vm.RefreshCommand.Execute(null);

        Assert.Equal([42, 43, 44], vm.Rows.Select(static row => row.CadPadId));
        Assert.Equal(42, Assert.Single(vm.Rows, static row => row.IsSelected).CadPadId);
        Assert.Equal(43, Assert.IsType<DxfEditChangeListEntry>(vm.SelectedEntry).CadPadId);
        Assert.Equal(43, Assert.Single(vm.Rows, static row => row.IsInspectorSelected).CadPadId);
        Assert.Equal(1, vm.SelectedVisibleCount);
        Assert.Equal("1 checked", vm.SelectedVisibleRowsSummaryText);
        Assert.False(vm.AreAllVisibleRowsSelected);

        vm.SetMovedKindCommand.Execute(null);

        Assert.False(Assert.Single(vm.Rows).IsSelected);
        Assert.Equal(0, vm.SelectedVisibleCount);
        Assert.False(vm.CanApplySelectedEntries);
        vm.SetHiddenKindCommand.Execute(null);
        vm.ApplySelectedEntriesCommand.Execute(null);

        Assert.Equal(42, Assert.Single(applied));
        Assert.Equal([43, 44], vm.Rows.Select(static row => row.CadPadId));
        Assert.Equal(0, vm.SelectedVisibleCount);
        Assert.False(vm.CanApplySelectedEntries);
    }

    [Fact]
    public void CheckedHiddenRows_DoNotEnableOrInvokeDuplicateBulkActions()
    {
        using var culture = TestCulture.UseEnUs();
        var hidden = CreateEntry(DxfEditChangeKind.Hidden, 42);
        var duplicate = CreateEntry(DxfEditChangeKind.Duplicate, 43);
        var applied = new List<DxfEditChangeListEntry>();
        var vm = new DxfEditChangeListViewModel(
            DxfEditChangeKind.Hidden, () => [hidden, duplicate], _ => { }, applied.Add);
        vm.AreAllVisibleRowsSelected = true;

        Assert.True(vm.CanApplySelectedEntries);
        Assert.False(vm.CanRestoreSelectedDuplicateEntries);
        Assert.False(vm.CanRehideSelectedDuplicateEntries);
        Assert.Equal(0, vm.RestoreSelectedDuplicateCount);
        Assert.Equal(0, vm.RehideSelectedDuplicateCount);
        Assert.False(vm.RestoreSelectedDuplicateEntriesCommand.CanExecute(null));
        Assert.False(vm.RehideSelectedDuplicateEntriesCommand.CanExecute(null));

        vm.RestoreSelectedDuplicateEntriesCommand.Execute(null);
        vm.RehideSelectedDuplicateEntriesCommand.Execute(null);

        Assert.Empty(applied);
        Assert.True(hidden.IsSelected);
        Assert.Same(hidden, vm.SelectedEntry);
        vm.SetDuplicateKindCommand.Execute(null);
        Assert.False(Assert.Single(vm.Rows).IsSelected);
        Assert.Equal(0, vm.SelectedVisibleCount);
    }

    private static DxfEditChangeListEntry CreateEntry(DxfEditChangeKind kind, int id)
    {
        var action = kind == DxfEditChangeKind.Moved ? "Restore layer" : "Restore";
        return new DxfEditChangeListEntry(
            kind, id, id, "SENSOR", kind.ToString(), "Layer signal", "Changed pad", "Changed pad detail",
            IconGlyphs.Undo, action, action, canFocus: kind == DxfEditChangeKind.Moved, canApply: true);
    }
}
