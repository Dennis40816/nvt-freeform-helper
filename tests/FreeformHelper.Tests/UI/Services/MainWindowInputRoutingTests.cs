// Copyright (c) 2026 Dennis Liu. All rights reserved.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FreeformHelper.UI.Services;
using FreeformHelper.UI.Views;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class MainWindowInputRoutingTests
{
    [Theory]
    [InlineData(Key.E, KeyModifiers.Control | KeyModifiers.Shift, false, "RestoreNotchExportSelection")]
    [InlineData(Key.E, KeyModifiers.Control | KeyModifiers.Shift, true, "RestoreNotchExportSelection")]
    [InlineData(Key.Delete, KeyModifiers.None, false, "DeleteSelectedCadPads")]
    [InlineData(Key.Delete, KeyModifiers.None, true, "DeleteSelectedCadPads")]
    [InlineData(Key.Z, KeyModifiers.Control, true, "Undo")]
    [InlineData(Key.S, KeyModifiers.Control, true, "SaveProject")]
    [InlineData(Key.A, KeyModifiers.Control, false, "SelectAllPads")]
    [InlineData(Key.F, KeyModifiers.None, false, "Fit")]
    public void Resolve_KnownShortcut_ReturnsItsAction(Key key, KeyModifiers modifiers, bool fromCanvas, string expected)
    {
        Assert.Equal(expected, MainWindowShortcutRouter.Resolve(key, modifiers, fromCanvas).ToString());
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.Control)]
    [InlineData(Key.F, KeyModifiers.None)]
    public void Resolve_SelectAllAndFitFromCanvas_StayWithTheCanvas(Key key, KeyModifiers modifiers)
    {
        Assert.Equal(MainWindowShortcut.None, MainWindowShortcutRouter.Resolve(key, modifiers, fromCanvas: true));
    }

    [Theory]
    [InlineData(Key.Delete, KeyModifiers.Control)]
    [InlineData(Key.Delete, KeyModifiers.Shift)]
    [InlineData(Key.Z, KeyModifiers.None)]
    [InlineData(Key.Z, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData(Key.S, KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(Key.E, KeyModifiers.Control)]
    [InlineData(Key.F, KeyModifiers.Shift)]
    [InlineData(Key.R, KeyModifiers.None)]
    [InlineData(Key.R, KeyModifiers.Control)]
    public void Resolve_OtherKeysOrModifiers_ReturnNone(Key key, KeyModifiers modifiers)
    {
        Assert.Equal(MainWindowShortcut.None, MainWindowShortcutRouter.Resolve(key, modifiers, fromCanvas: false));
    }

    [Theory]
    [InlineData(true, KeyModifiers.Control, true)]
    [InlineData(true, KeyModifiers.Control | KeyModifiers.Shift, true)]
    [InlineData(true, KeyModifiers.None, false)]
    [InlineData(true, KeyModifiers.Shift, false)]
    [InlineData(false, KeyModifiers.Control, false)]
    public void IsSoloRequest_NeedsLeftButtonAndControl(bool leftButton, KeyModifiers modifiers, bool expected)
    {
        Assert.Equal(expected, LayerTogglePointerPolicy.IsSoloRequest(leftButton, modifiers));
    }

    [AvaloniaFact]
    public void InputSources_TextControlsAreTextEntryAndPlainControlsAreNot()
    {
        Assert.True(MainWindowInputSources.IsTextEntry(new TextBox()));
        Assert.True(MainWindowInputSources.IsTextEntry(new ComboBox()));
        Assert.False(MainWindowInputSources.IsTextEntry(new Button()));
        Assert.False(MainWindowInputSources.IsTextEntry(null));
        Assert.False(MainWindowInputSources.IsTextEntry("not a visual"));
    }

    [AvaloniaFact]
    public void InputSources_PlainSourcesClearFocusAndAreNotCanvas()
    {
        Assert.True(MainWindowInputSources.ShouldClearFocus(null));
        Assert.True(MainWindowInputSources.ShouldClearFocus(new Button()));
        Assert.False(MainWindowInputSources.IsCanvas(new Button()));
        Assert.False(MainWindowInputSources.IsCanvas(null));
    }
}
