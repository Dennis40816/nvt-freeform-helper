// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.ObjectModel;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class WorkspaceToggleRegistryCharacterizationTests
{
    [Fact]
    public void UserToggleUpdatesSourceOnce()
    {
        var vm = new FreeformHelperViewModel();
        var item = Assert.Single(vm.WorkspaceHeaderItems, item => item.Id == "highlightUnmatched");
        var changes = new List<string>();
        vm.PropertyChanging += (sender, args) =>
        {
            if (args.PropertyName != nameof(vm.HighlightUnmatched)) return;
            Assert.Same(vm, sender);
            changes.Add($"changing:{vm.HighlightUnmatched}");
        };
        vm.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName != nameof(vm.HighlightUnmatched)) return;
            Assert.Same(vm, sender);
            changes.Add($"changed:{vm.HighlightUnmatched}");
        };

        item.IsChecked = false;
        item.IsChecked = false;

        Assert.False(vm.HighlightUnmatched);
        Assert.Equal(["changing:True", "changed:False"], changes);
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);
        Assert.True(vm.HighlightUnmatched);
        Assert.True(item.IsChecked);
        Assert.False(vm.CanUndo);
    }

    [Fact]
    public void SourceChangeDoesNotInvokeUserToggle()
    {
        var toggles = new List<bool>();
        var item = new WorkspaceHeaderItem("source", "Source", true, toggles.Add);

        item.SetCheckedFromSource(false);

        Assert.False(item.IsChecked);
        Assert.Empty(toggles);
        item.IsChecked = true;
        Assert.Equal([true], toggles);
    }

    [Fact]
    public void SourceSynchronizationCompletesBeforeRootNotification()
    {
        var vm = new FreeformHelperViewModel();
        var item = Assert.Single(vm.WorkspaceHeaderItems, item => item.Id == "highlightFreeform");
        var changes = new List<string>();
        item.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(item.IsChecked))
                changes.Add($"item:{vm.HighlightFreeform}:{item.IsChecked}");
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(vm.HighlightFreeform))
                changes.Add($"root:{vm.HighlightFreeform}:{item.IsChecked}");
        };

        vm.HighlightFreeform = false;

        Assert.Equal(["item:False:False", "root:False:False"], changes);
    }

    [Fact]
    public void CollectionReplacementPreservesRegistryBehavior()
    {
        var vm = new FreeformHelperViewModel();
        var registered = Assert.Single(vm.WorkspaceHeaderItems, item => item.Id == "highlightUnmatched");
        var replacementToggles = new List<bool>();
        var replacement = new WorkspaceHeaderItem(registered.Id, registered.Label, true, replacementToggles.Add);
        var items = new ObservableCollection<WorkspaceHeaderItem> { replacement };
        var notifications = new List<string>();
        vm.PropertyChanging += (_, args) =>
        {
            if (args.PropertyName != nameof(vm.WorkspaceHeaderItems)) return;
            Assert.NotSame(items, vm.WorkspaceHeaderItems);
            notifications.Add("changing");
        };
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(vm.WorkspaceHeaderItems)) return;
            Assert.Same(items, vm.WorkspaceHeaderItems);
            notifications.Add("changed");
        };

        vm.WorkspaceHeaderItems = items;
        vm.WorkspaceHeaderItems = items;
        vm.HighlightUnmatched = false;

        Assert.Equal(["changing", "changed"], notifications);
        Assert.Same(items, vm.WorkspaceHeaderItems);
        Assert.False(registered.IsChecked);
        Assert.True(replacement.IsChecked);
        Assert.Empty(replacementToggles);

        registered.IsChecked = true;
        Assert.True(vm.HighlightUnmatched);
        replacement.IsChecked = false;
        Assert.True(vm.HighlightUnmatched);
        Assert.Equal([false], replacementToggles);
    }
}
