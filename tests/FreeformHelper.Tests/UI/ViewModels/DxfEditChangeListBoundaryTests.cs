// Copyright (c) 2026 Dennis Liu. All rights reserved.
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Icons;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class DxfEditChangeListBoundaryTests
{
    [Theory]
    [InlineData(DxfEditChangeKind.Hidden, "Hidden", "Restore selected")]
    [InlineData(DxfEditChangeKind.Combined, "Combined", "Clear selected groups")]
    [InlineData(DxfEditChangeKind.Moved, "Moved", "Restore selected layers")]
    [InlineData(DxfEditChangeKind.Duplicate, "Duplicate", "Apply selected")]
    public void CategoryActions_WithUnnamedCad_DescribeTheVisibleScope(
        DxfEditChangeKind kind, string kindText, string bulkActionText)
    {
        using var culture = TestCulture.UseEnUs();
        var entry = CreateEntry(kind, cadName: " ");
        var vm = new DxfEditChangeListViewModel(kind, () => [entry], _ => { }, _ => { });

        Assert.Equal(kindText, entry.KindDisplayText);
        Assert.Equal("CAD 42", vm.SelectedRowHeaderText);
        Assert.Equal(bulkActionText, vm.BulkActionText);
        Assert.Equal(kind == DxfEditChangeKind.Duplicate, vm.IsDuplicateBulkMode);
        Assert.Equal(kind != DxfEditChangeKind.Duplicate, vm.IsSingleActionBulkMode);
        Assert.Equal(
            kind switch
            {
                DxfEditChangeKind.Hidden => IconGlyphs.Undo,
                DxfEditChangeKind.Combined => IconGlyphs.CallSplit,
                DxfEditChangeKind.Moved => IconGlyphs.DriveFileMove,
                _ => IconGlyphs.Edit,
            },
            vm.BulkActionGlyph);
    }

    [Fact]
    public void SelectEntryCommand_NullParameter_PreservesInspectorWithoutFocusing()
    {
        using var culture = TestCulture.UseEnUs();
        var entry = CreateEntry(DxfEditChangeKind.Moved);
        var focused = new List<DxfEditChangeListEntry>();
        var vm = new DxfEditChangeListViewModel(DxfEditChangeKind.Moved, () => [entry], focused.Add, _ => { });

        vm.SelectEntryCommand.Execute(null);

        Assert.Same(entry, vm.SelectedEntry);
        Assert.True(entry.IsInspectorSelected);
        Assert.Equal("CAD 42 · SENSOR", vm.SelectedRowHeaderText);
        Assert.Equal("Moved · Layer edited", vm.SelectedRowStatusText);
        Assert.Equal("Original layer: signal -> Current layer: edited.", vm.SelectedRowDetailText);
        Assert.True(vm.FocusSelectedEntryCommand.CanExecute(null));
        Assert.Empty(focused);
    }

    private static DxfEditChangeListEntry CreateEntry(
        DxfEditChangeKind kind, string cadName = "SENSOR", bool canApply = true)
    {
        return new DxfEditChangeListEntry(
            kind,
            cadPadId: 42,
            focusCadPadId: 42,
            cadName,
            statusText: kind.ToString(),
            currentLayerText: "Layer edited",
            listDetailText: "signal -> edited",
            detailText: "Original layer: signal -> Current layer: edited.",
            actionGlyph: IconGlyphs.Undo,
            actionLabel: kind switch
            {
                DxfEditChangeKind.Combined => "Clear group",
                DxfEditChangeKind.Duplicate => "Restore",
                _ => "Restore layer",
            },
            actionHint: "Move this CAD pad back to layer 'signal'.",
            canFocus: true,
            canApply);
    }
}
