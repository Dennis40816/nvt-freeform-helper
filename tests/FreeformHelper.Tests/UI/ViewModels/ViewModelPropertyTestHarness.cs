// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using FreeformHelper.UI.ViewModels;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>
/// Shares property assertions while each test class supplies its settings and persistence expectations.
/// </summary>
internal static class ViewModelPropertyTestHarness
{
    internal static async Task AssertChangeNotificationsAsync(
        FreeformHelperViewModel vm,
        string name,
        object newValue,
        IEnumerable<string> names,
        IReadOnlyCollection<string>? recalculated = null)
    {
        var observation = new PropertyChangeObservation(vm, names);

        Write(vm, name, newValue);
        await vm.WaitForGridRebuildIdleAsync();

        observation.AssertChanged(name, newValue, recalculated ?? []);
    }

    internal static void AssertChangeNotifications(
        FreeformHelperViewModel vm,
        string name,
        object newValue,
        IEnumerable<string> names)
    {
        var observation = new PropertyChangeObservation(vm, names);

        Write(vm, name, newValue);

        observation.AssertChanged(name, newValue, []);
    }

    internal static void AssertSameValueRaisesNothing(Action<FreeformHelperViewModel> assignSameValue)
    {
        var vm = new FreeformHelperViewModel();
        var raised = new ConcurrentQueue<string?>();
        vm.PropertyChanging += (_, args) => raised.Enqueue(args.PropertyName);
        vm.PropertyChanged += (_, args) => raised.Enqueue(args.PropertyName);

        assignSameValue(vm);

        Assert.Empty(raised);
    }

    internal static async Task<FreeformHelperViewModel> SetValueOnCleanProjectAsync(string name, object newValue)
    {
        var vm = new FreeformHelperViewModel();
        Assert.False(vm.HasUnsavedChanges);

        Write(vm, name, newValue);
        await vm.WaitForGridRebuildIdleAsync();

        return vm;
    }

    internal static async Task AssertUndoAsync(string name, object newValue, bool hasUndo)
    {
        var vm = new FreeformHelperViewModel();
        var start = Read(vm, name);
        Assert.False(vm.CanUndo);

        Write(vm, name, newValue);
        await vm.WaitForGridRebuildIdleAsync();

        Assert.Equal(hasUndo, vm.CanUndo);
        if (!hasUndo)
        {
            return;
        }

        vm.Undo();

        Assert.Equal(start, Read(vm, name));
    }

    internal static async Task AssertProjectRoundTripAsync(
        Action<FreeformHelperViewModel> configure,
        Action<FreeformHelperViewModel> assertLoadedValues)
    {
        using var files = new LoadingModeTestScope();
        await files.SaveAsync(configure);
        var first = await ReadWithoutSavedAtAsync(files.PathFor("project.json"));
        var vm = files.CreateViewModel();
        var secondPath = files.PathFor("project-second.json");
        vm.PickSaveProjectPathAsync = () => Task.FromResult<string?>(secondPath);

        var result = await vm.LoadProjectFromPathAsync(files.PathFor("project.json"));
        Assert.True(result.IsLoaded);
        assertLoadedValues(vm);
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.SaveProjectAsync());

        var second = await ReadWithoutSavedAtAsync(secondPath);
        Assert.Equal(first, second);
    }

    internal static void WriteValues(FreeformHelperViewModel vm, IEnumerable<(string Name, object Value)> values)
    {
        foreach (var (name, value) in values)
        {
            Write(vm, name, value);
        }
    }

    internal static void AssertValues(FreeformHelperViewModel vm, IEnumerable<(string Name, object Value)> expectedValues)
    {
        var mismatches = expectedValues
            .Where(setting => !Equals(setting.Value, Read(vm, setting.Name)))
            .Select(setting => setting.Name + "=" + Read(vm, setting.Name))
            .ToList();
        Assert.Equal("", string.Join(",", mismatches));
    }

    internal static object Read(FreeformHelperViewModel vm, string name) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.GetValue(vm)!;

    internal static void Write(FreeformHelperViewModel vm, string name, object value) =>
        typeof(FreeformHelperViewModel).GetProperty(name)!.SetValue(vm, value);

    // The save time is the only member that differs between two saves of the same state.
    private static async Task<string> ReadWithoutSavedAtAsync(string projectFilePath)
    {
        await using var stream = File.OpenRead(projectFilePath);
        var root = (await JsonNode.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken))!.AsObject();
        var key = root.Select(member => member.Key).Single(name => string.Equals(name, "SavedAt", StringComparison.OrdinalIgnoreCase));
        root.Remove(key);
        return root.ToJsonString();
    }

    private sealed class PropertyChangeObservation
    {
        private readonly FreeformHelperViewModel _vm;
        private readonly Dictionary<string, object> _before;
        private readonly ConcurrentQueue<string> _raised = new();

        internal PropertyChangeObservation(FreeformHelperViewModel vm, IEnumerable<string> names)
        {
            _vm = vm;
            _before = names.ToDictionary(name => name, name => Read(vm, name));
            vm.PropertyChanging += (_, args) => _raised.Enqueue("changing:" + args.PropertyName);
            vm.PropertyChanged += (_, args) => _raised.Enqueue("changed:" + args.PropertyName);
        }

        internal void AssertChanged(string name, object newValue, IReadOnlyCollection<string> recalculated)
        {
            var sequence = _raised.ToList();
            if (recalculated.Contains(name))
            {
                // The grid recalculates these settings, so the setting can change more than once.
                Assert.Contains("changing:" + name, sequence);
                Assert.Contains("changed:" + name, sequence);
            }
            else
            {
                Assert.Equal(1, sequence.Count(entry => entry == "changing:" + name));
                Assert.Equal(1, sequence.Count(entry => entry == "changed:" + name));
            }

            Assert.True(sequence.IndexOf("changing:" + name) < sequence.IndexOf("changed:" + name));
            var after = _before.Keys.ToDictionary(other => other, other => Read(_vm, other));
            foreach (var other in _before.Keys.Where(other => other != name && !recalculated.Contains(other)))
            {
                Assert.Equal(_before[other], after[other]);
            }

            // Recalculated settings do not necessarily retain the assigned value.
            if (!recalculated.Contains(name))
            {
                Assert.Equal(newValue, after[name]);
            }
        }
    }
}
