// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Headless.XUnit;
using FreeformHelper.Tests.TestInfrastructure;
using Xunit;

namespace FreeformHelper.Tests;

/// <summary>Current behaviour: cancelling or failing before state application keeps unsaved CAD edits.</summary>
[Collection("HeadlessUiSerial")]
public sealed class ProjectLoadAbortedDirtyCadCharacterizationTests
{
    [AvaloniaFact]
    public async Task LoadCommand_CurrentBehavior_CancelledPickerKeepsDirtyCadSettingsAndStatusAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.MatchThreshold = 0.61m;
        var regular = vm.RegularPads.Select(static pad => (pad.Index, pad.Freeform)).ToArray();
        var vertices = vm.RegularPads.SelectMany(static pad => pad.Polygon.Vertices).ToArray();
        var layers = vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)).ToArray();
        var status = vm.StatusText;
        var pendingChecks = 0;
        vm.EnsureNoPendingEdits = () => { pendingChecks++; return true; };
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(null);

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.Equal(regular, vm.RegularPads.Select(static pad => (pad.Index, pad.Freeform)));
        Assert.Equal(vertices, vm.RegularPads.SelectMany(static pad => pad.Polygon.Vertices));
        Assert.Equal(layers, vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)));
        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        Assert.Equal(status, vm.StatusText);
        Assert.Equal(0, pendingChecks);
    }

    [AvaloniaFact]
    public async Task LoadCommand_CurrentBehavior_CorruptFileKeepsDirtyCadAndSelectionAndReportsFailureAsync()
    {
        using var culture = TestCulture.UseEnUs();
        using var files = new ProjectLoadCharacterizationScope();
        var vm = await files.CreateSavedCadAsync("current.json");
        await ProjectLoadCharacterizationScope.HideOnlyPadAsync(vm);
        vm.MatchThreshold = 0.61m;
        var selectedIndex = vm.RegularPads[0].Index;
        vm.ApplyCanvasSelection([], [selectedIndex]);
        var regular = vm.RegularPads.Select(static pad => (pad.Index, pad.Freeform)).ToArray();
        var vertices = vm.RegularPads.SelectMany(static pad => pad.Polygon.Vertices).ToArray();
        var layers = vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)).ToArray();
        var corruptPath = files.PathFor("corrupt.json");
        await TestFiles.WriteTextAsync(corruptPath, "{", TestContext.Current.CancellationToken);
        vm.PickLoadProjectPathAsync = () => Task.FromResult<string?>(corruptPath);
        vm.StatusText = "Before failed load";

        await vm.LoadProjectCommand.ExecuteAsync(null);

        Assert.Equal(0.61m, vm.MatchThreshold);
        Assert.Equal(regular, vm.RegularPads.Select(static pad => (pad.Index, pad.Freeform)));
        Assert.Equal(vertices, vm.RegularPads.SelectMany(static pad => pad.Polygon.Vertices));
        Assert.Equal(layers, vm.LayerToggles.Select(static toggle => (toggle.Name, toggle.IsSelected)));
        Assert.Empty(vm.CadPads);
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.Equal(selectedIndex, Assert.Single(vm.SelectedRegularPadIndices));
        Assert.True(vm.HasUnsavedChanges);
        Assert.True(vm.CanUndo);
        Assert.StartsWith("Load project failed:", vm.StatusText, StringComparison.Ordinal);
    }
}
