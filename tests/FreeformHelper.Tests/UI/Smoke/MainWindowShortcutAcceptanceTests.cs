// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Tests.TestInfrastructure;
using FreeformHelper.UI.Controls;
using FreeformHelper.UI.ViewModels;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

[Collection("HeadlessUiSerial")]
public sealed class MainWindowShortcutAcceptanceTests
{
    [AvaloniaFact]
    public async Task CtrlZWithCanExecuteFalseLeavesKeyUnhandledAndWorkspaceUnchangedAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        await EmptyUndoHistoryAsync(vm);
        var canvas = FindCanvas(fixture);
        canvas.SetSelection([vm.CadPads[0].Id], []);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(vm.CanUndo);
        Assert.False(vm.UndoCommand.CanExecute(null));
        var assertUnchanged = CaptureWorkspace(fixture, canvas);

        var key = fixture.SendKey(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
        await HeadlessUiPump.FlushQueueAsync();

        Assert.False(key.Handled);
        assertUnchanged();
    }

    [AvaloniaFact]
    public async Task DeleteWithoutSelectionHasCanExecuteTrueAndHandlesKeyWithoutChangingPadsOrUndoAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        await EmptyUndoHistoryAsync(vm);
        var canvas = FindCanvas(fixture);
        canvas.SetSelection([], []);
        await HeadlessUiPump.FlushQueueAsync();
        Assert.False(vm.CanUndo);
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));
        var assertUnchanged = CaptureWorkspace(fixture, canvas);

        var key = fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        await vm.WaitForGridRebuildIdleAsync();
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(key.Handled);
        assertUnchanged();
    }

    [AvaloniaFact]
    public async Task CtrlAFromPlainControlSelectsEveryCadAndRegularPadExactlyOnceAndHandlesKeyAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var canvas = FindCanvas(fixture);
        canvas.SetSelection([], []);
        await HeadlessUiPump.FlushQueueAsync();
        fixture.FocusShortcutTarget();
        var changes = new List<PadCanvasSelectionChangedEventArgs>();
        canvas.SelectionChanged += (_, args) => changes.Add(args);

        var key = fixture.SendKey(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        await HeadlessUiPump.FlushQueueAsync();

        Assert.NotSame(canvas, key.Source);
        Assert.True(key.Handled);
        var changed = Assert.Single(changes);
        Assert.Equal(vm.CadPads.Select(static pad => pad.Id).Order(), changed.SelectedCadPadIds);
        Assert.Equal(vm.RegularPads.Select(static pad => pad.Index).Order(), changed.SelectedRegularPadIndices);
        Assert.Equal(changed.SelectedCadPadIds, vm.InteractionState.SelectedCadIds);
        Assert.Equal(changed.SelectedRegularPadIndices, vm.InteractionState.SelectedRegularIndices);
    }

    [AvaloniaFact]
    public async Task CtrlAFromCanvasDoesNotSelectAgainAtTheWindowAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var canvas = FindCanvas(fixture);
        Assert.True(canvas.Focus());
        var changes = new List<PadCanvasSelectionChangedEventArgs>();
        canvas.SelectionChanged += (_, args) => changes.Add(args);

        var key = fixture.SendKey(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
        await HeadlessUiPump.FlushQueueAsync();

        Assert.Same(canvas, key.Source);
        Assert.True(key.Handled);
        Assert.Single(changes);
    }

    [AvaloniaTheory]
    [InlineData(Key.Delete, KeyModifiers.None)]
    [InlineData(Key.Z, KeyModifiers.Control)]
    [InlineData(Key.A, KeyModifiers.Control)]
    [InlineData(Key.F, KeyModifiers.None)]
    public async Task AlreadyHandledKeyPreservesSelectionVisibilityUndoAndViewAsync(Key key, KeyModifiers modifiers)
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var canvas = await PrepareShortcutTargetsAsync(fixture);
        var assertUnchanged = CaptureWorkspace(fixture, canvas);

        var input = RaiseWindowKey(fixture.Window, key, modifiers, handled: true);
        await fixture.Helper.WaitForGridRebuildIdleAsync();
        await HeadlessUiPump.FlushQueueAsync();

        Assert.True(input.Handled);
        assertUnchanged();
    }

    [AvaloniaTheory]
    [InlineData("TextBox", Key.Delete, KeyModifiers.None)]
    [InlineData("TextBox", Key.Z, KeyModifiers.Control)]
    [InlineData("TextBox", Key.A, KeyModifiers.Control)]
    [InlineData("ConsoleDescendant", Key.Delete, KeyModifiers.None)]
    [InlineData("ConsoleDescendant", Key.Z, KeyModifiers.Control)]
    [InlineData("ConsoleDescendant", Key.A, KeyModifiers.Control)]
    [InlineData("ConsoleDescendant", Key.F, KeyModifiers.None)]
    [InlineData("NumberScrubberDescendant", Key.Delete, KeyModifiers.None)]
    [InlineData("NumberScrubberDescendant", Key.Z, KeyModifiers.Control)]
    [InlineData("NumberScrubberDescendant", Key.A, KeyModifiers.Control)]
    [InlineData("NumberScrubberDescendant", Key.F, KeyModifiers.None)]
    public async Task TextEntrySourceLeavesKeyUnhandledAndWorkspaceUnchangedAsync(
        string sourceKind, Key key, KeyModifiers modifiers)
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var canvas = await PrepareShortcutTargetsAsync(fixture);
        var source = await FindTextEntrySourceAsync(fixture, sourceKind);
        var assertUnchanged = CaptureWorkspace(fixture, canvas);

        var input = RaiseWindowKey(source, key, modifiers);
        await fixture.Helper.WaitForGridRebuildIdleAsync();
        await HeadlessUiPump.FlushQueueAsync();

        if (sourceKind != "TextBox")
        {
            // A TextBox consumes Delete, Ctrl+Z and Ctrl+A itself; for it the pin is the unchanged workspace.
            Assert.False(input.Handled);
        }

        assertUnchanged();
    }

    [AvaloniaTheory]
    [InlineData(Key.F, RawInputModifiers.None, PhysicalKey.F, "f")]
    [InlineData(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a")]
    public async Task LoadPickerAwaitLeavesFitAndSelectAllUnhandledAndWorkspaceUnchangedAsync(
        Key key, RawInputModifiers modifiers, PhysicalKey physicalKey, string symbol)
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var canvas = await PrepareShortcutTargetsAsync(fixture);
        var picker = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        vm.PickLoadProjectPathAsync = () => picker.Task.WaitAsync(TestContext.Current.CancellationToken);
        var load = vm.LoadProjectCommand.ExecuteAsync(null);
        try
        {
            Assert.False(vm.IsProjectEditingEnabled);
            Assert.False(vm.FitCommand.CanExecute(null));
            fixture.FocusShortcutTarget();
            var assertUnchanged = CaptureWorkspace(fixture, canvas);

            var input = fixture.SendKey(key, modifiers, physicalKey, symbol);
            await HeadlessUiPump.FlushQueueAsync();

            Assert.False(input.Handled);
            assertUnchanged();
        }
        finally
        {
            picker.TrySetResult(null);
            await load.WaitAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(vm.IsProjectEditingEnabled);
    }

    [AvaloniaFact]
    public async Task FWithFitCommandCanExecuteTrueInvokesCanvasHostOnceAndHandlesKeyAsync()
    {
        using var culture = TestCulture.UseEnUs();
        await using var fixture = await MainWindowGlobalInputFixture.CreateAsync();
        var vm = fixture.Helper;
        var originalHost = vm.CanvasHost;
        var probe = new SelectionLayerCanvasProbe();
        vm.CanvasHost = probe;
        try
        {
            Assert.True(vm.FitCommand.CanExecute(null));
            fixture.FocusShortcutTarget();

            var key = fixture.SendKey(Key.F, RawInputModifiers.None, PhysicalKey.F, "f");

            Assert.True(key.Handled);
            Assert.Equal(1, probe.FitToContentCount);
        }
        finally
        {
            vm.CanvasHost = originalHost;
        }
    }

    private static FreeformHelperView FindView(MainWindowGlobalInputFixture fixture) =>
        Assert.Single(fixture.Window.GetVisualDescendants().OfType<FreeformHelperView>());

    private static PadCanvas FindCanvas(MainWindowGlobalInputFixture fixture) =>
        Assert.IsType<PadCanvas>(FindView(fixture).FindControl<PadCanvas>("PadCanvas"));

    private static async Task EmptyUndoHistoryAsync(FreeformHelperViewModel vm)
    {
        while (vm.UndoCommand.CanExecute(null))
        {
            vm.UndoCommand.Execute(null);
            await vm.WaitForGridRebuildIdleAsync();
        }

        await HeadlessUiPump.FlushQueueAsync();
    }

    private static async Task<PadCanvas> PrepareShortcutTargetsAsync(MainWindowGlobalInputFixture fixture)
    {
        var vm = fixture.Helper;
        var canvas = FindCanvas(fixture);
        canvas.SetSelection([vm.CadPads[0].Id], []);
        Assert.True(fixture.SendKey(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null).Handled);
        await vm.WaitForGridRebuildIdleAsync();
        canvas.SetSelection([vm.CadPads[0].Id], [vm.RegularPads[0].Index]);
        canvas.FitBoundsOverride = new Rect2(1000, 2000, 1010, 2010);
        canvas.ResetView();
        await HeadlessUiPump.FlushQueueAsync();
        Assert.Equal(1, vm.DeletedCadPadCount);
        Assert.True(vm.UndoCommand.CanExecute(null));
        Assert.True(vm.DeleteSelectedCadPadsCommand.CanExecute(null));
        Assert.Single(vm.InteractionState.SelectedCadIds);
        Assert.Single(vm.InteractionState.SelectedRegularIndices);
        return canvas;
    }

    private static async Task<Control> FindTextEntrySourceAsync(MainWindowGlobalInputFixture fixture, string sourceKind)
    {
        var root = Assert.IsType<Grid>(fixture.Window.Content);
        switch (sourceKind)
        {
            case "TextBox":
                var input = new TextBox { Text = "Workspace notes" };
                root.Children.Add(input);
                await HeadlessUiPump.FlushQueueAsync();
                return input;
            case "ConsoleDescendant":
                var console = Assert.IsType<ConsolePanel>(FindView(fixture).FindControl<ConsolePanel>("ConsolePanelHost"));
                return Assert.IsType<Border>(console.FindControl<Border>("ConsoleBorder"));
            case "NumberScrubberDescendant":
                var scrubber = new NumberScrubber();
                root.Children.Add(scrubber);
                await HeadlessUiPump.FlushQueueAsync();
                return Assert.IsType<Border>(scrubber.FindControl<Border>("ScrubArea"));
            default:
                throw new ArgumentOutOfRangeException(nameof(sourceKind), sourceKind, "Unknown test input source.");
        }
    }

    private static KeyEventArgs RaiseWindowKey(
        Control source, Key key, KeyModifiers modifiers, bool handled = false)
    {
        var input = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = source,
            Key = key,
            KeyModifiers = modifiers,
            Handled = handled,
        };
        // Raise at the source so the event reaches the window with that source; raising at the window would replace the source.
        source.RaiseEvent(input);
        return input;
    }

    private static Action CaptureWorkspace(MainWindowGlobalInputFixture fixture, PadCanvas canvas)
    {
        var vm = fixture.Helper;
        var cadIds = vm.CadPads.Select(static pad => pad.Id).Order().ToArray();
        var regularIndices = vm.RegularPads.Select(static pad => pad.Index).Order().ToArray();
        var selectedCadIds = vm.InteractionState.SelectedCadIds.ToArray();
        var selectedRegularIndices = vm.InteractionState.SelectedRegularIndices.ToArray();
        var hiddenCount = vm.DeletedCadPadCount;
        var canUndo = vm.CanUndo;
        var unsaved = vm.HasUnsavedChanges;
        var frame = canvas.GetViewFrameSnapshot();
        return () =>
        {
            Assert.Equal(cadIds, vm.CadPads.Select(static pad => pad.Id).Order());
            Assert.Equal(regularIndices, vm.RegularPads.Select(static pad => pad.Index).Order());
            Assert.Equal(selectedCadIds, vm.InteractionState.SelectedCadIds);
            Assert.Equal(selectedRegularIndices, vm.InteractionState.SelectedRegularIndices);
            Assert.Equal(hiddenCount, vm.DeletedCadPadCount);
            Assert.Equal(canUndo, vm.CanUndo);
            Assert.Equal(unsaved, vm.HasUnsavedChanges);
            Assert.Equal(frame.WorldToScreen, canvas.GetViewFrameSnapshot().WorldToScreen);
        };
    }
}
