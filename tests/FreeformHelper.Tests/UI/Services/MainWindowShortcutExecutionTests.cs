// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MainWindowShortcutExecutionTests
{
    [Theory]
    [InlineData(Key.Delete, KeyModifiers.None, "Delete")]
    [InlineData(Key.Z, KeyModifiers.Control, "Undo")]
    public void EnabledCommandChecksCanExecuteBeforeExecuting(Key key, KeyModifiers modifiers, string command)
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(key, modifiers);

        Assert.True(handled);
        Assert.Equal([$"{command}.CanExecute", $"{command}.Execute"], probe.Calls);
    }

    [Theory]
    [InlineData(Key.Delete, KeyModifiers.None, "Delete")]
    [InlineData(Key.Z, KeyModifiers.Control, "Undo")]
    public void DisabledCommandDoesNotExecuteAndReturnsUnhandled(Key key, KeyModifiers modifiers, string command)
    {
        var probe = new ShortcutProbe();
        probe.Delete.IsEnabled = false;
        probe.Undo.IsEnabled = false;

        var handled = probe.Handle(key, modifiers);

        Assert.False(handled);
        Assert.Equal([$"{command}.CanExecute"], probe.Calls);
    }

    [Fact]
    public void FitExecutesWithoutCheckingCanExecute()
    {
        var probe = new ShortcutProbe();
        probe.Fit.IsEnabled = false;

        var handled = probe.Handle(Key.F, KeyModifiers.None);

        Assert.True(handled);
        Assert.Equal(["Fit.Execute"], probe.Calls);
    }

    [Fact]
    public void SelectAllCallsTheCanvasCallbackOnceAndReturnsHandled()
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.A, KeyModifiers.Control);

        Assert.True(handled);
        Assert.Equal(["SelectAll"], probe.Calls);
    }

    [Fact]
    public void SelectAllWithoutACanvasStillReturnsHandled()
    {
        var probe = new ShortcutProbe();
        Action? canvasSelectAll = null;

        var handled = probe.Handle(Key.A, KeyModifiers.Control, selectAllPads: () => canvasSelectAll?.Invoke());

        Assert.True(handled);
        Assert.Empty(probe.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoreCallsItsCallbackOnceAndReturnsItsResult(bool restored)
    {
        var probe = new ShortcutProbe { RestoreResult = restored };

        var handled = probe.Handle(Key.E, KeyModifiers.Control | KeyModifiers.Shift);

        Assert.Equal(restored, handled);
        Assert.Equal(["Restore"], probe.Calls);
    }

    [Fact]
    public void SaveCallsItsCallbackOnceAndReturnsHandled()
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.S, KeyModifiers.Control);

        Assert.True(handled);
        Assert.Equal(["Save"], probe.Calls);
    }

    [Fact]
    public void SaveWithoutARunnerStillReturnsHandled()
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.S, KeyModifiers.Control, hasSaveRunner: false);

        Assert.True(handled);
        Assert.Empty(probe.Calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void EditingDisabledOrAlreadyHandledInputCallsNothing(bool editingEnabled, bool alreadyHandled)
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.S, KeyModifiers.Control, editingEnabled, alreadyHandled);

        Assert.False(handled);
        Assert.Empty(probe.Calls);
    }

    [AvaloniaFact]
    public void TextEntryCallsNothingAndReturnsUnhandled()
    {
        var probe = new ShortcutProbe();
        var source = new TextBox();

        var handled = probe.Handle(Key.S, KeyModifiers.Control, source: source);

        Assert.False(handled);
        Assert.Empty(probe.Calls);
    }

    [AvaloniaTheory]
    [InlineData(Key.A, KeyModifiers.Control)]
    [InlineData(Key.F, KeyModifiers.None)]
    public void CanvasSelectAllAndFitStayWithTheCanvas(Key key, KeyModifiers modifiers)
    {
        var probe = new ShortcutProbe();
        var canvas = new FreeformHelper.UI.Controls.PadCanvas();

        var handled = probe.Handle(key, modifiers, source: canvas);

        Assert.False(handled);
        Assert.Empty(probe.Calls);
    }

    [Fact]
    public void UnknownShortcutCallsNothingAndReturnsUnhandled()
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.R, KeyModifiers.None);

        Assert.False(handled);
        Assert.Empty(probe.Calls);
    }

    [Fact]
    public void MissingShortcutContextCallsNothingAndReturnsUnhandled()
    {
        var probe = new ShortcutProbe();

        var handled = probe.Handle(Key.A, KeyModifiers.Control, hasShortcutContext: false);

        Assert.False(handled);
        Assert.Empty(probe.Calls);
    }

    private sealed class ShortcutProbe
    {
        public ShortcutProbe()
        {
            Delete = new RecordingShortcutCommand(Calls, "Delete");
            Undo = new RecordingShortcutCommand(Calls, "Undo");
            Fit = new RecordingShortcutCommand(Calls, "Fit");
        }

        public List<string> Calls { get; } = [];
        public RecordingShortcutCommand Delete { get; }
        public RecordingShortcutCommand Undo { get; }
        public RecordingShortcutCommand Fit { get; }
        public bool RestoreResult { get; init; }

        public bool Handle(
            Key key,
            KeyModifiers modifiers,
            bool editingEnabled = true,
            bool alreadyHandled = false,
            object? source = null,
            Action? selectAllPads = null,
            bool hasShortcutContext = true,
            bool hasSaveRunner = true)
        {
            return FreeformHelper.UI.Services.MainWindowShortcutExecution.TryHandle(
                editingEnabled, alreadyHandled, key, modifiers, source,
                hasShortcutContext ? Delete : null,
                hasShortcutContext ? Undo : null,
                hasShortcutContext ? Fit : null,
                selectAllPads ?? (() => Calls.Add("SelectAll")),
                () =>
                {
                    Calls.Add("Restore");
                    return RestoreResult;
                },
                hasSaveRunner ? () => Calls.Add("Save") : null);
        }
    }

    private sealed class RecordingShortcutCommand(List<string> calls, string name) : ICommand
    {
        public bool IsEnabled { get; set; } = true;

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            Assert.Null(parameter);
            calls.Add($"{name}.CanExecute");
            return IsEnabled;
        }

        public void Execute(object? parameter)
        {
            Assert.Null(parameter);
            calls.Add($"{name}.Execute");
        }
    }
}
