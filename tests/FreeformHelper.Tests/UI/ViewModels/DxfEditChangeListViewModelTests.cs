using System.Collections.ObjectModel;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeListViewModelTests
{
    private static readonly int[] AppliedHiddenCadIds = [10, 12];
    private static readonly int[] RestoredDuplicateCadIds = [101, 103];
    private static readonly int[] RehiddenDuplicateCadIds = [102];

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

        Assert.Equal(AppliedHiddenCadIds, appliedCadIds);
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
        Assert.Equal(RestoredDuplicateCadIds, appliedCadIds);

        appliedCadIds.Clear();
        vm.RehideSelectedDuplicateEntriesCommand.Execute(null);
        Assert.Equal(RehiddenDuplicateCadIds, appliedCadIds);
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
