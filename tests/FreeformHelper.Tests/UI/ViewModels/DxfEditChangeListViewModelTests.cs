using System.Collections.ObjectModel;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeListViewModelTests
{
    private static readonly int[] _appliedHiddenCadIds = [10, 12];
    private static readonly int[] _restoredDuplicateCadIds = [101, 103];
    private static readonly int[] _rehiddenDuplicateCadIds = [102];

    [Fact]
    public void Rows_ExposesReadOnlyCollectionFacade()
    {
        var vm = CreateViewModel(
            DxfEditChangeKind.Hidden,
            [CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore")]);

        Assert.IsType<ReadOnlyObservableCollection<DxfEditChangeListEntry>>(vm.Rows);
    }

    [Fact]
    public void AreAllVisibleRowsSelected_WhenSet_SelectsAllVisibleRows()
    {
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore"),
            CreateEntry(DxfEditChangeKind.Hidden, 11, "Restore"),
            CreateEntry(DxfEditChangeKind.Duplicate, 20, "Restore"),
        };
        var vm = CreateViewModel(DxfEditChangeKind.Hidden, entries);

        vm.AreAllVisibleRowsSelected = true;

        Assert.True(vm.Rows.All(static row => row.IsSelected));
        Assert.Equal(2, vm.SelectedVisibleCount);
        Assert.True(vm.CanApplySelectedEntries);
    }

    [Fact]
    public void ApplySelectedEntriesCommand_ForHiddenRows_AppliesSelectedVisibleRows()
    {
        var appliedCadIds = new List<int>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore"),
            CreateEntry(DxfEditChangeKind.Hidden, 11, "Restore"),
            CreateEntry(DxfEditChangeKind.Hidden, 12, "Restore"),
        };
        var vm = CreateViewModel(
            DxfEditChangeKind.Hidden,
            entries,
            applyEntry: entry => appliedCadIds.Add(entry.CadPadId));

        vm.Rows[0].IsSelected = true;
        vm.Rows[2].IsSelected = true;
        vm.ApplySelectedEntriesCommand.Execute(null);

        Assert.Equal(_appliedHiddenCadIds, appliedCadIds);
    }

    [Fact]
    public void DuplicateBulkCommands_OnlyApplyRowsMatchingRequestedOperation()
    {
        var appliedCadIds = new List<int>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Duplicate, 101, "Restore"),
            CreateEntry(DxfEditChangeKind.Duplicate, 102, "Hide again"),
            CreateEntry(DxfEditChangeKind.Duplicate, 103, "Restore"),
        };
        var vm = CreateViewModel(
            DxfEditChangeKind.Duplicate,
            entries,
            applyEntry: entry => appliedCadIds.Add(entry.CadPadId));

        foreach (var row in vm.Rows)
        {
            row.IsSelected = true;
        }

        vm.RestoreSelectedDuplicateEntriesCommand.Execute(null);
        Assert.Equal(_restoredDuplicateCadIds, appliedCadIds);

        appliedCadIds.Clear();
        vm.RehideSelectedDuplicateEntriesCommand.Execute(null);
        Assert.Equal(_rehiddenDuplicateCadIds, appliedCadIds);
    }

    [Fact]
    public void RotatedKind_UsesRestoreGeometryBulkAction()
    {
        var vm = CreateViewModel(
            DxfEditChangeKind.Rotated,
            [CreateEntry(DxfEditChangeKind.Rotated, 77, "Restore geometry")]);

        Assert.True(vm.IsRotatedKindSelected);
        Assert.Equal("Restore selected geometry", vm.BulkActionText);
        Assert.Equal(IconGlyphs.Undo, vm.BulkActionGlyph);
    }

    [Fact]
    public void SelectEntryCommand_OnlySelectsRow_WithoutInvokingFocus()
    {
        var focusCadIds = new List<int>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore"),
            CreateEntry(DxfEditChangeKind.Hidden, 11, "Restore"),
        };
        var vm = CreateViewModel(
            DxfEditChangeKind.Hidden,
            entries,
            focusEntry: entry => focusCadIds.Add(entry.CadPadId));

        var target = vm.Rows[1];
        vm.SelectEntryCommand.Execute(target);

        Assert.Same(target, vm.SelectedEntry);
        Assert.True(target.IsInspectorSelected);
        Assert.False(vm.Rows[0].IsInspectorSelected);
        Assert.Empty(focusCadIds);
    }

    [Theory]
    [InlineData(DxfEditChangeKind.Hidden, 10)]
    [InlineData(DxfEditChangeKind.Duplicate, 20)]
    [InlineData(DxfEditChangeKind.Combined, 30)]
    [InlineData(DxfEditChangeKind.Moved, 40)]
    [InlineData(DxfEditChangeKind.Rotated, 50)]
    public void SetKindCommands_ShowOnlyMatchingEntriesAndSelectVisibleRow(DxfEditChangeKind kind, int expectedCadId)
    {
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Hidden, 10, "Restore"),
            CreateEntry(DxfEditChangeKind.Duplicate, 20, "Restore"),
            CreateEntry(DxfEditChangeKind.Combined, 30, "Clear group"),
            CreateEntry(DxfEditChangeKind.Moved, 40, "Restore layer"),
            CreateEntry(DxfEditChangeKind.Rotated, 50, "Restore geometry"),
        };
        var initialKind = kind == DxfEditChangeKind.Hidden ? DxfEditChangeKind.Rotated : DxfEditChangeKind.Hidden;
        var vm = CreateViewModel(initialKind, entries);
        var command = kind switch
        {
            DxfEditChangeKind.Hidden => vm.SetHiddenKindCommand,
            DxfEditChangeKind.Duplicate => vm.SetDuplicateKindCommand,
            DxfEditChangeKind.Combined => vm.SetCombinedKindCommand,
            DxfEditChangeKind.Moved => vm.SetMovedKindCommand,
            DxfEditChangeKind.Rotated => vm.SetRotatedKindCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        command.Execute(null);

        Assert.Equal(kind, vm.SelectedKind);
        var row = Assert.Single(vm.Rows);
        Assert.Equal(kind, row.Kind);
        Assert.Equal(expectedCadId, row.CadPadId);
        Assert.Same(row, vm.SelectedEntry);
        Assert.True(row.IsInspectorSelected);
    }

    [Fact]
    public void RefreshCommand_ReloadsEntriesAndReplacesRemovedSelection()
    {
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Moved, 10, "Restore layer"),
            CreateEntry(DxfEditChangeKind.Moved, 11, "Restore layer"),
        };
        var vm = CreateViewModel(DxfEditChangeKind.Moved, entries);
        vm.SelectEntryCommand.Execute(vm.Rows[1]);
        entries.RemoveAt(1);
        entries.Add(CreateEntry(DxfEditChangeKind.Moved, 12, "Restore layer"));

        vm.RefreshCommand.Execute(null);

        Assert.Equal(2, vm.MovedCount);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal(10, vm.Rows[0].CadPadId);
        Assert.Equal(12, vm.Rows[1].CadPadId);
        Assert.Same(vm.Rows[0], vm.SelectedEntry);
        Assert.True(vm.Rows[0].IsInspectorSelected);
    }

    [Fact]
    public void FocusSelectedEntryCommand_FocusesSelectedRowAndDisablesWhenListIsEmpty()
    {
        var focusedEntries = new List<DxfEditChangeListEntry>();
        var entries = new List<DxfEditChangeListEntry>
        {
            CreateEntry(DxfEditChangeKind.Rotated, 10, "Restore geometry"),
            CreateEntry(DxfEditChangeKind.Rotated, 11, "Restore geometry"),
        };
        var vm = CreateViewModel(DxfEditChangeKind.Rotated, entries, focusEntry: focusedEntries.Add);
        var target = vm.Rows[1];
        vm.SelectEntryCommand.Execute(target);
        Assert.True(vm.FocusSelectedEntryCommand.CanExecute(null));

        vm.FocusSelectedEntryCommand.Execute(null);

        Assert.Same(target, Assert.Single(focusedEntries));
        Assert.Same(target, vm.SelectedEntry);

        entries.Clear();
        vm.RefreshCommand.Execute(null);

        Assert.Empty(vm.Rows);
        Assert.Null(vm.SelectedEntry);
        Assert.False(vm.FocusSelectedEntryCommand.CanExecute(null));
    }

    private static DxfEditChangeListViewModel CreateViewModel(
        DxfEditChangeKind initialKind,
        List<DxfEditChangeListEntry> entries,
        Action<DxfEditChangeListEntry>? focusEntry = null,
        Action<DxfEditChangeListEntry>? applyEntry = null)
    {
        return new DxfEditChangeListViewModel(
            initialKind,
            () => entries.Select(CloneEntry).ToList(),
            focusEntry ?? (_ => { }),
            applyEntry ?? (_ => { }));
    }

    private static DxfEditChangeListEntry CloneEntry(DxfEditChangeListEntry source)
    {
        return new DxfEditChangeListEntry(
            source.Kind,
            source.CadPadId,
            source.FocusCadPadId,
            source.CadName,
            source.StatusText,
            source.CurrentLayerText,
            source.ListDetailText,
            source.DetailText,
            source.ActionGlyph,
            source.ActionLabel,
            source.ActionHint,
            source.CanFocus,
            source.CanApply);
    }

    private static DxfEditChangeListEntry CreateEntry(DxfEditChangeKind kind, int cadPadId, string actionLabel)
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
            actionLabel == "Hide again" ? IconGlyphs.VisibilityOff : IconGlyphs.Undo,
            actionLabel,
            actionLabel,
            canFocus: true,
            canApply: true);
    }
}
